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

		public static void Postfix(HaulDestinationManager __instance)
			=> __instance.Cache().OnPriorityChanged(__instance);
	}
	
	public sealed class AddHaulDestinationPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HaulDestinationManager),
				nameof(HaulDestinationManager.AddHaulDestination));

		public static void Postfix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is ISlotGroupParent)
				__instance.Cache().OnPriorityChanged(__instance);
		}
	}
	
	public sealed class RemoveHaulDestinationPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HaulDestinationManager),
				nameof(HaulDestinationManager.RemoveHaulDestination));

		public static void Postfix(HaulDestinationManager __instance, IHaulDestination haulDestination)
		{
			if (haulDestination is ISlotGroupParent)
				__instance.Cache().OnPriorityChanged(__instance);
		}
	}
}