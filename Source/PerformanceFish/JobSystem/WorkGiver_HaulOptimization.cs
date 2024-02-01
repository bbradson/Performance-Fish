// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.ModCompatibility;

namespace PerformanceFish.JobSystem;

public sealed class WorkGiver_HaulOptimization : ClassWithFishPatches
{
	public sealed class PotentialWorkThingsGlobal_Patch : FishPatch
	{
		public override List<string> IncompatibleModIDs { get; } = [PackageIDs.MULTIPLAYER];
		
		public override string Description { get; }
			= "Sorts haulables by distance before running expensive hauling calculations on them to avoid checks on "
			+ "far away items";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(WorkGiver_Haul), nameof(WorkGiver_Haul.PotentialWorkThingsGlobal));

		public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Pawn? pawn)
		{
			if (pawn != null && __result is List<Thing> list)
				Sort(list, pawn);
			return __result;
		}

		private static void Sort(List<Thing> list, Pawn pawn)
		{
			((ThingPositionComparer)_comparer.Target).rootCell = pawn.Position;
			list.Sort(_comparer);
		}

		private static Comparison<Thing> _comparer = new ThingPositionComparer().Compare;
	}
}