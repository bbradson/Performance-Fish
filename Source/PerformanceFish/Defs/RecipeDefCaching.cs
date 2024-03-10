// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using RecipeUsersCache
	= PerformanceFish.Cache.ByInt<Verse.RecipeDef,
		PerformanceFish.Defs.RecipeDefCaching.AllRecipeUsers_Patch.RecipeUsersCacheValue>;

namespace PerformanceFish.Defs;

public sealed class RecipeDefCaching : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => true;

	public sealed class AllRecipeUsers_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Caches results of the AllRecipeUsers method. Becomes impactful with lots of unfinished things around, "
			+ "as those check everything for potentially accepting them";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(RecipeDef), nameof(RecipeDef.AllRecipeUsers));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(RecipeDef? __instance, ref IEnumerable<ThingDef> __result, out bool __state)
		{
			if (__instance is null)
			{
				__state = false;
				return true;
			}

			ref var cache = ref RecipeUsersCache.GetOrAddReference(__instance.shortHash);
			if (cache.Dirty)
				return __state = true;

			__result = cache.RecipeUsers;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(RecipeDef __instance, IEnumerable<ThingDef> __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(__instance, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(RecipeDef __instance, IEnumerable<ThingDef> __result)
			=> RecipeUsersCache.GetExistingReference(__instance).Update(__instance, __result);

		public record struct RecipeUsersCacheValue()
		{
			private int _defDatabaseVersion = -2;
			private int _defRecipeUsersVersion = -2;
			private RecipeDef _def;
			public List<ThingDef> RecipeUsers;

			public void Update(RecipeDef instance, IEnumerable<ThingDef> result)
			{
				if (RecipeUsers is { } list)
				{
					list.Clear();
					list.AddRange(result);
				}
				else
				{
					RecipeUsers = [..result];
				}

				_def = instance;
				_defDatabaseVersion = DefDatabase<ThingDef>.defsList._version;
				_defRecipeUsersVersion = instance.recipeUsers?._version ?? 0;
			}

			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
					=> _defDatabaseVersion != DefDatabase<ThingDef>.defsList._version
						|| _defRecipeUsersVersion != (_def.recipeUsers?._version ?? 0);
			}
		}
	}

	/*public sealed class PotentiallyMissingIngredients_Patch : FirstPriorityFishPatch
	{
		public override Expression<Action> TargetMethod { get; }
			= () => new RecipeDef().PotentiallyMissingIngredients(null, null);

		public static bool Prefix(RecipeDef __instance, Pawn billDoer, Map map, ref IEnumerable<ThingDef> __result)
		{
			if (!Cache.ByReference<RecipeDef, Pawn, Map, MissingIngredientsCache>.TryGetValue(__instance, billDoer, map,
				out var cache))
				return true;

			if (__instance.ingredients.Count != 0)
			{
				Log.Warning("RecipeDef ingredients mismatched its cache in Performance Fish, forcing a refresh");
				return true;
			}

			__result = cache.ingredientThingDefs;
			return false;
		}

		public static void Postfix(RecipeDef __instance, Pawn billDoer, Map map, IEnumerable<ThingDef> __result,
			bool __runOriginal)
		{
			if (!__runOriginal)
				return;

			Cache.ByReference<RecipeDef, Pawn, Map, MissingIngredientsCache>.Get[new(__instance, billDoer, map)]
				= new()
				{
					ingredientThingDefs = __result.ToArray(),
					firstIngredientsItemCount = __instance.ingredients.FirstOrDefault()?.count ?? 0,
					ShouldRefreshNow = false
				};
		}

		public static MissingIngredientsCache GetCache(RecipeDef def, Pawn billDoer, Map map)
			=> Cache.ByReference<RecipeDef, Pawn, Map, MissingIngredientsCache>.GetValue(def, billDoer, map);

		public record struct MissingIngredientsCache
		{
			public ThingDef[] ingredientThingDefs;
			private int _nextRefreshFrame;
			public int firstIngredientsItemCount;

			public bool ShouldRefreshNow
			{
				get => Time.frameCount > _nextRefreshFrame;
				set => _nextRefreshFrame = value ? 0 : Time.frameCount + 2048 + Math.Abs(Rand.Int % 1024);
			}

			public MissingIngredientsCache SetNewValue(Cache<RecipeDef, Pawn, Map, MissingIngredientsCache> key)
				=> throw new NotImplementedException();
		}
	}*/
}