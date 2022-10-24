// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*namespace PerformanceFish.Rendering;
public class GenDrawPatches : ClassWithFishPatches
{
	public class DrawMeshNowOrLater_Patch : FishPatch
	{
		public override Delegate TargetMethodGroup => (Action<Mesh, Vector3, Quaternion, Material, bool>)GenDraw.DrawMeshNowOrLater;

		public static void Replacement(Mesh mesh, Vector3 loc, Quaternion quat, Material mat, bool drawNow)
		{
			if (drawNow)
			{
				if (!mat.SetPass(0))
					Log.Error("SetPass(0) call failed on material " + mat.name + " with shader " + mat.shader.name);
				Graphics.DrawMeshNow(mesh, loc, quat);
			}
			else
			{
				Graphics.DrawMesh(mesh, loc, quat, mat, 0);
			}
		}
	}

	public class DrawMeshNowOrLaterWithMatrix_Patch : FishPatch
	{
		public override Delegate TargetMethodGroup => (Action<Mesh, Matrix4x4, Material, bool>)GenDraw.DrawMeshNowOrLater;

		public static void Replacement(Mesh mesh, Matrix4x4 matrix, Material mat, bool drawNow)
		{
			if (drawNow)
			{
				mat.SetPass(0);
				Graphics.DrawMeshNow(mesh, matrix);
			}
			else
			{
				Graphics.DrawMesh(mesh, matrix, mat, 0);
			}
		}
	}
}*/