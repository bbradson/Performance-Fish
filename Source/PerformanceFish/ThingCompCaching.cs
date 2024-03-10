// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using IdeoForbidsCache
	= PerformanceFish.Cache.ByInt<RimWorld.CompAssignableToPawn_Bed, Verse.Pawn,
		PerformanceFish.ThingCompCaching.IdeoForbidsCacheValue>;

namespace PerformanceFish;

public sealed class ThingCompCaching : ClassWithFishPatches
{
	public sealed class CompAssignableToPawn_Bed_Patch : FishPatch
	{
		public override string Description { get; }
			= "Caches results of CompAssignableToPawn_Bed.IdeoligionForbids"
				.AppendWhen(!ModsConfig.IdeologyActive, ". Requires Ideology");

		public override bool Enabled => base.Enabled && ModsConfig.IdeologyActive;

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.IdeoligionForbids));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(CompAssignableToPawn_Bed __instance, Pawn? pawn, ref bool __result, out bool __state)
		{
			if (pawn is null || __instance.Props.maxAssignedPawnsCount == 1)
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

	public record struct IdeoForbidsCacheValue()
	{
		public bool Result;
		private int _nextRefreshTick = -2;
		private int _assignedPawnsVersion = -2;
		private int _directRelationsVersion = -2;
		private List<Pawn> _assignedPawns = _nullPawns;
		private List<DirectPawnRelation> _directRelations = _nullRelations;
		
		public void Update(CompAssignableToPawn_Bed comp, Pawn pawn, bool result)
		{
			_directRelations = pawn.relations?.DirectRelations ?? _nullRelations;
			_assignedPawns = comp.assignedPawns ?? _nullPawns;
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

		private static readonly List<DirectPawnRelation> _nullRelations = [];
		private static readonly List<Pawn> _nullPawns = [];
	}
}