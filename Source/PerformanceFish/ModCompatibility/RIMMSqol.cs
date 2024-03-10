// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.ModCompatibility;

public static class RIMMSqol
{
	private const string ASSEMBLY_NAME = "RIMMSqol";
	
	public static bool Active => ActiveMods.RIMMSqol;

	public static string[] BlockedPatchNamespaces { get; } = ["WorldPawnGC"];
	
	public static ModContentPack? ModContentPack { get; }
		= ActiveMods.TryGetModContentPack(PackageIDs.RIMMSQOL);
	
	public static Type? HarmonyPatchNamespaceType { get; }
	
	public static MethodInfo? RegisterAllMethod { get; }

	static RIMMSqol()
	{
		if (ModContentPack is null)
			return;

		if (ModContentPack.TryGetAssembly(ASSEMBLY_NAME) is not { } assembly)
		{
			LogFailureToFind("assembly");
			return;
		}

		if ((HarmonyPatchNamespaceType = assembly.GetType("RIMMSqol.HarmonyPatchNamespace")) is null)
		{
			LogFailureToFind("harmony patch namespace type");
			return;
		}

		if ((RegisterAllMethod = AccessTools.DeclaredMethod(HarmonyPatchNamespaceType, "registerAll")) is null)
			LogFailureToFind("registerAll method");
	}

	private static void LogFailureToFind(string text)
		=> Log.Error($"Performance Fish failed to find RIMMSqol's {
			text}. This is likely to cause issues.");

	public static bool TryPatch()
		=> RegisterAllMethod is { } registerAllMethod
			&& PerformanceFishMod.Harmony.Patch(registerAllMethod, postfix: new(methodof(RegisterAllPostfix))) != null;

	public static void RegisterAllPostfix()
	{
		if (AccessTools.Field(HarmonyPatchNamespaceType, "namespaces").GetValue(null) is not IDictionary namespaces)
		{
			LogFailureToFind("harmony patch namespaces field");
			return;
		}

		foreach (var blockedPatchNamespace in BlockedPatchNamespaces)
		{
			if (!namespaces.Contains(blockedPatchNamespace))
			{
				LogFailureToFind(blockedPatchNamespace);
				continue;
			}

			var properties = namespaces[blockedPatchNamespace];
			if (AccessTools.Field(properties.GetType(), "active") is not { } activeField)
			{
				LogFailureToFind("harmony patch active field");
				return;
			}
			
			activeField.SetValue(properties, false);
		}
	}
}