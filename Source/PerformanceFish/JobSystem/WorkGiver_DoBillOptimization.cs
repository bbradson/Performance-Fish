// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using Verse.AI;
using IngredientCacheList
	= PerformanceFish.Utility.KeyedList<Verse.IngredientCount, (System.Collections.Generic.List<Verse.ThingDef> Defs,
		bool Found)>;
using OpCodes = System.Reflection.Emit.OpCodes;
using RecipeIngredientCache
	= PerformanceFish.Cache.ByInt<RimWorld.Bill,
		PerformanceFish.JobSystem.WorkGiver_DoBillOptimization.RecipeIngredientCacheValue>;

namespace PerformanceFish.JobSystem;

public sealed class WorkGiver_DoBillPrepatches : ClassWithFishPrepatches
{
	public static List<Type> BlackListedModExtensions
		=> _blackListedModExtensions ??= InitializeBlackListedModExtensions();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static List<Type> InitializeBlackListedModExtensions()
	{
		var list = new List<Type>();
		
		if (ModCompatibility.Types.VFEAncients.RecipeExtensionMend is { } VFEAncientsRecipeExtension)
			list.Add(VFEAncientsRecipeExtension);
		
		return list;
	}

	private static List<Type>? _blackListedModExtensions;

	public sealed class TryFindBestIngredientsHelper_InnerDelegate_Patch : FishPrepatch
	{
		public override List<string> IncompatibleModIDs { get; }
			= [ModCompatibility.PackageIDs.USE_BEST_MATERIALS];

		public override List<Type> LinkedPatches { get; } =
		[
			typeof(WorkGiver_DoBillOptimization.TryFindBestIngredientsHelper_Patch),
			typeof(WorkGiver_DoBillOptimization.TryFindBestIngredientsInSet_NoMixHelper_Patch)
		];

		public override string Description { get; }
			= "Optimizes WorkGiver_DoBill to only check things that get accepted by a bill's filter for its "
			+ "ingredient lookup, instead of looping over everything and then checking filters afterwards. "
			+ "Also caches these filter acceptances. Large performance impact.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.FirstMethod(DelegateInstanceType, static m
				=> m.GetParameters() is { Length: 1 } parameterInfos
				&& parameterInfos[0].ParameterType == typeof(Region)
				&& m.ReturnType == typeof(bool)
				&& MethodBodyReader.GetInstructions(null, m).Any(static instruction
					=> (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt)
					&& instruction.operand is MethodInfo info
					&& info == AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsMatching))));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(object __instance, Region r)
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
					|| bill.billStack?.billGiver
#if V1_4
						is Building_MechGestator
#else
						is Building_WorkTableAutonomous
#endif
					|| (BlackListedModExtensions.Count > 0
						&& IsBlackListed(bill))) // <-- Fuck these too
				{
					list = GetDefaultList(r);
				}
				else
				{
					ref var cache = ref RecipeIngredientCache.GetOrAddReference(bill.GetLoadID());
					if (cache.thingDefs is null || cache.Dirty)
					{
						cache.thingDefs = UpdateCache(bill, r, cache.thingDefs);
						cache.Update(bill);
					}

					list = GetList(r, cache.thingDefs);
				}
			}

			ActualLoop(list, r, instance.Pawn, instance.BaseValidator, instance.BillGiverIsPawn);

			instance.RegionsProcessed++;
			if (WorkGiver_DoBill.newRelevantThings.Count <= 0
				|| instance.RegionsProcessed <= instance.AdjacentRegionsAvailable)
				return false;

			//WorkGiver_DoBill.relevantThings.AddRange(WorkGiver_DoBill.newRelevantThings);
			InsertAtCorrectPosition(WorkGiver_DoBill.relevantThings, WorkGiver_DoBill.newRelevantThings,
				instance.Pawn);

			WorkGiver_DoBill.newRelevantThings.Clear();
			if (!instance.FoundAllIngredientsAndChoose(WorkGiver_DoBill.relevantThings))
				return false;

			instance.FoundAll = true;
			return true;
		}

		public static bool HasNoFilters(Bill bill)
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

		public static bool IsBlackListed(Bill? bill)
			=> bill is null
				|| bill.recipe.modExtensions.ExistsAndNotNull(static e
						=> BlackListedModExtensions.Contains(e.GetType()));

		public static List<Thing> GetDefaultList(Region r)
			=> r.listerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));

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

		private static readonly ThingPositionComparer _insertAtCorrectPositionComparer
			= new();

		public static void ActualLoop(List<Thing> list, Region r, Pawn pawn, Predicate<Thing> baseValidator,
			bool billGiverIsPawn)
		{
			var count = list.Count;
			for (var i = 0; i < count; i++)
			{
				var thing = list[i];
				if (WorkGiver_DoBill.processedThings.Contains(thing)
					|| !ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch,
						pawn)
					|| (billGiverIsPawn && thing.def.IsMedicine)
					|| !baseValidator(thing))
				{
					continue;
				}

				WorkGiver_DoBill.newRelevantThings.Add(thing);
				WorkGiver_DoBill.processedThings.Add(thing);
			}
		}

		public static List<Thing> GetList(Region region, IngredientCacheList cacheList)
		{
			var listsByDef = region.listerThings.listsByDef;
			
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
					if (!listsByDef.TryGetValue(defs[j], out var listByDef)
						|| !listByDef.Any()
						|| (i > 0 && !set.Add(defs[j])))
					{
						continue;
					}

					list.AddRange(listByDef);
				}
			}

			return list;
		}

		public static IngredientCacheList UpdateCache(Bill bill, Region r, IngredientCacheList? cacheList)
		{
			var recipe = bill.recipe;
			var ingredients = recipe.ingredients;
			var listsByDef = r.GetMap().listerThings.listsByDef;
			var result = cacheList ?? new(3);

			for (var i = 0; i < ingredients.Count;
				i++) // generally not larger than 3 in vanilla and anything that tries to be
			{
				if (result.TryGetValue(ingredients[i], out var ingredientList))
				{
					ingredientList.Defs.Clear();
					ingredientList.Found = false;
				}
				else
				{
					result[ingredients[i]] = ingredientList = new([], false);
				}

				var filter = ingredients[i].filter;

				switch (filter.allowedDefs.Count)
				{
					case < 1:
						continue;
					case 1:
						ingredientList.Defs.Add(filter.allowedDefs.First());
						continue;
				}

				var smallestFilter = bill.ingredientFilter;
				var mediumFilter = recipe.fixedIngredientFilter;
				var largestFilter = filter;

				OrderFiltersBySize(ref smallestFilter, ref mediumFilter, ref largestFilter);

				var cachedSmallestList = AllowedDefsListCache
					.GetAndCheck<ThingFilterPatches.AllowedDefsListCacheValue>(smallestFilter).Defs;
				
				if (cachedSmallestList.Count != smallestFilter.allowedDefs.Count)
				{
					Log.Warning($"Cached list count: {cachedSmallestList.Count}, actual filter list count: {
						smallestFilter.allowedDefs.Count}. Forcing a refresh now to fix.");
					ThingFilterPatches.ForceSynchronizeCache(smallestFilter);
					Log.Warning($"New cached list count: {cachedSmallestList.Count}, actual filter list count: {
						smallestFilter.allowedDefs.Count}.");
				}

				var smallestListCount = cachedSmallestList.Count;
				for (var k = 0; k < smallestListCount; k++)
				{
					var def = cachedSmallestList[k];
					if (listsByDef.TryGetValue(def, out var listByDef)
						&& listByDef.Any()
						&& mediumFilter.Allows(def)
						&& largestFilter.Allows(def))
					{
						ingredientList.Defs.Add(def);
					}
				}
			}

			return result;
		}

		private static void OrderFiltersBySize(ref ThingFilter smallestFilter, ref ThingFilter mediumFilter,
			ref ThingFilter largestFilter)
		{
			if (smallestFilter.allowedDefs.Count > mediumFilter.allowedDefs.Count)
				(smallestFilter, mediumFilter) = (mediumFilter, smallestFilter);

			if (smallestFilter.allowedDefs.Count > largestFilter.allowedDefs.Count)
				(smallestFilter, largestFilter) = (largestFilter, smallestFilter);

			if (mediumFilter.allowedDefs.Count > largestFilter.allowedDefs.Count)
				(mediumFilter, largestFilter) = (largestFilter, mediumFilter);
		}

		private static readonly List<Thing> _tempListForIngredients = [];
		private static readonly HashSet<ThingDef> _tempSetForIngredientDefs = [];
	}

	private static Type DelegateInstanceType { get; }
		= AccessTools.FirstInner(typeof(WorkGiver_DoBill), static t
			=> AccessTools.FirstMethod(t, static m
				=> m.Name.Contains($"<{nameof(WorkGiver_DoBill.TryFindBestIngredientsHelper)}>"))
			!= null
			&& AccessTools.Field(t, "pawn") != null
			&& AccessTools.Field(t, "baseValidator") != null
			&& AccessTools.Field(t, "billGiverIsPawn") != null
			&& AccessTools.Field(t, "regionsProcessed") != null
			&& AccessTools.Field(t, "adjacentRegionsAvailable") != null
			&& AccessTools.Field(t, "foundAllIngredientsAndChoose") != null
			&& AccessTools.Field(t, "foundAll") != null
			&& AccessTools.Field(t, "thingValidator") != null)
		?? ThrowHelper.ThrowInvalidOperationException<Type>("Performance Fish failed to find the target type "
			+ "for its WorkGiver_DoBill patch. This means that specific patch won't apply.");

	private static Type ThingValidatorInstanceType { get; }
		= AccessTools.FirstInner(typeof(WorkGiver_DoBill), static t
				=> AccessTools.FirstMethod(t, static m
					=> m.Name.Contains($"<{nameof(WorkGiver_DoBill.TryFindBestBillIngredients)}>"))
				!= null
				&& AccessTools.Field(t, "bill") != null
			//&& AccessTools.Field(t, "chosen") != null
			//&& AccessTools.Field(t, "billGiver") != null
			/*&& AccessTools.Field(t, "pawn") != null*/)
		?? ThrowHelper.ThrowInvalidOperationException<Type>("Performance Fish failed to find the thingValidator "
			+ "type for its WorkGiver_DoBill patch. This likely flat out breaks the entire patch.");

	// ReSharper disable once InconsistentNaming
	public static DelegateInstance _delegateInstance { get; }
		= (DelegateInstance)Activator.CreateInstance(
			typeof(DelegateInstanceImplementation<>).MakeGenericType(DelegateInstanceType));

	// ReSharper disable once InconsistentNaming
	public static ThingValidatorInstance _thingValidatorInstance { get; }
		= (ThingValidatorInstance)Activator.CreateInstance(
			typeof(ThingValidatorInstanceImplementation<>).MakeGenericType(ThingValidatorInstanceType));

	public abstract class DelegateInstance
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

	private sealed class DelegateInstanceImplementation<T> : DelegateInstance where T : class
	{
#pragma warning disable CS8618
		private T _instance;
#pragma warning restore CS8618

		public override DelegateInstance Get(object obj)
		{
			_instance = (T)obj;
			return this;
		}

		public override Pawn Pawn
		{
			get => _pawn(_instance);
			set => _pawn(_instance) = value;
		}

		public override Predicate<Thing> BaseValidator
		{
			get => _baseValidator(_instance);
			set => _baseValidator(_instance) = value;
		}

		public override bool BillGiverIsPawn
		{
			get => _billGiverIsPawn(_instance);
			set => _billGiverIsPawn(_instance) = value;
		}

		public override int RegionsProcessed
		{
			get => _regionsProcessed(_instance);
			set => _regionsProcessed(_instance) = value;
		}

		public override int AdjacentRegionsAvailable
		{
			get => _adjacentRegionsAvailable(_instance);
			set => _adjacentRegionsAvailable(_instance) = value;
		}

		public override Predicate<List<Thing>> FoundAllIngredientsAndChoose
		{
			get => _foundAllIngredientsAndChoose(_instance);
			set => _foundAllIngredientsAndChoose(_instance) = value;
		}

		public override bool FoundAll
		{
			get => _foundAll(_instance);
			set => _foundAll(_instance) = value;
		}

		public override Predicate<Thing> ThingValidator
		{
			get => _thingValidator(_instance);
			set => _thingValidator(_instance) = value;
		}

		private static AccessTools.FieldRef<T, Pawn> _pawn = AccessTools.FieldRefAccess<T, Pawn>("pawn");

		private static AccessTools.FieldRef<T, Predicate<Thing>> _baseValidator
			= AccessTools.FieldRefAccess<T, Predicate<Thing>>("baseValidator");

		private static AccessTools.FieldRef<T, bool> _billGiverIsPawn
			= AccessTools.FieldRefAccess<T, bool>("billGiverIsPawn");

		private static AccessTools.FieldRef<T, int> _regionsProcessed
			= AccessTools.FieldRefAccess<T, int>("regionsProcessed");

		private static AccessTools.FieldRef<T, int> _adjacentRegionsAvailable
			= AccessTools.FieldRefAccess<T, int>("adjacentRegionsAvailable");

		private static AccessTools.FieldRef<T, Predicate<List<Thing>>> _foundAllIngredientsAndChoose
			= AccessTools.FieldRefAccess<T, Predicate<List<Thing>>>("foundAllIngredientsAndChoose");

		private static AccessTools.FieldRef<T, bool> _foundAll = AccessTools.FieldRefAccess<T, bool>("foundAll");

		private static AccessTools.FieldRef<T, Predicate<Thing>> _thingValidator
			= AccessTools.FieldRefAccess<T, Predicate<Thing>>("thingValidator");
	}

	public abstract class ThingValidatorInstance
	{
		public abstract ThingValidatorInstance? Get(object obj);

		public abstract Bill Bill { get; set; }
		//public abstract Thing BillGiver { get; set; }
	}

	private sealed class ThingValidatorInstanceImplementation<T> : ThingValidatorInstance where T : class
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

		public override Bill Bill
		{
			get => _bill(_instance);
			set => _bill(_instance) = value;
		}
		//public override Thing BillGiver { get => _billGiver(_instance); set => _billGiver(_instance) = value; }

		private static AccessTools.FieldRef<T, Bill> _bill = AccessTools.FieldRefAccess<T, Bill>("bill");
		//private static AccessTools.FieldRef<T, Thing> _billGiver = AccessTools.FieldRefAccess<T, Thing>("billGiver");
	}
}

public sealed class WorkGiver_DoBillOptimization : ClassWithFishPatches
{
	public sealed class TryFindBestIngredientsHelper_Patch : FishPatch
	{
		public override bool ShowSettings => false;

		public override string Description { get; } = "Part of the DoBill optimization";

		public override Delegate TargetMethodGroup { get; } = WorkGiver_DoBill.TryFindBestIngredientsHelper;

		public static void Postfix(Predicate<Thing> thingValidator)
		{
			var thingValidatorInstance = WorkGiver_DoBillPrepatches._thingValidatorInstance.Get(thingValidator.Target);
			if (thingValidatorInstance is null)
				return;

			var cache = RecipeIngredientCache.GetOrAdd(thingValidatorInstance.Bill.loadID).thingDefs;
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

	public sealed class TryFindBestIngredientsInSet_NoMixHelper_Patch : FishPatch
	{
		public override List<Type> LinkedPatches { get; } = [typeof(TryFindBestIngredientsHelper_Patch)];
		
		public override string Description { get; }
			= "Part of the DoBill optimization. Bills that need multiple ingredients, like for example steel and "
			+ "components for guns, normally scan their surroundings for all those ingredients and count them, until "
			+ "enough of both is found. This patch removes ingredients from the lookup when enough of them is found, "
			+ "allowing the patched method to skip counting them and go straight to the other missing ingredient. In "
			+ "case of steel and components that could mean not having to go over 100s of steel stacks at times. "
			+ "Prevents large spikes from these situations, does nothing for all other bills.";

		public override Delegate TargetMethodGroup { get; } = WorkGiver_DoBill.TryFindBestIngredientsInSet_NoMixHelper;

		public static void Prefix(ref bool alreadySorted, Bill? bill)
			=> alreadySorted = alreadySorted
				|| (bill?.billStack is { } stack
					// ReSharper disable once MergeIntoPattern
					&& stack.billGiver is not
#if V1_4
						Building_MechGestator
#else
						Building_WorkTableAutonomous
#endif
				);

		public static CodeInstructions Transpiler(CodeInstructions instructions, MethodBase method,
			ILGenerator generator)
		{
			var i_variable = FishTranspiler.FirstLocalVariable(method, typeof(int));
			var new_i_label = generator.DefineLabel();

			return instructions.ReplaceAt((codes, i)
					=> i + 3 < codes.Count
					&& codes[i] == i_variable.Load()
					&& codes[i + 1] == FishTranspiler.Constant(1) // i++;
					&& codes[i + 2] == FishTranspiler.Add
					&& codes[i + 3] == i_variable.Store(),
				code =>
					[
						new(OpCodes.Br_S, new_i_label), // else
						FishTranspiler.FirstLocalVariable(method, typeof(IngredientCount))
							.WithLabels(code.ExtractLabels()),
						FishTranspiler.FirstArgument(method, typeof(Bill)),
						FishTranspiler.Call(MarkIngredientCountAsFound),
						code.WithLabels(new_i_label)
					]);

			// if (!flag)
			// {
			// 	if (missingIngredients == null)
			// 		return false;
			// 	missingIngredients.Add(ingredientCount);
			// }
			// else
			// {
			//  	MarkIngredientCountAsFound(ingredientCount, bill);	
			// }
		}

		public static void MarkIngredientCountAsFound(IngredientCount ingredientCount, Bill? bill)
		{
			if (bill?.billStack is not { } billStack
				|| billStack.billGiver is
#if V1_4
					Building_MechGestator
#else
					Building_WorkTableAutonomous
#endif
			)
			{
				return;
			}

			var cache = RecipeIngredientCache.GetOrAdd(bill.loadID).thingDefs;
			if (cache is null)
				return;

			ref var cacheItem = ref cache._items[cache.IndexOfKey(ingredientCount)];

			if ((cache.Count == 1 /*&& cacheItem.Value.Defs.Count == 1*/)
				|| bill.recipe?.workerCounterClass != typeof(RecipeWorkerCounter))
			{
				return;
			}

			cacheItem = new(cacheItem.Key, new(cacheItem.Value.Defs, true));
		}
	}

	public record struct RecipeIngredientCacheValue()
	{
		public IngredientCacheList? thingDefs;
		private int _nextRefreshTick = -2;

		public void Update(Bill bill)
			=> _nextRefreshTick = TickHelper.Add(GenTicks.TickRareInterval * 2, bill.loadID);

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}
	}
}