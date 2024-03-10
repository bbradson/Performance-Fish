// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;
using StyleDominanceCache
	= PerformanceFish.Cache.ByInt<Verse.Thing, RimWorld.Ideo,
		PerformanceFish.IdeoUtilityCaching.StyleDominanceCacheValue>;

namespace PerformanceFish;

public sealed class IdeoUtilityCaching : ClassWithFishPrepatches
{
	public sealed class GetStyleDominance_Patch : FishPrepatch
	{
		public override string Description { get; }
			= "Caches IdeoUtility.GetStyleDominance to only update about once every 900 ticks or so.";

		public override MethodBase TargetMethodBase { get; } = methodof(IdeoUtility.GetStyleDominance);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Thing? t, Ideo? ideo, ref float __result, out bool __state)
		{
			if (t is null || t.thingIDNumber < 0 || ideo is null)
			{
				__state = false;
				return true;
			}

			ref var cache = ref StyleDominanceCache.GetOrAddReference(new(t.thingIDNumber, ideo.id));

			if (cache.Dirty)
				return __state = true;

			__result = cache.Value;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Thing t, Ideo ideo, bool __state, float __result)
		{
			if (!__state)
				return;

			UpdateCache(t, ideo, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Thing t, Ideo ideo, float __result)
			=> StyleDominanceCache.GetExistingReference(t, ideo).Update(__result, t);
	}

	public record struct StyleDominanceCacheValue()
	{
		public float Value;
		private int _nextRefreshTick = -2;

		public void Update(float value, Thing thing)
		{
			Value = value;
			_nextRefreshTick = TickHelper.Add(900 - 128, thing.thingIDNumber, 256);
			// Pawn_StyleObserverTracker.StyleObservableTick runs every 900 ticks
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}
	}
}