// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using AllowedDefsListCache = PerformanceFish.Cache.ByReferenceRefreshable<Verse.ThingFilter, PerformanceFish.ThingFilterPatches.AllowedDefsListCacheValue>;

namespace PerformanceFish;
public class ThingFilterPatches : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => true;
	/*public class Allows_Patch : FishPatch
	{
		public override Expression<Action> TargetMethod => () => new ThingFilter().Allows(null as Thing);
		public static bool Prefix(ThingFilter __instance, ref bool __result, Thing t, out bool __state)
		{
			if (!Cache.ByReferenceRefreshable<Thing, ThingFilter, FishThingFilterInfo>.TryGetValue(t, __instance, out var cache))
				return __state = true;

			__result = cache.allows;
			return __state = false;
		}
		public static void Postfix(ThingFilter __instance, bool __result, Thing t, bool __state)
		{
			if (!__state)
				return;

			ThingFilterAllowsCache[new(t, __instance)] = new() { ShouldRefreshNow = false, allows = __result };
		}
		public static Dictionary<Cache.ByReferenceRefreshable<Thing, ThingFilter, FishThingFilterInfo>, FishThingFilterInfo> ThingFilterAllowsCache => Cache.ByReferenceRefreshable<Thing, ThingFilter, FishThingFilterInfo>.Get;
		public struct FishThingFilterInfo : ICanRefresh<Cache.ByReferenceRefreshable<Thing, ThingFilter, FishThingFilterInfo>, FishThingFilterInfo>
		{
			public int nextRefreshTick;
			public bool allows;

			public bool ShouldRefreshNow
			{
				get => nextRefreshTick < Current.Game.tickManager.TicksGame;
				set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
			}

			public FishThingFilterInfo SetNewValue(Cache<Thing, ThingFilter, FishThingFilterInfo> key) => throw new NotImplementedException();
		}
	}*/

	public class AllowedThingDefs_Patch : FishPatch
	{
		public override string Description => "Small optimization. Returns a list instead of hashset for better performance when enumerating";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllowedThingDefs));
		public static IEnumerable<ThingDef> Replacement(ThingFilter instance) => AllowedDefsListCache.GetValue(instance).defs;
		public static CodeInstructions Transpiler(CodeInstructions codes) => Reflection.GetCodeInstructions(Replacement);
	}

	public class AllStorableThingDefs_Patch : FishPatch
	{
		public override string Description => "Small optimization";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(ThingFilter), nameof(ThingFilter.AllStorableThingDefs));
		public static IEnumerable<ThingDef> Replacement()
		{
			var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
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
		public override string Description => "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";
		public override Expression<Action> TargetMethod => () => default(ThingFilter)!.CopyAllowancesFrom(null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(ThingFilter __instance) => AllowedDefsListCache.GetValue(__instance).defs.Clear();
	}

	public class SetAllow_Patch : FishPatch
	{
		public override string Description => "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";
		public override Expression<Action> TargetMethod => () => default(ThingFilter)!.SetAllow(default(ThingDef), default);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(ThingFilter __instance, ThingDef thingDef, bool allow)
		{
			if (__instance.allowedDefs is null)
				return;

			ref var cache = ref AllowedDefsListCache.GetValue(__instance);

			if (allow)
			{
				if (!__instance.allowedDefs.Contains(thingDef))
				{
					var index = ~Array.BinarySearch(cache.defs._items, 0, cache.defs.Count, thingDef, Comparer);
					//var index = ~cache.defs.BinarySearch(thingDef, Comparer);
					if (index < 0)
						index = ~index;
					cache.defs.Insert(index, thingDef);
				}
			}
			else
			{
				if (__instance.allowedDefs.Contains(thingDef))
					cache.defs.Remove(thingDef);
			}
		}

		private static Comparer<ThingDef> Comparer { get; set; }
			= Comparer<ThingDef>.Create(ComparerMethod);

		private static int ComparerMethod(ThingDef a, ThingDef b)
			=> a.BaseMarketValue.CompareTo(b.BaseMarketValue);
	}

	public class SetDisallowAll_Patch : FishPatch
	{
		public override string Description => "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";
		public override Expression<Action> TargetMethod => () => default(ThingFilter)!.SetDisallowAll(null, null);
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public class SetAllowAll_Patch : FishPatch
	{
		public override string Description => "Used by the WorkGiver_DoBill patch to allow for faster refreshing of its cache";
		public override Expression<Action> TargetMethod => () => default(ThingFilter)!.SetAllowAll(null, default);
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingFilter __instance) => ForceSynchronizeCache(__instance);
	}

	public static void ForceSynchronizeCache(ThingFilter filter) => AllowedDefsListCache.GetValueAndRefreshNow(filter);

	public struct AllowedDefsListCacheValue : Cache.IIsRefreshable<ThingFilter, AllowedDefsListCacheValue>
	{
		public List<ThingDef> defs;
		private int _nextRefreshTick;
		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 16384 + Math.Abs(Rand.Int % 4096);
		}
		public AllowedDefsListCacheValue SetNewValue(ThingFilter key)
		{
			var allowedDefs = key.allowedDefs;
			allowedDefs.Remove(null); // apparently happens

			if (defs is null)
				defs = new(allowedDefs);
			else
				defs.ReplaceContentsWith(allowedDefs);

			SortDefs(defs); // why, actually? I don't remember
			defs.Version()++;

			return this;
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

		private static float[] GetTempArrayForSorting(int defCount) => _tempArrayForSorting.Length >= defCount ? _tempArrayForSorting : _tempArrayForSorting = new float[defCount];
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

		private static float LogAndFixMarketValue(ThingDef def, Exception ex)
		{
			Log.Warning($"Exception thrown while calculating market value for def {def.defName} from mod {def.modContentPack?.Name ?? "null"}. Attempting to fix this now so it doesn't happen again.\n{ex}");
			var recipe = StatWorker_MarketValue.CalculableRecipe(def); //<-- this is slow, but gets cached by perf optimizer. Might be worth replicating

			if (recipe?.ingredients is { } ingredients)
				ingredients.Remove(null);
			else if (def.CostList is { } costList)
				costList.Remove(null);

			try
			{
				return def.BaseMarketValue;
			}
			catch (Exception ex2)
			{
				Log.Error($"Exception thrown while calculating market value for def {def.defName} from mod {def.modContentPack?.Name ?? "null"} again. Fix failed, F.\n{ex2}");
				return 0f;
			}
		}
	}
}