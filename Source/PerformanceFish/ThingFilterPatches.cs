// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using AllowedDefsListCache
	= PerformanceFish.Cache.ByReference<Verse.ThingFilter,
		PerformanceFish.ThingFilterPatches.AllowedDefsListCacheValue>;
using System.Linq;
using PerformanceFish.Cache;

namespace PerformanceFish;

public sealed class ThingFilterPatches : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => true;
	/*public sealed class Allows_Patch : FishPatch
	{
		public override Expression<Action> TargetMethod { get; } = static () => new ThingFilter().Allows(null as Thing);

		public static bool Prefix(ThingFilter __instance, ref bool __result, Thing t, out bool __state)
		{
			if (!Cache.ByReference<Thing, ThingFilter, FishThingFilterInfo>.Get.TryGetValue(new(t, __instance),
					out var cache)
				|| cache.Dirty)
				return __state = true;

			__result = cache.allows;
			return __state = false;
		}

		public static void Postfix(ThingFilter __instance, bool __result, Thing t, bool __state)
		{
			if (!__state)
				return;

			ThingFilterAllowsCache[new(t, __instance)] = new() { Dirty = false, allows = __result };
		}

		public static Dictionary<Cache.ByReference<Thing, ThingFilter, FishThingFilterInfo>, FishThingFilterInfo>
			ThingFilterAllowsCache
			=> Cache.ByReference<Thing, ThingFilter, FishThingFilterInfo>.Get;

		public record struct FishThingFilterInfo
		{
			public int nextRefreshTick;
			public bool allows;

			public bool Dirty
			{
				get => nextRefreshTick < Current.Game.tickManager.TicksGame;
				set
					=> nextRefreshTick
						= value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
			}
		}
	}*/

	public sealed class AllowedThingDefs_Patch : FishPatch
	{
		public override string Description { get; }
			= "Small optimization. Returns a list instead of hashset for better performance when enumerating";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedThingDefs));

		public static IEnumerable<ThingDef> Replacement(ThingFilter instance)
			=> AllowedDefsListCache.GetAndCheck<AllowedDefsListCacheValue>(instance).Defs;

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.GetCodeInstructions(Replacement);
	}

	public sealed class AllStorableThingDefs_Patch : FishPatch
	{
		public override string Description { get; } = "Small optimization";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllStorableThingDefs));

		public static ThingDef[]? AllStorableThingDefsCache;

		public static IEnumerable<ThingDef> Replacement() => AllStorableThingDefsCache ?? Update();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static IEnumerable<ThingDef> Update()
			=> Current.ProgramState != ProgramState.Playing
				? Original()
				: AllStorableThingDefsCache = Original().ToArray();

		public static IEnumerable<ThingDef> Original()
			=> DefDatabase<ThingDef>.AllDefs.Where(static def => def.EverStorable(true));

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(Replacement);
	}

	public sealed class CopyAllowancesFrom_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.CopyAllowancesFrom(null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(ThingFilter __instance, ThingFilter other)
		{
			var defs = AllowedDefsListCache.GetAndCheck<AllowedDefsListCacheValue>(__instance).Defs;
			defs.Clear();
			defs.AddRange(other.AllowedThingDefs);
		}
	}

	public sealed class SetAllow_Patch : FishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.SetAllow(default(ThingDef), default);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(ThingFilter __instance, ThingDef? thingDef, bool allow)
		{
			if (__instance.allowedDefs is null || thingDef is null)
				return;

			ref var cache
				= ref AllowedDefsListCache.GetAndCheck<AllowedDefsListCacheValue, int>(__instance, thingDef.shortHash);

			if (allow)
			{
				if (__instance.allowedDefs.Contains(thingDef))
					return;

				var index = ~Array.BinarySearch(cache.Defs._items, 0, cache.Defs.Count, thingDef, Comparer);
				if (index < 0)
					index = ~index;
				cache.Defs.Insert(index, thingDef);
			}
			else
			{
				if (__instance.allowedDefs.Contains(thingDef))
					cache.Defs.Remove(thingDef);
			}
		}

		private static Comparer<ThingDef> Comparer { get; set; }
			= Comparer<ThingDef>.Create(ComparerMethod);

		private static int ComparerMethod(ThingDef a, ThingDef b) => a.BaseMarketValue.CompareTo(b.BaseMarketValue);
	}

	public sealed class SetDisallowAll_Patch : FishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.SetDisallowAll(null, null);

		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public sealed class SetAllowAll_Patch : FishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.SetAllowAll(null, default);

		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public sealed class BestThingRequest_Patch : FishPatch
	{
		public override List<Type> LinkedPatches { get; } = [typeof(Listers.Things.ThingsMatching_Patch)];

		public override string? Description { get; }
			= "RimWorld is normally only capable of creating ThingRequests either for predefined groups or for single "
			+ "defs. This patch makes ThingFilters return requests for multiple defs, improving performance for "
			+ "cases where the fallback group would have to be something like all haulables. Refueling benefits a "
			+ "lot from this. Requires the patch on ListerThings.ThingsMatching to function.";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.BestThingRequest));

		public static void Postfix(ThingFilter __instance, ref ThingRequest __result)
		{
			if (__result.group is not ThingRequestGroup.HaulableEver and not ThingRequestGroup.Everything
				|| __instance.allowedDefs.Count > 16)
			{
				return;
			}

			__result.group = ThingRequestGroup.Undefined;
			__result.singleDef = Unsafe.As<ThingDef>(__instance.allowedDefs);
		}
	}

	public static void ForceSynchronizeCache(ThingFilter filter)
		=> AllowedDefsListCache.GetOrAddReference(filter).Update(ref filter);

	public record struct AllowedDefsListCacheValue() : ICacheable<ThingFilter>, ICacheable<ThingFilter, int>
	{
		public readonly List<ThingDef> Defs = [];
		private int _nextRefreshTick = -2;

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}
		
		public void SetDirty(bool value, int offset)
			=> _nextRefreshTick = value ? 0 : TickHelper.Add(16384, offset, 4096);

		public void Update(ref ThingFilter key) => Update(ref key, GenTicks.TickLongInterval);

		public void Update(ref ThingFilter key, int tickOffset)
		{
			var allowedDefs = key.allowedDefs;
			while (allowedDefs.Remove(null))
				; // apparently happens

			Defs.ReplaceContentsWith(allowedDefs);

			SortDefs(Defs); // TODO: figure out what this was meant for
			
			SetDirty(false, tickOffset + Defs.Count + (key.disallowedSpecialFilters?.Count ?? 0));
		}

		private static void SortDefs(List<ThingDef> defs)
		{
			var defCount = defs.Count;
			var defArray = defs._items;
			var marketValues = GetTempArrayForSorting(defCount);
			for (var i = 0; i < defCount; i++)
				marketValues[i] = defArray[i].BaseMarketValue;

			// Array.Sort(marketValues, defs); has no count argument

			defArray.AsSpan()[..defCount].Sort(marketValues.AsSpan()[..defCount]);
			defs._version++;
		}

		private static float[] GetTempArrayForSorting(int defCount)
			=> _tempArrayForSorting.Length >= defCount
				? _tempArrayForSorting
				: _tempArrayForSorting = new float[defCount];

		private static float[] _tempArrayForSorting = Array.Empty<float>();
	}
}