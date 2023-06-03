// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.ModCompatibility;

public static class ActiveMods
{
	public static readonly bool
		Fishery = Contains(PackageIDs.FISHERY),
		// GradientHair = Contains(PackageIDs.GRADIENT_HAIR),
		GraphicsSetter = Contains(PackageIDs.GRAPHICS_SETTINGS),
		Harmony = Contains(PackageIDs.HARMONY),
		Multiplayer = Contains(PackageIDs.MULTIPLAYER),
		PerformanceOptimizer = Contains(PackageIDs.PERFORMANCE_OPTIMIZER),
		Prepatcher = Contains(PackageIDs.PREPATCHER),
		VanillaExpandedFramework = Contains(PackageIDs.VANILLA_EXPANDED_FRAMEWORK);
	
	// ModsConfig.IsActive does not ignore _steam postfixes, causing it to fail when local copies were made
	public static bool Contains(string packageID) => TryGetModMetaData(packageID) != null;
	
	public static ModMetaData? TryGetModMetaData(string packageID)
		=> ModLister.GetActiveModWithIdentifier(packageID, true);

	public static ModContentPack? TryGetModContentPack(string packageID)
	{
		var allMods = LoadedModManager.RunningModsListForReading;
		var count = allMods.Count;
		for (var i = 0; i < count; i++)
		{
			if (allMods[i].PackageIdPlayerFacing.Equals(packageID, StringComparison.OrdinalIgnoreCase))
				return allMods[i];
		}

		return null;
	}
	
	public static Assembly? TryGetAssembly(this ModContentPack modContentPack, string name)
	{
		var assemblies = modContentPack.assemblies.loadedAssemblies;
		
		for (var i = 0; i < assemblies.Count; i++)
		{
			if (assemblies[i].GetName().Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				return assemblies[i];
		}

		return null;
	}

	public static void CheckRequirements()
	{
		if (!Fishery)
			ThrowForMissingModDependency(nameof(Fishery));

		if (!Harmony)
			ThrowForMissingModDependency(nameof(Harmony));

		VerifyLoadOrder(nameof(Fishery), PackageIDs.FISHERY, PackageIDs.PERFORMANCE_FISH, PackageIDs.BETTER_LOG);
		VerifyLoadOrder(nameof(Harmony), PackageIDs.HARMONY, PackageIDs.PERFORMANCE_FISH);

		CheckHarmonyVersion();
		CheckFisheryVersion();
	}

	private static void VerifyLoadOrder(string dependencyName, string dependencyID, params string[] dependingModIDs)
	{
		var activeModsInLoadOrder = (List<ModMetaData>)ModsConfig.ActiveModsInLoadOrder;
		if (activeModsInLoadOrder.FindIndex(mod => mod.PackageId == dependencyID)
			> activeModsInLoadOrder.FindIndex(mod => dependingModIDs.Contains(mod.PackageId)))
		{
			ThrowForIncorrectLoadOrder(dependencyName);
		}
	}

	private static void CheckFisheryVersion()
	{
		if (FisheryLib.FisheryLib.VERSION > FisheryLib.FisheryLib.CurrentlyLoadedVersion)
			ThrowForOutdatedModDependency(nameof(Fishery));
	}

	private static void CheckHarmonyVersion()
		=> CheckDependencyVersion(nameof(Harmony), typeof(Harmony), "0Harmony");

	private static void CheckDependencyVersion(string dependencyName, Type dependencyType,
		string dependencyAssemblyName)
	{
		if (typeof(PerformanceFishMod).Assembly.GetReferencedAssemblies().First(assembly
				=> assembly.FullName.StartsWith($"{dependencyAssemblyName}, Version", StringComparison.Ordinal)).Version
			> dependencyType.Assembly.GetName().Version)
		{
			ThrowForOutdatedModDependency(dependencyName);
		}
	}

	private static void ThrowForIncorrectLoadOrder(string dependencyName)
		=> ThrowHelper.ThrowOperationCanceledException($"Incorrect load order detected. {
			dependencyName} must be loaded before any mod depending on it.");

	private static void ThrowForOutdatedModDependency(string modName)
		=> ThrowHelper.ThrowOperationCanceledException($"Outdated {modName} mod detected. {
			modName} must be updated for Performance Fish to work correctly");

	private static void ThrowForMissingModDependency(string modName)
		=> ThrowHelper.ThrowOperationCanceledException(
			$"{modName} mod is missing. Performance Fish requires this mod in order to function.");
}