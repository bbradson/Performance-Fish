// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Utils;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Rendering;

public sealed class PrintImprovements : ClassWithFishPrepatches
{
	public sealed class PrintPlanePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Attempt at fixing the clipping between textures by slightly adjusting their angle to have the top left "
			+ "placed closer to the camera than the bottom right";
	
		public override MethodBase TargetMethodBase { get; } = methodof(Printer_Plane.PrintPlane);
	
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var codes = ilProcessor.instructions;
		
			var altitudeBiasParameter = ilProcessor.GetParameter("topVerticesAltitudeBias");
			var sizeParameter = ilProcessor.GetParameter("size");
		
			var altitudeBiasIndex = codes.FirstIndexOf(code => code.Operand == altitudeBiasParameter);
			
			LoadSizeAtAndCall(altitudeBiasIndex, TopLeftAdjustment);
		
			while (codes[++altitudeBiasIndex].Operand != altitudeBiasParameter)
				;
			
			LoadSizeAtAndCall(altitudeBiasIndex, TopRightAdjustment);
			
			while (codes[--altitudeBiasIndex].Operand is not 0f)
				;
			
			LoadSizeAtAndCall(altitudeBiasIndex, BottomLeftAdjustment);
			
			while (codes[++altitudeBiasIndex].Operand is not 0f)
				;
			
			LoadSizeAtAndCall(altitudeBiasIndex, BottomRightAdjustment);

			void LoadSizeAtAndCall(int instructionIndex, Delegate method)
			{
				codes[instructionIndex].OpCode = OpCodes.Ldarga;
				codes[instructionIndex].Operand = sizeParameter;
				ilProcessor.InsertAt(instructionIndex + 1, (OpCodes.Call, method.Method));
			}
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float TopLeftAdjustment(in Vector2 size)
			=>  (size.y * 0.0048f) + (size.x * 0.0002f);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float TopRightAdjustment(in Vector2 size)
			=>  (size.y * 0.0048f) - (size.x * 0.0002f);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BottomLeftAdjustment(in Vector2 size)
			=>  (size.y * -0.0048f) + (size.x * 0.0002f);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BottomRightAdjustment(in Vector2 size)
			=>  (size.y * -0.0048f) - (size.x * 0.0002f);
	}

	public sealed class PlantPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Fix for plants sometimes rendering behind hydroponics";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Plant), nameof(Plant.Print));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var body = ilProcessor.Body;
			body.SimplifyMacros();

			try
			{
				var codes = ilProcessor.instructions;
				var printPlaneMethod = methodof(Printer_Plane.PrintPlane);
				var printPlaneIndex = codes.FirstIndexOf(code
					=> code.Operand is MethodReference method && method.Is(printPlaneMethod));
				Guard.IsGreaterThanOrEqualTo(printPlaneIndex, 0);

				while (--printPlaneIndex >= 0 && codes[printPlaneIndex] is var code
					&& (code.Operand is not VariableDefinition variable || !variable.VariableType.Is(typeof(Vector3))))
					;

				if (printPlaneIndex < 0)
				{
					ThrowHelper.ThrowInvalidOperationException(
						"Failed to find center variable load instruction for PrintPlane call");
				}

				ilProcessor.InsertRange(printPlaneIndex + 1, OpCodes.Ldarg_0, (OpCodes.Call, methodof(AdjustCenter)));
			}
			finally
			{
				body.OptimizeMacros();
			}
		}

		public static Vector3 AdjustCenter(Vector3 center, Plant plant)
		{
			if (Math.Abs(center.y - LowPlantAltitude) > 0.001f)
				return center;

			var plantPosition = plant.PositionHeld;
			var map = plant.MapHeld;
			if (map.edificeGrid[plantPosition.CellToIndex(map)] is
			{
				def.altitudeLayer: AltitudeLayer.LowPlant
			} building)
			{
				center.y += ((plantPosition.z - building.OccupiedRect().minZ) * 0.01f) + 0.005f;
			}

			return center;
		}

		public static readonly float LowPlantAltitude = AltitudeLayer.LowPlant.AltitudeFor();
	}
}