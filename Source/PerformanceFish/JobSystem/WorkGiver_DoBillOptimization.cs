// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using Verse.AI;
using IngredientCacheList = PerformanceFish.Utility.KeyedList<Verse.IngredientCount, (System.Collections.Generic.List<Verse.ThingDef> Defs, bool Found)>;
using RecipeIngredientCache = PerformanceFish.Cache.ByIndex<RimWorld.Bill, PerformanceFish.JobSystem.WorkGiver_DoBillOptimization.RecipeIngredientCacheValue>;

namespace PerformanceFish.JobSystem;

public class WorkGiver_DoBillOptimization : ClassWithFishPatches
{
	public static List<Type> BlackListedModExtensions => _blackListedModExtensions ??= new() { Reflection.Type("VFEAncients", "VFEAncients.RecipeExtension_Mend") };
	private static List<Type>? _blackListedModExtensions;

	public class TryFindBestIngredientsHelper_InnerDelegate_Patch : FishPatch
	{
		public override string Description => "Optimizes WorkGiver_DoBill to only check things that get accepted by a bill's filter for its ingredient lookup, instead of looping over everything and then checking filters afterwards. " +
			"Also caches these filter acceptances. Large performance impact.";

		public override MethodBase TargetMethodInfo
			=> AccessTools.FirstMethod(DelegateInstanceType, m
				=> m.GetParameters() is { Length: 1 } parms
				&& parms[0].ParameterType == typeof(Region)
				&& m.ReturnType == typeof(bool)
				&& PatchProcessor.ReadMethodBody(m).Any(pair
					=> (pair.Key == OpCodes.Call || pair.Key == OpCodes.Callvirt)
					&& pair.Value is MethodInfo info
					&& info == AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsMatching))));

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions, MethodBase method)
			=> Reflection.MakeReplacementCall(
				   AccessTools.Method(typeof(TryFindBestIngredientsHelper_InnerDelegate_Patch), nameof(Replacement), generics: new[] { DelegateInstanceType }));

		public static bool Replacement<T>(T __instance, Region r) where T : class
		{
			var instance = _delegateInstance.Get(__instance);
			var thingValidator = _thingValidatorInstance.Get(instance.ThingValidator.Target);

			List<Thing> list;
			if (thingValidator is null) // <-- TryFindBestFixedIngredients, used by biosculptors, goes here. Fuck them
			{
				list = GetDefaultList(r);
			}
			else
			{
				var bill = thingValidator.Bill;

				if (HasNoFilters(bill) // apparently applies to a rim reborn benches?
#if V1_4
					|| bill.billStack?.billGiver is Building_MechGestator
#endif
					|| (BlackListedModExtensions.Exists(b => b != null) && IsBlackListed(bill)))  // <-- Fuck these too
				{
					list = GetDefaultList(r);
				}
				else
				{
					ref var cache = ref RecipeIngredientCache.Get.GetReference(bill);
					if (cache.thingDefs is null || cache.ShouldRefreshNow)
					{
						cache.thingDefs = UpdateCache(bill, r, cache.thingDefs);
						cache.ShouldRefreshNow = false;
						RecipeIngredientCache.Get[bill] = cache;
					}
					list = GetList(r.listerThings.listsByDef, cache.thingDefs);
				}
			}

			ActualLoop(list, r, instance.Pawn, instance.BaseValidator, instance.BillGiverIsPawn);

			//list.Clear();

			instance.RegionsProcessed++;
			if (WorkGiver_DoBill.newRelevantThings.Count > 0 && instance.RegionsProcessed > instance.AdjacentRegionsAvailable)
			{
				//WorkGiver_DoBill.relevantThings.AddRange(WorkGiver_DoBill.newRelevantThings);
				InsertAtCorrectPosition(WorkGiver_DoBill.relevantThings, WorkGiver_DoBill.newRelevantThings, instance.Pawn);

				WorkGiver_DoBill.newRelevantThings.Clear();
				if (instance.FoundAllIngredientsAndChoose(WorkGiver_DoBill.relevantThings))
				{
					instance.FoundAll = true;
					return true;
				}
			}
			return false;
		}

		private static bool HasNoFilters(Bill bill)
			=> bill.ingredientFilter is null
			|| bill.recipe.fixedIngredientFilter is null;

		/*private static bool HasNoFilters(Bill bill)
		{
			if (bill.ingredientFilter is null
				|| bill.recipe.fixedIngredientFilter is null)
			{
				return true;
			}

			var ingredients = bill.recipe.ingredients;
			for (var i = 0; i < ingredients.Count; i++)
			{
				if (ingredients[i].filter is null)
					return true;
			}

			return false;
		}*/

		private static bool IsBlackListed(Bill bill)
			=> bill is null
			|| (bill.recipe.modExtensions?.Exists(e
				=> BlackListedModExtensions.Contains(e.GetType()))
			?? false);

		private static List<Thing> GetDefaultList(Region r) => r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

		public static void InsertAtCorrectPosition(List<Thing> list, List<Thing> thingsToInsert, Pawn pawn)
		{
			var count = thingsToInsert.Count;
			var comparer = _insertAtCorrectPositionComparer;
			for (var i = 0; i < count; i++)
			{
				var thing = thingsToInsert[i];
				comparer.rootCell = WorkGiver_DoBill.GetBillGiverRootCell(thingsToInsert[i], pawn);

				var index = ~list.BinarySearch(thing, comparer);
				if (index < 0)
					index = ~index;
				list.Insert(index, thing);
			}
		}

		private static readonly ThingPositionComparer _insertAtCorrectPositionComparer = new();

		public static void ActualLoop(List<Thing> list, Region r, Pawn pawn, Predicate<Thing> baseValidator, bool billGiverIsPawn)
		{
			var count = list.Count;
			for (var i = 0; i < count; i++)
			{
				var thing = list[i];
				if (!WorkGiver_DoBill.processedThings.Contains(thing)
					&& ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn) && !(billGiverIsPawn && thing.def.IsMedicine) && baseValidator(thing))
				{
					WorkGiver_DoBill.newRelevantThings.Add(thing);
					WorkGiver_DoBill.processedThings.Add(thing);
				}
			}
		}

		public static List<Thing> GetList(Dictionary<ThingDef, List<Thing>> listsByDef, IngredientCacheList cacheList)
		{
			var list = _tempListForIngredients;
			list.Clear();
			var set = _tempSetForIngredientDefs;
			set.Clear();

			for (var i = 0; i < cacheList.Count; i++)
			{
				var (defs, found) = cacheList[i].Value;
				if (found)
					continue;

				var count = defs.Count;
				for (var j = 0; j < count; j++)
				{
					if (listsByDef.TryGetValue(defs[j], out var listByDef) && listByDef.Any())
					{
						if (i > 0 && !set.Add(defs[j]))
							continue;

						list.AddRange(listByDef);
					}
				}
			}

			return list;
		}

		public static IngredientCacheList UpdateCache(Bill bill, Region r, IngredientCacheList? cacheList)
		{
			var recipe = bill.recipe;
			var ingredients = recipe.ingredients;
			var listsByDef = r.Map.listerThings.listsByDef;
			var result = cacheList ?? new(3);

			for (var i = 0; i < ingredients.Count; i++) // generally not larger than 3 in vanilla and anything that tries to be
			{
				if (result.TryGetValue(ingredients[i], out var ingredientList))
				{
					ingredientList.Defs.Clear();
					ingredientList.Found = false;
				}
				else
				{
					result[ingredients[i]] = ingredientList = new(new(), false);
				}

				var filter = ingredients[i].filter;
				var allowedDefCount = filter.allowedDefs.Count;

				if (allowedDefCount < 1)
					continue;

				if (allowedDefCount == 1)
				{
					ingredientList.Defs.Add(filter.allowedDefs.First());
					continue;
				}

				var smallestFilter = bill.ingredientFilter;
				var mediumFilter = recipe.fixedIngredientFilter;
				var largestFilter = filter;

				OrderFiltersBySize(ref smallestFilter, ref mediumFilter, ref largestFilter);

				var cachedSmallestList = AllowedDefsListCache.GetValue(smallestFilter).defs;
				if (cachedSmallestList.Count != smallestFilter.allowedDefs.Count)
				{
					// Log.Warning($"Cached list count: {cachedSmallestList.Count}, actual filter list count: {smallestFilter.allowedDefs.Count}. Forcing a refresh now to fix.");
					ThingFilterPatches.ForceSynchronizeCache(smallestFilter);
					// Log.Warning($"New cached list count: {cachedSmallestList.Count}, actual filter list count: {smallestFilter.allowedDefs.Count}.");
					// didn't trigger in 1.3, but now does. Fuck
				}
				var smallestListCount = cachedSmallestList.Count;
				for (var k = 0; k < smallestListCount; k++)
				{
					var def = cachedSmallestList[k];
					if (listsByDef.TryGetValue(def, out var listByDef) && listByDef.Any()
						&& mediumFilter.Allows(def)
						&& largestFilter.Allows(def))
					{
						ingredientList.Defs.Add(def);
					}
				}
			}

			return result;
		}

		private static void OrderFiltersBySize(ref ThingFilter smallestFilter, ref ThingFilter mediumFilter, ref ThingFilter largestFilter)
		{
			if (smallestFilter.allowedDefs.Count > mediumFilter.allowedDefs.Count)
				(smallestFilter, mediumFilter) = (mediumFilter, smallestFilter);

			if (smallestFilter.allowedDefs.Count > largestFilter.allowedDefs.Count)
				(smallestFilter, largestFilter) = (largestFilter, smallestFilter);

			if (mediumFilter.allowedDefs.Count > largestFilter.allowedDefs.Count)
				(mediumFilter, largestFilter) = (largestFilter, mediumFilter);
		}

		private static readonly List<Thing> _tempListForIngredients = new();
		private static readonly HashSet<ThingDef> _tempSetForIngredientDefs = new();
	}

	private static Type DelegateInstanceType { get; }
		= AccessTools.FirstInner(typeof(WorkGiver_DoBill), t
			=> AccessTools.FirstMethod(t, m
				=> m.Name.Contains($"<{nameof(WorkGiver_DoBill.TryFindBestIngredientsHelper)}>")) != null
			&& AccessTools.Field(t, "pawn") != null
			&& AccessTools.Field(t, "baseValidator") != null
			&& AccessTools.Field(t, "billGiverIsPawn") != null
			&& AccessTools.Field(t, "regionsProcessed") != null
			&& AccessTools.Field(t, "adjacentRegionsAvailable") != null
			&& AccessTools.Field(t, "foundAllIngredientsAndChoose") != null
			&& AccessTools.Field(t, "foundAll") != null
			&& AccessTools.Field(t, "thingValidator") != null)
		?? throw new("Performance Fish failed to find the target type for its WorkGiver_DoBill patch. This means that specific patch won't apply.");

	private static Type ThingValidatorInstanceType { get; }
		= AccessTools.FirstInner(typeof(WorkGiver_DoBill), t
			=> AccessTools.FirstMethod(t, m
				=> m.Name.Contains($"<{nameof(WorkGiver_DoBill.TryFindBestBillIngredients)}>")) != null
			&& AccessTools.Field(t, "bill") != null
			//&& AccessTools.Field(t, "chosen") != null
			//&& AccessTools.Field(t, "billGiver") != null
			/*&& AccessTools.Field(t, "pawn") != null*/)
		?? throw new("Performance Fish failed to find the thingValidator type for its WorkGiver_DoBill patch. This likely flat out breaks the entire patch.");

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static DelegateInstance _delegateInstance { get; }
		= (DelegateInstance)Activator.CreateInstance(typeof(DelegateInstanceImplementation<>).MakeGenericType(DelegateInstanceType));

	[SuppressMessage("Style", "IDE1006:Naming Styles")]
	private static ThingValidatorInstance _thingValidatorInstance { get; }
		= (ThingValidatorInstance)Activator.CreateInstance(typeof(ThingValidatorInstanceImplementation<>).MakeGenericType(ThingValidatorInstanceType));

	private abstract class DelegateInstance
	{
		public abstract DelegateInstance Get(object obj);

		public abstract Pawn Pawn { get; set; }
		public abstract Predicate<Thing> BaseValidator { get; set; }
		public abstract bool BillGiverIsPawn { get; set; }
		public abstract int RegionsProcessed { get; set; }
		public abstract int AdjacentRegionsAvailable { get; set; }
		public abstract Predicate<List<Thing>> FoundAllIngredientsAndChoose { get; set; }
		public abstract bool FoundAll { get; set; }
		public abstract Predicate<Thing> ThingValidator { get; set; }
	}

	private class DelegateInstanceImplementation<T> : DelegateInstance where T : class
	{
#pragma warning disable CS8618
		private T _instance;
#pragma warning restore CS8618

		public override DelegateInstance Get(object obj)
		{
			_instance = (T)obj;
			return this;
		}

		public override Pawn Pawn { get => _pawn(_instance); set => _pawn(_instance) = value; }
		public override Predicate<Thing> BaseValidator { get => _baseValidator(_instance); set => _baseValidator(_instance) = value; }
		public override bool BillGiverIsPawn { get => _billGiverIsPawn(_instance); set => _billGiverIsPawn(_instance) = value; }
		public override int RegionsProcessed { get => _regionsProcessed(_instance); set => _regionsProcessed(_instance) = value; }
		public override int AdjacentRegionsAvailable { get => _adjacentRegionsAvailable(_instance); set => _adjacentRegionsAvailable(_instance) = value; }
		public override Predicate<List<Thing>> FoundAllIngredientsAndChoose { get => _foundAllIngredientsAndChoose(_instance); set => _foundAllIngredientsAndChoose(_instance) = value; }
		public override bool FoundAll { get => _foundAll(_instance); set => _foundAll(_instance) = value; }
		public override Predicate<Thing> ThingValidator { get => _thingValidator(_instance); set => _thingValidator(_instance) = value; }

		private static AccessTools.FieldRef<T, Pawn> _pawn = AccessTools.FieldRefAccess<T, Pawn>("pawn");
		private static AccessTools.FieldRef<T, Predicate<Thing>> _baseValidator = AccessTools.FieldRefAccess<T, Predicate<Thing>>("baseValidator");
		private static AccessTools.FieldRef<T, bool> _billGiverIsPawn = AccessTools.FieldRefAccess<T, bool>("billGiverIsPawn");
		private static AccessTools.FieldRef<T, int> _regionsProcessed = AccessTools.FieldRefAccess<T, int>("regionsProcessed");
		private static AccessTools.FieldRef<T, int> _adjacentRegionsAvailable = AccessTools.FieldRefAccess<T, int>("adjacentRegionsAvailable");
		private static AccessTools.FieldRef<T, Predicate<List<Thing>>> _foundAllIngredientsAndChoose = AccessTools.FieldRefAccess<T, Predicate<List<Thing>>>("foundAllIngredientsAndChoose");
		private static AccessTools.FieldRef<T, bool> _foundAll = AccessTools.FieldRefAccess<T, bool>("foundAll");
		private static AccessTools.FieldRef<T, Predicate<Thing>> _thingValidator = AccessTools.FieldRefAccess<T, Predicate<Thing>>("thingValidator");
	}

	private abstract class ThingValidatorInstance
	{
		public abstract ThingValidatorInstance? Get(object obj);

		public abstract Bill Bill { get; set; }
		//public abstract Thing BillGiver { get; set; }
	}

	private class ThingValidatorInstanceImplementation<T> : ThingValidatorInstance where T : class
	{
#pragma warning disable CS8618
		private T _instance;
#pragma warning restore CS8618

		public override ThingValidatorInstance? Get(object obj)
		{
			if (obj is T t)
			{
				_instance = t;
				return this;
			}
			else
			{
				return null;
			}
		}

		public override Bill Bill { get => _bill(_instance); set => _bill(_instance) = value; }
		//public override Thing BillGiver { get => _billGiver(_instance); set => _billGiver(_instance) = value; }

		private static AccessTools.FieldRef<T, Bill> _bill = AccessTools.FieldRefAccess<T, Bill>("bill");
		//private static AccessTools.FieldRef<T, Thing> _billGiver = AccessTools.FieldRefAccess<T, Thing>("billGiver");
	}

	public class TryFindBestIngredientsHelper_Patch : FishPatch
	{
		public override string Description => "Part of the DoBill optimization";

		public override Delegate TargetMethodGroup => WorkGiver_DoBill.TryFindBestIngredientsHelper;

		public static void Postfix(Predicate<Thing> thingValidator)
		{
			var thingValidatorInstance = _thingValidatorInstance.Get(thingValidator.Target);
			if (thingValidatorInstance is null)
				return;

			var cache = RecipeIngredientCache.Get[thingValidatorInstance.Bill].thingDefs;
			if (cache is null)
				return;

			var items = cache._items;
			for (var i = 0; i < items.Length; i++)
			{
				ref var item = ref items[i];
				if (item.Value.Found)
					item = new(item.Key, new(item.Value.Defs, false));
			}
		}
	}

	public class TryFindBestIngredientsInSet_NoMixHelper_Patch : FishPatch
	{
		public override string Description => "Part of the DoBill optimization";

		public override Delegate TargetMethodGroup => WorkGiver_DoBill.TryFindBestIngredientsInSet_NoMixHelper;

		public static void Prefix(ref bool alreadySorted, Bill? bill)
			=> alreadySorted = alreadySorted
			|| (bill?.billStack is { } stack
#if V1_4
			&& stack.billGiver is not Building_MechGestator
#endif
			);

		public static CodeInstructions? Transpiler(CodeInstructions codes, MethodBase method)
		{
			var i_variable = FishTranspiler.FirstLocalVariable(method, typeof(int));

			return codes.ReplaceAt(
				(codes, i)
					=> i + 3 < codes.Count
					&& codes[i] == i_variable.Load()
					&& codes[i + 1] == FishTranspiler.Constant(1) // i++;
					&& codes[i + 2] == FishTranspiler.Add
					&& codes[i + 3] == i_variable.Store(),
				code
					=> new[]
					{
						FishTranspiler.FirstLocalVariable(method, typeof(IngredientCount))
							.WithLabels(code.ExtractLabels()),
						FishTranspiler.FirstArgument(method, typeof(Bill)),
						FishTranspiler.Call(MarkIngredientCountAsFound),
						code
					}
				);
		}

		public static void MarkIngredientCountAsFound(IngredientCount ingredientCount, Bill bill)
		{
			if (bill?.billStack is not { } billStack
#if V1_4
				|| billStack.billGiver is Building_MechGestator
#endif
				)
			{
				return;
			}

			var cache = RecipeIngredientCache.Get[bill].thingDefs;
			if (cache is null)
				return;

			ref var cacheItem = ref cache._items[cache.IndexOfKey(ingredientCount)];

			if ((cache.Count == 1 && cacheItem.Value.Defs.Count == 1)
				|| bill.recipe?.workerCounterClass != typeof(RecipeWorkerCounter))
			{
				return;
			}

			cacheItem = new(cacheItem.Key, new(cacheItem.Value.Defs, true));
		}
	}

	public class ThingPositionComparer : IComparer<Thing>
	{
		public IntVec3 rootCell;

		public int Compare(Thing x, Thing y) => (x.Position - rootCell).LengthHorizontalSquared.CompareTo((y.Position - rootCell).LengthHorizontalSquared);
	}

	public struct RecipeIngredientCacheValue
	{
		public IngredientCacheList thingDefs;

		private int _nextRefreshTick;

		public RecipeIngredientCacheValue(IngredientCacheList thingDefs)
		{
			this.thingDefs = thingDefs;
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 384 + Math.Abs(Rand.Int % 256);
		}

		public bool ShouldRefreshNow
		{
			get => _nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 384 + Math.Abs(Rand.Int % 256);
		}
	}
}