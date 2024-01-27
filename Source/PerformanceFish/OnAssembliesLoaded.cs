// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Threading.Tasks;
using PerformanceFish.ModCompatibility;
using PerformanceFish.Prepatching;
using PerformanceFish.System;

namespace PerformanceFish;

/// <summary>
/// Gets called before CreateModClasses(), after rimworld finishes loading assemblies
/// </summary>
public static class OnAssembliesLoaded
{
	public static bool Loaded { get; private set; }
	
	public static void Start()
	{
		Loaded = true;
		
		TryPatchGraphicsSetter();
		TryPatchPerformanceOptimizer();
		
		TryInitialize(EqualityComparerOptimization.Optimization.Initialize);

		PerformanceFishMod.AllPatchClasses = PerformanceFishMod.InitializeAllPatchClasses<IHasFishPatch>();
		PerformanceFishMod.AllPrepatchClasses ??= PerformanceFishMod.InitializeAllPatchClasses<ClassWithFishPrepatches>();
		
		TryInitialize(AnalyzerFixes.Patch, true);
		
		_ = FishSettings.Instance;
		
		PerformanceFishMod.AllPrepatchClasses.BeforeHarmonyPatching();

		Parallel.ForEach(PerformanceFishMod.AllPatchClasses, static patchClass =>
		{
			try
			{
				if (!patchClass.RequiresLoadedGameForPatching)
					patchClass.Patches.PatchAll();
			}
			catch (Exception ex)
			{
				Log.Error($"{PerformanceFishMod.NAME} encountered an exception while trying to initialize {
					patchClass.GetType().Name}:\n{ex}");
			}
		});
		
		PerformanceFishMod.AllPrepatchClasses.OnPatchingCompleted();

		Cache.Utility.Initialize();
	}

	private static void TryPatchGraphicsSetter()
	{
		if (ActiveMods.GraphicsSetter && GraphicsSetter.TryPatch() is null)
		{
			Log.Error($"Failed to apply compatibility patch for {
				GraphicsSetter.Mod?.Name ?? "Graphics Settings+"}");
		}
	}
	
	private static void TryPatchPerformanceOptimizer()
	{
		if (ActiveMods.PerformanceOptimizer && !PerformanceOptimizer.TryPatch())
		{
			Log.Error($"Failed to apply compatibility patch for {
				PerformanceOptimizer.ModContentPack?.Name ?? "Performance Optimizer"}");
		}
	}
	
	private static async void TryInitialize(Action action, bool async = false)
	{
		try
		{
			if (async)
				await Task.Run(action).ConfigureAwait(false);
			else
				action();
		}
		catch (Exception ex)
		{
			Log.Error($"{PerformanceFishMod.NAME} encountered an exception while trying to initialize {
				action.Method.FullDescription()}:\n{ex}");
		}
	}
}