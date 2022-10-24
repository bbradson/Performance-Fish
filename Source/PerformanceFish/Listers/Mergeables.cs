// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using MergeablesCache = PerformanceFish.Cache.ByReferenceRefreshable<Verse.Thing, RimWorld.ListerMergeables, PerformanceFish.Listers.Mergeables.FishMergeableInfo>;

namespace PerformanceFish.Listers;

public class Mergeables : ClassWithFishPatches
{
	public class Check_Patch : FishPatch
	{
		public override string Description => "Optimization for mergeables checking";
		public override Expression<Action> TargetMethod => () => default(ListerMergeables)!.Check(null);
		public static void Check(ListerMergeables instance, Thing t)
		{
			var cache = GetAndCheckMergeablesCache(instance, t);

			if (cache.contains)
			{
				if (!instance.ShouldBeMergeable(t))
				{
					instance.mergeables.Remove(t);
					cache.contains = false;
					cache.ShouldRefreshNow = false;
					//MergeablesCache.Get[new(t, instance)] = cache;
				}
			}
			else
			{
				if (instance.ShouldBeMergeable(t))
				{
					if (!instance.mergeables.Contains(t))
						instance.mergeables.Add(t);
					cache.contains = true;
					cache.ShouldRefreshNow = false;
					//MergeablesCache.Get[new(t, instance)] = cache;
				}
			}
		}
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(Check);
	}

	public class CheckAdd_Patch : FishPatch
	{
		public override string Description => "Part of the ListerMergeables optimization";
		public override Expression<Action> TargetMethod => () => default(ListerMergeables)!.CheckAdd(null);
		public static void CheckAdd(ListerMergeables instance, Thing t)
		{
			var cache = GetAndCheckMergeablesCache(instance, t);

			if (cache.contains || !instance.ShouldBeMergeable(t))
				return;

			if (!instance.mergeables.Contains(t))
				instance.mergeables.Add(t);
			cache.contains = true;
			cache.ShouldRefreshNow = false;

			//MergeablesCache.Get[new(t, instance)] = cache;
		}
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(CheckAdd);
	}

	public class TryRemove_Patch : FishPatch
	{
		public override string Description => "Part of the ListerMergeables optimization";
		public override Expression<Action> TargetMethod => () => default(ListerMergeables)!.TryRemove(null);
		public static void TryRemove(ListerMergeables instance, Thing t)
		{
			if (t.def.category == ThingCategory.Item)
			{
				var cache = GetAndCheckMergeablesCache(instance, t);

				if (!cache.contains)
					return;

				instance.mergeables.Remove(t);
				cache.contains = false;
				cache.ShouldRefreshNow = false;

				//MergeablesCache.Get[new(t, instance)] = cache;
			}
		}
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(TryRemove);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static FishMergeableInfo GetAndCheckMergeablesCache(ListerMergeables lister, Thing thing)
	{
		var key = new MergeablesCache(thing, lister);
		return MergeablesCache.GetValue(ref key);
	}

	public struct FishMergeableInfo : Cache.IIsRefreshable<MergeablesCache, FishMergeableInfo>
	{
		public int nextRefreshTick;
		public bool contains;

		public bool ShouldRefreshNow
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => nextRefreshTick < Current.Game.tickManager.TicksGame;
			set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
		}

		public FishMergeableInfo SetNewValue(MergeablesCache key)
		{
			contains = key.Second.mergeables.Contains(key.First);
			ShouldRefreshNow = false;
			return this;
		}
	}
}