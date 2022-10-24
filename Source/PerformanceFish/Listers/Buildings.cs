// Copyright (c) 2022 bradson
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
	public static List<Thing> ThingsOfDefFast(ListerThings listerThings, ThingDef def) => listerThings.listsByDef.TryGetValue(def) ?? ListerThings.EmptyList;

	public class ColonistsHaveBuilding_Patch : FishPatch
	{
		public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
		public override Expression<Action> TargetMethod => () => default(ListerBuildings)!.ColonistsHaveBuilding(default(ThingDef));
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
		public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
		public override Expression<Action> TargetMethod => () => default(ListerBuildings)!.ColonistsHaveResearchBench();
		public static bool Prefix(ListerBuildings __instance, ref bool __result)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			foreach (var def in LazyInit.ResearchBenches)
			{
				if (ContainsDefForColonists(lister, def))
				{
					__result = true;
					break;
				}
			}
			return false;
		}
		public static class LazyInit
		{
			public static ThingDef[] ResearchBenches = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.thingClass == typeof(Building_ResearchBench)).ToArray();
			static LazyInit() { }
		}
	}

	public class ColonistsHaveBuildingWithPowerOn_Patch : FishPatch
	{
		public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
		public override Expression<Action> TargetMethod => () => default(ListerBuildings)!.ColonistsHaveBuildingWithPowerOn(null);
		public static bool Prefix(ListerBuildings __instance, ref bool __result, ThingDef def)
		{
			var lister = GetListerThings(__instance);
			if (lister is null)
				return true;

			var things = ThingsOfDefFast(lister, def);
			var playerFaction = Faction.OfPlayerSilentFail;
			for (var i = 0; i < things.Count; i++)
			{
				if (things[i].Faction == playerFaction
					&& things[i].TryGetComp<CompPowerTrader>() is not { PowerOn: false })
				{
					__result = true;
					break;
				}
			}
			return false;
		}
	}

	public class AllBuildingsColonistOfDef_Patch : FishPatch
	{
		public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
		public override Expression<Action> TargetMethod => () => default(ListerBuildings)!.AllBuildingsColonistOfDef(null);
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

	public class AllBuildingsNonColonistOfDef_Patch : FishPatch
	{
		public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
		public override Expression<Action> TargetMethod => () => default(ListerBuildings)!.AllBuildingsNonColonistOfDef(null);
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
			public override string Description => "ListerBuildings optimization. Should be relatively riskfree, but doesn't impact performance much outside of a few prevented spikes here and there either";
			public override Expression<Action> TargetMethodExpression => () => default(ListerBuildings).AllBuildingsColonistOfClass<Building>();
			public static bool Prefix(ListerBuildings __instance, MethodBase __originalMethod, ref IEnumerable __result)
			{
				var lister = GetListerThings(__instance);
				if (lister is null)
					return true;

				var type = __originalMethod.GetGenericArguments()[0]; //<-- This always returns Building instead of the type it's actually meant to look for
				DebugLog.Message($"AllBuildingsColonistOfClass running for type {type.Name}");
				if (!Replacements.TryGetValue(type, out var replacement))
					Replacements.Add(type, (GenericReplacementInterface)Activator.CreateInstance(typeof(GenericReplacement<>).MakeGenericType(type)));

				__result = replacement.Replacement(lister);
				return false;
			}

			public static Dictionary<Type, GenericReplacementInterface> Replacements { get; } = new();
			public interface GenericReplacementInterface
			{
				public IEnumerable Replacement(ListerThings lister);
			}
			public class GenericReplacement<T> : GenericReplacementInterface where T : Building
			{
				public IEnumerable Replacement(ListerThings lister)
				{
					buildingsOfClass ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def.thingClass == typeof(T)).ToArray();
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

				public ThingDef[] buildingsOfClass;
			}
		}
#endif

	public class Map_Patch : FishPatch
	{
		public override string Description => "Part of the ListerBuildings optimizations. Without this patch mods like Colony Manager throw errors during loading";
		public override MethodBase TargetMethodInfo => AccessTools.Constructor(typeof(Map));
		public static void Postfix(Map __instance) => _tempMap = __instance;
	}

	private static Map? _tempMap; //mapComps get constructed before maps are added to Game.Maps, so this makes the patches work there too

	public static void ThrowWarning(ThingDef def, Thing thing) => Log.Warning($"Tried to get building of def {def}, but its type is {thing.GetType()} instead of Building");

	public static ListerThings? GetListerThings(ListerBuildings listerBuildings)
	{
		var maps = Current.Game.Maps;
		for (var i = 0; i < maps.Count; i++)
		{
			if (maps[i].listerBuildings == listerBuildings)
				return maps[i].listerThings;
		}

		return _tempMap!.listerBuildings == listerBuildings ? _tempMap!.listerThings : null;
	}

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