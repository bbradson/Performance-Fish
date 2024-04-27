// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using static System.Reflection.Emit.OpCodes;

namespace PerformanceFish;

public sealed class MiscPrepatchedOptimizations : ClassWithFishPrepatches
{
	public sealed class GenList_ListFullCopy : FishPrepatch
	{
		public override string Description { get; }
			= "Optimization for a basic and frequently used list copying method. Replaces a for loop with an "
			+ "optimized IL instruction.";

		public override MethodBase TargetMethodBase { get; } = methodof(GenList.ListFullCopy<object>);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ListFullCopy<object>);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<T> ListFullCopy<T>(List<T> source) => source.Copy();
	}

	public sealed class GenCollection_FirstOrDefault : FishPrepatch
	{
		public override string? Description { get; }
			= "Minor optimization for a RimWorld method used for list lookups, by looping in a more efficient manner "
			+ "with fewer bounds checks";

		public override MethodBase TargetMethodBase { get; } = methodof(GenCollection.FirstOrDefault<object>);
	
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(FirstOrDefault<object>);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? FirstOrDefault<T>(List<T> list, Predicate<T> predicate) => list.FirstOrDefaultFast(predicate);
	}

	public sealed class GenGrid_InBoundsPatch : FishPrepatch
	{
		public override string? Description { get; } = "Tiny optimization by fixing an incorrect cast";

		public override MethodBase TargetMethodBase { get; } = methodof((Func<IntVec3, Map, bool>)GenGrid.InBounds);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ReplacementBody(IntVec3 c, Map map)
		{
			var size = map.Size;
			return ((uint)c.x < (uint)size.x) & ((uint)c.z < (uint)size.z);
		}
	}
}

public sealed class MiscOptimizations : ClassWithFishPatches
{
	public sealed class WindManager_WindManagerTick : FishPatch
	{
		public override string Description { get; }
			= "Throttles plant sway to update at most once per frame instead of every tick. Also fixes the plant sway "
			+ "setting to actually disable plant sway calculations when it should. By default it simply sets values to "
			+ "0 but then still goes through everything, sacrificing tps for no reason";

		public override Expression<Action> TargetMethod { get; } = static () => default(WindManager)!.WindManagerTick();

		public static CodeInstructions Transpiler(CodeInstructions instructions)
		{
			var find_CurrentMap = FishTranspiler.PropertyGetter(typeof(Find), nameof(Find.CurrentMap));
			var this_map = FishTranspiler.Field(typeof(WindManager), nameof(WindManager.map));

			return instructions.ReplaceAt((codes, i)
					=> i - 3 >= 0
					&& codes[i - 3] == find_CurrentMap
					&& codes[i - 1] == this_map
					&& codes[i].operand is Label, // goto return;
				static code =>
				[
					code, FishTranspiler.Call(ShouldSway), FishTranspiler.IfFalse_Short((Label)code.operand)
				]);
		}

		public static bool ShouldSway()
		{
			var currentFrame = Time.frameCount;
			if (_lastSwayFrame >= currentFrame)
				return false;

			if (Prefs.PlantWindSway != _lastPrefValue)
				_lastPrefValue = Prefs.PlantWindSway;
			else if (!Prefs.PlantWindSway)
				return false;

			_lastSwayFrame = currentFrame;
			return true;
		}

		private static int _lastSwayFrame;
		private static bool _lastPrefValue;
	}

	public sealed class CompRottable : FishPatch
	{
		public override string? Description { get; }
			= "Throttles rotting on misconfigured defs to only recalculate every 256 ticks, instead of constantly.";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(RimWorld.CompRottable), nameof(RimWorld.CompRottable.CompTick));

		public static CodeInstructions Transpiler(CodeInstructions codes, ILGenerator generator)
			=> Reflection.GetCodeInstructions(Replacement, generator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Replacement(RimWorld.CompRottable instance)
		{
			if ((TickHelper.TicksGame & 255) == (instance.parent.thingIDNumber & 255))
				instance.Tick(256);
		}
	}

	public sealed class RitualObligationTrigger_Date : FirstPriorityFishPatch
	{
		public override string? Description { get; }
			= "Literally just reorders instructions in this method. They were nonsensical, discarding 90% of the "
			+ "computation.";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(RimWorld.RitualObligationTrigger_Date),
				nameof(RimWorld.RitualObligationTrigger_Date.Tick));

		public static CodeInstructions Transpiler(CodeInstructions codes, ILGenerator generator)
			=> Reflection.GetCodeInstructions(Replacement, generator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Replacement(RimWorld.RitualObligationTrigger_Date instance)
		{
			if (instance.ritual.isAnytime)
				return;

			if ((!instance.mustBePlayerIdeo || Faction.OfPlayerSilentFail.ideos.Has(instance.ritual.ideo))
				&& instance.CurrentTickRelative() == instance.OccursOnTick())
			{
				instance.ritual.AddObligation(new(instance.ritual));
			}
		}
	}

	public sealed class ReportProbablyMissingAttributesFix : FishPatch
	{
		public override string? Description { get; }
			= """Fixes the "probably needs a StaticConstructorOnStartup attribute" log warning to display full """
			+ "type names to help identify what mod it's actually from.";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.FindIncludingInnerTypes(typeof(StaticConstructorOnStartupUtility),
				static t => AccessTools.FirstMethod(t, static m
					=> PatchProcessor.ReadMethodBody(m).Any(static pair
						=> pair.Value is string text
						&& text.Contains("probably needs a StaticConstructorOnStartup attribute"))));

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> codes.MethodReplacer(AccessTools.PropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name)),
				methodof(Reflection.FullDescription));
	}

	public sealed class DrawBatch_Flush : FishPatch
	{
		public override string Description { get; }
			= "Optimization of a fleck rendering method. Low - medium performance impact";

		public override Expression<Action> TargetMethod { get; } = static () => default(DrawBatch)!.Flush(default);

		public static CodeInstructions? Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
		{
			var codes = codeInstructions.AsOrToList();

			var instance_tmpPropertyBlocks
				= FishTranspiler.Field(typeof(DrawBatch), nameof(DrawBatch.tmpPropertyBlocks));
			var instance_tmpPropertyBlocks_instance_propertyBlockCache_AddRange
				= FishTranspiler.Call<Action<HashSet<DrawBatchPropertyBlock>, List<DrawBatchPropertyBlock>>>(
					GenCollection.AddRange);
			var instance_tmpPropertyBlocks_key_propertyBlock_Add
				= FishTranspiler.Call(new HashSet<DrawBatchPropertyBlock>().Add);

			var label_inFrontOf_instance_batches_Clear = generator.DefineLabel();
			var instance_batches_Clear
				= FishTranspiler.Call(new Dictionary<DrawBatch.BatchKey, List<DrawBatch.BatchData>>().Clear);

			codes[
				codes.FindIndex(c => c == instance_batches_Clear)
				- 2
			].labels.Add(label_inFrontOf_instance_batches_Clear);

			var instance_myPropertyBlocks = FishTranspiler.Field(typeof(DrawBatch), nameof(DrawBatch.myPropertyBlocks));
			var instance_myPropertyBlocks_GetEnumerator
				= FishTranspiler.Call(new HashSet<DrawBatchPropertyBlock>().GetEnumerator);

			var addRangeRemoved = false;
			var addRemoved = false;
			var checkFleckLeaksInserted = false;

			for (var i = 0; i < codes.Count; i++)
			{
				if (i + 4 < codes.Count
					&& codes[i + 1] == instance_tmpPropertyBlocks
					&& codes[i + 4] == instance_tmpPropertyBlocks_instance_propertyBlockCache_AddRange)
				{
					i += 4;
					addRangeRemoved = true;
				}
				else if (i + 5 < codes.Count
					&& codes[i + 1] == instance_tmpPropertyBlocks
					&& codes[i + 4] == instance_tmpPropertyBlocks_key_propertyBlock_Add
					&& codes[i + 5].opcode == Pop)
				{
					i += 5;
					addRemoved = true;
				}
				else if (i + 2 < codes.Count
					&& codes[i + 1] == instance_myPropertyBlocks
					&& codes[i + 2] == instance_myPropertyBlocks_GetEnumerator)
				{
					yield return codes[i];
					yield return FishTranspiler.Call(CheckFleckLeaks);
					yield return FishTranspiler.IfFalse(label_inFrontOf_instance_batches_Clear);
					yield return FishTranspiler.This;

					checkFleckLeaksInserted = true;
				}
				else
				{
					yield return codes[i];
				}
			}

			if (!addRangeRemoved || !addRemoved || !checkFleckLeaksInserted)
			{
				Log.Error("Performance Fish failed to apply its DrawBatch.Flush() patch. Mod Settings has an "
					+ "option to disable this");
			}
		}

		public static bool CheckFleckLeaks(DrawBatch instance)
		{
			if (instance.myPropertyBlocks.Count == instance.propertyBlockCache.Count)
				return false;

			instance.tmpPropertyBlocks.AddRange(instance.propertyBlockCache);
			return true;

		}

#if whatItsGonnaLookLikeAfterTranspiling
		public static void Flush(DrawBatch __instance, bool draw = true)
		{
			__instance.tmpPropertyBlock ??= new();
			__instance.tmpPropertyBlocks.Clear();
			// __instance.tmpPropertyBlocks.AddRange(__instance.propertyBlockCache); // <------------------------------- removed
			try
			{
				foreach (var (batchKey, batchDatas) in __instance.batches)
				{
					try
					{
						foreach (var batchData in batchDatas)
						{
							if (draw)
							{
								__instance.tmpPropertyBlock.Clear();
								batchKey.propertyBlock?.Write(__instance.tmpPropertyBlock);
								if (batchKey.renderInstanced)
								{
									batchKey.material.enableInstancing = true;
									if (batchData.hasAnyColors)
										__instance.tmpPropertyBlock.SetVectorArray("_Color", batchData.colors);
									Graphics.DrawMeshInstanced(batchKey.mesh, 0, batchKey.material, batchData.matrices,
										batchData.ptr,
										__instance.tmpPropertyBlock, UnityEngine.Rendering.ShadowCastingMode.On,
										receiveShadows: true, batchKey.layer);
								}
								else
								{
									for (var i = 0; i < batchData.ptr; i++)
									{
										var matrix = batchData.matrices[i];
										var vector = batchData.colors[i];
										if (batchData.hasAnyColors)
											__instance.tmpPropertyBlock.SetColor("_Color", vector);
										Graphics.DrawMesh(batchKey.mesh, matrix, batchKey.material, batchKey.layer,
											null, 0,
											__instance.tmpPropertyBlock);
									}
								}
							}

							batchData.Clear();
							__instance.batchDataListCache.Add(batchData);
						}
					}
					finally
					{
						if (batchKey.propertyBlock != null && __instance.myPropertyBlocks.Contains(batchKey.propertyBlock))
						{
							// __instance.tmpPropertyBlocks.Add(key.propertyBlock); // <-------------------------------- removed
							batchKey.propertyBlock.Clear();
							__instance.propertyBlockCache.Add(batchKey.propertyBlock);
						}

						__instance.batchListCache.Add(batchDatas);
						batchDatas.Clear();
					}
				}
			}
			finally
			{
				if (CheckFleckLeaks(__instance)) // <------------------------------------------------------------------- added
				{
					foreach (var myPropertyBlock in __instance.myPropertyBlocks)
					{
						if (!__instance.tmpPropertyBlocks.Contains(myPropertyBlock))
							Log.Warning("Property block from FleckDrawBatch leaked!"
								+ ((myPropertyBlock.leakDebugString == null)
									? null
									: ("Leak debug information: \n" + myPropertyBlock.leakDebugString)));
					}

					(__instance.myPropertyBlocks, __instance.tmpPropertyBlocks)
						= (__instance.tmpPropertyBlocks, __instance.myPropertyBlocks);
				}

				__instance.batches.Clear();
				__instance.lastBatchKey = default;
				__instance.lastBatchList = null;
			}
		}
#endif
	}
}