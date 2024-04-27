// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Listers;

public sealed class Buildings : ClassWithFishPrepatches
{
	public sealed class AllBuildingsColonistOfClassPatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes building lookups for generic types on ListerBuildings to directly return the result of cached "
			+ "data instead of iterating over all buildings on the map trying to find all matches";
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Building>);

		public static IEnumerable<T> ReplacementBody<T>(ListerBuildings __instance) where T : Building
			=> (IEnumerable<T>)__instance.Cache().ColonistBuildingsByType.GetOrAdd(typeof(T));
	}
	
#if !V1_4
	public sealed class AllColonistBuildingsOfTypePatch : FishPrepatch
	{
		public override string Description { get; }
			= "Same as AllBuildingsColonistOfClass, just there as a new copy in 1.5";
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.AllColonistBuildingsOfType));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Building>);

		public static IEnumerable<T> ReplacementBody<T>(ListerBuildings __instance)
			=> (IEnumerable<T>)__instance.Cache().ColonistBuildingsByType.GetOrAdd(typeof(T));
	}
#endif
	
	public sealed class ColonistsHaveResearchBenchPatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes research bench lookups on ListerBuildings to directly return the result of cached data "
			+ "instead of iterating over all buildings on the map trying to find the bench";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveResearchBench));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(ListerBuildings __instance)
			=> __instance.Cache().ColonistResearchBenches.Count > 0;
	}
	
	public sealed class ColonistsHaveBuildingPatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes building lookups by def on ListerBuildings to directly return the result of cached "
			+ "data instead of iterating over all buildings on the map trying to find any matches";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.ColonistsHaveBuilding),
				[typeof(ThingDef)]);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(ListerBuildings __instance, ThingDef def)
			=> __instance.Cache().ColonistBuildingsByDef.GetOrAdd(def).Count > 0;
	}

#if !V1_4
	public sealed class AllBuildingsColonistOfGroup_Patch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes building lookups by group on ListerBuildings to directly return the result of cached "
			+ "data instead of iterating over all buildings on the map trying to find all matches";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfGroup));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static List<Building> ReplacementBody(ListerBuildings __instance, ThingRequestGroup group)
		{
			var result = ListerBuildings.allBuildingsColonistOfGroupResult;
			result.Clear();
			
			if (group == ThingRequestGroup.Undefined)
				return result;

			var listerThings = TryGetListerThings(__instance);
			if (listerThings != null)
			{
				var thingsOfGroup = listerThings.ThingsInGroup(group);
				var playerFaction = Faction.OfPlayerSilentFail;
				for (var i = 0; i < thingsOfGroup.Count; i++)
				{
					if (thingsOfGroup[i] is Building building && building.Faction == playerFaction)
						result.Add(building);
				}
			}
			else
			{
				FallbackLoop(__instance, group);
			}
			
			return result;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void FallbackLoop(ListerBuildings listerBuildings, ThingRequestGroup group)
		{
			var allBuildingsColonist = listerBuildings.allBuildingsColonist;
			
			for (var i = 0; i < allBuildingsColonist.Count; i++)
			{
				if (group.Includes(allBuildingsColonist[i].def))
					ListerBuildings.allBuildingsColonistOfGroupResult.Add(allBuildingsColonist[i]);
			}
		}

		public static ListerThings? TryGetListerThings(ListerBuildings listerBuildings)
		{
			var maps = Current.Game.Maps;
			for (var i = 0; i < maps.Count; i++)
			{
				if (maps[i].listerBuildings == listerBuildings)
					return maps[i].listerThings;
			}

			return null;
		}
	}
#endif

	public sealed class AllBuildingsColonistOfDef_Patch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes building lookups by def on ListerBuildings to directly return the result of cached "
			+ "data instead of iterating over all buildings on the map trying to find all matches. Relatively large "
			+ "performance impact when having water power plants on the map";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfDef));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

#if !V1_5
		public static IEnumerable<Building> ReplacementBody(ListerBuildings __instance, ThingDef def)
			=> __instance.Cache().ColonistBuildingsByDef.GetOrAdd(def);
#else
		public static List<Building> ReplacementBody(ListerBuildings __instance, ThingDef def)
		{
			var result = ListerBuildings.allBuildingsColonistOfDefResult;
			result.Clear();
			result.AddRange(__instance.Cache().ColonistBuildingsByDef.GetOrAdd(def));
			return result;
		}
#endif
	}

	public sealed class AllBuildingsNonColonistOfDef_Patch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes building lookups by def on ListerBuildings to directly return the result of cached "
			+ "data instead of iterating over all buildings on the map trying to find all matches";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsNonColonistOfDef));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static IEnumerable<Building> ReplacementBody(ListerBuildings __instance, ThingDef def)
			=> __instance.Cache().NonColonistBuildingsByDef.GetOrAdd(def);
	}
	
	public sealed class ColonistsHaveBuildingWithPowerOnPatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes lookups for powered buildings of a given def on ListerBuildings to directly return the result "
			+ "of cached data instead of iterating over all buildings on the map trying to find any matches";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings),
				nameof(ListerBuildings.ColonistsHaveBuildingWithPowerOn));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(ListerBuildings __instance, ThingDef def)
		{
			var colonistBuildingsOfDef = __instance.Cache().ColonistBuildingsByDef.GetOrAdd(def);
			
			for (var i = 0; i < colonistBuildingsOfDef.Count; i++)
			{
				if (colonistBuildingsOfDef[i].TryGetComp<CompPowerTrader>() is not { PowerOn: false })
					return true;
			}

			return false;
		}
	}
	
	public sealed class AddPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; } =
		[
			typeof(AllBuildingsColonistOfClassPatch), typeof(ColonistsHaveResearchBenchPatch),
			typeof(ColonistsHaveBuildingPatch), typeof(AllBuildingsColonistOfDef_Patch),
			typeof(ColonistsHaveBuildingWithPowerOnPatch),
			typeof(DeepResourceGridOptimization.AnyActiveDeepScannersOnMapPatch)
		];
		
		public override string? Description { get; }
			= "Required for all other ListerBuildings patches. This assigns buildings to the cache when they're "
			+ "spawned";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.Add));

		public static void Postfix(ListerBuildings __instance, Building b)
		{
			var def = b.def;
			
			if (def.building is { isNaturalRock: true })
				return;

			var cache = __instance.Cache();
			
			if (b.Faction == Faction.OfPlayer)
			{
				cache.ColonistBuildingsByDef.GetOrAdd(def).Add(b);
				if (b is Building_ResearchBench researchBench)
					cache.ColonistResearchBenches.Add(researchBench);
				
				if (b.TryGetComp<CompDeepScanner>() != null)
					cache.ColonistDeepScanners.Add(b);
				
				var buildingType = b.GetType();

				do
				{
					if (buildingType == null)
						break;

					cache.ColonistBuildingsByType.GetOrAdd(buildingType).Add(b);
				}
				while ((buildingType = buildingType.BaseType) != typeof(Building));
			}
			else
			{
				cache.NonColonistBuildingsByDef.GetOrAdd(def).Add(b);
			}
		}
	}

	public sealed class RemovePatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; } =
		[
			typeof(AllBuildingsColonistOfClassPatch), typeof(ColonistsHaveResearchBenchPatch),
			typeof(ColonistsHaveBuildingPatch), typeof(AllBuildingsColonistOfDef_Patch),
			typeof(ColonistsHaveBuildingWithPowerOnPatch),
			typeof(DeepResourceGridOptimization.AnyActiveDeepScannersOnMapPatch)
		];

		public override string? Description { get; }
			= "Required for all other ListerBuildings patches. This removes buildings from the map when they're "
			+ "despawned";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerBuildings), nameof(ListerBuildings.Remove));

		public static void Postfix(ListerBuildings __instance, Building b)
		{
			var def = b.def;
			
			if (def.building is { isNaturalRock: true })
				return;

			var cache = __instance.Cache();
			
			if (b.Faction == Faction.OfPlayer)
			{
				cache.ColonistBuildingsByDef.GetOrAdd(def).Remove(b);
				if (b is Building_ResearchBench researchBench)
					cache.ColonistResearchBenches.Remove(researchBench);

				cache.ColonistDeepScanners.Remove(b);

				var buildingType = b.GetType();

				do
				{
					if (buildingType == null)
						break;

					cache.ColonistBuildingsByType.GetOrAdd(buildingType).Remove(b);
				}
				while ((buildingType = buildingType.BaseType) != typeof(Building));
			}
			else
			{
				cache.NonColonistBuildingsByDef.GetOrAdd(def).Remove(b);
			}
		}
	}

	public readonly record struct Cache()
	{
		public readonly FishTable<ThingDef, IndexedFishSet<Building>>
			ColonistBuildingsByDef = [],
			NonColonistBuildingsByDef = [];

		public readonly IndexedFishSet<Building_ResearchBench> ColonistResearchBenches = [];
		
		public readonly IndexedFishSet<Building> ColonistDeepScanners = [];

		public readonly FishTable<Type, IList> ColonistBuildingsByType = new()
		{
			ValueInitializer = static type
				=> (IList)Activator.CreateInstance(typeof(IndexedFishSet<>).MakeGenericType(type))
		};
	}
}