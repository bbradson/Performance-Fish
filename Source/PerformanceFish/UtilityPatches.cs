// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public class UtilityPatches : ClassWithFishPatches
{
	public class FinalizeInit_Patch : FishPatch
	{
		public override string Description => "Necessary for patches that require a loaded game to function";
		public override Expression<Action> TargetMethod => () => default(Game)!.FinalizeInit();
		public static void Postfix()
		{
			foreach (var patch in PerformanceFishMod.AllPatchClasses)
			{
				if (patch.RequiresLoadedGameForPatching)
					patch.Patches.PatchAll();
			}

			Benchmarking.SwitchFrame = GenTicks.TicksGame + 6000;
			Benchmarking.EndFrame = Benchmarking.SwitchFrame + 6000;
		}
	}

	public class ClearAllMapsAndWorld_Patch : FishPatch
	{
		public override string Description => "A patch to make the game clear all Performance Fish caches whenever saves get unloaded, to avoid anything getting carried over to a new game by mistake";
		public override Delegate TargetMethodGroup => Verse.Profile.MemoryUtility.ClearAllMapsAndWorld;
		public static void Postfix()
		{
			Cache.Utility.Clear();
			foreach (var patch in PerformanceFishMod.AllPatchClasses)
			{
				if (patch.RequiresLoadedGameForPatching)
					patch.Patches.UnpatchAll();
			}
		}
	}
}