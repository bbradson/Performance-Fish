// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if !V1_4
using Gilzoide.ManagedJobs;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using Unity.Jobs;

namespace PerformanceFish.Rendering;

public sealed class DynamicDrawManagerPatches : ClassWithFishPrepatches
{
	public sealed class DrawDynamicThingsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes rendering by reverting most of the threading added in 1.5, making 5 of the 6 parallel loops "
			+ "in DrawDynamicThings run on the main thread again. This generally improves rendering performance by "
			+ "about 10% or so";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(DynamicDrawManager), nameof(DynamicDrawManager.DrawDynamicThings));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(DynamicDrawManager instance)
		{
			if (!DebugViewSettings.drawThingsDynamic || instance.map.Disposed)
				return;

			instance.drawingNow = true;

			try
			{
				CullAndInitializeThings(instance);
				
				var hasAnyThingsToDraw = ThingsToDraw.Count > 0;
				if (hasAnyThingsToDraw)
				{
					if (!DebugViewSettings.singleThreadedDrawing)
						DoPreDrawThings();
				
					DrawThingsNow();
				}
				
				if (PawnShadowsToDraw.Count > 0)
					DrawShadowsNow();

				if (SilhouetteUtility.CanHighlightAny() && hasAnyThingsToDraw)
					DrawSilhouettes(instance);
			}
			catch (Exception ex)
			{
				Log.Error($"Exception drawing dynamic things for map '{instance.map}':\n{ex}");
			}
			finally
			{
				ThingsToDraw.Clear();
				PawnShadowsToDraw.Clear();
				instance.drawingNow = false;
			}
		}

		public static void CullAndInitializeThings(DynamicDrawManager instance)
		{
			var map = instance.map;
			var mapSizeX = map.SizeX();
			var fogGrid = map.fogGrid.fogGrid;
			var snowGrid = map.snowGrid.depthGrid;
			var checkShadows = MatBases.SunShadow.shader.isSupported;
			var currentViewRect = Find.CameraDriver.CurrentViewRect.ClipInsideMap(map).ExpandedBy(1);
			var shadowViewRect = SectionLayer_SunShadows.GetSunShadowsViewRect(map, currentViewRect);
			instance.drawThings.UnwrapArray(out var drawThings, out var drawThingsCount);
			
			for (var i = 0; i < drawThingsCount; i++)
			{
				var drawThing = drawThings.UnsafeLoad(i);
				var position = drawThing.Position;
				var drawThingDef = drawThing.def;
				var cellIndex = position.CellToIndex(mapSizeX);
				if ((!fogGrid[cellIndex] || drawThingDef.seeThroughFog)
					&& (drawThingDef.hideAtSnowDepth >= 1f
						|| drawThingDef.hideAtSnowDepth >= snowGrid[cellIndex]))
				{
					if (currentViewRect.Contains(position)
						|| currentViewRect.Overlaps(drawThing.OccupiedDrawRect()))
					{
						ThingsToDraw.Add(drawThing);
						
						if (!DebugViewSettings.singleThreadedDrawing)
							EnsureInitialized(drawThing);
					}
					else if (drawThing is Pawn pawn && checkShadows && shadowViewRect.Contains(drawThing.Position))
					{
						PawnShadowsToDraw.Add(pawn);
					}
				}
			}
		}

		public static void EnsureInitialized(Thing drawThing)
		{
			try
			{
				drawThing.DynamicDrawPhase(DrawPhase.EnsureInitialized);
			}
			catch (Exception ex)
			{
				Logging.EnsureInitializedException(drawThing, ex);
			}
		}
		
		public static void DrawShadowsNow()
		{
			PawnShadowsToDraw.UnwrapArray(out var pawnShadowsToDraw, out var pawnShadowsToDrawCount);
			
			for (var i = 0; i < pawnShadowsToDrawCount; i++)
			{
				var pawn = pawnShadowsToDraw.UnsafeLoad(i);
				try
				{
					pawn.DrawShadowAt(pawn.DrawPos);
				}
				catch (Exception ex)
				{
					Logging.DrawShadowException(pawn, ex);
				}
			}
		}

		public static void DrawThingsNow()
		{
			ThingsToDraw.UnwrapArray(out var thingsToDraw, out var thingsToDrawCount);
			
			for (var i = 0; i < thingsToDrawCount; i++)
			{
				var drawThing = thingsToDraw.UnsafeLoad(i);
				try
				{
					drawThing.DynamicDrawPhase(DrawPhase.Draw);
				}
				catch (Exception ex)
				{
					Logging.DrawException(drawThing, ex);
				}
			}
		}

		public static void DoPreDrawThings()
		{
			ThingsToDraw.UnwrapArray(out PreDrawThingsJob.thingsToDraw, out var thingsToDrawCount);
			new ManagedJobParallelFor(PreDrawThingsJob)
				.Schedule(thingsToDrawCount, UnityData.GetIdealBatchCount(thingsToDrawCount)).Complete();
		}

		public static readonly List<Thing> ThingsToDraw = [];
		public static readonly List<Pawn> PawnShadowsToDraw = [];
		public static readonly PreDrawThings PreDrawThingsJob = new();

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void DrawSilhouettes(DynamicDrawManager instance)
		{
			ThingsToDraw.UnwrapArray(out var thingsToDraw, out var thingsToDrawCount);

			var inverseFovScale = Find.CameraDriver.InverseFovScale;
			var altitude = AltitudeLayer.Silhouettes.AltitudeFor();
			var angleAxis = Quaternion.AngleAxis(0f, Vector3.up);

			for (var i = 0; i < thingsToDrawCount; i++)
			{
				if (thingsToDraw.UnsafeLoad(i) is not Pawn pawn || !SilhouetteUtility.ShouldDrawSilhouette(pawn))
					continue;

				var pawnRenderer = pawn.Drawer.renderer;

				var drawSize = pawnRenderer.SilhouetteGraphic.drawSize;
				drawSize.x = drawSize.x < 2.5f ? drawSize.x + SilhouetteUtility.AdjustScale(drawSize.x) : drawSize.x;
				drawSize.y = drawSize.y < 2.5f ? drawSize.y + SilhouetteUtility.AdjustScale(drawSize.y) : drawSize.y;

				var pos = pawnRenderer.SilhouettePos;
				pos.y = altitude;

				SilhouetteUtility.DrawSilhouetteJob(pawn, Matrix4x4.TRS(pos, angleAxis,
					inverseFovScale with { x = inverseFovScale.x * drawSize.x, z = inverseFovScale.z * drawSize.y }));
			}
		}
		
		public class PreDrawThings : IJobParallelFor
		{
			public Thing[] thingsToDraw = Array.Empty<Thing>();
			
			public void Execute(int index)
			{
				var thing = thingsToDraw[index];
				try
				{
					thing.DynamicDrawPhase(DrawPhase.ParallelPreDraw);
				}
				catch (Exception ex)
				{
					Logging.ParallelPreDrawException(thing, ex);
				}
			}
		}

		public static class Logging
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void EnsureInitializedException(Thing drawThing, Exception ex)
				=> Log.Error($"Exception in EnsureInitialized for '{drawThing}' at cell {
					drawThing.PositionHeld}:\n{ex}");

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void DrawException(Thing drawThing, Exception ex)
				=> Log.Error($"Exception drawing '{drawThing}' at cell {drawThing.PositionHeld}:\n{ex}");

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void DrawShadowException(Pawn pawn, Exception ex)
				=> Log.Error($"Exception drawing shadow for '{pawn}' at cell {pawn.PositionHeld}:\n{ex}");
			
			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void ParallelPreDrawException(Thing thing, Exception ex)
				=> Log.Error($"Exception in ParallelPreDraw for '{thing}' at cell {thing.PositionHeld}:\n{ex}");
		}
	}
}
#endif