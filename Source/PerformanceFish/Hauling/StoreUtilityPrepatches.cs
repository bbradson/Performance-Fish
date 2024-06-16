// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// #define STORAGE_GROUP_DEBUG

using System.Diagnostics;
using System.Linq;
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
			typeof(SlotGroupPrepatches.Notify_AddedCellPatch), typeof(SlotGroupPrepatches.Notify_LostCellPatch),
			typeof(StorageSettingsPatches.AllowedToAcceptPatch)
		];

		public override string? Description { get; }
			= "Optimizes hauling by splitting storage areas into smaller regions with cached capacity information. "
			+ "Additionally makes lookups more thorough when having the 'Improve hauling accuracy' setting enabled, "
			+ "raising the odds of getting closer cells as haul destinations. StorageSettingsPatches:AllowedToAccept "
			+ "requires this patch due to shared caches";

		public override MethodBase TargetMethodBase { get; }
			= methodof(StoreUtility.TryFindBestBetterStoreCellForWorker);
		
		protected internal override void OnPatchingCompleted() => ThingEvents.Initialized += TryRegister;

		public static void TryRegister(Thing thing)
		{
			var thingEvents = thing.Events();
			
			if (thing is ISlotGroupParent)
				thingEvents.Spawned += StorageSettingsPatches.InitializeSlotGroupParent;

			if (!thing.IsItem())
				return;

			thingEvents.RegisteredAtThingGrid += TryNotifyReceivedThing;
			thingEvents.DeregisteredAtThingGrid += TryNotifyLostThing;
		}

		public static void TryNotifyReceivedThing(Thing thing, Map map, in IntVec3 cell)
		{
			if (cell.GetSlotGroupParent(map) is { } parent and Thing)
				parent.GetThingStoreSettings()?.Cache().Notify_ReceivedThing(thing);
			
			map.StorageDistrictGrid()[cell]?.AddThing(thing);
		}

		public static void TryNotifyLostThing(Thing thing, Map map, in IntVec3 cell)
		{
			if (cell.GetSlotGroupParent(map) is { } parent and Thing)
				parent.GetThingStoreSettings()?.Cache().Notify_LostThing(thing);
			
			map.StorageDistrictGrid()[cell]?.RemoveThing(thing);
		}

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(Thing t, Pawn carrier, Map map, Faction faction,
#if V1_4
			SlotGroup? slotGroup,
#else
			ISlotGroup? slotGroup,
#endif
			bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared,
			ref StoragePriority foundPriority)
		{
			if (slotGroup == null || !slotGroup.Settings.AllowedToAccept(t) || !CapacityAllows(slotGroup, t))
				return;
			
			var thingPosition = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
			var slotGroupCells = slotGroup.CellsList;
			
			var maxValidCellsToCheck
				= !FishSettings.ImproveHaulingAccuracy && needAccurateResult
					? Mathf.FloorToInt(slotGroupCells.Count * Rand.Range(0.005f, 0.018f))
					: 0;
		
		StartOfDistrictLoop:
			var districts = GetDistricts(slotGroup);
			
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

#if V1_4
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static StorageDistrict[] GetDistricts(SlotGroup slotGroup) => slotGroup.Districts();
		// wrapper for analyzer compatibility, as it otherwise throws on ref returns
#else
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static StorageDistrict[] GetDistricts(ISlotGroup iSlotGroup)
			=> iSlotGroup is SlotGroup slotGroup ? slotGroup.Districts() : StorageDistrict.GetDefaultArray();
#endif

		public static bool CapacityAllows(ISlotGroup iSlotGroup, Thing t)
			=> iSlotGroup is not SlotGroup slotGroup || CapacityAllows(slotGroup, t);
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool CapacityAllows(SlotGroup slotGroup, Thing t)
		{
			var storageSettings = slotGroup.GetThingStoreSettings();
			if (storageSettings?.owner is not Thing)
				return true;

			ref var cache = ref storageSettings.Cache();
			StorageSettingsPatches.Debug.VerifyItemCount((Thing)storageSettings.owner, ref cache);
			
			return cache.FreeSlots > 0 || AcceptsForStacking(ref cache, t);
		}

		public static bool AcceptsForStacking(ref StorageSettingsPatches.StorageSettingsCache cache, Thing t)
			=> cache.StoredThingsOfDef(t.def).ContainsThingStackableWith(t);
	}

	public sealed class TryFindBestBetterStoreCellForPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes a loop in the method to treat storage groups as proper groups without duplicate checks and to "
			+ "make use of cached storage priorities for fewer comparisons against those. Also removes haulables from "
			+ "ListerHaulables if the found best store cell turns out to be the current cell";

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

#if STORAGE_GROUP_DEBUG
			if (!Debug.LoggedOnce && !(Current.Game?.tickManager?.Paused ?? true))
				Debug.LogStuff(haulDestinationManager);
#endif
			
			var foundPriority = currentPriority;
			var closestDistSquared = (float)int.MaxValue;
			var closestSlot = IntVec3.Invalid;

			var hadToFixCache = false;

		StartOfLoop:
			var listInPriorityOrderCount = listInPriorityOrder.Count;
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
					if (i >= listInPriorityOrderCount)
						goto FixCache;
					
					var slotGroup = listInPriorityOrder[i++];
					int otherGroupMembers;
					var storageGroup = slotGroup.StorageGroup;
					if (storageGroup != null)
					{
						otherGroupMembers = storageGroup.SpawnedMemberCount() - 1;
						if (otherGroupMembers < 0)
							goto FixCache;
						
						Debug.VerifyStorageGroup(otherGroupMembers, listInPriorityOrder, i, storageGroup);
						
						groupsOfPriorityCount -= otherGroupMembers;
						
						if (!storageGroup.Accepts(t))
						{
							i += otherGroupMembers;
							continue;
						}
					}
					else
					{
						otherGroupMembers = 0;
					}

					while (true)
					{
						StoreUtility.TryFindBestBetterStoreCellForWorker(t, carrier, map, faction, slotGroup,
							needAccurateResult, ref closestSlot, ref closestDistSquared, ref foundPriority);

						if (otherGroupMembers <= 0)
							break;
						
						if (i >= listInPriorityOrderCount)
							goto FixCache;
						
						otherGroupMembers--;
						slotGroup = listInPriorityOrder[i++];
					}
				}
			}

		Result:
			if (!closestSlot.IsValid)
			{
				foundCell = IntVec3.Invalid;
				return false;
			}
			
			foundCell = closestSlot;
			return true;
			
		FixCache:
			if (!hadToFixCache)
			{
				hadToFixCache = true;
				UpdateCache(haulDestinationManager);
				goto StartOfLoop;
			}
			else
			{
				LogErrorForFailedTryFindBestBetterStoreCellForAttempt(t, carrier, map, currentPriority, faction,
					closestSlot);
				goto Result;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void LogErrorForFailedTryFindBestBetterStoreCellForAttempt(Thing t, Pawn carrier, Map? map,
			StoragePriority currentPriority, Faction faction, IntVec3 closestSlot)
			=> Log.Error($"Performance Fish's TryFindBestBetterStoreCellFor patch failed to compute accurate "
				+ $"results after a recache attempt for thing '{t}', pawn '{carrier}', map '{
					map}', currentPriority '{currentPriority}', faction '{
						faction}'. The last found cell was '{
							closestSlot}'. It is most likely incompatible with something in the mod list.\n{
								Debug.GetStorageGroupInfo(map?.haulDestinationManager)}");

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void UpdateCache(HaulDestinationManager haulDestinationManager)
		{
			var storageGroups = haulDestinationManager.map.storageGroups.groups;
			for (var i = storageGroups.Count; i-- > 0;)
			{
				var group = storageGroups[i];
				var members = group.members;
				for (var j = members.Count; j-- > 0;)
				{
					var member = members[j];
					if (member.Group != group)
						members.RemoveAt(j);
				}
				
				if (members.Count == 0)
					storageGroups.RemoveAt(i);
			}
			
			haulDestinationManager.RecalculateStorageGroupMemberCount();
			
			haulDestinationManager.AllGroupsListInPriorityOrder.InsertionSort(HaulDestinationManager
				.CompareSlotGroupPrioritiesDescending);
			haulDestinationManager.Cache().OnPriorityChanged(haulDestinationManager);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int[] GetCachedGroupCountByPriority(HaulDestinationManager haulDestinationManager)
			=> haulDestinationManager.Cache().GroupCountByPriority;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void TryRemoveFromListerHaulables(Thing t, StoragePriority currentPriority)
		{
			if (ModCompatibility.ActiveMods.Multiplayer | !HaulablesTickPatchActive)
				return;

			if (t.IsInAnyStorage() /*t.IsInValidStorage()*/ && t.TryGetMapHeld() is { } map)
				map.listerHaulables.Cache().ThingsQueuedToRemove.Add(t);

			// remove for any storage instead of only valid storage to prevent further haul attempts until the next
			// automatic ListerHaulablesTick cycle
		}

		private static int _haulablesTickPatchActive = int.MaxValue;

		private static bool HaulablesTickPatchActive
			=> _haulablesTickPatchActive != int.MaxValue
				? _haulablesTickPatchActive.AsBool()
				: UpdateHaulablesTickPatchActive();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool UpdateHaulablesTickPatchActive()
			=> (_haulablesTickPatchActive = Get<Haulables.TickPatch>().IsActive.AsInt()).AsBool();

		public static class Debug
		{
			[Conditional("STORAGE_GROUP_DEBUG")]
			public static void VerifyStorageGroup(int otherGroupMembers, List<SlotGroup> listInPriorityOrder, int i,
				StorageGroup storageGroup)
			{
				for (var b = otherGroupMembers; b-- > 0;)
				{
					var otherSlotGroup = listInPriorityOrder[i + b];
					if (otherSlotGroup.StorageGroup == storageGroup)
						continue;

					Log.Error($"Incorrect haul destination order! Storage '{
						otherSlotGroup.parent}' was expected to have Storage group '{storageGroup}', but had '{
							otherSlotGroup.StorageGroup.ToStringSafe()}' instead");
				}
			}

			[Conditional("STORAGE_GROUP_DEBUG")]
			public static void LogStuff(HaulDestinationManager haulDestinationManager)
			{
				LoggedOnce = true;
				Log.Message(GetStorageGroupInfo(haulDestinationManager));
			}

			public static string GetStorageGroupInfo(HaulDestinationManager? haulDestinationManager)
			{
				var slotGroupsInPriorityOrder = haulDestinationManager?.AllGroupsListInPriorityOrder;
				if (slotGroupsInPriorityOrder is null)
					return string.Empty;
				
				var storageGroups = haulDestinationManager!.map.storageGroups.groups;

				return StringHelper.Resolve($"SlotGroup count: {
					slotGroupsInPriorityOrder.Count}, StorageGroup count: {
						storageGroups.Count}, slotGroups in storage groups: {storageGroups
							.Select(static group => group!.SpawnedMemberCount())
							.Sum()}, outside of storage groups: {slotGroupsInPriorityOrder
							.Select(static slotGroup => slotGroup.StorageGroup)
							.Where(Is.Null).Count()}");
			}

			public static bool LoggedOnce;
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