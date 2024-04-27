// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;

namespace PerformanceFish.Hauling;

public class StorageDistrict
{
	public int FreeSlots = 0xFFFFFFF;

	private int _storedThingCount;

	public int TotalSlots => Parent.GetTotalSlots(Cells.Count);

	public List<IntVec3> Cells { get; } = [];

	private FishTable<ushort, List<Thing>> _storedThingsByDef = new();
	
	public SlotGroup Parent { get; }

	public Map Map { get; } = null!;

	public StorageDistrict(SlotGroup? parent)
	{
		Parent = parent!;

		if (parent != null)
			Map = parent.Map;
	}

	public void AddCell(in IntVec3 cell)
	{
		Cells.Add(cell);
		var cellIndex = new CellIndex(cell, Map);
		Map.StorageDistrictGrid()[cellIndex] = this;

		FreeSlots = TotalSlots - _storedThingCount;
		
		var thingsAtCell = cellIndex.GetThingList(Map);
		for (var i = 0; i < thingsAtCell.Count; i++)
		{
			var thing = thingsAtCell[i];
			if (!thing.IsItem())
				continue;

			AddThing(thing);
		}
	}

	public IntVec3 PopCell()
	{
		var cell = Cells.Pop();
		DeregisterCell(cell);
		return cell;
	}

	public bool TryRemoveCell(in IntVec3 cell)
	{
		if (!Cells.Remove(cell))
			return false;

		DeregisterCell(cell);
		return true;
	}

	private void DeregisterCell(in IntVec3 cell)
	{
		var cellIndex = new CellIndex(cell, Map);

		ref var districtGridSlot = ref Map.StorageDistrictGrid()[cellIndex];
		if (districtGridSlot == this)
			districtGridSlot = null;
		
		var thingsAtCell = cellIndex.GetThingList(Map);
		for (var i = 0; i < thingsAtCell.Count; i++)
		{
			var thing = thingsAtCell[i];
			if (!thing.IsItem())
				continue;

			RemoveThing(thing);
		}
	}

	public void AddThing(Thing thing)
	{
		StoredThingsOfDef(thing.def).Add(thing);

		FreeSlots = TotalSlots - ++_storedThingCount;
	}

	public void RemoveThing(Thing thing)
	{
		if (!StoredThingsOfDef(thing.def).Remove(thing))
		{
			ThrowForInvalidRemovalAttempt(thing);
		}

		FreeSlots = TotalSlots - --_storedThingCount;
	}

	[DoesNotReturn]
	private void ThrowForInvalidRemovalAttempt(Thing thing)
		=> ThrowHelper.ThrowInvalidOperationException($"Failed removing thing '{
			thing}' from district at cells '{Cells.ToStringSafeEnumerable()}'");

	public List<Thing> StoredThingsOfDef(ThingDef def) => _storedThingsByDef.GetOrAdd(def.shortHash);
	
	public bool CapacityAllows(Thing t) => FreeSlots > 0 || StoredThingsOfDef(t.def).ContainsThingStackableWith(t);
	
	public static StorageDistrict[] GetDefaultArray() => _defaultDistrictArray;

	public void Dispose()
	{
		if (this == _defaultDistrict)
			return;
		
		var districtGrid = Map.StorageDistrictGrid();
		var cells = Cells;
		
		for (var i = cells.Count; i-- > 0;)
		{
			ref var district = ref districtGrid[cells[i]];
			if (district == this)
				district = null;
		}
	}
	
	private static StorageDistrict _defaultDistrict = new(null);
	private static StorageDistrict[] _defaultDistrictArray = [_defaultDistrict];
}