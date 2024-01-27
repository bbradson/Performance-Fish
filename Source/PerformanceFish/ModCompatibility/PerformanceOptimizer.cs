// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.ModCompatibility;

public static class PerformanceOptimizer
{
	private const string ASSEMBLY_NAME = "PerformanceOptimizer";
	
	public static bool Active => ActiveMods.PerformanceOptimizer;
	
	public static ModContentPack? ModContentPack { get; }
		= ActiveMods.TryGetModContentPack(PackageIDs.PERFORMANCE_OPTIMIZER);
	
	public static Type? OptimizationBaseType { get; }
	
	public static Type[]? BlockedPatches { get; }

	public static string[] BlockedPatchNames { get; }
		=
		[
			"PerformanceOptimizer.Optimization_FasterGetCompReplacement",
			"PerformanceOptimizer.Optimization_IdeoUtility_GetStyleDominance"
		];

	static PerformanceOptimizer()
	{
		if (ModContentPack is null)
			return;

		var assembly = ModContentPack.TryGetAssembly(ASSEMBLY_NAME);
		
		if (assembly is null)
		{
			Log.Error("Performance Fish failed to find Performance Optimizer's assembly. "
				+ "This is likely to cause issues.");
			return;
		}

		OptimizationBaseType = assembly.GetType("PerformanceOptimizer.Optimization");
		
		BlockedPatches = Array.ConvertAll(BlockedPatchNames, name => assembly.GetType(name));
	}

	public static bool TryPatch()
	{
		if (BlockedPatches.NullOrEmpty())
			return false;

		foreach (var patch in BlockedPatches.Concat(OptimizationBaseType))
		{
			if (AccessTools.DeclaredPropertyGetter(patch, "IsEnabled") is { } isEnabledGetter)
				PerformanceFishMod.Harmony.Patch(isEnabledGetter, new(methodof(IsEnabledPatch)));
		}
		
		return true;
	}

	public static bool IsEnabledPatch(object __instance, ref bool __result)
		=> !BlockedPatches!.Contains(__instance.GetType())
			|| (__result = false);
}