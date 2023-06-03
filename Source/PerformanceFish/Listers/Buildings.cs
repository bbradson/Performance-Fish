// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;

namespace PerformanceFish.Listers;

public class Buildings : ClassWithFishPatches
{
	/// <summary>
	/// Get Things from the ListerThings dictionary with a rather quick lookup instead of looping over every single element in the lists of listerBuildings
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static List<Thing> ThingsOfDefFast(ListerThings listerThings, ThingDef def)
		=> listerThings.listsByDef.TryGetValue(def) ?? ListerThings.EmptyList;

	public class ColonistsHaveBuilding_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much "
				+ "outside of a few prevented spikes here and there either";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.ColonistsHaveBuilding(default(ThingDef));

		public static bool Prefix(ListerBuildings __instance, ref bool __result, ThingDef def)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			__result = ContainsDefForColonists(lister, def);
			return false;
		}
	}

	public class ColonistsHaveResearchBench_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much "
				+ "outside of a few prevented spikes here and there either";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.ColonistsHaveResearchBench();

		public static bool Prefix(ListerBuildings __instance, ref bool __result)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			var researchBenches = LazyInit.ResearchBenches;
			for (var i = 0; i < researchBenches.Length; i++)
			{
				if (!ContainsDefForColonists(lister, researchBenches[i]))
					continue;

				__result = true;
				break;
			}

			return false;
		}

		public static class LazyInit
		{
			public static ThingDef[] ResearchBenches = DefDatabase<ThingDef>.AllDefsListForReading
				.Where(static def => def.thingClass == typeof(Building_ResearchBench)).ToArray();

			static LazyInit()
			{
				// beforefieldinit
			}
		}
	}

	public class ColonistsHaveBuildingWithPowerOn_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much "
				+ "outside of a few prevented spikes here and there either";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.ColonistsHaveBuildingWithPowerOn(null);

		public static bool Prefix(ListerBuildings __instance, ref bool __result, ThingDef def)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			var things = ThingsOfDefFast(lister, def);
			var playerFaction = Faction.OfPlayerSilentFail;
			for (var i = 0; i < things.Count; i++)
			{
				if (things[i].Faction != playerFaction
					|| things[i].TryGetComp<CompPowerTrader>() is { PowerOn: false })
				{
					continue;
				}

				__result = true;
				break;
			}

			return false;
		}
	}

	public class AllBuildingsColonistOfDef_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Relatively large performance impact when having water power plants on the "
			+ "map.";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.AllBuildingsColonistOfDef(null);

		public static bool Prefix(ListerBuildings __instance, ref IEnumerable<Building> __result, ThingDef def)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			__result = AllBuildingsColonistOfDefReplacement(lister, def);
			return false;
		}

		public static IEnumerable<Building> AllBuildingsColonistOfDefReplacement(ListerThings lister, ThingDef def)
		{
			var things = ThingsOfDefFast(lister, def);
			var playerFaction = Faction.OfPlayerSilentFail;
			for (var i = 0; i < things.Count; i++)
			{
				if (things[i] is Building building)
				{
					if (building.Faction == playerFaction)
						yield return building;
				}
				else
				{
					ThrowWarning(def, things[i]);
					yield break;
				}
			}
		}
	}

	public record struct AllBuildingsColonistOfDefCacheValue
	{
		public List<Building> Buildings;
	}

	public class AllBuildingsNonColonistOfDef_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much "
			+ "outside of a few prevented spikes here and there either";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.AllBuildingsNonColonistOfDef(null);

		public static bool Prefix(ListerBuildings __instance, ref IEnumerable<Building> __result, ThingDef def)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			__result = AllBuildingsNonColonistOfDefReplacement(lister, def);
			return false;
		}

		public static IEnumerable<Building> AllBuildingsNonColonistOfDefReplacement(ListerThings lister, ThingDef def)
		{
			var things = ThingsOfDefFast(lister, def);
			var playerFaction = Faction.OfPlayerSilentFail;
			for (var i = 0; i < things.Count; i++)
			{
				if (things[i] is Building building)
				{
					if (building.Faction != playerFaction)
						yield return building;
				}
				else
				{
					ThrowWarning(def, things[i]);
					yield break;
				}
			}
		}
	}

#if harmonyIsFailingOnThisOne
	public class AllBuildingsColonistOfClass_Patch : FishPatch
	{
		public override string Description { get; }
			= "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much "
			+ "outside of a few prevented spikes here and there either";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerBuildings)!.AllBuildingsColonistOfClass<Building>();

		public static bool Prefix(ListerBuildings __instance, MethodBase __originalMethod, ref IEnumerable __result)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			var type = __originalMethod.GetGenericArguments()[0]; //<-- This always returns Building instead of the
                                                         // type it's actually meant to look for
			DebugLog.Message($"AllBuildingsColonistOfClass running for type {type.Name}");
			if (!Replacements.TryGetValue(type, out var replacement))
			{
				Replacements.Add(type,
					replacement = (GenericReplacementBase)Activator.CreateInstance(
						typeof(GenericReplacement<>).MakeGenericType(type)));
			}

			__result = replacement.Replacement(lister);
			return false;
		}

		public static Dictionary<Type, GenericReplacementBase> Replacements { get; } = new();

		public abstract class GenericReplacementBase
		{
			public abstract IEnumerable Replacement(ListerThings lister);
		}

		public class GenericReplacement<T> : GenericReplacementBase where T : Building
		{
			public override IEnumerable Replacement(ListerThings lister)
			{
				buildingsOfClass ??= DefDatabase<ThingDef>.AllDefsListForReading
					.Where(static def => def.thingClass == typeof(T)).ToArray();
				
				foreach (var def in buildingsOfClass)
				{
					var things = ThingsOfDefFast(lister, def);
					for (var i = 0; i < things.Count; i++)
					{
						if (things[i].Faction == Faction.OfPlayerSilentFail)
							yield return things[i];
					}
				}
			}

			public ThingDef[]? buildingsOfClass;
		}
	}
#endif

	public class Map_Patch : FishPatch
	{
		public override string Description { get; }
			= "Part of the ListerBuildings optimizations. Without this patch mods like Colony Manager throw errors "
				+ "during loading";

		public override MethodBase TargetMethodInfo { get; } = AccessTools.Constructor(typeof(Map));
		public static void Postfix(Map __instance) => _tempMap = __instance;
	}

	private static Map? _tempMap;
	// mapComps get constructed before maps are added to Game.Maps, so this makes the patches work there too

	public static void ThrowWarning(ThingDef def, Thing thing)
		=> Log.Warning($"Tried to get building of def {def}, but its type is {thing.GetType()} instead of Building");

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ListerThings? GetListerThings(ListerBuildings listerBuildings)
	{
		var maps = Current.gameInt.Maps;
		for (var i = 0; i < maps.Count; i++)
		{
			if (maps[i].listerBuildings == listerBuildings)
				return maps[i].listerThings;
		}

		return GetListerThingsUsingTempMapFallback(listerBuildings);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ListerThings? GetListerThingsUsingTempMapFallback(ListerBuildings listerBuildings)
		=> _tempMap!.listerBuildings == listerBuildings ? _tempMap.listerThings : null;

	public static bool ContainsDefForColonists(ListerThings listerThings, ThingDef def)
	{
		if (!listerThings.listsByDef.TryGetValue(def, out var things))
			return false;

		var playerFaction = Faction.OfPlayerSilentFail;
		for (var i = 0; i < things.Count; i++)
		{
			if (things[i].Faction == playerFaction)
				return true;
		}

		return false;
	}
}