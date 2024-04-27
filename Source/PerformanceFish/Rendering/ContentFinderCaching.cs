// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Rendering;

public sealed class ContentFinderCaching : ClassWithFishPrepatches
{
	public sealed class Get_Patch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches texture lookups on the ContentFinder. This has a relatively large performance impact when used "
			+ "with mods like humanoid alien races";

		public override MethodBase TargetMethodBase { get; } = methodof(ContentFinder<Texture2D>.Get);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix<T>(string itemPath, bool reportFailure, ref T? __result, out bool __state)
		{
			ref var cache = ref Cache.ByReference<string, CacheValue<T>>.GetOrAddReference(itemPath);
			
			if (!cache.Cached)
				return __state = true;
			
			__result = cache.Value;
			
			return __state = reportFailure && __result is null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix<T>(string itemPath, T? __result, bool __state)
		{
			if (!__state)
				return;
			
			UpdateCache(itemPath, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache<T>(string itemPath, T? __result)
		{
			if (!OnStartup.State.Initialized)
				return;
			
			ref var cache = ref Cache.ByReference<string, CacheValue<T>>.GetExistingReference(itemPath);

			cache.Cached = true;
			cache.Value = __result;
		}
	}

	public record struct CacheValue<T>(bool Cached, T? Value);
}