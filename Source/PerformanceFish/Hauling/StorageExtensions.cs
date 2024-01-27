// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Hauling;

public static class StorageExtensions
{
	public static int TrueItemsPerCell(this Thing slotGroupParent)
		=> slotGroupParent.TrueItemsPerCell(slotGroupParent.TryGetLwmCompProperties());
	
	public static int TrueItemsPerCell(this Thing slotGroupParent, CompProperties? lwmProps)
		=> lwmProps != null
			? Math.Max(LwmCompCacheValue.LwmMinNumberStacks!(lwmProps), LwmCompCacheValue.LwmMaxNumberStacks!(lwmProps))
			: slotGroupParent is Building building
				? building.MaxItemsInCell
				: 1;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static CompProperties? TryGetLwmCompProperties(this Thing thing)
		=> LwmCompCache.GetOrAdd(thing).CompProperties;

	public static int GetTotalSlots(this SlotGroup slotGroup) => slotGroup.GetTotalSlots(slotGroup.CellsList.Count);
	
	public static int GetTotalSlots(this SlotGroup slotGroup, int cellCount)
		=> slotGroup.parent is Building storage
			? storage.TrueItemsPerCell() * cellCount
			: cellCount;

	static StorageExtensions() => LwmCompCacheValue.EnsureInitialized();
}