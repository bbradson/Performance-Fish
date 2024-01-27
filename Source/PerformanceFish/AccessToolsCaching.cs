// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;

namespace PerformanceFish;

public sealed class AccessToolsCaching : ClassWithFishPatches
{
	public sealed class AllTypes : FishPatch
	{
		public override string? Description { get; } = "Caches AccessTools.AllTypes to only recalculate when the "
			+ "loaded assembly count changes. Improves load times.";

		public override Delegate? TargetMethodGroup { get; } = AccessTools.AllTypes;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(ref IEnumerable<Type> __result, out bool __state)
		{
			var currentAssemblyCount = AppDomain.CurrentDomain.GetAssemblies().Length;
			if (currentAssemblyCount != _cachedAssemblyCount)
			{
				_cachedAssemblyCount = currentAssemblyCount;
				return __state = true;
			}

			__result = _cachedTypes;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(ref IEnumerable<Type> __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(ref __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(ref IEnumerable<Type> __result) => __result = _cachedTypes = __result.ToArray();

		private static Type[] _cachedTypes = Type.EmptyTypes;
		private static int _cachedAssemblyCount;
	}
}