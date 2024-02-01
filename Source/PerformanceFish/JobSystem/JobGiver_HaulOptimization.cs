// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.ModCompatibility;

namespace PerformanceFish.JobSystem;

public sealed class JobGiver_HaulOptimization : ClassWithFishPatches
{
	public sealed class PotentialWorkThingsGlobal_Patch : FishPatch
	{
		public override List<string> IncompatibleModIDs { get; } = [PackageIDs.MULTIPLAYER];

		public override string Description { get; }
			= "Sorts haulables by distance before running expensive hauling calculations on them to avoid checks on "
			+ "far away items";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(JobGiver_Haul), nameof(JobGiver_Haul.TryGiveJob));

		public static CodeInstructions? Transpiler(CodeInstructions codes, MethodBase method)
		{
			var pawn_Map_listerHaulables_ThingsPotentiallyNeedingHauling
				= FishTranspiler.Call(static () => default(ListerHaulables)!.ThingsPotentiallyNeedingHauling());

			try
			{
				return codes.InsertAfter(c => c == pawn_Map_listerHaulables_ThingsPotentiallyNeedingHauling,
					new CodeInstruction[]
					{
						FishTranspiler.Argument(method, "pawn"),
						FishTranspiler.Call(SortedThingsPotentiallyNeedingHauling)
					});
			}
			catch (Exception ex)
			{
				Log.Error($"{ex}");
				return null;
			}
		}

		public static List<Thing> SortedThingsPotentiallyNeedingHauling(List<Thing> things, Pawn pawn)
		{
			((ThingPositionComparer)_comparer.Target).rootCell = pawn.Position;
			things.Sort(_comparer);
			return things;
		}

		private static Comparison<Thing> _comparer = new ThingPositionComparer().Compare;
	}
}