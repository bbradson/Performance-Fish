// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// #define GAS_DEBUG_L1 // erroneous gas changes
// #define GAS_DEBUG_L2 // total gas grid changes
// #define GAS_DEBUG_L3 // individual cell changes

using System.Diagnostics;
#if V1_4
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
#if !V1_4
using Gilzoide.ManagedJobs;
using LudeonTK;
using Unity.Jobs;
#endif
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class GasGridOptimization : ClassWithFishPrepatches
{
	public sealed class SetDirectPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override string? Description { get; }
			= "Required by the gas grid optimization";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.SetDirect));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(GasGrid __instance, int index, byte smoke, byte toxic, byte rotStink
#if !V1_4
			, byte deadlife
#endif
		)
		{
			if (!ModsConfig.BiotechActive)
				toxic = 0;

#if !V1_4
			if (!ModsConfig.AnomalyActive)
				deadlife = 0;
#endif

			var gasGrids = __instance.ParallelGasGrids();

			gasGrids[0].SetDirect(index, smoke);
			gasGrids[1].SetDirect(index, toxic);
			gasGrids[2].SetDirect(index, rotStink);
#if !V1_4
			gasGrids[3].SetDirect(index, deadlife);
#endif
		}
	}

	public sealed class TickPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; } =
		[
			typeof(SetDirectPatch),
#if V1_4
			typeof(ColorAtPatch),
#endif
			typeof(AddGasPatch), typeof(AnyGasAtPatch),
			typeof(DensityAtPatch), typeof(Debug_ClearAllPatch), typeof(Debug_FillAllPatch),
			typeof(Notify_ThingSpawnedPatch), typeof(ExposeDataPatch), typeof(MouseOverReadOutOnGUI),
			typeof(DebugToolsGeneralPushGas)
		];
		
		public static object? MonitorObject { get; private set; }
		
		public override string? Description { get; }
			= "Optimizes the gas grid to utilize highly performant bit manipulation for much faster processing of "
			+ "cells, usually in buckets of 64 simultaneously checked cells, and threading to process the different "
			+ "gas types in parallel. Additionally exposes gas to xml through the PerformanceFish.GasDef type. "
			+ "Performance impact scales with map size, but is generally large. Threading requires the threading "
			+ "enabled setting to be turned on. Default is off";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.Tick));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);
		
		public static void ReplacementBody(GasGrid __instance)
		{
			var gasGrids = __instance.ParallelGasGrids();
			if (gasGrids.Length < 1)
				return;

#if !V1_4
			JobHandle handle;
#endif
			if (FishSettings.ThreadingEnabled
#if V1_4
				&& TryGetRegisteredGasGridWorkers(__instance, out var monitorObject))
			{
				TryPulseRegisteredGasGridWorkers(gasGrids, monitorObject);
#else
			)
			{
				handle = new ManagedJobParallelFor(__instance.ParallelJob()).Schedule(gasGrids.Length - 1, 1);
#endif
			}
			else
			{
#if !V1_4
				Unsafe.SkipInit(out handle);
#endif
				for (var i = 1; i < gasGrids.Length; i++)
					gasGrids[i].Tick();
			}
			
			var firstGasGrid = gasGrids[0];
			firstGasGrid.Tick();
			
			if (FishSettings.ThreadingEnabled)
			{
#if V1_4
				WaitForGasGridWorkers(gasGrids);
#else
				handle.Complete();
#endif
			}
		}

#if V1_4
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool TryGetRegisteredGasGridWorkers(GasGrid __instance,
			[NotNullWhen(true)] out object? monitorObject)
			=> (monitorObject = __instance.MonitorObject()) != null
				&& ParallelNoAlloc.HasAnyRegisteredWorkers(monitorObject);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void TryPulseRegisteredGasGridWorkers(ParallelGasGrid[] gasGrids, object monitorObject)
		{
			if (gasGrids.Length > 1)
				ParallelNoAlloc.PulseRegisteredBackgroundWorkers(monitorObject);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void WaitForGasGridWorkers(ParallelGasGrid[] gasGrids)
		{
			if (gasGrids.Length <= 1)
				return;

			var currentTick = TickHelper.TicksGame;
			for (var i = 1; i < gasGrids.Length;)
			{
				Thread.MemoryBarrier();
				if (gasGrids[i].LastFinishedTick == currentTick)
					i++;
			}
		}
#endif
	}

#if !V1_4
	public class ParallelJob : IJobParallelFor
	{
		public ParallelGasGrid[] GasGrids = null!;
			
		public void Execute(int index) => GasGrids[index + 1].Tick();
	}
#endif

	public sealed class ColorAtPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
#if V1_4
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.ColorAt));
#else
			= AccessTools.DeclaredMethod(typeof(SectionLayer_Gas), nameof(SectionLayer_Gas.ColorAt));
#endif
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

#if V1_4
		public static Color ReplacementBody(GasGrid __instance, IntVec3 cell)
		{
			var cellIndex = cell.CellToIndex(__instance.map);

			var gasGrids = __instance.ParallelGasGrids();
			var gasGridCount = gasGrids.Length;

			Span<float> densities = stackalloc float[gasGridCount];
			var sum = 0f;

			for (var i = 0; i < gasGridCount; i++)
				sum += densities[i] = gasGrids[i].DensityAt(cellIndex);

			var resultColor = default(Color);

			for (var i = 0; i < gasGridCount; i++)
				resultColor += gasGrids[i].Color * (densities[i] / sum);

			resultColor.a = Mathf.Lerp(GasGrid.AlphaRange.min, GasGrid.AlphaRange.max,
				sum / (/*gasGridCount*/ 3 * byte.MaxValue)); // maintain vanilla transparency
			
			return resultColor;
		}
#else
		public static Color ReplacementBody(SectionLayer_Gas __instance, IntVec3 cell)
		{
			if (__instance.GetType() != typeof(SectionLayer_Gas))
				return __instance.DensityAt(cell);
			
			var map = __instance.Map;
			var cellIndex = cell.CellToIndex(map);

			var gasGrids = map.gasGrid.ParallelGasGrids();
			var gasGridCount = gasGrids.Length;
			
			Color resultColor;
			resultColor.r = gasGrids[0].DensityAt(cellIndex);
			resultColor.g = gasGrids[1].DensityAt(cellIndex);
			resultColor.b = gasGrids[2].DensityAt(cellIndex);
			resultColor.a = gasGrids[3].DensityAt(cellIndex);

			if (gasGridCount > 4)
			{
				for (var i = gasGridCount; i-- > 4;)
				{
					var density = gasGrids[i].DensityAt(cellIndex);
					if (density > 0)
						resultColor += gasGrids[i].Color * (density * 0.25f);
				}
				// Not remotely accurate, but the shader now essentially applies the previous version's formula with
				// hardcoded values to each density in the result. High values, especially on a, lead to faded results.
				// The choices are basically blind smoke color, tox gas color, rot stink color, dead life dust color, or
				// something in between now
			}
			
			return resultColor;
		}
#endif
	}

	public sealed class AddGasPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.AddGas));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(GasGrid __instance, IntVec3 cell, GasType gasType, int amount,
			bool canOverflow = true)
			=> __instance.ParallelGasGrids()[(int)gasType >> 3].AddGas(cell, amount, canOverflow);
	}

	public sealed class AnyGasAtPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.AnyGasAt), [typeof(int)]);
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(GasGrid __instance, int idx)
		{
			var gasGrids = __instance.ParallelGasGrids();
			for (var i = 0; i < gasGrids.Length; i++)
			{
				if (gasGrids[i].AnyGasAt(idx))
					return true;
			}

			return false;
		}
	}
	
	public sealed class DensityAtPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.DensityAt),
				[typeof(int), typeof(GasType)]);
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static byte ReplacementBody(GasGrid __instance, int index, GasType gasType)
			=> __instance.ParallelGasGrids()[(int)gasType >> 3].DensityAt(index);
	}
	
	public sealed class Debug_ClearAllPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.Debug_ClearAll));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(GasGrid __instance)
		{
			var gasGrids = __instance.ParallelGasGrids();
			
			for (var i = 0; i < gasGrids.Length; i++)
				gasGrids[i].Clear();

			__instance.map.mapDrawer.WholeMapChanged(
#if V1_4
				MapMeshFlag.Gas
#else
				MapMeshFlagDefOf.Gas
#endif
				);
		}
	}
	
	public sealed class Debug_FillAllPatch : FishPrepatch
	{
		public override string? Description { get; } = "Adds custom gases to the debug fill all action";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.Debug_FillAll));

		public static void Postfix(GasGrid __instance)
		{
			var gasGrids = __instance.ParallelGasGrids();
			if (gasGrids.Length < 3)
				return;
			
			var cellCount = __instance.gasDensity.Length;
			var cellIndices = __instance.map.cellIndices;
			
			for (var i = 0; i < cellCount; i++)
			{
				if (!__instance.GasCanMoveTo(cellIndices.IndexToCell(i)))
					continue;

				for (var j = 3; j < gasGrids.Length; j++)
					gasGrids[j].SetDirect(i, 255);
			}
		}
	}
	
	public sealed class Notify_ThingSpawnedPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.Notify_ThingSpawned));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(GasGrid __instance, Thing thing)
		{
			if (!thing.IsSpawned() || thing.def.Fillage != FillCategory.Full)
				return;

			var grids = __instance.ParallelGasGrids();
			var gridCount = grids.Length;
			var map = __instance.map;
			var mapDrawer = map.mapDrawer;
			var mapSizeX = GetMapSizeX(map);

			foreach (var occupiedCell in thing.OccupiedRect())
			{
				var anyGridDirty = false;
				
				for (var i = gridCount; i-- > 0;)
				{
					var grid = grids[i];
					var cellIndex = occupiedCell.CellToIndex(mapSizeX);

					if (!grid.AnyGasAt(cellIndex))
						continue;

					grid.SetDirect(cellIndex, 0);
					anyGridDirty = true;
				}
				
				if (anyGridDirty)
				{
					mapDrawer.MapMeshDirty(occupiedCell,
#if V1_4
				MapMeshFlag.Gas
#else
						MapMeshFlagDefOf.Gas
#endif
					);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetMapSizeX(Map map) => map.cellIndices.mapSizeX;
	}

	public sealed class ExposeDataPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(GasGrid), nameof(GasGrid.ExposeData));

		public static void Prefix(GasGrid __instance)
		{
			if (Scribe.mode != LoadSaveMode.Saving)
				return;
			
			var grids = __instance.ParallelGasGrids();
			var smokeGrid = grids[0];
			var toxicGrid = grids[1];
			var rotStinkGrid = grids[2];
#if !V1_4
			var deadLifeGrid = grids[3];
#endif

			for (var i = smokeGrid.CellCount; i-- > 0;)
			{
				__instance.gasDensity[i]
#if V1_4
					= (rotStinkGrid.DensityAt(i) << 16)
					| (toxicGrid.DensityAt(i) << 8)
					| smokeGrid.DensityAt(i);
#else
					= ((uint)deadLifeGrid.DensityAt(i) << 24)
					| ((uint)rotStinkGrid.DensityAt(i) << 16)
					| ((uint)toxicGrid.DensityAt(i) << 8)
					| smokeGrid.DensityAt(i);
#endif
			}
		}

		public static void Postfix(GasGrid __instance)
		{
			if (Scribe.mode != LoadSaveMode.LoadingVars)
				return;
			
			var grids = __instance.ParallelGasGrids();
			var smokeGrid = grids[0];
			var toxicGrid = grids[1];
			var rotStinkGrid = grids[2];
#if !V1_4
			var deadLifeGrid = grids[3];
#endif
			
			for (var i = smokeGrid.CellCount; i-- > 0;)
			{
				var gasDensityAtCell = __instance.gasDensity[i];
				
				smokeGrid.SetDirect(i, (byte)gasDensityAtCell);
				// ReSharper disable once RedundantCast
				toxicGrid.SetDirect(i, (byte)((uint)gasDensityAtCell >> 8));
				// ReSharper disable once RedundantCast
				rotStinkGrid.SetDirect(i, (byte)((uint)gasDensityAtCell >> 16));
#if !V1_4
				deadLifeGrid.SetDirect(i, (byte)(gasDensityAtCell >> 24));
#endif
			}

			for (var i = grids.Length; i-- > 0;)
			{
				var grid = grids[i];
				grid.CycleIndexDiffusion = __instance.cycleIndexDiffusion;
				grid.CycleIndexDissipation = __instance.cycleIndexDissipation;
			}
		}
	}

	public sealed class DebugToolsGeneralPushGas : FishPrepatch
	{
		public override string? Description { get; } = "Adds a debug action for adding custom gas defs";

		public override MethodBase TargetMethodBase { get; } = methodof(DebugToolsGeneral.PushGas);

		public static List<DebugActionNode> Postfix(List<DebugActionNode> __result)
		{
			var gasDefs = DefDatabase<GasDef>.AllDefsListForReading;

			for (var i = 3; i < gasDefs.Count; i++)
			{
				var gasDef = gasDefs[i];
				
				__result.Add(new(gasDef.LabelCap, DebugActionType.ToolMap,
					() => Find.CurrentMap.gasGrid.AddGas(UI.MouseCell(), gasDef, 5f)));
			}
			
			return __result;
		}
	}

	public sealed class MouseOverReadOutOnGUI : FishPrepatch
	{
		public override string? Description { get; }
			= "Adds custom gas defs to the mouse over readout on the bottom left";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(MouseoverReadout), nameof(MouseoverReadout.MouseoverReadoutOnGUI));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var drawGasMethod = AccessTools.DeclaredMethod(typeof(MouseoverReadout), nameof(MouseoverReadout.DrawGas));

			var instructions = ilProcessor.instructions;

			var lastDrawGasIndex = instructions.Count - instructions.Reverse().FirstIndexOf(instruction
				=> instruction.Operand is MethodReference method && method.Is(drawGasMethod)) - 1;
			
			var heightVariable = instructions[lastDrawGasIndex - 1];
			if (heightVariable.OpCode != OpCodes.Ldloca_S && heightVariable.OpCode != OpCodes.Ldloca)
			{
				ThrowHelper.ThrowInvalidOperationException(
					$"Failed to find height variable for MouseOverReadOutOnGUI patch. Expected Ldloca_S or "
					+ $"Ldloca, got '{heightVariable}' instead.");
			}

			var cellVariable = instructions[lastDrawGasIndex - 4];
			if (!cellVariable.OpCode.Name.Contains("ldloc", StringComparison.OrdinalIgnoreCase))
			{
				ThrowHelper.ThrowInvalidOperationException(
					$"Failed to cell variable for MouseOverReadOutOnGUI patch. Expected Ldloc, got {
						cellVariable} instead.");
			}
			
			ilProcessor.InsertRange(lastDrawGasIndex + 1,
				cellVariable,
				heightVariable,
				(OpCodes.Call, methodof(DrawCustomGases)));
		}
		
		public static void DrawCustomGases(IntVec3 cell, ref float curYOffset)
		{
			var map = Find.CurrentMap;
			var gasGrids = map.gasGrid.ParallelGasGrids();
			var cellIndex = new CellIndex(cell, map);

			for (var i = 3; i < gasGrids.Length; i++)
			{
				var gasGrid = gasGrids[i];
				if (!gasGrid.AnyGasAt(cellIndex))
					continue;

				Widgets.Label(
					new(MouseoverReadout.BotLeft.x, UI.screenHeight - MouseoverReadout.BotLeft.y - curYOffset, 999f,
						999f),
					string.Concat(gasGrid.GasDef.LabelCap, " ",
						(gasGrid.DensityAt(cellIndex) / 255f).ToStringPercent("F0")));
				
				curYOffset += 19f;
			}
		}
	}

	public class ParallelGasGrid
	{
		private ulong[] _gasCoverageForIndicesInRandomOrder;
		private byte[] _gasDensity;
		private volatile int _lastFinishedTick = -1;

		private bool _allowedToRun;

		public event Action<ParallelGasGrid>? Ticked;

		public GasDef GasDef { get; private set; }

		public int LastFinishedTick => _lastFinishedTick;
		
		public int DissipationRate => GasDef.dissipationRate;

		public bool Diffuses => GasDef.diffuses;

		public Color Color => GasDef.color;
		
		public bool CalculateGasEffects => anyGasEverAdded;
		
		public Map Map;
		
		public int
			CycleIndexDiffusion,
			CycleIndexDissipation;
		
		public IntVec3[] CardinalDirections;
		
		public List<IntVec3> CellsInRandomOrder = null!;

		public int[]
			CellIndicesInRandomOrder = null!,
			SourceIndicesOfRandomCells = null!;
		
		public bool anyGasEverAdded;

		private DissipationTicker _dissipationTicker;
		private DiffusionTicker _diffusionTicker;
		
		public int CellCount { get; private set; }
		
#pragma warning disable CS8618
		public ParallelGasGrid(Map map, GasDef gasDef)
#pragma warning restore CS8618
			=> map.InvokeWhenCellIndicesReady(m => Initialize(m, gasDef));

		private void Initialize(Map map, GasDef gasDef)
		{
			GasDef = gasDef;

			_allowedToRun = true switch
			{
				_ when gasDef == GasDefOf.ToxGas => ModLister.BiotechInstalled,
#if !V1_4
				_ when gasDef == GasDefOf.DeadLifeDust => ModLister.AnomalyInstalled,
#endif
				_ => true
			};
			
			Map = map;
			CellCount = map.cellIndices.NumGridCells;
			_gasDensity = new byte[CellCount];
			_gasCoverageForIndicesInRandomOrder = new ulong[((_gasDensity.Length - 1) >> 6) + 1];
			CellIndicesInRandomOrder = new int[CellCount];
			SourceIndicesOfRandomCells = new int[CellCount];
			CardinalDirections = new IntVec3[GenAdj.CardinalDirections.Length];
			Array.Copy(GenAdj.CardinalDirections, CardinalDirections, GenAdj.CardinalDirections.Length);
			// CycleIndexDiffusion = Rand.Range(0, map.Area >> 1);

			_dissipationTicker = new() { GasGrid = this };
			_diffusionTicker = new() { GasGrid = this };

			Ticked += _onFirstTickAction;
		}

		private static Action<ParallelGasGrid> _onFirstTickAction = TickInitialize;

		private static void TickInitialize(ParallelGasGrid grid)
		{
			// map.info is somehow null during ExposeData, which itself runs after CreateComponents and initializes a
			// 2nd copy of every component for each map instance, of which apparently three get created per map, two
			// of those thrown away, so cellsInRandomOrder.GetAll fails and throws when accessed from either
			// CreateComponents or ExposeData while loading a game. A total of 6 instances of each component get
			// created per map while loading it's stupid
			// Also cellsInRandomOrder itself initializes after gasGrid within CreateComponents, but that's easy to
			// solve
			var map = grid.Map;
			grid.CellsInRandomOrder = map.cellsInRandomOrder.GetAll();
			grid.UpdateCellIndicesInRandomOrder();
			
			var mapGasGrid = map.gasGrid;
			grid.CycleIndexDiffusion = mapGasGrid.cycleIndexDiffusion;
			grid.CycleIndexDissipation = mapGasGrid.cycleIndexDissipation;
			
			grid.Ticked -= _onFirstTickAction;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CellToIndex(in IntVec3 cell) => cell.CellToIndex(Map);

		public void UpdateCellIndicesInRandomOrder()
		{
			if (Map.cellIndices.NumGridCells != CellCount)
			{
				Log.Error($"Map size changed from {CellCount} to {
					Map.cellIndices.NumGridCells}. This is not supported.");
				return;
			}

			var mapSizeX = Map.Size.x;
			for (var i = CellIndicesInRandomOrder.Length; i-- > 0;)
			{
				SourceIndicesOfRandomCells[
					CellIndicesInRandomOrder[i] = CellIndicesUtility.CellToIndex(CellsInRandomOrder[i], mapSizeX)] = i;
			}
			
			_gasCoverageForIndicesInRandomOrder.Clear();

			for (var i = _gasDensity.Length; i-- > 0;)
			{
				if (_gasDensity[i] > 0)
					SetDirect(i, _gasDensity[i]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AnyGasAt(int idx) => _gasDensity[idx] > 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AnyGasAt(in IntVec3 cell) => AnyGasAt(CellToIndex(cell));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool AnyGasAt(CellIndex cellIndex) => AnyGasAt(cellIndex.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte DensityAt(int index) => _gasDensity[index];
		
		public byte DensityAt(in IntVec3 cell) => DensityAt(CellToIndex(cell));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte DensityAt(CellIndex cellIndex) => DensityAt(cellIndex.Value);

		public void SetDirect(in IntVec3 cell, byte density) => SetDirect(CellToIndex(cell), density);

		public void SetDirect(CellIndex cellIndex, byte density) => SetDirect(cellIndex.Value, density);
		
		public void SetDirect(int index, byte density)
		{
			_gasDensity[index] = density;

			index = SourceIndicesOfRandomCells[index];

			ref var gasCoverageBucket = ref _gasCoverageForIndicesInRandomOrder[index >> 6];
			var hasGas = density > 0;
			gasCoverageBucket ^= ((ulong)-(long)hasGas.AsInt() ^ gasCoverageBucket) & (1UL << (index & 63));
			anyGasEverAdded |= hasGas;
		}

		public void AddGas(CellIndex cellIndex, int amount, bool canOverflow = true)
			=> AddGas(IndexToCell(cellIndex.Value), amount, canOverflow);
		
		public void AddGas(in IntVec3 cell, int amount, bool canOverflow = true)
		{
			if (amount <= 0 || !GasCanMoveTo(cell) || !_allowedToRun)
				return;
			
			anyGasEverAdded = true;
			var index = CellToIndex(cell);

			SetDirect(index, AdjustedDensity(_gasDensity[index] + amount, out var overflow));
			
			Map.mapDrawer.MapMeshDirty(cell, 
#if V1_4
				MapMeshFlag.Gas
#else
				MapMeshFlagDefOf.Gas
#endif
				);
			
			if (canOverflow && overflow > 0)
				Overflow(cell, overflow);
		}
		
		private static byte AdjustedDensity(int newDensity, out int overflow)
		{
			if (newDensity > 255)
			{
				overflow = newDensity - 255;
				return byte.MaxValue;
			}
			
			overflow = 0;
			if (newDensity < 0)
				return 0;
			
			return (byte)newDensity;
		}
		
		private void Overflow(in IntVec3 cell, int amount)
		{
			if (amount <= 0)
				return;
			
			var remainingAmount = amount;
			
			Map.floodFiller.FloodFill(cell, GasCanMoveTo, c =>
			{
				var num = Mathf.Min(remainingAmount, 255 - DensityAt(c));
				if (num > 0)
				{
					AddGas(c, num, false);
					remainingAmount -= num;
				}

				return remainingAmount <= 0;
			}, GenRadial.NumCellsInRadius(40f), true);
		}

		public void Clear()
		{
			_gasCoverageForIndicesInRandomOrder.Clear();
			_gasDensity.Clear();
			anyGasEverAdded = false;
		}

		private abstract class BitwiseGasTicker
		{
			public required ParallelGasGrid GasGrid;
			public abstract ref int CellCycleIndex { get; }
			
			public void TickCells(int area, int cellCountToTick)
			{
				var cellCycleIndex = CellCycleIndex;
				
				if ((cellCycleIndex & 63) != 0)
					TickOddCells(area, ref cellCycleIndex);
				
				var gasCoverageArray = GasGrid._gasCoverageForIndicesInRandomOrder;

				cellCountToTick >>= 6;
				cellCountToTick += 1;
				for (var i = 0; i < cellCountToTick; i++)
				{
					if (area - cellCycleIndex < 64)
						TickOddCells(area, ref cellCycleIndex);

					var gasCoverage = gasCoverageArray[cellCycleIndex >> 6];
					if (gasCoverage != default)
						TickBucket(gasCoverage, cellCycleIndex);

					cellCycleIndex += 64;
				}

				CellCycleIndex = cellCycleIndex;
			}
			
			private void TickBucket(ulong gasCoverage, int cellCycleIndex)
			{
				for (var i = 0; i < 64;)
				{
					var gasCoverageSlice = (gasCoverage >> i) & 0xFF;
					if (gasCoverageSlice == default)
					{
						i += 8;
						continue;
					}

					for (var j = 0; j < 8; j++)
					{
						if (((gasCoverageSlice >> j) & 1UL) != default)
							TickingAction(cellCycleIndex + i);

						i++;
					}
				}
			}

			public abstract void TickingAction(int cellCycleIndex);

			[MethodImpl(MethodImplOptions.NoInlining)]
			private void TickOddCells(int area, ref int cellCycleIndex)
			{
				if (cellCycleIndex < area)
				{
					var gasCoverage = GasGrid._gasCoverageForIndicesInRandomOrder[cellCycleIndex >> 6];

					do
					{
						if ((cellCycleIndex & 63) == 0)
							return;

						if (((gasCoverage >> cellCycleIndex) & 1UL) != default)
							TickingAction(cellCycleIndex);

						cellCycleIndex++;
					}
					while (cellCycleIndex < area);
				}

				cellCycleIndex = 0;
			}
		}

		private sealed class DissipationTicker : BitwiseGasTicker
		{
			public override ref int CellCycleIndex => ref GasGrid.CycleIndexDissipation;

			public override void TickingAction(int cellCycleIndex)
				=> GasGrid.DissipateGasAt(GasGrid.CellIndicesInRandomOrder[cellCycleIndex]);
		}

		private sealed class DiffusionTicker : BitwiseGasTicker
		{
			public override ref int CellCycleIndex => ref GasGrid.CycleIndexDiffusion;

			public override void TickingAction(int cellCycleIndex)
				=> GasGrid.DiffuseGasAt(GasGrid.CellsInRandomOrder[cellCycleIndex]);
		}
		
		public void Tick()
		{
			try
			{
				if (!CalculateGasEffects)
				{
#if GAS_DEBUG_L1
				if (CheckCurrentlyContainsGas())
				{
					anyGasEverAdded = true;
					Debug.LogIncorrectAnyGasEverAdded(this);
				}
#endif
					return;
				}

				Ticked?.Invoke(this);

				var area = Map.Area;
				// CellsInRandomOrder = Map.cellsInRandomOrder.GetAll();
				// UpdateCellIndicesInRandomOrder();
				// Assigned in PostInitialize instead. Ludeon was likely doing this due to component construction order in
				// Map.ConstructComponents

#if GAS_DEBUG_L2
			using var previousDensity = new PooledArray<byte>(_gasDensity.Length);
			Array.Copy(_gasDensity, 0, previousDensity.BackingArray, 0, _gasDensity.Length);
#endif

				_dissipationTicker.TickCells(area, (area + 63) >> 6); // Mathf.CeilToInt(area * (1f / 64f))
				
				var mapGasGrid = Map.gasGrid;
				mapGasGrid.cycleIndexDissipation = CycleIndexDissipation;

				if (Diffuses)
				{
					_diffusionTicker.TickCells(area, (area + 31) >> 5); // Mathf.CeilToInt(area * (1f / 32f))
					mapGasGrid.cycleIndexDiffusion = CycleIndexDiffusion;
				}

#if GAS_DEBUG_L2
			Debug.LogTotalGasChange(this, previousDensity);
#endif

				if (Map.IsHashIntervalTick(600))
					RecalculateEverHadGas();
			}
			finally
			{
				_lastFinishedTick = TickHelper.TicksGame;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void RecalculateEverHadGas() => anyGasEverAdded = CheckCurrentlyContainsGas();

		private bool CheckCurrentlyContainsGas()
		{
			for (var i = _gasCoverageForIndicesInRandomOrder.Length; i-- > 0;)
			{
				if (_gasCoverageForIndicesInRandomOrder[i] != default)
					return true;
			}

			return false;
		}
		
		private void DissipateGasAt(int index)
		{
#if GAS_DEBUG_L1
			var previousDensity = _gasDensity[index];
			if (previousDensity == 0)
				Debug.LogInvalidDissipationAttempt(this, index);
#endif

			var densityAfterDissipation = Math.Max(_gasDensity[index] - DissipationRate, 0);
			
			Debug.LogDissipationRate(this, index, densityAfterDissipation);
			SetDirect(index, (byte)densityAfterDissipation);

#if GAS_DEBUG_L1
			if (_gasDensity[index] >= previousDensity)
				Debug.LogFailedDissipation(this, index, previousDensity);
#endif

			if (densityAfterDissipation == 0)
			{
				Debug.LogMapMeshDirty(this, index);
				Map.mapDrawer.MapMeshDirty(IndexToCell(index),
#if V1_4
				MapMeshFlag.Gas
#else
					MapMeshFlagDefOf.Gas
#endif
					);
			}
		}

		private void DiffuseGasAt(IntVec3 cell)
		{
			var originCellIndex = CellToIndex(cell);
			int densityAtOriginCell = _gasDensity[originCellIndex];
			if (densityAtOriginCell < 17)
				return;
			
			var diffusedToAnyCell = false;
			CardinalDirections.Shuffle();
			
			for (var i = 0; i < CardinalDirections.Length; i++)
			{
				var otherCell = cell + CardinalDirections[i];
				if (!GasCanMoveTo(otherCell))
					continue;
				
				var otherCellIndex = CellToIndex(otherCell);
				int densityOtOtherCell = _gasDensity[otherCellIndex];
				
				if (!TryDiffuseIndividualGas(ref densityAtOriginCell, ref densityOtOtherCell))
					continue;

				SetDirect(otherCellIndex, (byte)densityOtOtherCell);
				Map.mapDrawer.MapMeshDirty(otherCell,
#if V1_4
				MapMeshFlag.Gas
#else
					MapMeshFlagDefOf.Gas
#endif
					);
				diffusedToAnyCell = true;
				if (densityAtOriginCell < 17)
					break;
			}

			if (!diffusedToAnyCell)
				return;

			SetDirect(originCellIndex, (byte)densityAtOriginCell);
			Map.mapDrawer.MapMeshDirty(cell,
#if V1_4
				MapMeshFlag.Gas
#else
				MapMeshFlagDefOf.Gas
#endif
				);
		}
		
		private static bool TryDiffuseIndividualGas(ref int gasA, ref int gasB)
		{
			if (gasA < 17)
				return false;
			
			var num = Mathf.Abs(gasA - gasB) >> 1;

			if (gasA <= gasB || num < 17)
				return false;

			Debug.LogDiffusionRate(gasA, gasB, num);

			gasA -= num;
			gasB += num;
			
#if GAS_DEBUG_L1
			if (gasA < 0 || gasB < 0)
				Log.Error("Diffused below 0!");
#endif
			
			return true;
		}

		public bool GasCanMoveTo(IntVec3 cell)
		{
			if (!cell.InBounds(Map))
				return false;
			if (cell.Filled(Map))
				return cell.GetDoor(Map)?.Open ?? false;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private IntVec3 IndexToCell(int index)
			=> CellIndicesUtility.IndexToCell(index, Map.cellIndices.mapSizeX);

		/// <summary>
		/// For modders. Performance Fish doesn't automatically scribe added custom defs.
		/// </summary>
		public void Scribe(string label)
			=> MapExposeUtility.ExposeUshort(Map,
				cell => DensityAt(cell),
				(cell, value) => SetDirect(cell, (byte)value),
				label);

		// ReSharper disable UnusedMember.Local
		private static class Debug
		{
			[MethodImpl(MethodImplOptions.NoInlining)]
			[Conditional("GAS_DEBUG_L1")]
			internal static void LogIncorrectAnyGasEverAdded(ParallelGasGrid grid)
			{
				var gasDensities = grid._gasDensity;
				var firstCellIndexWithGas = Array.FindIndex(gasDensities, static g => g > 0);
				
				Log.Error($"GasGrid of type '{grid.GasDef.defName}' contains gas at cell '{
					grid.IndexToCell(firstCellIndexWithGas)}' with index {
						firstCellIndexWithGas}, but anyGasEverAdded=false. This index had a source index of {
							grid.SourceIndicesOfRandomCells[firstCellIndexWithGas]} before randomization");
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			[Conditional("GAS_DEBUG_L1")]
			internal static void LogInvalidDissipationAttempt(ParallelGasGrid grid, int index)
				=> Log.Error($"Tried to dissipate gas at cell '{
					grid.IndexToCell(index)}' with density of 0");

			[MethodImpl(MethodImplOptions.NoInlining)]
			[Conditional("GAS_DEBUG_L1")]
			internal static void LogFailedDissipation(ParallelGasGrid grid, int index, byte previousDensity)
				=> Log.Error($"GasDensity went from '{previousDensity}' to '{grid._gasDensity[index]}' at cell '{
					grid.IndexToCell(index)}' while trying to dissipate. It should've decreased");
			
			[MethodImpl(MethodImplOptions.NoInlining)]
			[Conditional("GAS_DEBUG_L2")]
			internal static void LogTotalGasChange(ParallelGasGrid grid, PooledArray<byte> previousDensity)
				=> Log.Message($"Total density for {grid.GasDef.defName} changed from {
					previousDensity.Sum(static b => b)} to {grid._gasDensity.Sum(static b => b)}");
			
			[Conditional("GAS_DEBUG_L3")]
			internal static void LogMapMeshDirty(ParallelGasGrid grid, int index)
				=> Log.Message($"Dirtied MapMesh for '{grid.GasDef.defName}' at '{index}'");

			[Conditional("GAS_DEBUG_L3")]
			internal static void LogDissipationRate(ParallelGasGrid grid, int index, int desiredResult)
				=> Log.Message($"Dissipating {grid._gasDensity[index] - desiredResult} gas of type '{
					grid.GasDef.defName}' at cell '{grid.IndexToCell(index)}' with target rate {
						grid.DissipationRate}. Was {grid._gasDensity[index]}, becomes {desiredResult}");
			
			[Conditional("GAS_DEBUG_L3")]
			internal static void LogDiffusionRate(int gasA, int gasB, int num)
				=> Log.Message($"Diffusing {num} gas from cell with density {gasA} to cell with density {gasB}");
		}
		// ReSharper restore UnusedMember.Local
	}
}