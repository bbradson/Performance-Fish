// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using IdeoForbidsCache
	= PerformanceFish.Cache.ByInt<RimWorld.CompAssignableToPawn_Bed, Verse.Pawn,
		PerformanceFish.ThingCompCaching.IdeoForbidsCacheValue>;

namespace PerformanceFish;

public class ThingCompCaching : ClassWithFishPatches
{
	public class CompAssignableToPawn_Bed_Patch : FishPatch
	{
		public override string Description { get; } = "Caches results of CompAssignableToPawn_Bed.IdeoligionForbids";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.IdeoligionForbids));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(CompAssignableToPawn_Bed __instance, Pawn? pawn, ref bool __result, out bool __state)
		{
			if (pawn is null)
			{
				__state = false;
				return true;
			}

			ref var cache
				= ref IdeoForbidsCache.GetOrAddReference(new(__instance.parent.thingIDNumber, pawn.thingIDNumber));

			if (cache.Dirty)
				return __state = true;

			__result = cache.Result;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(__instance, pawn, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(CompAssignableToPawn_Bed __instance, Pawn pawn, bool __result)
			=> IdeoForbidsCache.GetExistingReference(__instance, pawn).Update(__instance, pawn, __result);
	}

	public record struct IdeoForbidsCacheValue
	{
		public bool Result;
		private int _nextRefreshTick;
		private int _assignedPawnsVersion;
		private int _directRelationsVersion;
		private List<Pawn> _assignedPawns;
		private List<DirectPawnRelation> _directRelations;
		
		public void Update(CompAssignableToPawn_Bed comp, Pawn pawn, bool result)
		{
			_directRelations = pawn.relations.DirectRelations;
			_assignedPawns = comp.assignedPawns;
			_directRelationsVersion = _directRelations._version;
			Result = result;
			_assignedPawnsVersion = _assignedPawns._version;
			_nextRefreshTick = TickHelper.Add(1024, pawn.thingIDNumber);
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
				=> TickHelper.Past(_nextRefreshTick)
					|| _assignedPawnsVersion != _assignedPawns._version
					|| _directRelationsVersion != _directRelations._version;
		}
	}
}