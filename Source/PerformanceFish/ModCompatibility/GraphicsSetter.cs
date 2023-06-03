// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.ModCompatibility;

public static class GraphicsSetter
{
	public static MethodBase? TargetMethod { get; }
		= AccessTools.Method("GraphicSetter.Patches.TextureLoadingPatch+LoadPNGPatch:Prefix")
		?? AccessTools.Method("GraphicSetter.GraphicsPatches+LoadPNGPatch:Prefix");

	public static ModMetaData? Mod { get; } = ActiveMods.TryGetModMetaData(PackageIDs.GRAPHICS_SETTINGS);

	public static MethodBase? TryPatch()
		=> TargetMethod != null
			? PerformanceFishMod.Harmony.Patch(TargetMethod, transpiler: new(methodof(Transpiler)))
			: null;

	public static CodeInstructions Transpiler(CodeInstructions codes)
	{
		yield return FishTranspiler.Constant(1);
		yield return FishTranspiler.Return;
	}
}