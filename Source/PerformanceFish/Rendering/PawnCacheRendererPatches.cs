// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*namespace PerformanceFish.Rendering;
public class PawnCacheRendererPatches : ClassWithFishPatches   // The PawnCacheRenderer's Camera.Render() call has a considerable performance impact despite only being used for rendering of single pawns. These are failed attempts at lowering its impact.
{
	private static bool _optimizeCameraEnabled;

	public class OnPostRender_Patch : FishPatch
	{
		public override MethodBase TargetMethodInfo => AccessTools.DeclaredMethod(typeof(PawnCacheRenderer), nameof(PawnCacheRenderer.OnPostRender));

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(OnPostRender);

		public static void OnPostRender(PawnCacheRenderer __instance)
		{
			Find.PawnCacheCamera.cullingMask = DefaultCullingMask;
			__instance.pawn.Drawer.renderer.RenderCache(__instance.rotation, __instance.angle, __instance.positionOffset, __instance.renderHead, __instance.renderBody, __instance.portrait, __instance.renderHeadgear, __instance.renderClothes, __instance.overrideApparelColor, __instance.overrideHairColor, __instance.stylingStation);
			Find.PawnCacheCamera.cullingMask = 16; // random
		}

		private static int DefaultCullingMask => _defaultCullinkMask ??= Find.PawnCacheCamera.cullingMask;
		private static int? _defaultCullinkMask;
	}

	public class RenderPawn_Patch : FishPatch
	{
		public override MethodBase TargetMethodInfo => AccessTools.DeclaredMethod(typeof(PawnCacheRenderer), nameof(PawnCacheRenderer.RenderPawn));

		public static void Prefix(ref Vector3 cameraOffset, ref Vector3 positionOffset)
		{
			cameraOffset += new Vector3(2300f, 0f, 0f);
			positionOffset += new Vector3(2300f, 0f, 0f);
		}

		private static int DefaultCullingMask => _defaultCullinkMask ??= Find.PawnCacheCamera.cullingMask;
		private static int? _defaultCullinkMask;
	}

	public static bool OptimizeCameraEnabled
	{
		get => _optimizeCameraEnabled;
		set
		{
			_optimizeCameraEnabled = value;
			OptimizeCamera();
		}
	}

	public static void OptimizeCamera()
	{
		var cacheCamera = Find.PawnCacheCamera;
		var camera = Find.Camera;
		camera.renderingPath = _optimizeCameraEnabled ? RenderingPath.VertexLit : DefaultCameraRenderingPath;
		cacheCamera.renderingPath = _optimizeCameraEnabled ? RenderingPath.VertexLit : DefaultCacheCameraRenderingPath;
	}

	private static RenderingPath DefaultCameraRenderingPath => _defaultCameraRenderingPath ??= Find.Camera.renderingPath;
	private static RenderingPath DefaultCacheCameraRenderingPath => _defaultCacheCameraRenderingPath ??= Find.PawnCacheCamera.renderingPath;
	private static RenderingPath? _defaultCameraRenderingPath;
	private static RenderingPath? _defaultCacheCameraRenderingPath;
}*/