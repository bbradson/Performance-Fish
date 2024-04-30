// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Security;
using Verse.Sound;

namespace PerformanceFish.Utility;

public static class MiscExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SecuritySafeCritical]
	public static bool AsBool(this int value) // ref on parameter instead of var would enforce stack allocation
	{
		var temp = (byte)value; // bool is 32 bits in .NET, but the jit compiler only eliminates when casting to byte first
		return Unsafe.As<byte, bool>(ref temp); // result is a direct return with no instructions whatsoever
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SecuritySafeCritical]
	public static int AsInt(this bool value)
	{
		var temp = value;
		return Unsafe.As<bool, byte>(ref temp);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsItem(this Thing thing) => thing.def.category == ThingCategory.Item;

	public static StorageSettings? TryGetGroupStoreSettings(this ISlotGroupParent slotGroupParent)
		=> slotGroupParent.TryGetStorageGroup()?.GetStoreSettings();

	public static StorageGroup? TryGetStorageGroup(this ISlotGroupParent slotGroupParent)
		=> slotGroupParent is IStorageGroupMember groupMember ? groupMember.Group : null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Accepts(this StorageGroup storageGroup, Thing t)
		=> storageGroup.GetStoreSettings().AllowedToAccept(t);

	public static ISlotGroupParent? GetSlotGroupParent(this in IntVec3 position, Map map)
		=> map.haulDestinationManager.SlotGroupAt(position)?.parent;

	public static StorageSettings? GetThingStoreSettings(this SlotGroup slotGroup)
		=> slotGroup.parent.GetThingStoreSettings();

	public static StorageSettings? GetThingStoreSettings(this ISlotGroupParent slotGroupParent)
		=> slotGroupParent is IStorageGroupMember groupMember
			? groupMember.ThingStoreSettings
			: slotGroupParent.GetStoreSettings();

	public static ISlotGroupParent? StoringSlotGroupParent(this Thing thing)
		=> thing.Position.GetSlotGroupParent(thing.GetMap());

	public static int SlotGroupCellCount(this Thing thing) => ((ISlotGroupParent)thing).AllSlotCellsList().Count;

	public static List<Thing> GetThingListUnchecked(this in IntVec3 cell, Map map)
		=> map.thingGrid.ThingsListAtFast(cell.CellToIndex(map));

	public static List<Thing> GetThingListUnchecked(this IntVec2 cell, Map map)
		=> map.thingGrid.ThingsListAtFast(cell.CellToIndex(map));

	public static Region? RegionAtUnchecked(this IntVec3 c, Map map,
		RegionType allowedRegionTypes = RegionType.Set_Passable)
	{
		map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
		
		return map.regionGrid.regionGrid[c.CellToIndex(map)] is { valid: true } region
			&& (region.type & allowedRegionTypes) != RegionType.None
				? region
				: null;
	}

	public static Region? GetValidRegionAtUnchecked(this RegionGrid grid, in IntVec3 c)
	{
		var map = grid.map;
		
		map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
		return grid.regionGrid[c.CellToIndex(map)] is { valid: true } region ? region : null;
	}

	public static Vector2 GetRotatedDrawSize(this Thing thing)
		=> !thing.Graphic.ShouldDrawRotated && thing.Rotation.IsHorizontal ? thing.DrawSize.Rotated() : thing.DrawSize;

	public static bool ContainsThingStackableWith(this List<Thing> thingsOfSingleDef, Thing t)
	{
		var def = t.def;

		for (var i = thingsOfSingleDef.Count; i-- > 0;)
		{
			if (thingsOfSingleDef[i].stackCount < def.stackLimit && thingsOfSingleDef[i].CanStackWith(t))
				return true;
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int SizeX(this Map map) => map.cellIndices.mapSizeX;

	public static Map? TryGetMapHeld(this Thing thing)
	{
		var maps = Current.Game.Maps;
		var mapIndex = (uint)thing.mapIndexOrState;

		return mapIndex < (uint)maps.Count
			? maps[(int)mapIndex]
			: thing.ParentHolder is { } parentHolder
				? ThingOwnerUtility.GetRootMap(parentHolder)
				: null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Map? TryGetMap(this Thing thing)
	{
		var maps = Current.Game.Maps;
		var mapIndex = (uint)thing.mapIndexOrState;
		
		return mapIndex < (uint)maps.Count ? maps[(int)mapIndex] : null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Map GetMap(this Thing thing) => Current.Game.Maps[thing.mapIndexOrState];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Map GetMap(this Region region) => Current.Game.Maps[region.mapIndex];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsSpawned(this Thing thing) => (uint)thing.mapIndexOrState < (uint)Current.Game.Maps.Count;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetLoadID(this Bill bill) => bill.loadID;

	public static void InvokeWhenCellIndicesReady(this Map map, Action<Map> action)
	{
		if (map.cellIndices != null)
			action(map);
		else
			map.Events().ComponentsConstructed += action;
	}
	
	public static void AddLetterSilently(this LetterStack letterStack, Letter letter, Color color, bool playSound = false,
		string? debugInfo = null)
	{
		var letterDef = letter.def.MemberwiseClone();
		letterDef.index = 0;
		letterDef.shortHash = 0;
		letterDef.color = color;
		letterDef.flashColor = color;
		letter.def = letterDef;
		letterStack.AddLetterSilently(letter, playSound, debugInfo);
	}

	public static void AddLetterSilently(this LetterStack letterStack, Letter letter, bool playSound = false,
		string? debugInfo = null)
	{
		if (playSound)
			letter.def.arriveSound.PlayOneShotOnCamera();
		
		letter.arrivalTime = Time.time;
		letter.arrivalTick = Find.TickManager.TicksGame;
		letter.debugInfo = debugInfo;
		letterStack.letters.Add(letter);
		letter.Received();
	}
}