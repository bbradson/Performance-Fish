// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq.Expressions;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using FisheryLib;
global using FisheryLib.Utility.Diagnostics;
global using HarmonyLib;
global using PerformanceFish.Utility;
global using RimWorld;
global using UnityEngine;
global using Verse;
global using static FisheryLib.Aliases;
global using CodeInstructions = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;
using System.Linq;
using System.Security;
using PerformanceFish.System;

[assembly: AllowPartiallyTrustedCallers]
[assembly: SecurityTransparent]
[assembly: SecurityRules(SecurityRuleSet.Level2, SkipVerificationInFullTrust = true)]

namespace PerformanceFish;

public class PerformanceFishMod : Mod
{
	public const string NAME = "Performance Fish";
	public const decimal VERSION = 0.3M;

#pragma warning disable IDE0079 // Visual Studio freaking out a bit over here
#pragma warning disable CS8618
	public static FishSettings Settings { get; private set; }
	public static PerformanceFishMod Mod { get; private set; }
	public static Harmony Harmony { get; private set; }
	public static IHasFishPatch[] AllPatchClasses { get; private set; }
#pragma warning restore CS8618, IDE0079

	public PerformanceFishMod(ModContentPack content) : base(content)
	{
		DebugLog.Message($"Initializing {NAME} v{VERSION}");

		if (Mod != null)
			ThrowHelper.ThrowInvalidOperationException($"Initialized a second instance of the {NAME} mod. This should never happen. Cancelling now to avoid further issues.");

		Mod = this;

		TryInitialize(EqualityComparerOptimization.Initialize);

		Harmony = new(content.PackageIdPlayerFacing);

		AllPatchClasses = Assembly.GetExecutingAssembly().GetTypes()
			.Where(t => typeof(IHasFishPatch).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
			.Select(type
				=> {
					try
					{
						return SingletonFactory<IHasFishPatch>.Get(type);
					}
					catch (Exception ex)
					{
						Log.Error($"Performance Fish encountered an exception while trying to initialize {type.Name}:\n{ex}");
						return null!;
					}
				})
			.Where(p => p != null)
			.ToArray();

		ReflectionCaching.Initialize();

		try
		{
			Settings = GetSettings<FishSettings>();
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an expection while trying to load its settings\n{ex}");
			WriteSettings();
		}

		TryInitialize(AnalyzerFixes.Patch);

		foreach (var patch in AllPatchClasses)
		{
			try
			{
				if (!patch.RequiresLoadedGameForPatching)
					patch.Patches.PatchAll();
			}
			catch (Exception ex)
			{
				Log.Error($"Performance Fish encountered an exception while trying to initialize {patch.GetType().Name}:\n{ex}");
			}
		}
	}

	private static void TryInitialize(Action action)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {action.Method.FullDescription()}:\n{ex}");
		}
	}

	public static T? Get<T>()
	{
		foreach (var patch in AllPatchClasses)
		{
			if (patch is T patchOfT)
				return patchOfT;
		}
		return default;
	}

	public override string SettingsCategory() => "Performance Fish";
	public override void DoSettingsWindowContents(Rect inRect) => FishSettings.DoSettingsWindowContents(inRect);
}