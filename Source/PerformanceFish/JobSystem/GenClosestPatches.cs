// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.JobSystem;

public sealed class GenClosestPatches : ClassWithFishPrepatches
{
	public sealed class ClosestThingReachablePatch : FishPrepatch
	{
		public override string Description { get; }
			= "Job scanning optimization making repairing, wardening and tending use a cache of targets where "
			+ "appropriate";
		
		public override MethodBase TargetMethodBase => methodof(GenClosest.ClosestThingReachable);

		public static void Prefix(Map map, ref ThingRequest thingReq, IEnumerable<Thing>? customGlobalSearchSet)
		{
			if (!thingReq.CanBeFoundInRegion || customGlobalSearchSet is not ICollection<Thing> collection)
				return;

			if (map.listerThings.ThingsMatching(thingReq).Count > collection.Count * 5)
				thingReq = ThingRequest.ForUndefined();
		}
	}
}