// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;
using PerformanceFish.Cache;
using StorageSettingsCache
	= PerformanceFish.Cache.ByReference<Verse.Thing, RimWorld.StorageSettings,
		PerformanceFish.Listers.StorageSettingsPatches.StorageSettingsCacheValue>;

namespace PerformanceFish.Listers;

public class StorageSettingsPatches : ClassWithFishPatches
{
	public class AllowedToAccept_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "StorageSettings caching";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(StorageSettings)!.AllowedToAccept(default(Thing));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(StorageSettings __instance, Thing t, ref bool __result, out bool __state)
		{
			ref var cache = ref StorageSettingsCache.GetOrAddReference(new(t, __instance));

			if (cache.Dirty)
				return __state = true;

			__result = cache.AllowedToAccept;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(StorageSettings __instance, Thing t, bool __result, bool __state)
		{
			if (!__state)
				return;

			StorageSettingsCache.Update<StorageSettingsCacheValue, bool>(t, __instance, __result);
		}
	}

	public class TryNotifyChanged_Patch : FishPatch
	{
		public override string Description { get; } = "StorageSettings cache resetting";
		public override Expression<Action> TargetMethod { get; } = static () => default(StorageSettings)!.TryNotifyChanged();

		public static void Postfix(StorageSettings __instance)
		{
			StorageSettingsCache.Get.Clear();
			HaulablesCache.Get.Clear();
			MergeablesCache.Get.Clear();
		}
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public record struct StorageSettingsCacheValue : ICacheable<StorageSettingsCache, bool>
	{
		private int _nextRefreshTick;
		public bool AllowedToAccept;

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}

		public void Update(ref StorageSettingsCache key, bool allowedToAccept)
		{
			AllowedToAccept = allowedToAccept;
			_nextRefreshTick = TickHelper.Add(3072, key.First.thingIDNumber, 2048);
		}
	}
}