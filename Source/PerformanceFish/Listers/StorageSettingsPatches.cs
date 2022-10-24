// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using StorageSettingsCache = PerformanceFish.Cache.ByReferenceRefreshable<Verse.Thing, RimWorld.StorageSettings, PerformanceFish.Listers.StorageSettingsPatches.FishStorageSettingsInfo>;

namespace PerformanceFish.Listers;

public class StorageSettingsPatches : ClassWithFishPatches
{
	public class AllowedToAccept_Patch : FirstPriorityFishPatch
	{
		public override string Description => "StorageSettings caching";
		public override Expression<Action> TargetMethod => () => default(StorageSettings)!.AllowedToAccept(default(Thing));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool Prefix(StorageSettings __instance, Thing t, ref bool __result, out bool __state)
		{
			var key = new StorageSettingsCache(t, __instance);
			ref var cache = ref StorageSettingsCache.Get.TryGetReferenceUnsafe(ref key);

			if (Unsafe.IsNullRef(ref cache) || cache.ShouldRefreshNow)
				return __state = true;

			__result = cache.allowedToAccept;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(StorageSettings __instance, Thing t, bool __result, bool __state)
		{
			if (!__state)
				return;

			StorageSettingsCache.Get[new(t, __instance)] = new() { ShouldRefreshNow = false, allowedToAccept = __result };
		}
	}

	public class TryNotifyChanged_Patch : FishPatch
	{
		public override string Description => "StorageSettings cache resetting";
		public override Expression<Action> TargetMethod => () => default(StorageSettings)!.TryNotifyChanged();
		public static void Postfix(StorageSettings __instance)
		{
			StorageSettingsCache.Get.Clear();
			HaulablesCache.Get.Clear();
			MergeablesCache.Get.Clear();
		}
	}

	public struct FishStorageSettingsInfo : Cache.IIsRefreshable<StorageSettingsCache, FishStorageSettingsInfo>
	{
		public int nextRefreshTick;
		public bool allowedToAccept;

		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}
		public FishStorageSettingsInfo SetNewValue(StorageSettingsCache key) => throw new NotImplementedException();
	}
}