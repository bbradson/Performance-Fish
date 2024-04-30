// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Hauling;

public sealed class HaulDestinationManagerPatches : ClassWithFishPrepatches
{
	public sealed class Notify_HaulDestinationChangedPriorityPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HaulDestinationManager),
				nameof(HaulDestinationManager.Notify_HaulDestinationChangedPriority));

		public static void Prefix(HaulDestinationManager __instance) => __instance.RecalculateStorageGroupMemberCount();

		public static void Postfix(HaulDestinationManager __instance)
			=> __instance.Cache().OnPriorityChanged(__instance);
	}
	
	public sealed class AddHaulDestinationPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HaulDestinationManager),
				nameof(HaulDestinationManager.AddHaulDestination));

		public static void Prefix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is IStorageGroupMember { Group: { } group })
				group.SpawnedMemberCount()++;
		}

		public static void Postfix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is not ISlotGroupParent slotGroupParent)
				return;

			__instance.Cache().OnPriorityChanged(__instance);
			slotGroupParent.GetSlotGroup().NotifyCellCountChanged();
		}
	}
	
	public sealed class RemoveHaulDestinationPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HaulDestinationManager),
				nameof(HaulDestinationManager.RemoveHaulDestination));

		public static void Prefix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is IStorageGroupMember { Group: { } group })
				group.SpawnedMemberCount()--;
		}

		public static void Postfix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is not ISlotGroupParent slotGroupParent)
				return;

			__instance.Cache().OnPriorityChanged(__instance);
			slotGroupParent.GetSlotGroup().ResetDistricts();
		}
	}
	
	public sealed class CompareSlotGroupPrioritiesDescendingPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; }
			= [typeof(StoreUtilityPrepatches.TryFindBestBetterStoreCellForPatch)];

		public override string? Description { get; }
			= "Modifies storage sort order within the haul destination manager to put groups of linked storages first "
			+ "within their priority, descending by size and then loadID. "
			+ "StoreUtilityPrepatches:TryFindBestBetterStoreCellFor requires this";

		public override MethodBase TargetMethodBase { get; }
			= methodof(HaulDestinationManager.CompareSlotGroupPrioritiesDescending);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Postfix(int __result, SlotGroup a, SlotGroup b)
		{
			if (__result != 0)
				return __result;
			
			var storageSettingsGroupA = a.StorageGroup;
			var storageSettingsGroupB = b.StorageGroup;

			return storageSettingsGroupA == null
				? storageSettingsGroupB == null
					? 0
					: 1
				: storageSettingsGroupB == null
					? -1
					: CompareStorageSettingsGroups(storageSettingsGroupA, storageSettingsGroupB);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int CompareStorageSettingsGroups(StorageGroup a, StorageGroup b)
		{
			var result = b.SpawnedMemberCount().CompareTo(a.SpawnedMemberCount());
			return result != 0 ? result : a.loadID.CompareTo(b.loadID);
		}
	}
}