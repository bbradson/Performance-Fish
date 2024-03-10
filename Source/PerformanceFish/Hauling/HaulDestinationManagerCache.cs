// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Hauling;

public record struct HaulDestinationManagerCache()
{
	public static readonly int StoragePriorityCount = Enum.GetValues(typeof(StoragePriority)).Length;

	public event Action<HaulDestinationManager>? PriorityChanged;
	
	public int[] GroupCountByPriority = new int[StoragePriorityCount];

	[MethodImpl(MethodImplOptions.NoInlining)]
	public void OnPriorityChanged(HaulDestinationManager manager)
	{
		var slotGroupsByPriority = manager.AllGroupsListInPriorityOrder;
		
		Array.Clear(GroupCountByPriority, 0, GroupCountByPriority.Length);
		for (var i = slotGroupsByPriority.Count; i-- > 0;)
			GroupCountByPriority[(int)slotGroupsByPriority[i].Settings.Priority]++;

		PriorityChanged?.Invoke(manager);
	}
}