// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if V1_3
//mostly integrated into vanilla with 1.4

using System.Linq;
using AllDesignationsOnCache =
	PerformanceFish.Cache.ByIntRefreshable<Verse.DesignationManager, Verse.Thing,
		PerformanceFish.DesignationManagerCaching.DesignationCache<Verse.Thing>>;
using DesignationsOfDefCache =
	PerformanceFish.Cache.ByIntRefreshable<Verse.DesignationManager, Verse.DesignationDef,
		PerformanceFish.DesignationManagerCaching.DesignationCache<Verse.DesignationDef>>;

namespace PerformanceFish;

public sealed class DesignationManagerCaching : ClassWithFishPatches
{
	public sealed class SpawnedDesignationsOfDef_Patch : FishPatch
	{
		public override string Description => "Designation caching";

		public override Expression<Action> TargetMethod
			=> () => default(DesignationManager)!.SpawnedDesignationsOfDef(null);

		public static bool Prefix(DesignationManager __instance, DesignationDef def,
			ref IEnumerable<Designation> __result, out bool __state)
		{
			if (def.GetType() != typeof(DesignationDef)) // different types have different indexing. Can't use those
			{
				__state = false;
				return true;
			}

			if (!DesignationsOfDefCache.TryGetValue(new(__instance, def), out var value))
				return __state = true;

			__result = value.designations;
			return __state = false;
		}

		public static void Postfix(DesignationManager __instance, DesignationDef def, bool __state,
			IEnumerable<Designation> __result)
		{
			if (__state)
				RefreshDesignationCache(DesignationsOfDefCache.Get, __instance, def, __result);
		}
	}

	public static void RefreshDesignationCache<T>( /*Cache.ByIndex<DesignationManager, T, DesignationCache<T>>*/
		Dictionary<Cache.ByIntRefreshable<DesignationManager, T, DesignationCache<T>>, DesignationCache<T>> dict,
		DesignationManager instance, T key, IEnumerable<Designation> result)
		where T : notnull
	{
		if (dict.TryGetValue(new(instance, key), out var cache))
			cache.UpdateList(result);
		else
			cache.Initialize(result, instance);

		dict[new(instance, key)] = cache;
	}

	public sealed class AnySpawnedDesignationOfDef_Patch : FishPatch
	{
		public override string Description => "Designation caching";

		public override Expression<Action> TargetMethod
			=> () => default(DesignationManager)!.AnySpawnedDesignationOfDef(null);

		public static bool AnySpawnedDesignationOfDef(DesignationManager __instance, DesignationDef def)
			=> __instance.SpawnedDesignationsOfDef(def).Any();

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.GetCodeInstructions(AnySpawnedDesignationOfDef);
	}

	public sealed class AllDesignationsOn_Patch : FishPatch
	{
		public override string Description => "Designation caching";
		public override Expression<Action> TargetMethod => () => default(DesignationManager)!.AllDesignationsOn(null);

		public static bool Prefix(DesignationManager __instance, Thing t, ref IEnumerable<Designation> __result,
			out bool __state)
		{
			if (!AllDesignationsOnCache.TryGetValue(new(__instance, t), out var value))
				return __state = true;

			__result = value.designations;
			return __state = false;
		}

		public static void Postfix(DesignationManager __instance, Thing t, bool __state,
			IEnumerable<Designation> __result)
		{
			if (__state)
				RefreshDesignationCache(AllDesignationsOnCache.Get, __instance, t, __result);
		}
	}

	public sealed class DesignationOn_Patch : FishPatch
	{
		public override string Description => "Designation caching";
		public override Expression<Action> TargetMethod => () => default(DesignationManager)!.DesignationOn(null);

		public static Designation DesignationOn(DesignationManager __instance, Thing t)
			=> __instance.AllDesignationsOn(t).FirstOrDefault();

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.GetCodeInstructions(DesignationOn);
	}

	public sealed class DesignationOn_ByDef_Patch : FishPatch
	{
		public override string Description => "Designation caching";
		public override Expression<Action> TargetMethod => () => default(DesignationManager)!.DesignationOn(null, null);

		public static Designation? DesignationOn(DesignationManager __instance, Thing t, DesignationDef def)
			=> FirstDesignationOfDef(__instance.AllDesignationsOn(t), def);

		public static Designation? FirstDesignationOfDef(IEnumerable<Designation> designations, DesignationDef def)
		{
			if (designations is not List<Designation> designationList)
				return Fallback(designations, def);

			for (var i = 0; i < designationList.Count; i++)
			{
				var designation = designationList[i];
				if (designation.def == def)
					return designation;
			}

			return null;
		}

		public static Designation? Fallback(IEnumerable<Designation> designations, DesignationDef def)
		{
			foreach (var designation in designations)
			{
				if (designation.def == def)
					return designation;
			}

			return null;
		}

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.GetCodeInstructions(DesignationOn);
	}

	public sealed class HasMapDesignationOn_Patch : FishPatch
	{
		public override string Description => "Designation caching";
		public override Expression<Action> TargetMethod => () => default(DesignationManager)!.HasMapDesignationOn(null);

		public static bool HasMapDesignationOn(DesignationManager __instance, Thing t)
			=> __instance.AllDesignationsOn(t).Any();

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.GetCodeInstructions(HasMapDesignationOn);
	}

	public record struct DesignationCache<T> : Cache.IIsRefreshable<
		Cache.ByIntRefreshable<DesignationManager, T, DesignationCache<T>>, DesignationCache<T>>
		where T : notnull
	{
		public IEnumerable<Designation> designations;
		private int _nextRefreshTick;
		private int _managerListState;
		private DesignationManager _manager;

		public void Initialize(IEnumerable<Designation> designations, DesignationManager manager)
		{
			this.designations = new List<Designation>(designations);
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
			_manager = manager;
			_managerListState = manager.allDesignations.Version();
		}

		public void UpdateList(IEnumerable<Designation> designations)
		{
			var cachedList = (List<Designation>)this.designations;
			cachedList.Clear();
			cachedList.AddRange(designations);
			ShouldRefreshNow = false;
			_managerListState = _manager.allDesignations.Version();
		}

		public bool ShouldRefreshNow
		{
			get
				=> _nextRefreshTick < Current.Game.tickManager.TicksGame
					|| _managerListState != _manager.allDesignations.Version();
			set => _nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}

		public DesignationCache<T> SetNewValue(Cache.ByIntRefreshable<DesignationManager, T, DesignationCache<T>> key)
			=> throw new NotImplementedException();
	}
}

#endif