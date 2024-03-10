// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using SharedAltarCache
	= PerformanceFish.Cache.ByInt<RimWorld.Ideo,
		PerformanceFish.ThoughtWorker_Precept_AltarSharingOptimization.SharedAltarCacheValue>;

namespace PerformanceFish;

public sealed class ThoughtWorker_Precept_AltarSharingOptimization : ClassWithFishPatches
{
	public sealed class SharedAltar_Patch : FirstPriorityFishPatch
	{
		public override bool Enabled => base.Enabled && ModsConfig.IdeologyActive;

		public override string Description { get; }
			= "Caches results of ideology's shared altar thoughtworker and throttles it to wait a minimum of 128 ticks "
			+ "between refreshes. Quite impactful on performance";

		public override MethodBase TargetMethodInfo { get; } = AccessTools.Method(
			typeof(ThoughtWorker_Precept_AltarSharing), nameof(ThoughtWorker_Precept_AltarSharing.SharedAltar));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(ThoughtWorker_Precept_AltarSharing __instance, Pawn pawn, ref Thing? __result,
			out bool __state)
		{
			if (!pawn.IsSpawned() || pawn.Ideo is not { } ideo)
			{
				__result = null;
				return __state = false;
			}

			ref var cache = ref SharedAltarCache.GetOrAddReference(ideo.id);
			if (cache.Dirty)
				return __state = true;

			__result = cache.thing;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn pawn, Thing __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(pawn, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Pawn pawn, Thing __result)
			=> SharedAltarCache.GetExistingReference(pawn.Ideo.id) = new(pawn, __result);
	}

	public record struct SharedAltarCacheValue
	{
		public Thing? thing;
		private int _nextLateRefreshTick = -2;
		private int _nextEarlyRefreshTick = -2;
		private int _allStructuresListVersion = -2;
		private ListerThings _lister = null!;

		public SharedAltarCacheValue()
		{
		}

		public SharedAltarCacheValue(Pawn pawn, Thing? result)
		{
			thing = result;
			_nextLateRefreshTick = TickHelper.Add(3072, pawn.thingIDNumber, 2048);
			_nextEarlyRefreshTick = TickHelper.Add(128, pawn.thingIDNumber, 128);
			_lister = pawn.GetMap().listerThings;
			_allStructuresListVersion = _lister.listsByGroup[(int)ThingRequestGroup.BuildingArtificial]._version;
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				var ticks = TickHelper.TicksGame;
				return _nextLateRefreshTick < ticks
					|| (_nextEarlyRefreshTick < ticks
						&& _allStructuresListVersion
						!= _lister.listsByGroup[(int)ThingRequestGroup.BuildingArtificial]._version);
			}
		}

		public bool Equals(SharedAltarCacheValue other) => (thing == other.thing) & (_lister == other._lister);

		public override int GetHashCode() => HashCode.Combine(thing, _lister);
	}
}