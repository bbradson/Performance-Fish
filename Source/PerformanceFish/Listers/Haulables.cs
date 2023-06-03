// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using HaulablesCache
	= PerformanceFish.Cache.ByReference<Verse.Thing, RimWorld.ListerHaulables,
		PerformanceFish.Listers.Haulables.HaulablesCacheValue>;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PerformanceFish.Cache;
using PerformanceFish.ModCompatibility;

namespace PerformanceFish.Listers;

public class Haulables : ClassWithFishPatches
{
	public class MapPreTick_Patch : FishPatch
	{
		public override bool Enabled => base.Enabled && !ActiveMods.Multiplayer;
		
		public override string Description { get; }
			= "Throttling of the ListerHaulablesTick. This marks whether items should be moved from one stockpile to "
				+ "another. When throttled these checks get spread out more evenly, helping against spikes that can "
				+ "happen with large deep storage containers.";

		public override Expression<Action> TargetMethod { get; } = static () => default(Map)!.MapPreTick();

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> codes.MethodReplacer(new ListerHaulables(null).ListerHaulablesTick,
				ThreadedOrThrottled_ListerHaulablesTick);

		public static void ThreadedOrThrottled_ListerHaulablesTick(ListerHaulables instance)
		{
			MainThread = Environment.CurrentManagedThreadId;

			if (false && FishSettings.ThreadingEnabled)
				Threaded_ListerHaulablesTick(instance);
			else
				Throttled_ListerHaulablesTick(instance);
		}

		//disabled
		public static void Threaded_ListerHaulablesTick(ListerHaulables instance)
		{
			DequeueQueuedThings(instance);

			var currentCellCheckCount = GetCellCheckCount(instance);
			if (currentCellCheckCount is null)
				return;

			if (_currentTask is { IsCompleted: false })
			{
				if (currentCellCheckCount.value > 1)
					currentCellCheckCount.value--;
				if (Volatile.Read(ref _queue) == 0)
				{
					Interlocked.Increment(ref _queue);
					_currentTask = _currentTask.ContinueWith(t =>
					{
						if (t.Exception is { } taskException)
							Log.Error(taskException.ToString());

						lock (Lock)
						{
							_skippedTicks = 0;
							Interlocked.Decrement(ref _queue);
							ListerHaulablesTick_Patch.CurrentCellCheckCount = currentCellCheckCount.value;
							instance.ListerHaulablesTick();
						}
					});
				}
				else
				{
					if (_skippedTicks >= 9)
					{
						Log.Warning("Skipped 10 or more ListerHaulables ticks. This often means there's some "
							+ "kind of hauling error happening");
						_skippedTicks = 0;
					}
					else
					{
						_skippedTicks++;
					}
				}

				return;
			}
			else
			{
				if (currentCellCheckCount.value < ListerHaulables.CellsPerTick)
					currentCellCheckCount.value++;
			}

			if (_currentTask?.Exception is { } exception)
				Log.Error(exception.ToString());

			_currentTask = Task.Run(() =>
			{
				lock (Lock)
				{
					_skippedTicks = 0;
					ListerHaulablesTick_Patch.CurrentCellCheckCount = currentCellCheckCount.value;
					instance.ListerHaulablesTick();
				}
			});
		}

		public static void DequeueQueuedThings(ListerHaulables instance)
		{
			while (ThingsToRemove.TryDequeue(out var thing))
				TryRemove(instance, thing);

			while (ThingsToAdd.TryDequeue(out var thing))
				TryAdd(instance, thing);
		}

		public static void Throttled_ListerHaulablesTick(ListerHaulables instance)
		{
			var cellCheckCount = GetCellCheckCount(instance);
			if (cellCheckCount is null)
				return;

			ListerHaulablesTick_Patch.CurrentCellCheckCount = cellCheckCount.value;
			Stopwatch.Start();
			try
			{
				instance.ListerHaulablesTick();
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}

			Stopwatch.Stop();
			if (Stopwatch.ElapsedTicks > 4000L)
			{
				if (cellCheckCount.value > 0)
					cellCheckCount.value--;
			}
			else
			{
				if (cellCheckCount.value < 4)
					cellCheckCount.value++;
			}

			Stopwatch.Reset();
		}

		private static ListerHaulablesTick_Patch.CellCheckCount? GetCellCheckCount(ListerHaulables instance)
		{
			var groupCycleIndex = ListerHaulables.groupCycleIndex + 1;
			if (groupCycleIndex >= int.MaxValue)
				groupCycleIndex = 0;
			if (groupCycleIndex == _lastGroupCycleIndex)
			{
				groupCycleIndex++;
				if (groupCycleIndex >= int.MaxValue)
					groupCycleIndex = 0;
			}

			_lastGroupCycleIndex = groupCycleIndex;

			var allGroupsListForReading = instance.map.haulDestinationManager.AllGroupsListForReading;
			if (allGroupsListForReading.Count == 0)
				return null;

			var num = groupCycleIndex % allGroupsListForReading.Count;
			var slotGroup = allGroupsListForReading[num];
			return slotGroup.CellsList.Count == 0
				? null
				: ListerHaulablesTick_Patch.CellCheckCountsBySlotGroup.GetOrCreateValue(slotGroup);
		}

		public static int MainThread { get; private set; }
		public static ConcurrentQueue<Thing> ThingsToRemove { get; } = new();
		public static ConcurrentQueue<Thing> ThingsToAdd { get; } = new();
		private static Stopwatch Stopwatch { get; } = new();

		private static int _lastGroupCycleIndex;
		private static int _skippedTicks;
		private static int _queue;
		private static Task? _currentTask;

		private static object Lock { get; } = new();
	}

	public class ListerHaulablesTick_Patch : FishPatch
	{
		public override bool Enabled => base.Enabled && !ActiveMods.Multiplayer;
		
		public override string Description { get; } = "Part of the ListerHaulables throttling optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerHaulables)!.ListerHaulablesTick();

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> codes //.MethodReplacer(GridsUtility.GetThingList, GetCopyOfThingList)
				.Manipulator(static c => c.LoadsConstant(4), static c =>
				{
					c.opcode = OpCodes.Call;
					c.operand = typeof(ListerHaulablesTick_Patch).GetProperty(nameof(CurrentCellCheckCount))!.GetMethod;
				});

		/*public static List<Thing> GetCopyOfThingList(IntVec3 c, Map map)
		{
			if (FishSettings.ThreadingEnabled)
			{
				PrivateThingList.ReplaceContentsWith(map.thingGrid.ThingsListAt(c));
				return PrivateThingList;
			}
			else
			{
				return map.thingGrid.ThingsListAt(c);
			}
		}*/
		public static int CurrentCellCheckCount { get; set; } = ListerHaulables.CellsPerTick;
		public static ConditionalWeakTable<SlotGroup, CellCheckCount> CellCheckCountsBySlotGroup { get; } = new();

		public class CellCheckCount
		{
			public int value = ListerHaulables.CellsPerTick;
		}
		//private static List<Thing> PrivateThingList { get; } = new();
	}

	public static void TryAdd(ListerHaulables instance, Thing t)
	{
		ref var cache = ref HaulablesCache.GetAndCheck<HaulablesCacheValue>(t, instance);
		if (cache.Contains)
			return;

		if (!instance.haulables.Contains(t))
			instance.haulables.Add(t);
		
		cache.SetValue(true, t);
	}

	public static void ThreadSafeTryAdd(ListerHaulables instance, Thing t)
	{
		if (Environment.CurrentManagedThreadId == MapPreTick_Patch.MainThread || !FishSettings.ThreadingEnabled)
		{
			TryAdd(instance, t);
		}
		else
		{
			if (!HaulablesCache.GetAndCheck<HaulablesCacheValue>(t, instance).Contains)
				MapPreTick_Patch.ThingsToAdd.Enqueue(t);
		}
	}

	public static void TryRemove(ListerHaulables instance, Thing t)
	{
		ref var cache = ref HaulablesCache.GetAndCheck<HaulablesCacheValue>(t, instance);
		if (!cache.Contains)
			return;

		instance.haulables.Remove(t);
		cache.SetValue(false, t);
	}

	public static void ThreadSafeTryRemove(ListerHaulables instance, Thing t)
	{
		if (Environment.CurrentManagedThreadId == MapPreTick_Patch.MainThread || !FishSettings.ThreadingEnabled)
		{
			TryRemove(instance, t);
		}
		else
		{
			if (HaulablesCache.GetAndCheck<HaulablesCacheValue>(t, instance).Contains)
				MapPreTick_Patch.ThingsToRemove.Enqueue(t);
		}
	}

	public class Check_Patch : FishPatch
	{
		public override string Description { get; } = "Optimization for haulables checking";
		public override Expression<Action> TargetMethod { get; } = static () => default(ListerHaulables)!.Check(null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Check(ListerHaulables instance, Thing t)
		{
			// So the situation here is this: Vanilla RimWorld normally checks 4 storage cells per tick for valid haul
			// destinations, usually with the intent of reaching higher priority storage. This check itself can get
			// quite expensive, but 4 of them aren't at all enough to be worth bothering with. Now problems arise when
			// LWM's deep storage comes into play: You put 5 stacks of items in one cell and suddenly 4 checks turn
			// into 20. 20 hauling checks in turn is a number large enough to cause performance issues in colonies with
			// high wealth.
			
			if (!t.def.alwaysHaulable && !t.def.designateHaulable)
				return;

			if (instance.ShouldBeHaulable(t))
				ThreadSafeTryAdd(instance, t);
			else
				ThreadSafeTryRemove(instance, t);
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
			=> Reflection.GetCodeInstructions(Check, generator);
	}

	public class TryRemove_Patch : FishPatch
	{
		public override string Description { get; } = "Part of the ListerHaulables optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerHaulables)!.TryRemove(null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void TryRemove(ListerHaulables instance, Thing t)
		{
			if (t.def.category == ThingCategory.Item)
				ThreadSafeTryRemove(instance, t);
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
			=> Reflection.GetCodeInstructions(TryRemove, generator);
	}

	public class CheckAdd_Patch : FishPatch
	{
		public override string Description { get; } = "Part of the ListerHaulables optimization";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(ListerHaulables)!.CheckAdd(null);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CheckAdd(ListerHaulables instance, Thing t)
		{
			if (!t.def.alwaysHaulable && !t.def.designateHaulable)
				return;
			
			if (HaulablesCache.GetAndCheck<HaulablesCacheValue>(t, instance).Contains
				|| !instance.ShouldBeHaulable(t))
			{
				return;
			}

			ThreadSafeTryAdd(instance, t);
		}

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
			=> Reflection.GetCodeInstructions(CheckAdd, generator);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public record struct HaulablesCacheValue : ICacheable<HaulablesCache>
	{
		private int _nextRefreshTick;
		public bool Contains;

		public void Update(ref HaulablesCache key)
		{
			Contains = key.Second.haulables.Contains(key.First);
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