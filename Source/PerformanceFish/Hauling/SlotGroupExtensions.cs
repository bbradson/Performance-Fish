// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using FisheryLib.Pools;

namespace PerformanceFish.Hauling;

public static class SlotGroupExtensions
{
	public const int
		MINIMUM_DISTRICT_COUNT = 3,
		MINIMUM_DISTRICT_SLOTS = 9,
		MINIMUM_DISTRICT_CELLS = 3;
	
	public static bool ShouldHaveDistricts([NotNullWhen(true)] this SlotGroup? slotGroup, out int cellCountIfTrue,
		out int totalSlotsIfTrue)
	{
		if (slotGroup is null
			|| (slotGroup.parent is Building building
				&& (!building.IsSpawned() || RimfactoryExtensionCacheValue.HasExtension(building))))
		{
			Unsafe.SkipInit(out cellCountIfTrue);
			goto False;
		}
		
		cellCountIfTrue = slotGroup.CellsList.Count;
		if (cellCountIfTrue >= MINIMUM_DISTRICT_COUNT * MINIMUM_DISTRICT_CELLS)
		{
			return (totalSlotsIfTrue = slotGroup.GetTotalSlots(cellCountIfTrue))
				>= MINIMUM_DISTRICT_COUNT * MINIMUM_DISTRICT_SLOTS;
		}

	False:
		Unsafe.SkipInit(out totalSlotsIfTrue);
		return false;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ResetAndRemakeDistricts(this SlotGroup slotGroup)
	{
		slotGroup.ResetDistricts();
		slotGroup.NotifyCellCountChanged();
	}
	
	public static void ResetDistricts(this SlotGroup slotGroup)
	{
		ref var districts = ref slotGroup.Districts();
		
		for (var i = districts.Length; i-- > 0;)
			districts[i].Dispose();

		districts = StorageDistrict.GetDefaultArray();
	}
	
	public static void NotifyCellCountChanged(this SlotGroup slotGroup)
	{
		if (slotGroup.ShouldHaveDistricts(out var cellCount, out var totalSlots))
			slotGroup.AdjustDistricts(totalSlots, cellCount);
		else
			slotGroup.ResetDistricts();
	}
	
	public static void AdjustDistricts(this SlotGroup slotGroup, int totalSlots, int cellCount,
		Span<IntVec3> cellsToAdd = default, Span<IntVec3> cellsToRemove = default)
	{
		var districtCount = Math.Min(totalSlots / MINIMUM_DISTRICT_SLOTS, cellCount / MINIMUM_DISTRICT_COUNT);
		ref var districts = ref slotGroup.Districts();

		using var cellBufferPooled = new PooledIList<List<IntVec3>>();
		var cellBuffer = cellBufferPooled.List;
		var hasDistricts = districts.Length > 1;

		if (cellsToRemove.Length > 0)
		{
			if (hasDistricts)
			{
				for (var i = cellsToRemove.Length; i-- > 0;)
				{
					for (var j = districts.Length; j-- > 0;)
					{
						if (districts[j].TryRemoveCell(cellsToRemove[i]))
							break;
					}
				}
			}
			else
			{
				Log.Error($"Tried removing cells from districts in slotGroup of {
					slotGroup.parent}, but none exist.");
			}
		}
		
		if (cellsToAdd.Length > 0 && hasDistricts)
		{
			for (var i = cellsToAdd.Length; i-- > 0;)
				cellBuffer.Add(cellsToAdd[i]);
		}

		if (hasDistricts)
		{
			if (districts.Length > districtCount)
			{
				for (var i = districtCount - 1; i < districts.Length; i++)
					cellBuffer.AddRange(districts[i].Cells);
			}
		}
		else
		{
			cellBuffer.AddRange(slotGroup.CellsList);
		}

		if (districts.Length != districtCount)
		{
			if (hasDistricts)
				Array.Resize(ref districts, districtCount);
			else
				districts = new StorageDistrict[districtCount];

			for (var i = 0; i < districts.Length; i++)
				((StorageDistrict?[])districts)[i] ??= new(slotGroup);
		}

		var districtSize = cellCount / districtCount;
		var finalDistrictSize = cellCount - (districtSize * (districtCount - 1));
		
		for (var i = 0; i < districtCount; i++)
		{
			var district = districts[i];
			var desiredDistrictSize = i != districtCount - 1 ? districtSize : finalDistrictSize;

			while (district.Cells.Count > desiredDistrictSize)
				cellBuffer.Add(district.PopCell());
		}
		
		for (var i = 0; i < districtCount; i++)
		{
			var district = districts[i];

			while (district.Cells.Count < districtSize)
				district.AddCell(cellBuffer.Pop());
		}
		
		while (cellBuffer.Count > 0)
			districts[^1].AddCell(cellBuffer.Pop());
	}
}