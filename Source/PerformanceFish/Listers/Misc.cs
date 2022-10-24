// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Listers;
public class Misc : ClassWithFishPatches
{
	public class Building_Destroy_Patch : FishPatch
	{
		public override string Description => "Used for haulables caching. Items don't become haulable after deconstructing stockpiles without this patch";
		public override Expression<Action> TargetMethod => () => default(Building)!.Destroy(default);
		public static void Prefix(Building __instance)
		{
			if (__instance is not IHaulDestination)
				return;
			HaulablesCache.Get.Clear();
			MergeablesCache.Get.Clear();
		}
	}
}