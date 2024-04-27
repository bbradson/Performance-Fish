// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// #define DEBUG

global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.Linq.Expressions;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using FisheryLib;
global using FisheryLib.Cecil;
global using FisheryLib.Collections;
global using FisheryLib.Utility.Diagnostics;
global using HarmonyLib;
global using PerformanceFish.Utility;
global using PerformanceFish.Patching;
global using RimWorld;
global using UnityEngine;
global using Verse;
global using static FisheryLib.Aliases;
global using CodeInstructions = System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction>;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using JetBrains.Annotations;
using PerformanceFish.ModCompatibility;
using PerformanceFish.Prepatching;

[assembly: AllowPartiallyTrustedCallers]
[assembly: SecurityRules(SecurityRuleSet.Level2, SkipVerificationInFullTrust = true)]
[assembly: Debuggable(false, false)]

[module: SkipLocalsInit]

namespace PerformanceFish;

[PublicAPI]
public sealed class PerformanceFishMod : Mod
{
	public const string
		NAME = "Performance Fish",
		PACKAGE_ID = PackageIDs.PERFORMANCE_FISH;
	
	[Obsolete("I am not incrementing this version number")]
	public const decimal VERSION = 0.6M;

	public static Version LoadedVersion => typeof(PerformanceFishMod).Assembly.GetLoadedVersion();

	public static FishSettings? Settings { get; internal set; }
	public static PerformanceFishMod? Mod { get; private set; }

	public static Harmony Harmony => _harmony.Value;

	public static IHasFishPatch[]? AllPatchClasses { get; internal set; }
	public static ClassWithFishPrepatches[]? AllPrepatchClasses { get; internal set; }

	private static FishPatch[]? _allFishPatches;

	private static FishPrepatchBase[]? _allFishPrepatches;
	private static readonly Lazy<Harmony> _harmony = new(static () => new(PACKAGE_ID), true);

	public static IEnumerable<FishPatch> AllHarmonyPatches
		=> _allFishPatches ??= AllPatchClasses?.SelectMany(static patchClass => patchClass.Patches.All.Values).ToArray()
			?? Array.Empty<FishPatch>();

	public static IEnumerable<FishPrepatchBase> AllPrepatches
		=> _allFishPrepatches ??= AllPrepatchClasses?.SelectMany(static patchClass => patchClass.Patches.All.Values)
				.ToArray()
			?? Array.Empty<FishPrepatchBase>();

	public PerformanceFishMod(ModContentPack content) : base(content)
	{
		try
		{
			ActiveMods.CheckRequirements();
		}
		catch (Exception e)
		{
			Log.Error(e.ToString());
			ApplyModInactiveLetterPatch();
			return;
		}
		
		InitializeMod();
	}

	private void InitializeMod()
	{
		DebugLog.Message($"Initializing {NAME} v{typeof(PerformanceFishMod).Assembly.GetLoadedVersion()}");

		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (Mod != null)
		{
			Verse.Log.TryOpenLogWindow();
			ThrowHelper.ThrowInvalidOperationException($"Initialized a second instance of the {
				NAME} mod. This should never happen. Cancelling now to avoid further issues.");
		}

		Mod = this;

		if (!ActiveMods.Prepatcher)
		{
			Verse.Log.TryOpenLogWindow();
			Log.Error("Prepatcher mod is missing. Performance Fish will not be working correctly in this state.");
		}

		if (ActiveMods.TryGetModContentPack(PackageIDs.PREPATCHER) is not { } prepatcherContent
			|| prepatcherContent.assemblies?.loadedAssemblies is not [_,..])
		{
			Verse.Log.TryOpenLogWindow();
			Log.Error("Prepatcher assemblies are missing. This usually indicates an incomplete download or a "
				+ "copy of github sources instead of the actual mod.");
		}

		if (!OnAssembliesLoaded.Loaded)
		{
			Verse.Log.TryOpenLogWindow();
			Log.Error("Performance Fish failed to initialize entirely. There are either critical dependencies "
				+ "missing or mods with hard incompatibilities present.");
			ApplyModInactiveLetterPatch();
		}

		modSettings = Settings;
		if (modSettings is null)
		{
			Verse.Log.TryOpenLogWindow();
			Log.Error("Performance Fish failed to initialize its mod settings.");
		}
		else
		{
			modSettings.Mod = this;
		}

#if DEBUG
		LogPatchCount();
#endif
	}

	private static void ApplyModInactiveLetterPatch()
		=> Harmony.Patch(AccessTools.DeclaredMethod(typeof(Game), nameof(Game.FinalizeInit)),
			postfix: new(methodof(ThrowModInactiveLetter), Priority.Last));

	public static void ThrowModInactiveLetter()
		=> Find.LetterStack.AddLetterSilently(LetterMaker.MakeLetter("Performance Fish inactive",
			"Missing dependencies or hard incompatibilities caused Performance Fish to entirely fail to "
			+ "initialize. It will not be doing anything in this state.", LetterDefOf.Bossgroup));

	public static void LogPatchCount()
	{
		var allFishPatches = AllPatchClasses!.SelectMany(static patchClass => patchClass.Patches.All.Values).ToList();
		var allFishPrepatches = AllPrepatchClasses!.SelectMany(static patchClass => patchClass.Patches.All.Values)
			.ToList();

		var prefixCount = SumFor(allFishPatches, static fishPatch => fishPatch.PrefixMethodInfo);
		var postfixCount = SumFor(allFishPatches, static fishPatch => fishPatch.PostfixMethodInfo);
		var transpilerCount = SumFor(allFishPatches, static fishPatch => fishPatch.TranspilerMethodInfo);
		var prepatcherPrefixCount = SumFor(allFishPrepatches, static fishPrepatch
			=> (fishPrepatch as FishPrepatch)?.PrefixMethodInfo);
		var prepatcherPostfixCount = SumFor(
			allFishPrepatches, static fishPrepatch => (fishPrepatch as FishPrepatch)?.PostfixMethodInfo);
		var prepatcherTranspilerCount = SumFor(allFishPrepatches, static fishPrepatch
			=> fishPrepatch is FishPrepatch { PrefixMethodInfo: null, PostfixMethodInfo: null }
				? methodof(SumFor<object>) : null);
		var prepatcherClassPatchCount = SumFor(allFishPrepatches, static fishPrepatch
			=> fishPrepatch is FishClassPrepatch ? methodof(SumFor<object>) : null);
		
		Log.Message($"Performance Fish applied {prefixCount} harmony prefixes, {postfixCount} harmony postfixes, {
			transpilerCount} harmony transpilers, {prepatcherPrefixCount} prepatcher prefixes, {
				prepatcherPostfixCount} prepatcher postfixes, {prepatcherTranspilerCount} prepatcher transpilers and {
					prepatcherClassPatchCount} prepatcher class patch{
						(prepatcherClassPatchCount == 1 ? "" : "es")}. That's a total of {prefixCount + postfixCount
							+ transpilerCount + prepatcherPrefixCount + prepatcherPostfixCount
							+ prepatcherTranspilerCount + prepatcherClassPatchCount} patches. Amazing!\n\n"
			+ $"The following methods were patched:\n{string.Join("\n",
				allFishPatches
					.SelectMany(static fishPatch => fishPatch.TargetMethodInfos)
					.Concat(allFishPrepatches
						.Select(static fishPrepatch => (fishPrepatch as FishPrepatch)?.TargetMethodBase))
					.Where(Is.NotNull)
					.Select(static method => method.FullDescription()))}\n"
			+ $"===================================================================================");
	}

	private static int SumFor<T>(List<T> allFishPatches, Func<T, MethodInfo?> methodInfoGetter)
		=> allFishPatches.Sum(fishPatch => methodInfoGetter(fishPatch) != null ? 1 : 0);

	internal static T[] InitializeAllPatchClasses<T>() where T : notnull
	{
		var types = Assembly.GetExecutingAssembly().GetTypes();
		var list = new List<T>(types.Length);

		if (typeof(T).IsAssignableTo(typeof(FishPatch)))
			Parallel.ForEach(types, InitializePatchClass);
		else
			Array.ForEach(types, InitializePatchClass);

		return list.ToArray();
		
		void InitializePatchClass(Type type)
		{
			if (!type.IsAssignableTo(typeof(T))
				|| type.IsInterface
				|| type.IsAbstract)
			{
				return;
			}

			try
			{
				var patchClass = SingletonFactory<T>.Get(type);

				lock (((ICollection)list).SyncRoot)
					list.Add(patchClass);
			}
			catch (Exception ex)
			{
				Log.Error($"{NAME} encountered an exception while trying to initialize {type.Name}:\n{ex}");
			}
		}
	}

	public static T? Get<T>()
	{
		for (var i = AllPatchClasses!.Length - 1; i >= 0; i--)
		{
			if (AllPatchClasses[i] is T patchOfT)
				return patchOfT;
		}

		return default;
	}

	public override string SettingsCategory() => Mod!.Content.Name;
	public override void DoSettingsWindowContents(Rect inRect) => FishSettings.DoSettingsWindowContents(inRect);
}