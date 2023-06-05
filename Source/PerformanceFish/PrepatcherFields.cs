// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Planet;
using Prepatcher;
using RimWorld.Planet;

namespace PerformanceFish;

public static class PrepatcherFields
{
	[PrepatcherField]
	[ValueInitializer(nameof(CreateGeneList))]
	public static extern List<Gene> GenesToTick(this Pawn_GeneTracker geneTracker);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateWorldPawnsCache))]
	public static extern ref WorldPawnsOptimization.Cache Cache(this WorldPawns worldPawns);

	[PrepatcherField]
	[ValueInitializer(nameof(CreateBedHashSet))]
	public static extern HashSet<Building_Bed> UniqueContainedBeds(this Room room);

#if false
	[PrepatcherField]
	[ValueInitializer(nameof(CreateHediffList))]
	public static extern List<Hediff> HediffsToTick(this Pawn_HealthTracker healthTracker);

	public static List<Hediff> CreateHediffList() => new();
#endif
	
	public static List<Gene> CreateGeneList() => new();
	public static WorldPawnsOptimization.Cache CreateWorldPawnsCache() => new();
	public static HashSet<Building_Bed> CreateBedHashSet() => new();
}