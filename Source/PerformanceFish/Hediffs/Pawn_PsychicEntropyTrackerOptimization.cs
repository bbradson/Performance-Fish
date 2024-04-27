// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Hediffs;

public sealed class Pawn_PsychicEntropyTrackerOptimization : ClassWithFishPatches
{
	public sealed class Psylink_Patch : FirstPriorityFishPatch
	{
		public override List<Type> LinkedPatches { get; }
			= [typeof(HediffSetCaching.DirtyCache)];
		
		public override string Description { get; }
			= "Caches/throttles the psylink method to only update on hediff changes or after 128 ticks";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Pawn_PsychicEntropyTracker),
				nameof(Pawn_PsychicEntropyTracker.Psylink));

		public static CodeInstructions Transpiler(CodeInstructions codes) => Reflection.GetCodeInstructions(Psylink);

		public static Hediff_Psylink Psylink(Pawn_PsychicEntropyTracker __instance)
		{
			if (__instance.psylinkCachedForTick <= TickHelper.TicksGame)
			{
				__instance.psylinkCached = __instance.pawn.GetMainPsylinkSource();
				__instance.psylinkCachedForTick = TickHelper.TicksGame + 128;
			}

			return __instance.psylinkCached;
		}
	}
}