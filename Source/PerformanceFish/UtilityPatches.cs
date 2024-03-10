// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public sealed class UtilityPatches : ClassWithFishPatches
{
	public sealed class FinalizeInit_Patch : FishPatch
	{
		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override string Description { get; }
			= "Necessary for patches that require a loaded game to function";
		
		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit));

		[HarmonyPriority(Priority.VeryLow)]
		public static void Postfix()
		{
			foreach (var patch in PerformanceFishMod.AllPatchClasses!)
			{
				if (patch.RequiresLoadedGameForPatching)
					patch.Patches.PatchAll();
			}

			Benchmarking.SwitchFrame = GenTicks.TicksGame + 6000;
			Benchmarking.EndFrame = Benchmarking.SwitchFrame + 6000;
		}
	}

	public sealed class ClearAllMapsAndWorld_Patch : FishPatch
	{
		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override string Description { get; }
			= "This clears all Performance Fish caches whenever saves get unloaded, to avoid anything getting carried "
			+ "over to a new game by mistake";

		public override Delegate TargetMethodGroup => Verse.Profile.MemoryUtility.ClearAllMapsAndWorld;

		[HarmonyPriority(Priority.VeryLow)]
		public static void Postfix()
		{
			Cache.Utility.Clear();
			StatCaching.ResetAllStatsCaches();
			ParallelNoAlloc.ClearAll();
			foreach (var patch in PerformanceFishMod.AllPatchClasses!)
			{
				if (patch.RequiresLoadedGameForPatching)
					patch.Patches.UnpatchAll();
			}
		}
	}
}