// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using IdeoForbidsCache = PerformanceFish.Cache.ByIntRefreshable<RimWorld.CompAssignableToPawn_Bed, Verse.Pawn, PerformanceFish.ThingCompCaching.IdeoForbidsCacheValue>;

namespace PerformanceFish;

public class ThingCompCaching : ClassWithFishPatches
{
	public class CompAssignableToPawn_Bed_Patch : FishPatch
	{
		public override string Description => "Caches results of CompAssignableToPawn_Bed.IdeoligionForbids";
		public override MethodBase TargetMethodInfo => AccessTools.Method(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.IdeoligionForbids));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(CompAssignableToPawn_Bed __instance, Pawn pawn, ref bool __result, out bool __state)
		{
			if (pawn is null)
			{
				__state = false;
				return true;
			}

			var key = new IdeoForbidsCache(__instance.parent.thingIDNumber, pawn.thingIDNumber);
			ref var cache = ref IdeoForbidsCache.Get.TryGetReferenceUnsafe(ref key);

			if (Unsafe.IsNullRef(ref cache) || cache.ShouldRefreshNow)
				return __state = true;

			__result = cache.Result;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, bool __result, bool __state)
		{
			if (!__state)
				return;

			IdeoForbidsCache.Get[new(__instance, pawn)] = new(__instance, __result, pawn);
		}
	}

	public struct IdeoForbidsCacheValue : Cache.IIsRefreshable<IdeoForbidsCache, IdeoForbidsCacheValue>
	{
		public bool Result;
		private int _nextRefreshTick;
		private int _assignedPawnsVersion;
		private int _directRelationsVersion;
		private List<Pawn> _assignedPawns;
		private List<DirectPawnRelation> _directRelations;

		public IdeoForbidsCacheValue(CompAssignableToPawn_Bed comp, bool result, Pawn pawn)
		{
			_directRelations = pawn.relations.DirectRelations;
			_assignedPawns = comp.assignedPawns;
			_directRelationsVersion = _directRelations.Version();
			Result = result;
			_assignedPawnsVersion = _assignedPawns.Version();
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 1024 + Math.Abs(Rand.Int % 512);
		}
		public bool ShouldRefreshNow
		{
			get
				=> _nextRefreshTick < Current.Game.tickManager.TicksGame
				|| _assignedPawnsVersion != _assignedPawns.Version()
				|| _directRelationsVersion != _directRelations.Version();

			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 2048 + Math.Abs(Rand.Int % 1024);
		}

		public IdeoForbidsCacheValue SetNewValue(IdeoForbidsCache key) => throw new NotImplementedException();
	}
}