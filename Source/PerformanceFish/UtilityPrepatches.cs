// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using PerformanceFish.Prepatching;
using Verse.Steam;

namespace PerformanceFish;

public sealed class UtilityPrepatches : ClassWithFishPrepatches
{
	public sealed class CreateModClassesPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook for very early loading of patches, right after the game finishes loading up game assemblies into "
			+ "its process and before it launches any processes on them. Certain patches require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; } = methodof(LoadedModManager.CreateModClasses);

		public static void Prefix()
		{
			try
			{
				OnAssembliesLoaded.Start();
			}
			catch (Exception e)
			{
				Log.Error($"Exception caught within OnAssembliesLoaded:\n{e}\n{new StackTrace(true)}");
			}
		}
	}

	public sealed class BetterPrinting : FishPrepatch
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
			=>  (size.y * 0.0045f) + (size.x * 0.0005f);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float TopRightAdjustment(in Vector2 size)
			=>  (size.y * 0.0045f) - (size.x * 0.0005f);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BottomLeftAdjustment(in Vector2 size)
			=>  (size.y * -0.0045f) + (size.x * 0.0005f);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float BottomRightAdjustment(in Vector2 size)
			=>  (size.y * -0.0045f) - (size.x * 0.0005f);
	}

	public sealed class NoSteamLogWarning : FishPrepatch
	{
		public override string? Description { get; }
			= "Disables the \"[Steamworks.NET] SteamAPI.Init() failed\" warning. It's duplicated with prepatcher "
			+ "anyway";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(SteamManager), nameof(SteamManager.InitIfNeeded));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var codes = ilProcessor.instructions;
			
			var warningIndex = codes.FirstIndexOf(static code
				=> code.Operand is string text && text.Contains("SteamAPI.Init() failed"));

			if (warningIndex < 0)
				return;
			
			codes[warningIndex].OpCode = OpCodes.Nop;
			codes[warningIndex].Operand = null;
			
			codes[++warningIndex].OpCode = OpCodes.Nop;
			codes[warningIndex].Operand = null;
		}
	}
	
	public sealed class NoSteamFailWindow : FishPrepatch
	{
		public override string? Description { get; }
			= "Disables the window that opens when running the steam version of rimworld without steam. Off by default";

		public override bool DefaultState => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(UIRoot_Entry), nameof(UIRoot_Entry.Init));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var codes = ilProcessor.instructions;
			
			var steamInitializedProperty
				= AccessTools.PropertyGetter(typeof(SteamManager), nameof(SteamManager.Initialized));

			var instruction = codes.FirstOrDefault(code
				=> code.Operand is MethodReference method && method.Is(steamInitializedProperty));

			if (instruction is null)
				return;
			
			instruction.OpCode = OpCodes.Ldc_I4_1;
			instruction.Operand = null;
		}
	}
}