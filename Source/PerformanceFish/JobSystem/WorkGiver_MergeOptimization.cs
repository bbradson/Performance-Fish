// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Verse.AI;

namespace PerformanceFish.JobSystem;

public class WorkGiver_MergeOptimization : ClassWithFishPatches
{
	public class JobOnThing_Patch : FishPatch
	{
		public override string Description { get; }
			= "Removes mergeables from ListerMergeables if they no longer qualify as mergeable when trying to check "
				+ "for a merging job on them. Vanilla RimWorld just fails and keeps them in";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(WorkGiver_Merge), nameof(WorkGiver_Merge.JobOnThing));

		public static bool Prefix(Thing t, ref Job? __result)
		{
			if (t.GetSlotGroup() is not null)
				return true;

			RemoveFromListerMergeables(t);
			__result = null;
			return false;
		}

		private static void RemoveFromListerMergeables(Thing t)
		{
			var map = t.MapHeld;
			map.listerMergeables.TryRemove(t);
			map.listerHaulables.Check(t);
		}
	}
}