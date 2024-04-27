// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Rendering;

public sealed class GraphicPatches : ClassWithFishPrepatches
{
	public sealed class MultiInitFix : FishPrepatch
	{
		public override string? Description { get; }
			= "Fixes a bug in this type of Graphic that causes it to return with null material when failing to find a "
			+ "texture, instead of the usual pink square seen in all other Graphics";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Graphic_Multi), nameof(Graphic_Multi.Init));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Graphic_Multi __instance)
		{
			if (__instance.MatSingle != null)
				return;

			AssignBadMats(__instance);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void AssignBadMats(Graphic_Multi instance)
		{
			var mats = instance.mats;
			for (var i = mats.Length; i-- > 0;)
			{
				if (mats[i] == null)
					mats[i] = BaseContent.BadMat;
			}
		}
	}
}