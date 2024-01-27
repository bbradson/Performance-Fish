// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Events;

public sealed class ThingGridEvents : ClassWithFishPrepatches
{
	/// <summary>
	/// Invoked for every thing.Position change, including for motes and pawns
	/// </summary>
	public sealed class RegisterInCellPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things register in a cell. Does nothing by itself, but certain "
			+ "functions require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingGrid), nameof(ThingGrid.RegisterInCell));

		public static void OnRegistered(ThingGrid __instance, Thing t, in IntVec3 c)
			=> t.Events().OnRegisteredAtThingGrid(t, __instance.map, c);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.InsertRange(ilProcessor.instructions.FirstIndexOf(static code
					=> code.OpCode == OpCodes.Callvirt
					&& code.Operand is MethodReference { Name: "Add" } method
					&& method.DeclaringType.Name.Contains("List"))
				+ 1,
				OpCodes.Ldarg_0,
				OpCodes.Ldarg_1,
				(OpCodes.Ldarga_S, ilProcessor.Body.GetParameter(2)),
				(OpCodes.Call, methodof(OnRegistered)));
	}
	
	public sealed class DeregisterInCellPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things deregister from a cell. Does nothing by itself, but certain "
			+ "functions require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingGrid), nameof(ThingGrid.DeregisterInCell));

		public static void OnDeregistered(ThingGrid __instance, Thing t, in IntVec3 c)
			=> t.Events().OnDeregisteredAtThingGrid(t, __instance.map, c);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.InsertRange(ilProcessor.instructions.FirstIndexOf(static code
					=> code.OpCode == OpCodes.Callvirt
					&& code.Operand is MethodReference { Name: "Remove" } method
					&& method.DeclaringType.Name.Contains("List"))
				+ 1,
				OpCodes.Ldarg_0,
				OpCodes.Ldarg_1,
				(OpCodes.Ldarga, ilProcessor.Body.GetParameter(2)),
				(OpCodes.Call, methodof(OnDeregistered)));
	}

	public delegate void EventHandler(Thing thing, Map map, in IntVec3 cell);
}