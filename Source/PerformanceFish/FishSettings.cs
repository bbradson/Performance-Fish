// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.IO;
using System.Linq;
using JetBrains.Annotations;
using PerformanceFish.ModCompatibility;
using PerformanceFish.Planet;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

[PublicAPI]
public class FishSettings : ModSettings
{
	public static bool SettingsLoaded { get; private set; }
	
	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref _threadingEnabled, "ThreadingEnabled");
		Scribe_Values.Look(ref MothballEverything, "MothballEverything");

		if (PerformanceFishMod.AllPatchClasses is { } allPatchClasses)
		{
			foreach (var patchClass in allPatchClasses)
				patchClass.Patches.Scribe();
		}

		if (PerformanceFishMod.AllPrepatchClasses is { } allPrepatchClasses)
		{
			foreach (var patchClass in allPrepatchClasses)
				patchClass.Patches.Scribe();
		}

		SettingsLoaded = true;
	}

	public static void DoSettingsWindowContents(Rect inRect)
	{
		var windowRect = Find.WindowStack.currentlyDrawnWindow.windowRect;
		if (Widgets.ButtonText(new(windowRect.width - Window.CloseButSize.x - (Window.StandardMargin * 2f),
				windowRect.height - Window.FooterRowHeight - Window.StandardMargin, Window.CloseButSize.x,
				Window.CloseButSize.y),
			"Reset all"))
		{
			ResetAll();
		}
		
		Widgets.BeginScrollView(inRect, ref _scrollPosition, _scrollRect);
		var ls = new Listing_Standard();
		ls.Begin(_scrollRect);

		var checkModEnabled = ModEnabled;
		ls.CheckboxLabeled("Toggle all patches", ref checkModEnabled,
			"This enables or disables all patches of the mod with one click. For testing purposes");
		if (checkModEnabled != ModEnabled)
		{
			ModEnabled = checkModEnabled;
			ToggleAllPatches(checkModEnabled);
		}
		
		ls.Gap();
		var curFontStyle = Text.CurFontStyle;
		var curFontSize = curFontStyle.fontSize;
		curFontStyle.fontSize = 20;
		ls.Label("Harmony Patches");
		curFontStyle.fontSize = curFontSize;
		ls.GapLine(2f);
		ls.Gap();

		foreach (var patchClass in PerformanceFishMod.AllPatchClasses!)
		{
			try
			{
				ls.Label(patchClass.GetType().Name,
					tooltip: patchClass is IHasDescription classWithDescription
						? classWithDescription.Description
						: null);

				ls.GapLine(2f);
				foreach (var patch in patchClass.Patches)
				{
					if (ShouldSkipForScrollView(inRect.height, Text.LineHeight, ls.curY, _scrollPosition.y))
					{
						ls.curY += Text.LineHeight;
						continue;
					}

					var check = patch.Enabled;
					if (patch.Description == null)
						Widgets.DrawHighlightIfMouseover(_scrollRect with { y = ls.curY, height = Text.LineHeight });
					var label = patch.Name ?? patch.GetType().Name;
					if (label.EndsWith("_Patch"))
						label = label.Remove(label.Length - 6);
					else if (label.EndsWith("Patch"))
						label = label.Remove(label.Length - 5);
					ls.CheckboxLabeled(label, ref check, patch.Description);
					if (check != patch.Enabled)
						patch.Enabled = check;
				}

				ls.Gap();
			}
			catch (Exception ex)
			{
				Log.Error($"{ex}");
			}
		}
		
		ls.Gap();
		curFontStyle.fontSize = 20;
		ls.Label("Prepatches");
		curFontStyle.fontSize = curFontSize;
		ls.GapLine();
		ls.Gap();
		
		foreach (var patchClass in PerformanceFishMod.AllPrepatchClasses!)
		{
			try
			{
				ls.Label(patchClass.GetType().Name,
					// ReSharper disable once SuspiciousTypeConversion.Global
					tooltip: patchClass is IHasDescription classWithDescription
						? classWithDescription.Description
						: null);

				ls.GapLine(2f);
				foreach (var patch in patchClass.Patches)
				{
					if (ShouldSkipForScrollView(inRect.height, Text.LineHeight, ls.curY, _scrollPosition.y))
					{
						ls.curY += Text.LineHeight;
						continue;
					}

					var check = patch.Enabled;
					if (patch.Description == null)
						Widgets.DrawHighlightIfMouseover(_scrollRect with { y = ls.curY, height = Text.LineHeight });
					var label = patch.Name ?? patch.GetType().Name;
					if (label.EndsWith("_Patch"))
						label = label.Remove(label.Length - 6);
					else if (label.EndsWith("Patch"))
						label = label.Remove(label.Length - 5);
					ls.CheckboxLabeled(label, ref check, patch.Description);
					if (check != patch.Enabled)
						patch.Enabled = check;
				}

				ls.Gap();
			}
			catch (Exception ex)
			{
				Log.Error($"{ex}");
			}
		}
		
		ls.Gap();
		ls.CheckboxLabeled("Mothball everything", ref MothballEverything, MothballEverythingDescription);
		
		ls.Gap();

		if (ls.ButtonText("Log hediffs affected by mothball optimization"))
		{
			Log.Message($"Allowed defs for mothballing: {MothballOptimization.AllowedDefs.ToStringSafeEnumerable()}");
			Log.Message($"Blocking defs for mothballing: {DefDatabase<HediffDef>.AllDefsListForReading.Where(static def
				=> !MothballOptimization.AllowedDefs.Contains(def)).ToStringSafeEnumerable()}");
			
			Verse.Log.TryOpenLogWindow();
		}

		ls.Gap();
		
		if (ls.ButtonText("Log patch count"))
		{
			PerformanceFishMod.LogPatchCount();
			Verse.Log.TryOpenLogWindow();
		}

		ls.Gap();
		
		if (ls.ButtonText("Log cache utilization"))
		{
			Cache.Utility.LogCurrentCacheUtilization();
			Verse.Log.TryOpenLogWindow();
		}

		ls.Gap();
		if (ls.ButtonText("Clear Cache"))
			Cache.Utility.Clear();

		ls.End();

		Widgets.EndScrollView();
		_scrollRect = _scrollRect with
		{
			height = ls.curY + 50f, width = inRect.width - GUI.skin.verticalScrollbar.fixedWidth - 5f
		};
	}

	public static string MothballEverythingDescription
		= MothballOptimization.BASIC_DESCRIPTION + " This setting removes all conditions, enabling every world "
		+ "pawn for mothballing. Note, health conditions do not progress on mothballed pawns. Requires the "
		+ "WorldPawnsDefPreventingMothball patch to apply";

	private static void ToggleAllPatches(bool newState) => ToggleAllPatches(_ => newState, _ => newState);

	private static void ToggleAllPatches(Func<FishPatch, bool> fishPatchFunc,
		Func<FishPrepatchBase, bool> fishPrepatchFunc)
	{
		foreach (var patchClass in PerformanceFishMod.AllPatchClasses!)
		{
			foreach (var patch in patchClass.Patches)
				patch.Enabled = fishPatchFunc(patch);
		}

		foreach (var patchClass in PerformanceFishMod.AllPrepatchClasses!)
		{
			foreach (var patch in patchClass.Patches)
				patch.Enabled = fishPrepatchFunc(patch);
		}
		
		Cache.Utility.Clear();
	}

	private static void ResetAll()
	{
		ToggleAllPatches(static patch => patch.DefaultState, static prepatch => prepatch.DefaultState);
		TryDeleteModSettingsFile();
	}

	private static void TryDeleteModSettingsFile()
	{
		try
		{
			var settingsFile = new FileInfo(LoadedModManager.GetSettingsFilename(
				PerformanceFishMod.Mod!.Content.FolderName, nameof(PerformanceFishMod)));
			if (settingsFile.Exists)
				settingsFile.Delete();
		}
		catch (Exception e)
		{
			Log.Error($"Exception thrown while trying to delete mod settings:\n{e}");
		}
	}

	public static bool ShouldSkipForScrollView(float scrollViewSize, float entrySize, float entryPosition,
		float scrollPosition)
		=> entryPosition + entrySize < scrollPosition || entryPosition > scrollPosition + scrollViewSize;

	public static bool ModEnabled
	{
		get => _modEnabled;
		set => _modEnabled = value;
	}

	private static bool _modEnabled = true;

	public static bool MothballEverything;

	public static bool ThreadingEnabled
	{
		get => false; //_threadingEnabled;
		set
		{
			if (value == _threadingEnabled)
				return;

			ThreadingPatches.ForEach(p => p.Enabled = value);
			_threadingEnabled = value;
		}
	}

	private static bool _threadingEnabled;
	public static List<FishPatch> ThreadingPatches { get; } = new();
	private static Rect _scrollRect = new(0f, 0f, 500f, 9001f);
	private static Vector2 _scrollPosition;

	private static FishSettings TryLoadModSettings(bool fallBackAttempt = false)
	{
		try
		{
			return PerformanceFishMod.Settings = LoadedModManager.ReadModSettings<FishSettings>(
				ActiveMods.TryGetModContentPack(PerformanceFishMod.PACKAGE_ID)!.FolderName, nameof(PerformanceFishMod));
		}
		catch (Exception ex)
		{
			Log.Error($"{PerformanceFishMod.NAME} encountered an exception while trying to load its settings. "
				+ $"Resetting now to avoid further issues.\n{ex}");
			TryDeleteModSettingsFile();

			return !fallBackAttempt ? TryLoadModSettings(true) : null!;
		}
	}

	public static FishSettings Instance { get; private set; } = TryLoadModSettings();
}