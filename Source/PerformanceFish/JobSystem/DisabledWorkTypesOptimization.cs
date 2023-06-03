// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.JobSystem;

public class DisabledWorkTypesOptimization : ClassWithFishPatches
{
	public class PawnGetDisabledWorkTypes : FishPatch
	{
		public override string? Description { get; }
			= "Essentially fixes a bug in Pawn.GetDisabledWorkTypes that causes the method to discard its cache and "
			+ "constantly keep recalculating. With the patch applied it'll return early when a valid cache exists.";

		public override MethodBase? TargetMethodInfo { get; }
			= AccessTools.Method(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes));

		[HarmonyPriority(Priority.VeryLow)]
		public static bool Prefix(Pawn __instance, bool permanentOnly, out List<WorkTypeDef> __result)
			=> (__result = permanentOnly
				? __instance.cachedDisabledWorkTypesPermanent
				: __instance.cachedDisabledWorkTypes) is null;
	}
}