// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PsyfocusGainCache
	= PerformanceFish.Cache.ByInt<Verse.Pawn, Verse.Thing,
		PerformanceFish.MeditationUtilityCaching.PsyfocusCacheValue>;

namespace PerformanceFish;

// Suggested by https://github.com/notfood
public sealed class MeditationUtilityCaching : ClassWithFishPatches
{
	public sealed class PsyfocusGainPerTick : FirstPriorityFishPatch
	{
		public override string? Description { get; } = "Caches psyfocus gain per tick while the target remains "
			+ "constant. Impactful in psycasting focused playthroughs";

		public override Delegate TargetMethodGroup { get; } = MeditationUtility.PsyfocusGainPerTick;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Pawn pawn, Thing? focus, ref float __result, out bool __state)
		{
			ref var cache = ref PsyfocusGainCache.GetOrAddReference(pawn.thingIDNumber, focus?.thingIDNumber ?? 0);

			if (cache.Dirty)
				return __state = true;

			__result = cache.PsyfocusGain;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn pawn, Thing? focus, float __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(pawn, focus, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Pawn pawn, Thing? focus, float __result)
			=> PsyfocusGainCache.GetExistingReference(pawn.thingIDNumber, focus?.thingIDNumber ?? 0)
				.Update(__result, pawn);
	}

	public record struct PsyfocusCacheValue()
	{
		private int _nextRefreshTick = -2;
		public float PsyfocusGain;

		public void Update(float psyfocusGain, Pawn pawn)
		{
			PsyfocusGain = psyfocusGain;
			_nextRefreshTick = TickHelper.Add(GenTicks.TickRareInterval, pawn.thingIDNumber);
		}

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}
	}
}