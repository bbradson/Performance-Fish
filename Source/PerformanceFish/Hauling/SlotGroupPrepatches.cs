// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Hauling;

public sealed class SlotGroupPrepatches : ClassWithFishPrepatches
{
	public sealed class Notify_AddedCellPatch : FishPrepatch
	{
		public override bool ShowSettings => false;
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(SlotGroup), nameof(SlotGroup.Notify_AddedCell));
		
		public static void Postfix(SlotGroup __instance, IntVec3 c)
		{
			if (__instance.ShouldHaveDistricts(out var cellCount, out var totalSlots))
				__instance.AdjustDistricts(totalSlots, cellCount, stackalloc IntVec3[] { c });
		}
	}
	
	public sealed class Notify_LostCellPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(SlotGroup), nameof(SlotGroup.Notify_LostCell));
		
		public static void Postfix(SlotGroup __instance, IntVec3 c)
		{
			if (__instance.Districts().Length <= 1)
				return;

			if (__instance.ShouldHaveDistricts(out var cellCount, out var totalSlots))
				__instance.AdjustDistricts(totalSlots, cellCount, cellsToRemove: stackalloc IntVec3[] { c });
			else
				__instance.ResetDistricts();
		}
	}
}