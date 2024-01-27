// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if fixed // with commit from Jul 3, 2023

namespace PerformanceFish.ModCompatibility;

public sealed class VanillaExpandedFixes : ClassWithFishPatches
{
	public sealed class ExpandableGraphicData : FishPatch
	{
		public override string? Description { get; }
			= "Fixes the LoadAllFiles method in Vanilla Expanded Framework's ExpandableGraphicData to not scan through "
			+ "all texture folders of all mods for every graphic and instead just use cached files the game already "
			+ "loaded. Improves load times and prevents dds related errors.";

		public override bool Enabled => ActiveMods.VanillaExpandedFramework && base.Enabled;

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method("VFECore.ExpandableGraphicData:LoadAllFiles");

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(LoadAllFiles);
		
		public static List<string> LoadAllFiles(object __instance, string? folderPath)
		{
			var paths = new List<string>();
			var mods = LoadedModManager.RunningModsListForReading;
			var count = mods.Count;

			var prefix = !folderPath.NullOrEmpty() && folderPath![^1] == '/'
				? folderPath
				: folderPath + '/';
			
			for (var i = 0; i < count; i++)
			{
				foreach (var path in mods[i].GetContentHolder<Texture2D>().contentListTrie.GetByPrefix(prefix))
					paths.Add(path);
			}

			return paths;
		}
	}
}

#endif