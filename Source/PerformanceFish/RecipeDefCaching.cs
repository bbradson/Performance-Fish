// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using RecipeUsersCache = PerformanceFish.Cache.ByIndex<Verse.RecipeDef, PerformanceFish.RecipeDefCaching.AllRecipeUsers_Patch.RecipeUsersCacheValue>;

namespace PerformanceFish;

public class RecipeDefCaching : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => true;
	public class AllRecipeUsers_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches results of the AllRecipeUsers method. Becomes impactful with lots of unfinished things around, as those check everything for potentially accepting them";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(RecipeDef), nameof(RecipeDef.AllRecipeUsers));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(RecipeDef __instance, ref IEnumerable<ThingDef> __result, out bool __state)
		{
			if (__instance is null || __instance.GetType() != typeof(RecipeDef))
			{
				__state = false;
				return true;
			}

			ref var cache = ref RecipeUsersCache.Get.GetReference(__instance);
			if (cache.ShouldRefreshNow)
				return __state = true;

			__result = cache.recipeUsers;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(RecipeDef __instance, IEnumerable<ThingDef> __result, bool __state)
		{
			if (!__state)
				return;

			var cache = RecipeUsersCache.Get[__instance];

			if (cache.recipeUsers is List<ThingDef> list)
			{
				list.Clear();
				list.AddRange(__result);
			}
			else
			{
				cache.recipeUsers = new List<ThingDef>(__result);
			}
			cache.ShouldRefreshNow = false;

			RecipeUsersCache.Get[__instance] = cache;
		}

		public struct RecipeUsersCacheValue : Cache.IIsRefreshable<RecipeDef, RecipeUsersCacheValue>
		{
			public IEnumerable<ThingDef> recipeUsers;
			private int _nextRefreshFrame;

			public bool ShouldRefreshNow
			{
				get => Time.frameCount > _nextRefreshFrame;
				set => _nextRefreshFrame = value ? 0 : Time.frameCount + 4096 + Math.Abs(Rand.Int % 2048);
			}
			public RecipeUsersCacheValue SetNewValue(RecipeDef key) => throw new NotImplementedException();
		}
	}

	/*public class PotentiallyMissingIngredients_Patch : FirstPriorityFishPatch
	{
		public override Expression<Action> TargetMethod => () => new RecipeDef().PotentiallyMissingIngredients(null, null);
		public static bool Prefix(RecipeDef __instance, Pawn billDoer, Map map, ref IEnumerable<ThingDef> __result)
		{
			if (!Cache<RecipeDef, Pawn, Map, MissingIngredientsCache>.TryGetValue(__instance, billDoer, map, out var cache))
				return true;
			if (__instance.ingredients.Count != 0)
			{
				Log.Warning("RecipeDef ingredients mismatched its cache in Performance Fish, forcing a refresh");
				return true;
			}

			__result = cache.ingredientThingDefs;
			return false;
		}
		public static void Postfix(RecipeDef __instance, Pawn billDoer, Map map, IEnumerable<ThingDef> __result, bool __runOriginal)
		{
			if (!__runOriginal)
				return;
			Cache<RecipeDef, Pawn, Map, MissingIngredientsCache>.Get[new(__instance, billDoer, map)]
				= new() { ingredientThingDefs = __result.ToArray(), firstIngredientsItemCount = __instance.ingredients.FirstOrDefault()?.count ?? 0, ShouldRefreshNow = false };
		}

		public static MissingIngredientsCache GetCache(RecipeDef def, Pawn billDoer, Map map) => Cache<RecipeDef, Pawn, Map, MissingIngredientsCache>.GetValue(def, billDoer, map);
		public struct MissingIngredientsCache : ICanRefresh<Cache<RecipeDef, Pawn, Map, MissingIngredientsCache>, MissingIngredientsCache>
		{
			public ThingDef[] ingredientThingDefs;
			private int _nextRefreshFrame;
			public int firstIngredientsItemCount;

			public bool ShouldRefreshNow
			{
				get => Time.frameCount > _nextRefreshFrame;
				set => _nextRefreshFrame = value ? 0 : Time.frameCount + 2048 + Math.Abs(Rand.Int % 1024);
			}
			public MissingIngredientsCache SetNewValue(Cache<RecipeDef, Pawn, Map, MissingIngredientsCache> key) => throw new NotImplementedException();
		}
	}*/
}