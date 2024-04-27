// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class ThingPatches : ClassWithFishPrepatches
{
	public sealed class ExposeDataFixStuffPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "This is an attempt at better logging and fixing errors coming from missing stuff, like wood or cloth, "
			+ "after mod removal. Only runs when loading a save and only when detecting things in this erroneous "
			+ "state. No performance impact, unless the errors were causing issues that ended up fixed.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.ExposeData));

		public static void Postfix(Thing __instance)
		{
			if (Scribe.mode == LoadSaveMode.LoadingVars
				&& __instance is { def: { MadeFromStuff: true } def, Stuff: null })
			{
				TryFixStuff(__instance, def);
			}
		}

		private static void TryFixStuff(Thing __instance, ThingDef def)
		{
			string label;
			try
			{
				label = __instance.ToString();
			}
			catch (Exception e)
			{
				label = __instance.thingIDNumber.ToStringCached();
				Log.Error($"Exception trying to fetch label for thing with def '{def}':\n{e}");
			}

			Log.Error($"Thing '{label}' of def '{def}' is madeFromStuff but stuff=null. Assigning default.");

			__instance.SetStuffDirect(GenStuff.DefaultStuffFor(def));
		}
	}

	public sealed class SpawnedPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization for this property reducing its amount of instructions by 2/3 without changing results";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Thing), nameof(Thing.Spawned));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(MiscExtensions.IsSpawned);
	}

	public sealed class MapHeldPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization for this property reducing its amount of instructions by 2/3 without changing results";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Thing), nameof(Thing.MapHeld));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(MiscExtensions.TryGetMapHeld);
	}

	public sealed class InitializeCompsFix : FishPrepatch
	{
		public override string? Description { get; }
			= "Improves the error message in ThingWithComps.InitializeComps to include thing, props and mod";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingWithComps), nameof(ThingWithComps.InitializeComps));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var codes = ilProcessor.instructions;

			var errorMessageIndex = codes.FirstIndexOf(static code
				=> code.Operand is "Could not instantiate or initialize a ThingComp: ");

			codes[errorMessageIndex].OpCode = OpCodes.Ldarg_0;
			codes[errorMessageIndex].Operand = null;

			ilProcessor.InsertRange(errorMessageIndex + 1,
				(OpCodes.Ldloca,
					ilProcessor.Body.Variables.First(local => local.VariableType == module.TypeSystem.Int32)),
				(OpCodes.Call, methodof(MakeErrorMessage)));
		}

		public static string MakeErrorMessage(ThingWithComps thing, ref int index)
		{
			var compProps = thing.def.comps;
			if (compProps.TryGetItem(index, out var props))
				compProps.RemoveAt(index--);

			return $"Could not instantiate or initialize a ThingComp with props '{
				(props != null! ? props.GetType().FullName : "null")}' on thing '{thing}' from mod '{
					thing.def.modContentPack?.Name ?? "null"}': ";
		}
	}
}