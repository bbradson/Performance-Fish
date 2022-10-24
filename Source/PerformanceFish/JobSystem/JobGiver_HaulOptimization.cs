// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.JobSystem;

public class JobGiver_HaulOptimization : ClassWithFishPatches
{
    public class PotentialWorkThingsGlobal_Patch : FishPatch
    {
        public override string Description => "Sorts haulables by distance before running expensive hauling calculations on them to avoid checks on far away items";
        public override MethodBase TargetMethodInfo => AccessTools.Method(typeof(JobGiver_Haul), nameof(JobGiver_Haul.TryGiveJob));
        public static CodeInstructions? Transpiler(CodeInstructions codes, MethodBase method)
        {
            var pawn_Map_listerHaulables_ThingsPotentiallyNeedingHauling = FishTranspiler.Call(() => default(ListerHaulables)!.ThingsPotentiallyNeedingHauling());

            try
            {
                return codes.InsertAfter(
                    c => c == pawn_Map_listerHaulables_ThingsPotentiallyNeedingHauling,
                    new CodeInstruction[]
                    {
                    FishTranspiler.Argument(method, "pawn"),
                    FishTranspiler.Call(SortedThingsPotentiallyNeedingHauling)
                    });
            }
            catch (Exception ex)
            {
                Log.Error($"{ex}");
                //Log.Error("Performance Fish failed to apply its JobGiver_Haul transpiler. This particular optimization will be doing nothing now");
                return null;
            }
        }
        public static List<Thing> SortedThingsPotentiallyNeedingHauling(List<Thing> things, Pawn pawn)
        {
            _comparer.rootCell = pawn.Position;
            things.Sort(_comparer);
            return things;
        }
        private static WorkGiver_DoBillOptimization.ThingPositionComparer _comparer = new();
    }
}