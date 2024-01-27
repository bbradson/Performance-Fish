// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;

namespace PerformanceFish.Prepatching;

public static class FishPrepatchExtensions
{
	internal static void ApplyPatches(this ClassWithFishPrepatches[] allPrepatchClasses, ModuleDefinition module)
	{
		var prepatches = ToSortedList(allPrepatchClasses, true);
		
		foreach (var prepatch in prepatches)
		{
			if (!prepatch.Enabled)
				continue;
			
			try
			{
				prepatch.ApplyPatch(module);
			}
			catch (Exception ex)
			{
				Log.Error($"Exception while applying prepatch '{prepatch}' for '{PerformanceFishMod.NAME}':\n{ex}");
			}
		}
	}
	
	internal static void BeforeHarmonyPatching(this ClassWithFishPrepatches[] allPrepatchClasses)
		=> allPrepatchClasses.FinalizePrepatchAction(static patch => patch.BeforeHarmonyPatching());

	internal static void OnPatchingCompleted(this ClassWithFishPrepatches[] allPrepatchClasses)
		=> allPrepatchClasses.FinalizePrepatchAction(static patch => patch.OnPatchingCompleted());

	private static void FinalizePrepatchAction(this ClassWithFishPrepatches[] allPrepatchClasses,
		Action<FishPrepatchBase> action)
	{
		var prepatches = ToSortedList(allPrepatchClasses, false);

		foreach (var prepatch in prepatches)
		{
			if (!prepatch.IsActive)
				continue;
			
			try
			{
				action(prepatch);
			}
			catch (Exception ex)
			{
				Log.Error($"Exception while finalizing prepatch '{prepatch}' for '{PerformanceFishMod.NAME}':\n{ex}");
			}
		}
	}

	private static List<FishPrepatchBase> ToSortedList(this ClassWithFishPrepatches[] allPrepatchClasses,
		bool checkEnabledState)
	{
		var prepatches = new List<FishPrepatchBase>();

		foreach (var prepatchClass in allPrepatchClasses)
		{
			if (!checkEnabledState || prepatchClass.Enabled)
				prepatches.AddRange(prepatchClass.Patches.All.Values);
		}

		prepatches.Sort(FishPrepatchBase.PriorityComparer);
		return prepatches;
	}
}