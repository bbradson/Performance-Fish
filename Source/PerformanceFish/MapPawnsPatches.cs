// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if disabled
namespace PerformanceFish;
public static class MapPawnsPatches
{
	public static List<Pawn> GetAllPawns(MapPawns instance)
	{
		List<Pawn> allPawnsUnspawned = instance.AllPawnsUnspawned;
		if (allPawnsUnspawned.Count == 0)
			return instance.pawnsSpawned;
		instance.allPawnsResult.Clear();
		instance.allPawnsResult.AddRange(instance.pawnsSpawned);
		instance.allPawnsResult.AddRange(allPawnsUnspawned);
		return instance.allPawnsResult;
	}

	public static List<Pawn> AllPawnsUnspawned(MapPawns instance)
	{
		instance.allPawnsUnspawnedResult.Clear();
		ThingOwnerUtility.GetAllThingsRecursively(instance.map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), instance.allPawnsUnspawnedResult, allowUnreal: true, null, alsoGetSpawnedThings: false);
		for (int num = instance.allPawnsUnspawnedResult.Count - 1; num >= 0; num--)
		{
			if (instance.allPawnsUnspawnedResult[num].Dead)
				instance.allPawnsUnspawnedResult.RemoveAt(num);
		}
		return instance.allPawnsUnspawnedResult;
	}

	public static List<Pawn> PawnsInFaction(MapPawns instance, Faction faction)
	{
		if (faction == null)
		{
			Log.Error("Called PawnsInFaction with null faction.");
			return new List<Pawn>();
		}
		if (!instance.pawnsInFactionResult.TryGetValue(faction, out var value))
		{
			value = new List<Pawn>();
			instance.pawnsInFactionResult.Add(faction, value);
		}
		value.Clear();
		var allPawns = instance.AllPawns;
		for (var i = 0; i < allPawns.Count; i++)
		{
			if (allPawns[i].Faction == faction)
				value.Add(allPawns[i]);
		}
		return value;
	}

	public static List<Pawn> FreeHumanlikesOfFaction(MapPawns instance, Faction faction)
	{
		if (!instance.freeHumanlikesOfFactionResult.TryGetValue(faction, out var value))
		{
			value = new List<Pawn>();
			instance.freeHumanlikesOfFactionResult.Add(faction, value);
		}
		value.Clear();
		List<Pawn> allPawns = instance.AllPawns;
		for (int i = 0; i < allPawns.Count; i++)
		{
			if (allPawns[i].Faction == faction && (allPawns[i].HostFaction == null || allPawns[i].IsSlave) && allPawns[i].RaceProps.Humanlike)
				value.Add(allPawns[i]);
		}
		return value;
	}

	public static List<Pawn> FreeColonistsAndPrisoners(MapPawns instance)
	{
		List<Pawn> freeColonists = instance.FreeColonists;
		List<Pawn> prisonersOfColony = instance.PrisonersOfColony;
		if (prisonersOfColony.Count == 0)
			return freeColonists;
		instance.freeColonistsAndPrisonersResult.Clear();
		instance.freeColonistsAndPrisonersResult.AddRange(freeColonists);
		instance.freeColonistsAndPrisonersResult.AddRange(prisonersOfColony);
		return instance.freeColonistsAndPrisonersResult;
	}

	public static List<Pawn> PrisonersOfColony(MapPawns instance)
	{
		instance.prisonersOfColonyResult.Clear();
		List<Pawn> allPawns = instance.AllPawns;
		for (int i = 0; i < allPawns.Count; i++)
		{
			if (allPawns[i].IsPrisonerOfColony)
				instance.prisonersOfColonyResult.Add(allPawns[i]);
		}
		return instance.prisonersOfColonyResult;
	}
}
#endif