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
		=> Math.Max(lwmProps == null
				? 0
				: Math.Max(LwmCompCacheValue.LwmMinNumberStacks!(lwmProps),
					LwmCompCacheValue.LwmMaxNumberStacks!(lwmProps)),
			slotGroupParent is Building building ? building.MaxItemsInCell : 1);

	public static int TrueTotalSlots(this Thing slotGroupParent, CompProperties? lwmProps,
		DefModExtension? rimfactoryExtension)
		=> Math.Max(rimfactoryExtension == null ? 0 : RimfactoryExtensionCacheValue.Limit!(rimfactoryExtension),
			slotGroupParent.TrueItemsPerCell(lwmProps) * slotGroupParent.SlotGroupCellCount());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static CompProperties? TryGetLwmCompProperties(this Thing thing)
		=> LwmCompCache.GetOrAdd(thing).CompProperties;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DefModExtension? TryGetRimfactoryExtension(this Thing thing)
		=> RimfactoryExtensionCache.GetOrAdd(thing).ModExtension;

	public static int GetTotalSlots(this SlotGroup slotGroup) => slotGroup.GetTotalSlots(slotGroup.CellsList.Count);
	
	public static int GetTotalSlots(this SlotGroup slotGroup, int cellCount)
		=> slotGroup.parent is Building storage
			? storage.TrueItemsPerCell() * cellCount
			: cellCount;
	
	public static void RecalculateStorageGroupMemberCount(this HaulDestinationManager haulDestinationManager)
	{
		haulDestinationManager.map.storageGroups.groups.UnwrapArray(out var storageGroups, out var storageGroupCount);
		for (var i = storageGroupCount; --i >= 0;)
			storageGroups.UnsafeLoad(i).SpawnedMemberCount() = 0;

		haulDestinationManager.AllGroupsListInPriorityOrder.UnwrapArray(out var slotGroupsByPriority,
			out var slotGroupCount);
		for (var i = slotGroupCount; --i >= 0;)
		{
			if (slotGroupsByPriority.UnsafeLoad(i).StorageGroup is { } storageGroup)
				storageGroup.SpawnedMemberCount()++;
		}
	}

	static StorageExtensions()
	{
		LwmCompCacheValue.EnsureInitialized();
		RimfactoryExtensionCacheValue.EnsureInitialized();
	}
}