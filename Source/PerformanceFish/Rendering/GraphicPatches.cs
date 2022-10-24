// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*namespace PerformanceFish.Rendering;
public class GraphicPatches : ClassWithFishPatches
{
	public class DrawMeshInt_Patch : FishPatch
	{
		public override MethodBase TargetMethodInfo => AccessTools.DeclaredMethod(typeof(Graphic), nameof(Graphic.DrawMeshInt));

		public static void Replacement(Graphic __instance, Mesh mesh, Vector3 loc, Quaternion quat, Material mat)
		{
			Graphics.DrawMesh(mesh, loc, quat, mat, 0);
		}
	}

	public class Shadow_DrawWorker_Patch : FishPatch
	{
		public override MethodBase TargetMethodInfo => AccessTools.DeclaredMethod(typeof(Graphic_Shadow), nameof(Graphic_Shadow.DrawWorker));

		public static void Replacement(Graphic_Shadow __instance, Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
		{
			if (__instance.shadowMesh is null
				|| thingDef is null
				|| __instance.shadowInfo is null
				|| (Find.CurrentMap is { } map && loc.ToIntVec3().InBounds(map) && map.roofGrid.Roofed(loc.ToIntVec3()))
				|| !DebugViewSettings.drawShadows)
			{
				return;
			}

			var position = loc + __instance.shadowInfo.offset;
			position.y = AltitudeLayer.Shadows.AltitudeFor();
			Graphics.DrawMesh(__instance.shadowMesh, position, rot.AsQuat, MatBases.SunShadowFade, 0);
		}
	}
}*/