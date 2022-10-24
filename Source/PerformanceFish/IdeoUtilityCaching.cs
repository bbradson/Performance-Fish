// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Cache;
using StyleDominanceCache = PerformanceFish.Cache.ByIntRefreshable<Verse.Thing, RimWorld.Ideo, PerformanceFish.IdeoUtilityCaching.StyleDominanceCacheValue>;

namespace PerformanceFish;
public class IdeoUtilityCaching : ClassWithFishPatches
{
	public class GetStyleDominance_Patch : FishPatch
	{
		public override string? Description => "Caches IdeoUtility.GetStyleDominance to only update about once every 600 ticks or so. Only active when not using Performance Optimizer, as that mod includes a similar version too.";
		public override bool Enabled => base.Enabled && !ModsConfig.IsActive("Taranchuk.PerformanceOptimizer");
		public override Delegate? TargetMethodGroup => IdeoUtility.GetStyleDominance;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Thing t, Ideo ideo, ref float __result, out bool __state)
		{
			if (t is null || ideo is null || t.thingIDNumber < 0)
			{
				__state = false;
				return true;
			}

			var key = new StyleDominanceCache(t.thingIDNumber, ideo.id);
			ref var cache = ref StyleDominanceCache.Get.TryGetReferenceUnsafe(ref key);

			if (Unsafe.IsNullRef(ref cache) || cache.ShouldRefreshNow)
				return __state = true;

			__result = cache.Value;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Thing t, Ideo ideo, bool __state, float __result)
		{
			if (!__state)
				return;

			StyleDominanceCache.Get[new(t, ideo)] = new(__result);
		}
	}

	public struct StyleDominanceCacheValue : IIsRefreshable<StyleDominanceCache, StyleDominanceCacheValue>
	{
		public float Value;
		private int _nextRefreshTick;

		public StyleDominanceCacheValue(float value)
		{
			Value = value;
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 512 + Math.Abs(Rand.Int % 256); // Pawn_StyleObserverTracker.StyleObservableTick runs every 900 ticks
		}

		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 512 + Math.Abs(Rand.Int % 256);
		}

		StyleDominanceCacheValue IIsRefreshable<StyleDominanceCache, StyleDominanceCacheValue>.SetNewValue(StyleDominanceCache key) => throw new NotImplementedException();
	}
}