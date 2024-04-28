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
		HumanResources = Contains(PackageIDs.HUMAN_RESOURCES),
		Multiplayer = Contains(PackageIDs.MULTIPLAYER),
		PerformanceOptimizer = Contains(PackageIDs.PERFORMANCE_OPTIMIZER),
		Prepatcher = Contains(PackageIDs.PREPATCHER),
		PrisonLabor = Contains(PackageIDs.PRISON_LABOR),
		RIMMSqol = Contains(PackageIDs.RIMMSQOL),
		UseBestMaterials = Contains(PackageIDs.USE_BEST_MATERIALS),
		VanillaExpandedFramework = Contains(PackageIDs.VANILLA_EXPANDED_FRAMEWORK);
	
	// ModsConfig.IsActive does not ignore _steam postfixes, causing it to fail when local copies were made
	public static bool Contains(string packageID) => TryGetModMetaData(packageID) != null;

	public static bool ContainsAnyOf<T>(T packageIDs) where T : IEnumerable<string>
	{
		foreach (var id in packageIDs)
		{
			if (Contains(id))
				return true;
		}

		return false;
	}

	public static bool ContainsAllOf<T>(T packageIDs) where T : IEnumerable<string>
	{
		foreach (var id in packageIDs)
		{
			if (!Contains(id))
				return false;
		}

		return true;
	}

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
			WarnForMissingModDependency(nameof(Harmony));

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
			ErrorForIncorrectLoadOrder(dependencyName);
		}
	}

	private static void CheckFisheryVersion()
		=> CheckDependencyVersion(nameof(Fishery), typeof(FisheryLib.FisheryLib), "1Fishery");

	private static void CheckHarmonyVersion()
		=> CheckDependencyVersion(nameof(Harmony), typeof(Harmony), "0Harmony");

	private static void CheckDependencyVersion(string dependencyName, Type dependencyType,
		string dependencyAssemblyName)
	{
		var expectedVersion = typeof(PerformanceFishMod).Assembly.GetReferencedAssemblyVersion(dependencyAssemblyName);
		var loadedAssembly = dependencyType.Assembly;
		if (expectedVersion > loadedAssembly.GetLoadedVersion())
			LogForOutdatedModDependency(dependencyName, expectedVersion, loadedAssembly);
	}

	private static void ErrorForIncorrectLoadOrder(string dependencyName)
		=> Log.Error($"Incorrect load order detected. {dependencyName} must be loaded before any mod depending on it.");

	private static void LogForOutdatedModDependency(string modName, Version expected, Assembly loaded)
	{
		var loadedVersion = loaded.GetLoadedVersion();
		var modMessage = $"Outdated {modName} mod detected. Expected {expected}, loaded is {
			loadedVersion} from path \"{TryGetAssemblyLocation(loaded)}\". {modName} ";
		var beUpdated = " be updated for Performance Fish to work correctly";
		
		if (loadedVersion.Major >= expected.Major && loadedVersion.Minor >= expected.Minor)
			Log.Warning($"{modMessage}may need to{beUpdated}");
		else
			Log.Error($"{modMessage}must{beUpdated}");
	}

	private static string TryGetAssemblyLocation(Assembly assembly)
	{
		var location = assembly.Location;
		if (!location.NullOrEmpty())
			return location;

		var assemblyName = assembly.GetName().Name;
		var firstFoundAssemblyDir = default(string);
		var allMods = LoadedModManager.RunningModsListForReading;
		var count = allMods.Count;
		for (var i = 0; i < count; i++)
		{
			var mod = allMods[i];
			if (mod.TryGetAssembly(assemblyName) is not { } foundAssembly)
				continue;

			if (foundAssembly == assembly)
				return mod.RootDir;
			
			if (firstFoundAssemblyDir != null)
				continue;

			var foundAssemblyLocation = foundAssembly.Location;
			firstFoundAssemblyDir = !foundAssemblyLocation.NullOrEmpty() ? foundAssemblyLocation : mod.RootDir;
		}

		return firstFoundAssemblyDir ?? "Unknown";
	}
	
	private static void ThrowForMissingModDependency(string modName)
	{
		Log.Error($"{modName} mod is missing. Performance Fish requires this mod in order to function.");
		ThrowHelper.ThrowOperationCanceledException();
	}

	private static void WarnForMissingModDependency(string modName)
		=> Log.Warning(
			$"{modName} mod is missing. Performance Fish may not work correctly without this dependency.");
}

public static class Installed
{
	public static bool[] DLC =
	[
		ModLister.RoyaltyInstalled, ModLister.IdeologyInstalled, ModLister.BiotechInstalled
#if !V1_4
		, ModLister.AnomalyInstalled
#endif
	];
}

public enum DLCs
{
	Royalty,
	Ideology,
	Biotech,
	Anomaly
}