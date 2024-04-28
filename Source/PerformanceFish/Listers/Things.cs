// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Listers;

public sealed class ThingsPrepatches : ClassWithFishPrepatches
{
	public sealed class AddPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Required to keep the ListerThings cache synced";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.Add));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(ListerThings __instance, Thing t)
		{
			if (!ListerThings.EverListable(t.def, __instance.use))
				return;

			lock (__instance.LockObject())
			{
				AddToDefList(__instance, t);

				var allGroups = ThingListGroupHelper.AllGroups;
				foreach (var thingRequestGroup in allGroups)
				{
					if ((__instance.use == ListerThingsUse.Region && !thingRequestGroup.StoreInRegion())
						|| !thingRequestGroup.Includes(t.def))
					{
						continue;
					}

					AddToGroupList(__instance, t, thingRequestGroup);
				}
				
				AddToTypeList(__instance, t);
			}

#if !V1_4
			__instance.thingListChangedCallbacks?.onThingAdded?.Invoke(t);
#endif
		}
	}

	public sealed class RemovePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes ListerThings to keep a cache of thing indices within the lister, to greatly speed up removal "
			+ "and in turn improve performance when despawning happens. Late game colonies and maps in biomes with "
			+ "large amounts of plants benefit most from this";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.Remove));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(ListerThings __instance, Thing t)
		{
			if (!ListerThings.EverListable(t.def, __instance.use))
				return;

			lock (__instance.LockObject())
			{
				RemoveFromDefList(__instance, t);

				var allGroups = ThingListGroupHelper.AllGroups;
				for (var i = 0; i < allGroups.Length; i++)
				{
					var thingRequestGroup = allGroups[i];
					if ((__instance.use == ListerThingsUse.Region && !thingRequestGroup.StoreInRegion())
						|| !thingRequestGroup.Includes(t.def))
					{
						continue;
					}
				
					RemoveFromGroupList(__instance, t, thingRequestGroup);
				}
				
				RemoveFromTypeList(__instance, t);
			}
			
#if !V1_4
			__instance.thingListChangedCallbacks?.onThingRemoved?.Invoke(t);
#endif
		}
	}

	public static void AddToDefList(ListerThings lister, Thing thing)
	{
		if (!lister.listsByDef.TryGetValue(thing.def, out var value))
			lister.listsByDef.Add(thing.def, value = []);

		value.Add(thing);
		lister.IndexMapByDef()[thing.GetKey()] = value.Count - 1;
	}

	public static void AddToGroupList(ListerThings lister, Thing thing, ThingRequestGroup thingRequestGroup)
	{
		ref var list = ref lister.listsByGroup[(uint)thingRequestGroup];
		ref var hash = ref lister.stateHashByGroup[(uint)thingRequestGroup];
		
		if (list == null)
		{
			list = [];
			hash = 0;
		}

		list.Add(thing);
		hash++;

		lister.IndexMapByGroup()[new(thingRequestGroup, thing.GetKey())] = list.Count - 1;
	}

	public static void AddToTypeList(ListerThings lister, Thing thing)
	{
		var thingType = thing.GetType();
		if (thingType == typeof(Thing))
			return;

		var cache = lister.Cache();

		do
		{
			if (thingType == null)
				break;

			cache.ThingsByType.GetOrAdd(thingType).Add(thing);
		}
		while ((thingType = thingType.BaseType) != typeof(Thing));
	}

	public static void RemoveFromDefList(ListerThings lister, Thing thing)
	{
		var thingKey = thing.GetKey();
		var indexMapByDef = lister.IndexMapByDef();
		
		if (lister.listsByDef.TryGetValue(thing.def) is not { } listByDef)
		{
			LogFailureToRemoveThingFromLister(lister, thing);
			return;
		}

		var indexByDef = indexMapByDef.TryGetValue(thingKey, out var knownIndexByDef)
			&& knownIndexByDef < listByDef.Count
			&& listByDef[knownIndexByDef] == thing
				? knownIndexByDef
				: listByDef.LastIndexOf(thing);

		if (indexByDef >= 0)
			listByDef.RemoveAtFastUnordered(indexByDef);

		indexMapByDef.Remove(thingKey);
		if (indexByDef < listByDef.Count && indexByDef >= 0)
			indexMapByDef[listByDef[indexByDef].GetKey()] = indexByDef;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void LogFailureToRemoveThingFromLister(ListerThings lister, Thing thing)
		=> Log.Error($"Tried and failed removing Thing '{thing}' of map '{
			thing.TryGetMap().ToStringSafe()}' from ListerThings of map '{
				lister.AllThings?.FirstOrDefault()?.TryGetMap().ToStringSafe()}'.\n{
					new StackTrace(2, true)}");

	public static void RemoveFromGroupList(ListerThings lister, Thing thing, ThingRequestGroup thingRequestGroup)
	{
		var indexMapByGroup = lister.IndexMapByGroup();
		
		if (lister.listsByGroup[(int)thingRequestGroup] is not { } listByGroup)
			return;

		var groupMapKey = new GroupThingPair(thingRequestGroup, thing.GetKey());

		var indexByGroup
			= indexMapByGroup.TryGetValue(groupMapKey, out var knownIndexByGroup)
			&& knownIndexByGroup < listByGroup.Count
			&& listByGroup[knownIndexByGroup] == thing
				? knownIndexByGroup
				: listByGroup.LastIndexOf(thing);

		if (indexByGroup >= 0)
			listByGroup.RemoveAtFastUnordered(indexByGroup);

		indexMapByGroup.Remove(groupMapKey);
		if (indexByGroup < listByGroup.Count && indexByGroup >= 0)
			indexMapByGroup[new(thingRequestGroup, listByGroup[indexByGroup].GetKey())] = indexByGroup;

		lister.stateHashByGroup[(uint)thingRequestGroup]++;
	}

	public static void RemoveFromTypeList(ListerThings lister, Thing thing)
	{
		var thingType = thing.GetType();
		if (thingType == typeof(Thing))
			return;

		var cache = lister.Cache();

		do
		{
			if (thingType == null)
				break;

			cache.ThingsByType.GetOrAdd(thingType).Remove(thing);
		}
		while ((thingType = thingType.BaseType) != typeof(Thing));
	}

	public sealed class ContainsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization utilizing a cache for faster Contains checks";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.Contains));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(ListerThings __instance, Thing t)
		{
			lock (__instance.LockObject())
			{
				return __instance.IndexMapByDef().ContainsKey(t.GetKey())
					|| (__instance.listsByDef.TryGetValue(t.def)?.Contains(t) ?? false);
			}
		}
	}

	public sealed class ClearPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; }
			= [typeof(AddPatch), typeof(RemovePatch), typeof(ContainsPatch)];

		public override string? Description { get; }
			= "Required to keep the ListerThings cache synced";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.Clear));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(ListerThings __instance)
		{
			lock (__instance.LockObject())
			{
				__instance.listsByDef.Clear();

				var listsByGroup = __instance.listsByGroup;
				
				for (var i = 0; i < listsByGroup.Length; i++)
				{
					listsByGroup[i]?.Clear();
					__instance.stateHashByGroup[i] = 0;
				}
				
				__instance.IndexMapByDef().Clear();
				__instance.IndexMapByGroup().Clear();
				ClearTypeLists(__instance);
			}
			
			// no thing __instance.thingListChangedCallbacks?.onThingRemoved?.Invoke(t); calls in original
		}

		public static void ClearTypeLists(ListerThings lister)
		{
			var lists = lister.Cache().ThingsByType;
			foreach (var (_, list) in lists)
				list.Clear();
		}
	}
	
#if !V1_4
	public sealed class GetThingsOfTypePatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes thing lookups for generic types on ListerThings to directly return the result of cached "
			+ "data instead of iterating over all things on the map trying to find all matches";
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.GetThingsOfType), Type.EmptyTypes);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Thing>);

		public static IEnumerable<T> ReplacementBody<T>(ListerThings __instance) where T : Thing
			=> typeof(T) != typeof(Thing) // normally logs an error instead of simply returning all things
				? (IEnumerable<T>)__instance.Cache().ThingsByType.GetOrAdd(typeof(T))
				: (IEnumerable<T>)__instance.AllThings;
	}
	
	public sealed class GetThingsOfTypeIntoListPatch : FishPrepatch
	{
		public override string Description { get; }
			= "Optimizes thing lookups for generic types on ListerThings to directly return the result of cached "
			+ "data instead of iterating over all things on the map trying to find all matches";

		public override MethodBase TargetMethodBase { get; }
			= typeof(ListerThings).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
				.First(static method => method.Name == nameof(ListerThings.GetThingsOfType)
					&& method.ReturnType == typeof(void)
					&& method.GetParameters().Length == 1);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Thing>);

		public static void ReplacementBody<T>(ListerThings __instance, List<T> list) where T : Thing
			=> list.AddRange(typeof(T) != typeof(Thing) // normally logs an error instead of simply returning all things
				? (IEnumerable<T>)__instance.Cache().ThingsByType.GetOrAdd(typeof(T))
				: (IEnumerable<T>)__instance.AllThings);
	}
#endif
}

public sealed class Things : ClassWithFishPatches
{
	public sealed class ThingsMatching_Patch : FirstPriorityFishPatch
	{
		public override List<Type> LinkedPatches { get; } = [typeof(ThingFilterPatches.BestThingRequest_Patch)];

		public override string Description { get; }
			= "Part of the ThingFilter.BestThingRequest optimization. Required by that particular patch and gets "
			+ "toggled alongside it";

		public override MethodBase? TargetMethodInfo { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.ThingsMatching));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(ListerThings __instance, ThingRequest req, ref List<Thing> __result)
		{
			if ((object)req.singleDef is not HashSet<ThingDef> hashSet)
				return true;

			__result = ThingsOfDefsFor(__instance, hashSet);
			return false;
		}
	}

	public static List<Thing> ThingsOfDefsFor(ListerThings lister, HashSet<ThingDef> set)
	{
		var list = GetThingsOfDefsListFor(set);
		list.Clear();

		foreach (var def in set)
		{
			if (lister.listsByDef.TryGetValue(def) is { } thingsOfSingleDef)
				list.AddRangeFast(thingsOfSingleDef);
		}

		return list;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static List<Thing> GetThingsOfDefsListFor(IEnumerable<ThingDef> thingDefs)
		=> (_thingsOfDefsDictionary
				??= PerformanceFish.Cache.Utility.AddNew<FishTable<IEnumerable<ThingDef>, List<Thing>>>())
			.GetOrAdd(thingDefs);

	[ThreadStatic]
	private static FishTable<IEnumerable<ThingDef>, List<Thing>>? _thingsOfDefsDictionary;

#if disabled
	public sealed class ThingRequest_IsUndefined_Patch : FishPatch
	{
		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.IsUndefined));

		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingRequest __instance, ref bool __result)
		{
			if (__instance.singleDef is not FishCache)
				return;

			__result = NewResult(__instance);
		}

		public static bool NewResult(ThingRequest __instance)
			=> __instance.group == ThingRequestGroup.Undefined && !((FishCache)__instance.singleDef).IsSingleDef;
	}

	public sealed class ThingRequest_CanBeFoundInRegion_Patch : FirstPriorityFishPatch
	{
		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.CanBeFoundInRegion));

		public static bool Prefix(ThingRequest __instance, ref bool __result)
		{
			if (__instance.singleDef is not FishCache)
				return true;

			__result = NewResult(__instance);
			return false;
		}

		public static bool NewResult(ThingRequest __instance)
			=> ((FishCache)__instance.singleDef).IsSingleDef
				|| (__instance.group != ThingRequestGroup.Undefined
					&& (__instance.group == ThingRequestGroup.Nothing || __instance.group.StoreInRegion()));
	}

	public sealed class Add_Patch : FishPatch
	{
		public override Expression<Action> TargetMethod { get; } = static () => new ListerThings(default).Add(null);

		public static void Postfix(ListerThings __instance, Thing t)
		{
			if (t.TryGetComp<CompStyleable>() is not { } style || style.SourcePrecept is null)
				return;

			Cache.ByReference<ListerThings, CompStyleablesInfo>.GetOrAddReference(__instance).Things.Add(t);
		}
	}

	public sealed class Remove_Patch : FishPatch
	{
		public override Expression<Action> TargetMethod { get; } = static () => new ListerThings(default).Remove(null);

		public static void Postfix(ListerThings __instance, Thing t)
		{
			if (t.TryGetComp<CompStyleable>() is not { } style || style.SourcePrecept is null)
				return;

			Cache.ByReference<ListerThings, CompStyleablesInfo>.GetOrAddReference(__instance).Things.Remove(t);
		}
	}

	public sealed class Clear_Patch : FishPatch
	{
		public override Expression<Action> TargetMethod { get; } = static () => new ListerThings(default).Clear();

		public static void Postfix(ListerThings __instance, Thing t)
		{
			if (t.TryGetComp<CompStyleable>() is not { } style || style.SourcePrecept is null)
				return;

			Cache.ByReference<ListerThings, CompStyleablesInfo>.GetOrAddReference(__instance).Things.Clear();
		}
	}

	public sealed class ThingsMatching_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Part of the work scanning optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => new ListerThings(default).ThingsMatching(default);

		public static bool Prefix(ListerThings __instance, ThingRequest req, ref List<Thing> __result)
		{
			if (req.singleDef is not FishCache)
				return true;

			__result = GetCache(__instance, req);
			return false;
		}

		public static List<Thing> GetCache(ListerThings __instance, ThingRequest req)
		{
			var fishCache = (FishCache)req.singleDef;
			var cachedThings = fishCache.FilteredThings ?? ListerThings.EmptyList;
			/*if (__instance.use == ListerThingsUse.Region && cachedThings.Count > 20)
			{
				var regionalThings = GetForRegion(__instance, req);
				return regionalThings.Count < cachedThings.Count ? regionalThings : cachedThings;
			}
			else
			{
				return cachedThings;
			}*/
			return null;
		}
		/*public static List<Thing> GetForRegion(ListerThings __instance, ThingRequest req)
			=> new(TryIssueJobPackage_Patch.Generic<Thing>.TryGetPrefilteredPotentialWorkThingsForList(
				(((FishCache)req.singleDef) is var cache && cache.IsSingleDef
				? __instance.listsByDef.TryGetValue(cache.Def, out var value) ? value : null
				: __instance.listsByGroup.TryGetItem((int)req.group, out var item) ? item : null) ?? ListerThings.EmptyList, CurrentPawn));*/
	}

	public static CompStyleablesInfo GetCompStyleablesCache(ListerThings lister)
		=> Cache.ByReference<ListerThings, CompStyleablesInfo>.GetOrAddReference(lister);

	public record struct CompStyleablesInfo
	{
		public List<Thing> Things;
		private int _nextRefreshTick;

		public bool Dirty
		{
			get => _nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}

		public CompStyleablesInfo SetNewValue(ListerThings key)
		{
			if (Things is null)
				Things = new();
			else
				Things.Clear();
			var allBuildings = key.listsByGroup[(int)ThingRequestGroup.BuildingArtificial];
			var count = allBuildings.Count;
			for (var i = 0; i < count; i++)
			{
				if (allBuildings[i].TryGetComp<CompStyleable>() is { SourcePrecept: not null })
					Things.Add(allBuildings[i]);
			}

			return this;
		}
	}

	public class FishCache : ThingDef
	{
		public bool IsSingleDef => Def != null;
		public ThingDef Def { get; private set; }
		public List<Thing> FilteredThings { get; set; }

		public static FishCache ForDef(ThingDef def)
		{
			if (_dictOfDefs.TryGetValue(def, out var value))
				return value;

			_dictOfDefs[def] = value = new(def.defName) { Def = def };
			return value;
		}

		public static FishCache ForGroup(ThingRequestGroup group)
		{
			if (_dictOfGroups.TryGetValue(group, out var value))
				return value;

			_dictOfGroups[group] = value = new(group.ToString());
			return value;
		}

		public static void ClearAll()
		{
			_dictOfGroups.Clear();
			_dictOfDefs.Clear();
		}

		private FishCache(string name) => defName = name;
		private static Dictionary<ThingRequestGroup, FishCache> _dictOfGroups { get; } = new();
		private static Dictionary<ThingDef, FishCache> _dictOfDefs { get; } = new();
	}
#endif
	
	public readonly record struct Cache()
	{
		public readonly FishTable<Type, IList> ThingsByType = new()
		{
			ValueInitializer = static type
				=> (IList)_thingsListInitializerDefinition.MakeGenericMethod(type).Invoke(null, null)
		};

		private static readonly MethodInfo _thingsListInitializerDefinition
			= methodof(InitializeThingsList<Thing>).GetGenericMethodDefinition();
		
		public static unsafe IndexedFishSet<T> InitializeThingsList<T>() where T : Thing
#pragma warning disable CS8622
			=> new(static () => new(0, keyHashCodeByRefGetter: &GetKeyByRef, keyHashCodeGetter: &ThingHelper.GetKey));
#pragma warning restore CS8622

		private static int GetKeyByRef<T>(ref T thing) where T : Thing => thing.GetKey();
	}
}