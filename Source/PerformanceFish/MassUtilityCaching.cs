// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using GearMassCache = PerformanceFish.Cache.ByIndex<Verse.Pawn, PerformanceFish.MassUtilityCaching.GearMassCacheValue>;
using InventoryMassCache = PerformanceFish.Cache.ByIndex<Verse.Pawn, PerformanceFish.MassUtilityCaching.InventoryMassCacheValue>;

namespace PerformanceFish;

public class MassUtilityCaching : ClassWithFishPatches
{
	public class GearMass_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches pawn gear mass. Hauling jobs check this quite a lot";
		public override Delegate TargetMethodGroup => MassUtility.GearMass;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Pawn p, ref float __result, out bool __state)
		{
			ref var cache = ref GearMassCache.Get.GetReference(p);
			if (cache.ShouldRefreshNow(p))
				return __state = true;

			__result = cache.mass;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn p, bool __state, float __result)
		{
			if (!__state)
				return;

			GearMassCache.Get[p] = new(__result, p);
		}
	}

	public class InventoryMass_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches pawn inventory mass. Hauling jobs check this quite a lot";
		public override Delegate TargetMethodGroup => MassUtility.InventoryMass;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Pawn p, ref float __result, out bool __state)
		{
			ref var cache = ref InventoryMassCache.Get.GetReference(p);
			if (cache.ShouldRefreshNow(p))
				return __state = true;

			__result = cache.mass;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn p, bool __state, float __result)
		{
			if (!__state)
				return;

			InventoryMassCache.Get[p] = new(__result, p);
		}
	}

	public struct GearMassCacheValue
	{
		public float mass;
		private int _nextRefreshTick;
		private int _equipmentListsState;
		public GearMassCacheValue(float mass, Pawn p)
		{
			this.mass = mass;
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
			_equipmentListsState = GetEquipmentListsState(p);
		}
		private static int GetEquipmentListsState(Pawn p)
		{
			var result = p.apparel != null ? p.apparel.WornApparel.Version() : 0;
			if (p.equipment != null)
				result += p.equipment.AllEquipmentListForReading.Version();
			return result;
		}
		public bool ShouldRefreshNow(Pawn p) => _nextRefreshTick < Current.Game.tickManager.TicksGame || _equipmentListsState != GetEquipmentListsState(p);
	}

	public struct InventoryMassCacheValue
	{
		public float mass;
		private int _nextRefreshTick;
		private int _inventoryListsState;
		public InventoryMassCacheValue(float mass, Pawn p)
		{
			this.mass = mass;
			_nextRefreshTick = Current.Game.tickManager.TicksGame + 256 + Math.Abs(Rand.Int % 256);
			_inventoryListsState = GetInventoryListsState(p); //shorter refresh interval because of stack sizes not being considered by the listVersion check
		}
		private static int GetInventoryListsState(Pawn p) => p.inventory?.innerContainer is { } container ? container.innerList.Version() : 0;
		public bool ShouldRefreshNow(Pawn p) => _nextRefreshTick < Current.Game.tickManager.TicksGame || _inventoryListsState != GetInventoryListsState(p);
	}
}