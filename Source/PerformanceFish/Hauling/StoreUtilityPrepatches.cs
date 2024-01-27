// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Events;
using PerformanceFish.Listers;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Hauling;

public sealed class StoreUtilityPrepatches : ClassWithFishPrepatches
{
	public sealed class NoStorageBlockersInPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes this frequently accessed storage check by caching fully blocked cells in a bit array and "
			+ "reordering instructions to make better use of caches.";

		public override MethodBase TargetMethodBase { get; } = methodof(StoreUtility.NoStorageBlockersIn);

		protected internal override void OnPatchingCompleted() => ThingEvents.Initialized += TryRegister;

		public static void TryRegister(Thing thing)
		{
			if (!IsStorageBlocker(thing.def))
				return;
			
			var thingEvents = thing.Events();
			thingEvents.RegisteredAtThingGrid += SetStorageBlocker;
			thingEvents.DeregisteredAtThingGrid += TryUnsetStorageBlocker;
		}
		
		public static bool IsStorageBlocker(ThingDef thingDef)
			=> (thingDef.entityDefToBuild != null && thingDef.entityDefToBuild.passability != 0)
				|| (thingDef.passability != 0
					&& thingDef.surfaceType == SurfaceType.None
					&& thingDef.category != ThingCategory.Item);

		public static void SetStorageBlocker(Thing thing, Map map, in IntVec3 cell)
			=> map.StorageBlockerGrid()[cell] = true;

		public static void TryUnsetStorageBlocker(Thing thing, Map map, in IntVec3 cell)
		{
			var thingsList = map.thingGrid.ThingsListAt(cell);

			for (var i = thingsList.Count; i-- > 0;)
			{
				var otherThing = thingsList[i];
				if (otherThing != thing && IsStorageBlocker(otherThing.def))
					return;
			}

			map.StorageBlockerGrid()[cell] = false;
		}
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(IntVec3 c, Map map, Thing thing)
		{
			if (map.StorageBlockerGrid()[c])
				return false;

			if (c.GetItemCount(map) < c.GetMaxItemsAllowedInCell(map))
				return true;
			
			var thingsAtCell = map.thingGrid.ThingsListAt(c);
			var thingCount = thingsAtCell.Count;
			for (var i = 0; i < thingCount; i++)
			{
				var thingAtCell = thingsAtCell[i];

				if (thingAtCell.stackCount < thingAtCell.def.stackLimit
					&& thingAtCell.CanStackWith(thing)
					&& thingAtCell.def.EverStorable(false))
				{
					return true;
				}
			}

			return false;
		}
	}

	public sealed class TryFindBestBetterStoreCellForWorkerPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; } =
		[
			typeof(SlotGroupPrepatches.Notify_AddedCellPatch), typeof(SlotGroupPrepatches.Notify_LostCellPatch)
		];

		public override string? Description { get; }
			= "Optimizes hauling by splitting storage areas into smaller regions with cached capacity information. "
			+ "Additionally makes lookups more thorough when having the 'Improve hauling accuracy' setting enabled, "
			+ "raising the odds of getting closer cells as haul destinations";

		public override MethodBase TargetMethodBase { get; }
			= methodof(StoreUtility.TryFindBestBetterStoreCellForWorker);
		
		protected internal override void OnPatchingCompleted() => ThingEvents.Initialized += TryRegister;

		public static void TryRegister(Thing thing)
		{
			var thingEvents = thing.Events();
			
			if (thing is ISlotGroupParent)
			{
				thingEvents.Spawned += NotifySlotGroupCellCountChanged;
				thingEvents.DeSpawned += NotifySlotGroupCellCountChanged;
			}

			if (!thing.IsItem())
				return;

			thingEvents.RegisteredAtThingGrid += TryNotifyReceivedThing;
			thingEvents.DeregisteredAtThingGrid += TryNotifyLostThing;
		}

		public static void NotifySlotGroupCellCountChanged(Thing slotGroupParent, Map map)
			=> NotifySlotGroupCellCountChanged(slotGroupParent);
		
		public static void NotifySlotGroupCellCountChanged(Thing slotGroupParent)
			=> slotGroupParent.GetSlotGroup()?.NotifyCellCountChanged();

		public static void TryNotifyReceivedThing(Thing thing, Map map, in IntVec3 cell)
			=> map.StorageDistrictGrid()[cell]?.AddThing(thing);

		public static void TryNotifyLostThing(Thing thing, Map map, in IntVec3 cell)
			=> map.StorageDistrictGrid()[cell]?.RemoveThing(thing);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(Thing t, Pawn carrier, Map map, Faction faction, SlotGroup? slotGroup,
			bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared,
			ref StoragePriority foundPriority)
		{
			if (slotGroup == null || !slotGroup.parent.Accepts(t))
				return;
			
			var thingPosition = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
			var slotGroupCells = slotGroup.CellsList;
			
			var maxValidCellsToCheck
				= !FishSettings.ImproveHaulingAccuracy && needAccurateResult
					? Mathf.FloorToInt(slotGroupCells.Count * Rand.Range(0.005f, 0.018f))
					: 0;
		
		StartOfDistrictLoop:
			var districts = slotGroup.Districts();
			
			for (var districtIndex = 0; districtIndex < districts.Length; districtIndex++)
			{
				var district = districts[districtIndex];
		
				if (!district.CapacityAllows(t))
					continue;
		
				var districtCells = districts.Length > 1 ? district.Cells : slotGroupCells;
				
				for (var i = 0; i < districtCells.Count; i++)
				{
					var slotGroupCell = districtCells[i];
					var cellDistance = (float)(thingPosition - slotGroupCell).LengthHorizontalSquared;
		
					if (cellDistance > closestDistSquared
						|| !StoreUtility.IsGoodStoreCell(slotGroupCell, map, t, carrier, faction))
					{
						continue;
					}

					if (districts.Length > 1 && slotGroupCell.GetSlotGroup(map) != district.Parent)
					{
						district.Parent.ResetAndRemakeDistricts(); // TODO: figure out why this happens
						goto StartOfDistrictLoop;
					}
					
					closestSlot = slotGroupCell;
					closestDistSquared = cellDistance;
					foundPriority = slotGroup.Settings.Priority;
					if (!FishSettings.ImproveHaulingAccuracy && (districtIndex * i) + i >= maxValidCellsToCheck)
						return;
				}
			}
		}
	}

	public sealed class TryFindBestBetterStoreCellForPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes a loop in the method to make use of cached storage priorities for fewer comparisons against "
			+ "those. Also removes haulables from ListerHaulables if the found best store cell turns out to be the "
			+ "current cell";

		public override MethodBase TargetMethodBase { get; } = methodof(StoreUtility.TryFindBestBetterStoreCellFor);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(Thing t, Pawn carrier, Map map,
			StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, bool needAccurateResult = true)
		{
			var haulDestinationManager = map.haulDestinationManager;
			List<SlotGroup> listInPriorityOrder = haulDestinationManager.AllGroupsListInPriorityOrder;
			if (listInPriorityOrder.Count == 0)
			{
				foundCell = IntVec3.Invalid;
				return false;
			}
			
			var foundPriority = currentPriority;
			var closestDistSquared = (float)int.MaxValue;
			var closestSlot = IntVec3.Invalid;

			var groupCountByPriority = GetCachedGroupCountByPriority(haulDestinationManager);
			var i = 0;
			for (var priorityGroupIndex = groupCountByPriority.Length; priorityGroupIndex-- > 0;)
			{
				var groupsOfPriorityCount = groupCountByPriority[priorityGroupIndex];
				
				if ((StoragePriority)priorityGroupIndex < foundPriority) // succeeded finding a higher priority storage
					break;

				if ((StoragePriority)priorityGroupIndex <= currentPriority) // current storage is highest valid priority
				{
					TryRemoveFromListerHaulables(t, currentPriority);
					break;
				}

				while (groupsOfPriorityCount-- > 0)
				{
					StoreUtility.TryFindBestBetterStoreCellForWorker(t, carrier, map, faction, listInPriorityOrder[i++],
						needAccurateResult, ref closestSlot, ref closestDistSquared, ref foundPriority);
				}
			}

			if (!closestSlot.IsValid)
			{
				foundCell = IntVec3.Invalid;
				return false;
			}
			
			foundCell = closestSlot;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int[] GetCachedGroupCountByPriority(HaulDestinationManager haulDestinationManager)
			=> haulDestinationManager.Cache().GroupCountByPriority;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void TryRemoveFromListerHaulables(Thing t, StoragePriority currentPriority)
		{
			if (t.IsInAnyStorage() /*t.IsInValidStorage()*/ && t.TryGetMapHeld() is { } map)
				map.listerHaulables.TryRemoveDirectly(t);
			
			// remove for any storage instead of only valid storage to prevent further haul attempts until the next
			// automatic ListerHaulablesTick cycle
		}
	}

	public sealed class GetSlotGroupPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization for this method by reducing its amount of instructions without changing results";
		
		public override MethodBase TargetMethodBase { get; }
			= methodof((Func<Thing, SlotGroup>)StoreUtility.GetSlotGroup);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static SlotGroup? ReplacementBody(Thing thing)
			=> thing.TryGetMap() is { } map ? thing.Position.GetSlotGroup(map) : null;
	}

	public sealed class CurrentHaulDestinationOfPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization for this method by reducing its amount of instructions without changing results";
		
		public override MethodBase TargetMethodBase { get; }
			= methodof(StoreUtility.CurrentHaulDestinationOf);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static IHaulDestination? ReplacementBody(Thing t)
			=> t.TryGetMap() is { } map
				? map.haulDestinationManager.SlotGroupParentAt(t.Position)
				: t.ParentHolder as IHaulDestination;
	}
}