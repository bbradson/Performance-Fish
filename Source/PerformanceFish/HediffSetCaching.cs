// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Collections;
using HediffCache = PerformanceFish.Cache.ByIntRefreshable<Verse.HediffSet, Verse.HediffDef, PerformanceFish.HediffSetCaching.HediffCacheValue>;
using VisibleHediffCache = PerformanceFish.Cache.ByIntRefreshable<Verse.HediffSet, Verse.HediffDef, PerformanceFish.HediffSetCaching.VisibleHediffCacheValue>;

namespace PerformanceFish;

public class HediffSetCaching : ClassWithFishPatches
{
	public class HasHediff_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches results of the HediffSet.HasHediff method. This scales with the number of hediffs pawns have and can make a decent difference in transhumanist playthroughs.";
		public override Expression<Action> TargetMethod => () => default(HediffSet)!.HasHediff(null, default);
		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.GetCodeInstructions(Replacement);
		public static bool Replacement(HediffSet __instance, HediffDef def, bool mustBeVisible) => __instance.GetFirstHediffOfDef(def, mustBeVisible) != null;
	}

	public class GetFirstHediffOfDef_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches results of the HediffSet.GetFirstHediffOfDef method. This scales with the number of hediffs pawns have and can make a decent difference in transhumanist playthroughs.";
		public override Expression<Action> TargetMethod => () => default(HediffSet)!.GetFirstHediffOfDef(null, default);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(HediffSet __instance, HediffDef def, bool mustBeVisible, ref Hediff? __result, out bool __state)
		{
			if (__instance.hediffs.Count < 5 || def is null || def.GetType() != typeof(HediffDef)) // different types have different indexing. Can't use those
			{
				__state = false;
				return true;
			}

			var key = new HediffCache() { First = __instance.pawn.thingIDNumber, Second = def.index };
			ref var cache = ref mustBeVisible ? ref Unsafe.As<VisibleHediffCacheValue, HediffCacheValue>(ref VisibleHediffCache.Get.TryGetReferenceUnsafe(ref Unsafe.As<HediffCache, VisibleHediffCache>(ref key)))
				: ref HediffCache.Get.TryGetReferenceUnsafe(ref key);

			if (Unsafe.IsNullRef(ref cache) || cache.ShouldRefreshNow)
				return __state = true;

			__result = cache.Hediff;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(HediffSet __instance, HediffDef def, bool mustBeVisible, Hediff? __result, bool __state)
		{
			if (!__state)
				return;

			if (mustBeVisible)
				VisibleHediffCache.Get[new(__instance, def)] = new(__instance, __result);
			else
				HediffCache.Get[new(__instance, def)] = new(__instance, __result);
		}
	}

	public class HediffSet_DirtyCache_Patch : FishPatch
	{
		public override string? Description => "Patched to trigger Psylink cache clearing for the psychic entropy optimization.";
		public override MethodBase TargetMethodInfo => AccessTools.Method(typeof(HediffSet), nameof(HediffSet.DirtyCache));
		public static void Postfix(HediffSet __instance)
		{
			if (__instance.pawn.psychicEntropy is { } entropy)
				entropy.psylinkCachedForTick = 0;
		}
	}

	public struct HediffCacheValue : Cache.IIsRefreshable<HediffCache, HediffCacheValue>
	{
		private int _nextRefreshTick;
		private int _version;
		private HediffSet _set;
		public Hediff? Hediff;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public HediffCacheValue(HediffSet set, Hediff? hediff)
		{
			_set = set;
			Hediff = hediff;
			_version = set.hediffs._version;
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}
		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _nextRefreshTick < Current.Game.tickManager.ticksGameInt || _version != _set.hediffs._version;
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}

		public HediffCacheValue SetNewValue(HediffCache key) => throw new NotImplementedException();
	}

	public struct VisibleHediffCacheValue : Cache.IIsRefreshable<VisibleHediffCache, VisibleHediffCacheValue>
	{
		public HediffCacheValue Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VisibleHediffCacheValue(HediffSet set, Hediff? hediff) => Value = new(set, hediff);

		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Value.ShouldRefreshNow;
			set => Value.ShouldRefreshNow = value;
		}

		public VisibleHediffCacheValue SetNewValue(VisibleHediffCache key) => throw new NotImplementedException();
	}
}