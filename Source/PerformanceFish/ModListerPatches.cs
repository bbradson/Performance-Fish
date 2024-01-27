// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class ModListerPatches : ClassWithFishPrepatches
{
	public sealed class AnyFromListActivePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Fixes a LoadFolders bug causing it to normally not recognize steam versions of mods if another local "
			+ "copy exists.";

		public override MethodBase TargetMethodBase { get; } = methodof(ModLister.AnyFromListActive);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> GetActiveModWithIdentifierFix(ilProcessor, module);
	}
	
	public sealed class ModIncompatibilityIsSatisfiedPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Fixes a mod manager bug causing it to normally not recognize steam versions of mods for incompatibility "
			+ "checks if another local copy exists.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(ModIncompatibility), nameof(ModIncompatibility.IsSatisfied));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> GetActiveModWithIdentifierFix(ilProcessor, module);
	}
	
	private static void GetActiveModWithIdentifierFix(ILProcessor ilProcessor, ModuleDefinition module)
	{
		var instructions = ilProcessor.instructions;
		var success = false;

		for (var i = 0; i + 1 < instructions.Count; i++)
		{
			if (instructions[i].OpCode == OpCodes.Ldc_I4_0
				&& instructions[i + 1].Operand is MethodReference
				{
					Name: nameof(ModLister.GetActiveModWithIdentifier)
				})
			{
				instructions[i].OpCode = OpCodes.Ldc_I4_1;
				success = true;
			}
		}
			
		if (!success)
		{
			Log.Error($"Performance Fish failed to apply its patch on '{
				ilProcessor.GetMethod().FullName}'. This should be harmless as it's meant to be just a bugfix.");
		}
	}
}