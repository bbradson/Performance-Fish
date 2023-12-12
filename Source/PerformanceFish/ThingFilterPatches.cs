// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using AllowedDefsListCache
	= PerformanceFish.Cache.ByReference<Verse.ThingFilter,
		PerformanceFish.ThingFilterPatches.AllowedDefsListCacheValue>;
using PerformanceFish.Cache;

namespace PerformanceFish;

public class ThingFilterPatches : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => true;
	/*public class Allows_Patch : FishPatch
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

	public class AllowedThingDefs_Patch : FishPatch
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

	public class AllStorableThingDefs_Patch : FishPatch
	{
		public override string Description { get; } = "Small optimization";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllStorableThingDefs));

		public static IEnumerable<ThingDef> Replacement()
		{
			var allDefs = DefDatabase<ThingDef>.defsList;
			var count = allDefs.Count;
			for (var i = 0; i < count; i++)
			{
				if (allDefs[i].EverStorable(true))
					yield return allDefs[i];
			}
		}

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(Replacement);
	}

	public class CopyAllowancesFrom_Patch : FirstPriorityFishPatch
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

	public class SetAllow_Patch : FishPatch
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

	public class SetDisallowAll_Patch : FishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.SetDisallowAll(null, null);

		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public class SetAllowAll_Patch : FishPatch
	{
		public override string Description { get; }
			= "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ThingFilter)!.SetAllowAll(null, default);

		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public class BestThingRequest_Patch : FishPatch
	{
		public override string? Description { get; }
			= "RimWorld is normally only capable of creating ThingRequests either for predefined groups or for single "
			+ "defs. This patch makes ThingFilters return requests for multiple defs, improving performance for "
			+ "cases where the fallback group would have to be something like all haulables. Refueling benefits a "
			+ "lot from this. Requires the patch on ListerThings.ThingsMatching to function.";

		public override bool Enabled
		{
			set
			{
				if (base.Enabled == value)
					return;

				Get<Listers.Things.ThingsMatching_Patch>().Enabled = base.Enabled = value;
			}
		}

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.BestThingRequest));

		public static void Postfix(ThingFilter __instance, ref ThingRequest __result)
		{
			if (__result.group is not (ThingRequestGroup.HaulableEver or ThingRequestGroup.Everything)
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

	public record struct AllowedDefsListCacheValue : ICacheable<ThingFilter>, ICacheable<ThingFilter, int>
	{
		public List<ThingDef> Defs = new();
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
			allowedDefs.Remove(null); // apparently happens

			Defs.ReplaceContentsWith(allowedDefs);

			SortDefs(Defs); // why, actually? I don't remember
			
			SetDirty(false, tickOffset + Defs.Count + (key.disallowedSpecialFilters?.Count ?? 0));
		}

		private static void SortDefs(List<ThingDef> defs)
		{
			var defCount = defs.Count;
			var defArray = defs._items;
			var marketValues = GetTempArrayForSorting(defCount);
			for (var i = 0; i < defCount; i++)
				marketValues[i] = GetMarketValueSafely(defArray[i]);

			//Array.Sort(marketValues, defs); would require an array of perfect length, generating garbage

			defArray.AsSpan()[..defCount].Sort(marketValues.AsSpan()[..defCount], null);
			defs._version++;
		}

		private static float[] GetTempArrayForSorting(int defCount)
			=> _tempArrayForSorting.Length >= defCount
				? _tempArrayForSorting
				: _tempArrayForSorting = new float[defCount];

		private static float[] _tempArrayForSorting = Array.Empty<float>();

		private static float GetMarketValueSafely(ThingDef def)
		{
			try
			{
				return def.BaseMarketValue;
			}
			catch (Exception ex)
			{
				return LogAndFixMarketValue(def, ex);
			}
		}

		private static float LogAndFixMarketValue(ThingDef def, Exception exception)
		{
			Guard.IsNotNull(def);
			Log.Warning($"Exception thrown while calculating market value for def {def.defName} from mod {
				def.modContentPack?.Name ?? "null"}. Attempting to fix this now so it doesn't happen again.\n{
					exception}");

			RecipeDef? recipe;
			try
			{
				recipe = StatWorker_MarketValue.CalculableRecipe(def);
				// this is slow, but gets cached by perf optimizer. Might be worth replicating
			}
			catch (Exception ex)
			{
				Log.Error($"Exception thrown while calculating recipe for def {def.defName} from mod {
					def.modContentPack?.Name ?? "null"}.\n{ex}");
				recipe = null;
			}

			if (recipe?.ingredients is { } ingredients)
				ingredients.RemoveAll(static ingredientCount => ingredientCount is null);
			
			if (def.CostList is { } costList)
				costList.RemoveAll(static thingDefCountClass => thingDefCountClass?.thingDef is null);

			try
			{
				return def.BaseMarketValue;
			}
			catch (Exception ex)
			{
				Log.Error($"Exception thrown while calculating market value for def {def.defName} from mod {
						def.modContentPack?.Name ?? "null"} again. Fix failed, F.\n{ex}");
				
				return 0f;
			}
		}

		public AllowedDefsListCacheValue()
		{
		}
	}
}