// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public class FishSettings : ModSettings
{
	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref _threadingEnabled, "ThreadingEnabled", false);

		foreach (var patchClass in PerformanceFishMod.AllPatchClasses)
			patchClass.Patches.Scribe();
	}

	public static void DoSettingsWindowContents(Rect inRect)
	{
		Widgets.BeginScrollView(inRect, ref _scrollPosition, _scrollRect);
		var ls = new Listing_Standard();
		ls.Begin(_scrollRect);

		var checkModEnabled = ModEnabled;
		ls.CheckboxLabeled("Toggle all patches", ref checkModEnabled, "This enables or disables all patches of the mod with one click. For testing purposes");
		if (checkModEnabled != ModEnabled)
		{
			ModEnabled = checkModEnabled;
			foreach (var patchClass in PerformanceFishMod.AllPatchClasses)
			{
				foreach (var patch in patchClass.Patches)
					patch.Enabled = checkModEnabled;

				if (checkModEnabled && (!patchClass.RequiresLoadedGameForPatching || Current.ProgramState == ProgramState.Playing))
					patchClass.Patches.PatchAll();
			}
		}

		foreach (var patchClass in PerformanceFishMod.AllPatchClasses)
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

		//ls.Gap();
		//var threadingEnabled = ThreadingEnabled;
		//ls.CheckboxLabeled("Allow threading", ref threadingEnabled, "Currently affects the ListerHaulables tick, ingredient lookups in WorkGiver_DoBill and refreshing of the shared altar thoughtworker cache. " +
		//	"Experimental and disabled by default.");
		//ThreadingEnabled = threadingEnabled;

		ls.Gap();
		if (ls.ButtonText("Clear Cache"))
			Cache.Utility.Clear();

		ls.End();

		Widgets.EndScrollView();
		_scrollRect = _scrollRect with { height = ls.curY + 50f, width = inRect.width - GUI.skin.verticalScrollbar.fixedWidth - 5f };
	}

	public static bool ShouldSkipForScrollView(float scrollViewSize, float entrySize, float entryPosition, float scrollPosition)
		=> entryPosition + entrySize < scrollPosition || entryPosition > scrollPosition + scrollViewSize;

	public static bool ModEnabled { get => _modEnabled; set => _modEnabled = value; }
	private static bool _modEnabled = true;
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
	private static bool _threadingEnabled = false;
	public static List<FishPatch> ThreadingPatches { get; } = new();
	private static Rect _scrollRect = new(0f, 0f, 500f, 9001f);
	private static Vector2 _scrollPosition;
}