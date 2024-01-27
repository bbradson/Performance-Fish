// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if bad

global using MergeablesCache
	= PerformanceFish.Cache.ByReference<Verse.Thing, RimWorld.ListerMergeables,
		PerformanceFish.Listers.Mergeables.MergeablesCacheValue>;
using PerformanceFish.Cache;

namespace PerformanceFish.Listers;

public sealed class Mergeables : ClassWithFishPatches
{
	public sealed class Check_Patch : FishPatch
	{
		protected internal override void OnPatchingCompleted()
		{
			Hauling.StorageSettingsPatches.TryNotifyChanged_Patch.Changed += _ => MergeablesCache.Get.Clear();

			Events.ThingEvents.Initialized += thing =>
			{
				if (thing is IHaulDestination)
					thing.Events().Destroying += _ => MergeablesCache.Get.Clear();
			};
		}
		
		public override string Description { get; } = "Optimization for mergeables checking";
		public override Expression<Action> TargetMethod { get; } = static () => default(ListerMergeables)!.Check(null);

		public static void Check(ListerMergeables instance, Thing t)
		{
			ref var cache = ref MergeablesCache.GetAndCheck<MergeablesCacheValue>(t, instance);

			if (cache.Contains)
			{
				if (instance.ShouldBeMergeable(t))
					return;

				instance.mergeables.Remove(t);
				
				cache.SetValue(false, t);
			}
			else
			{
				if (!instance.ShouldBeMergeable(t))
					return;

				if (!instance.mergeables.Contains(t))
					instance.mergeables.Add(t);
				
				cache.SetValue(true, t);
			}
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(Check);
	}

	public sealed class CheckAdd_Patch : FishPatch
	{
		public override string Description { get; } = "Part of the ListerMergeables optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerMergeables)!.CheckAdd(null);

		public static void CheckAdd(ListerMergeables instance, Thing t)
		{
			ref var cache = ref MergeablesCache.GetAndCheck<MergeablesCacheValue>(t, instance);

			if (cache.Contains || !instance.ShouldBeMergeable(t))
				return;

			if (!instance.mergeables.Contains(t))
				instance.mergeables.Add(t);
			
			cache.SetValue(true, t);
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(CheckAdd);
	}

	public sealed class TryRemove_Patch : FishPatch
	{
		public override string Description { get; } = "Part of the ListerMergeables optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerMergeables)!.TryRemove(null);

		public static void TryRemove(ListerMergeables instance, Thing t)
		{
			if (!t.IsItem())
				return;

			ref var cache = ref MergeablesCache.GetAndCheck<MergeablesCacheValue>(t, instance);

			if (!cache.Contains)
				return;

			instance.mergeables.Remove(t);
			cache.SetValue(false, t);
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(TryRemove);
	}

	public record struct MergeablesCacheValue : ICacheable<MergeablesCache>
	{
		private int _nextRefreshTick;
		public bool Contains;
		
		public void Update(ref MergeablesCache key)
		{
			Contains = key.Second.mergeables.Contains(key.First);
			SetDirty(false, key.First.thingIDNumber);
		}

		public void SetValue(bool contains, Thing thing)
		{
			Contains = contains;
			SetDirty(false, thing.thingIDNumber);
		}
		
		public void SetDirty(bool value, int offset)
			=> _nextRefreshTick = value ? 0 : TickHelper.Add(3072, offset, 2048);

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TickHelper.Past(_nextRefreshTick);
		}
	}
}
#endif