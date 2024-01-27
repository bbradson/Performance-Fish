// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using PerformanceFish.Events;
using PerformanceFish.Hauling;
using PerformanceFish.Planet;
using Prepatcher;
using RimWorld.Planet;

namespace PerformanceFish;

public static class PrepatcherFields
{
	[PrepatcherField]
	[ValueInitializer(nameof(CreateMapEvents))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern MapEvents.Instanced Events(this Map map);
	
	[PrepatcherField]
	[ValueInitializer(nameof(CreateThingEvents))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ThingEvents.Instanced Events(this Thing thing);
	
	[PrepatcherField]
	[ValueInitializer(nameof(CreateGeneList))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern List<Gene> GenesToTick(this Pawn_GeneTracker geneTracker);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateWorldPawnsCache))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref WorldPawnsOptimization.Cache Cache(this WorldPawns worldPawns);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateBedHashSet))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern HashSet<Building_Bed> UniqueContainedBeds(this Room room);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateIndexMap))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern FishTable<int, int> IndexMap<T>(this ThingOwner<T> thingOwner) where T : Thing;

	[PrepatcherField]
	[ValueInitializer(nameof(CreateIndexMap))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern FishTable<int, int> IndexMapByDef(this ListerThings listerThings);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateIndexMapByGroup))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern FishTable<GroupThingPair, int> IndexMapByGroup(this ListerThings listerThings);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateItemCountGrid))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern Cache.CellGrid<int> ItemCountGrid(this Map map);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateBitCellGrid))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern Cache.BitCellGrid StorageBlockerGrid(this Map map);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateStorageSettingsCache))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref StorageSettingsPatches.StorageSettingsCache Cache(this StorageSettings storageSettings);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateParallelGasGridArray))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern GasGridOptimization.ParallelGasGrid[] ParallelGasGrids(this GasGrid gasGrid);

	[PrepatcherField]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref Texture2D? ExpandingIconCache(this WorldObject worldObject);

	[PrepatcherField]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref Color? ExpandingIconColorCache(this WorldObject worldObject);

	[PrepatcherField]
	[ValueInitializer(nameof(GetDefaultDistrictArray))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref StorageDistrict[] Districts(this SlotGroup slotGroup);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateStorageDistrictGrid))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern Cache.CellGrid<StorageDistrict?> StorageDistrictGrid(this Map map);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateListerBuildingsCache))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref Listers.Buildings.Cache Cache(this ListerBuildings listerBuildings);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateListerHaulablesCache))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref Listers.Haulables.Cache Cache(this ListerHaulables listerHaulables);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateHaulDestinationManagerCache))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref HaulDestinationManagerCache Cache(this HaulDestinationManager manager);

	[PrepatcherField]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern ref object MonitorObject(this GasGrid gasGrid);

	// [PrepatcherField]
	// [ValueInitializer(nameof(CreateHediffCompList))]
	// [MethodImpl(MethodImplOptions.AggressiveInlining)]
	// public static extern List<HediffComp> HediffCompsWithVisible(this HediffWithComps hediff);
	//
	// [PrepatcherField]
	// [ValueInitializer(nameof(CreateHediffCompList))]
	// [MethodImpl(MethodImplOptions.AggressiveInlining)]
	// public static extern List<HediffComp> HediffCompsWithShouldRemove(this HediffWithComps hediff);
	//
	// [PrepatcherField]
	// [ValueInitializer(nameof(CreateHediffCompList))]
	// [MethodImpl(MethodImplOptions.AggressiveInlining)]
	// public static extern List<HediffComp> HediffCompsWithPostTick(this HediffWithComps hediff);

#if false
	[PrepatcherField]
	[ValueInitializer(nameof(CreateHediffList))]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static extern List<Hediff> HediffsToTick(this Pawn_HealthTracker healthTracker);

	public static List<Hediff> CreateHediffList() => new();
#endif
	
	public static List<Gene> CreateGeneList() => [];
	public static WorldPawnsOptimization.Cache CreateWorldPawnsCache() => new();
	public static HashSet<Building_Bed> CreateBedHashSet() => [];
	public static FishTable<int, int> CreateIndexMap() => new();
	public static FishTable<GroupThingPair, int> CreateIndexMapByGroup() => new();
	public static Cache.CellGrid<int> CreateItemCountGrid(Map map) => new(map);
	public static Cache.BitCellGrid CreateBitCellGrid(Map map) => new(map);
	public static MapEvents.Instanced CreateMapEvents() => new();
	public static ThingEvents.Instanced CreateThingEvents() => new();
	public static StorageSettingsPatches.StorageSettingsCache CreateStorageSettingsCache() => new();
	public static StorageDistrict[] GetDefaultDistrictArray() => StorageDistrict.GetDefaultArray();
	public static Cache.CellGrid<StorageDistrict> CreateStorageDistrictGrid(Map map) => new(map);
	public static Listers.Buildings.Cache CreateListerBuildingsCache() => new();
	public static Listers.Haulables.Cache CreateListerHaulablesCache() => new();
	public static HaulDestinationManagerCache CreateHaulDestinationManagerCache() => new();

	public static GasGridOptimization.ParallelGasGrid[] CreateParallelGasGridArray(GasGrid gasGrid)
	{
		DefDatabase<GasDef>.SetIndices();
		
		var map = gasGrid.map;
		var gasDefs = DefDatabase<GasDef>.AllDefsListForReading;

		if (gasDefs.Count < 3)
		{
			Log.Error($"GasDefs are missing! Expected are at least 3, DefDatabase contains {
				gasDefs.Count} instead. ParallelGasGrid won't work correctly in this state.");
		}
		else
		{
			if (gasDefs[0] != GasDefOf.BlindSmoke || gasDefs[1] != GasDefOf.ToxGas || gasDefs[2] != GasDefOf.RotStink)
			{
				Log.Warning("GasDefs out of order. Sorting now to fix.");
				var allGasDefs = gasDefs.ToList();

				allGasDefs.Remove(gasDefs[0] = GasDefOf.BlindSmoke);
				allGasDefs.Remove(gasDefs[1] = GasDefOf.ToxGas);
				allGasDefs.Remove(gasDefs[2] = GasDefOf.RotStink);

				for (var i = 0; i < allGasDefs.Count; i++)
					gasDefs[i + 3] = allGasDefs[i];
				
				DefDatabase<GasDef>.SetIndices();
			}
		}
		
		var grids = new GasGridOptimization.ParallelGasGrid[gasDefs.Count];
		for (var i = 0; i < grids.Length; i++)
			grids[i] = new(map, gasDefs[i]);

		return grids;
	}

	// public static List<HediffComp> CreateHediffCompList() => new();
}