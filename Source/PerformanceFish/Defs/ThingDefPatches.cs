// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using PerformanceFish.Prepatching;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace PerformanceFish.Defs;

public sealed class ThingDefPatches : ClassWithFishPrepatches
{
	public const string FISH_HASH_FIELD_NAME = "FishHash";
	
	public sealed class GetHashCodePatch : FishClassPrepatch
	{
		public override string? Description { get; }
			= "Def.GetHashCode is overridden to return defName.GetHashCode, which is rather slow as that's a string. "
			+ "This patch caches the hashcode in a field added through prepatcher and simply returns that instead.";

		public override Type Type { get; } = typeof(BuildableDef);

		public override void FreePatch(TypeDefinition typeDefinition)
		{
			var module = typeDefinition.Module;

			var fishHashField = new FieldDefinition(FISH_HASH_FIELD_NAME, FieldAttributes.Private,
				module.TypeSystem.Int32);
			
			typeDefinition.Fields.Insert(0, fishHashField);

			var constructorBody = typeDefinition.GetConstructors().First().Body;
			
			constructorBody.GetILProcessor().InsertRange(constructorBody.Instructions.Count - 2,
				OpCodes.Ldarg_0,
				(OpCodes.Ldc_I4, -42),
				(OpCodes.Stfld, fishHashField));
			
			constructorBody.OptimizeMacros();
			
			var getHashCodeBody = typeDefinition.GetMethod(nameof(BuildableDef.GetHashCode))!.Body;

			var variables = getHashCodeBody.Variables;
			variables.Clear();
			
			var hashVariable = new VariableDefinition(module.TypeSystem.Int32);
			variables.Add(hashVariable);

			var instructions = getHashCodeBody.Instructions;
			instructions.Clear();

			var ilProcessor = getHashCodeBody.GetILProcessor();
			ilProcessor.InsertRange(0,
				OpCodes.Ldarg_0,
				(OpCodes.Ldfld, fishHashField),
				(OpCodes.Stloc, hashVariable),
				(OpCodes.Ldloc, hashVariable),
				(OpCodes.Ldc_I4, -42),
				// 5 bne_un_s ^2
				OpCodes.Ldarg_0,
				(OpCodes.Call, methodof(CreateLongHash)),
				OpCodes.Ret,
				(OpCodes.Ldloc, hashVariable),
				OpCodes.Ret);
			
			ilProcessor.InsertAt(5, OpCodes.Bne_Un_S, instructions[^2]);

			getHashCodeBody.MaxStackSize = 2;
			getHashCodeBody.Method.ImplAttributes |= MethodImplAttributes.AggressiveInlining;
			getHashCodeBody.OptimizeMacros();
		}

		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
		// public static int ReplacementBody(BuildableDef instance)
		// {
		// 	var hash = instance.FishHash;
		// 	return hash != -42 ? hash : CreateLongHash(instance);
		// }

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int CreateLongHash(BuildableDef instance)
		{
			var defName = instance.defName;
			if (defName is null)
				return 0;
			
			var defHash = defName.GetHashCode();
			if (defName is "" or Def.DefaultDefName)
				return defHash;

			ref var cacheHash = ref Lazy.FishHashField(instance);
			cacheHash = defHash != -42 ? defHash : defHash + 1;
			return cacheHash;
		}

		private static class Lazy
		{
			internal static readonly AccessTools.FieldRef<BuildableDef, int> FishHashField
				= AccessTools.FieldRefAccess<BuildableDef, int>(FISH_HASH_FIELD_NAME);
		
			static Lazy()
			{
				// prevent beforefieldinit
			}
		}
	}
}