// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using GearMassCache = PerformanceFish.Cache.ByInt<Verse.Pawn, PerformanceFish.MassUtilityCaching.GearMassCacheValue>;
using InventoryMassCache
	= PerformanceFish.Cache.ByInt<Verse.Pawn, PerformanceFish.MassUtilityCaching.InventoryMassCacheValue>;

namespace PerformanceFish;

public sealed class MassUtilityCaching : ClassWithFishPatches
{
	public sealed class GearMass_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Caches pawn gear mass. Hauling jobs check this quite a lot";
		public override Delegate TargetMethodGroup { get; } = MassUtility.GearMass;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Pawn p, ref float __result, out bool __state)
		{
			ref var cache = ref GearMassCache.GetOrAddReference(p.thingIDNumber);
			if (cache.IsDirty(p))
				return __state = true;

			__result = cache.Mass;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn p, bool __state, float __result)
		{
			if (!__state)
				return;

			UpdateCache(p, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Pawn p, float __result)
			=> GearMassCache.GetExistingReference(p).Update(__result, p);
	}

	public sealed class InventoryMass_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Caches pawn inventory mass. Hauling jobs check this quite a lot";
		public override Delegate TargetMethodGroup { get; } = MassUtility.InventoryMass;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Pawn p, ref float __result, out bool __state)
		{
			ref var cache = ref InventoryMassCache.GetOrAddReference(p.thingIDNumber);
			if (cache.IsDirty(p))
				return __state = true;

			__result = cache.Mass;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Pawn p, bool __state, float __result)
		{
			if (!__state)
				return;

			UpdateCache(p, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Pawn p, float __result)
			=> InventoryMassCache.GetExistingReference(p).Update(__result, p);
	}

	public record struct GearMassCacheValue()
	{
		public float Mass;
		private int _nextRefreshTick = -2;
		private int _equipmentListsState = -69123;

		public void Update(float mass, Pawn p)
		{
			Mass = mass;
			_nextRefreshTick = TickHelper.Add(3072, p.thingIDNumber, 2048);
			_equipmentListsState = GetEquipmentListsState(p);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetEquipmentListsState(Pawn p)
		{
			var result = p.apparel != null ? p.apparel.WornApparel._version : -1;
			if (p.equipment != null)
			{
				var equipmentListVersion = (uint)p.equipment.AllEquipmentListForReading._version;
				result += (int)((equipmentListVersion << 16) | (equipmentListVersion >> 16));
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsDirty(Pawn p)
			=> TickHelper.Past(_nextRefreshTick)
				|| _equipmentListsState != GetEquipmentListsState(p);
	}

	public record struct InventoryMassCacheValue()
	{
		public float Mass;
		private int _nextRefreshTick = -2;
		private int _inventoryListsState = -2;

		public void Update(float mass, Pawn p)
		{
			Mass = mass;
			// shorter refresh interval because of stack sizes not being considered by the listVersion check
			_nextRefreshTick = TickHelper.Add(GenTicks.TickRareInterval, p.thingIDNumber);
			_inventoryListsState = GetInventoryListsState(p); 
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetInventoryListsState(Pawn p)
			=> p.inventory?.innerContainer is { } container ? container.innerList._version : -42;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsDirty(Pawn p)
			=> TickHelper.Past(_nextRefreshTick)
				|| _inventoryListsState != GetInventoryListsState(p);
	}
}