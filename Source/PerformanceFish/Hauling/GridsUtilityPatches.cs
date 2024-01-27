// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Events;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Hauling;

public sealed class GridsUtilityPatches : ClassWithFishPrepatches
{
	public sealed class GetItemCountPatch : FishPrepatch
	{
		public override int Priority => HarmonyLib.Priority.First;

		public override string? Description { get; }
			= "Caches item counts in a grid, to not have to repeat counting on every request.";

		public override MethodBase TargetMethodBase { get; } = methodof(GridsUtility.GetItemCount);

		protected internal override void BeforeHarmonyPatching() => ThingEvents.Initialized += TryRegister;

		public static void TryRegister(Thing thing)
		{
			if (!thing.IsItem())
				return;

			var thingEvents = thing.Events();
			thingEvents.RegisteredAtThingGrid += Increment;
			thingEvents.DeregisteredAtThingGrid += Decrement;
		}

		public static void Increment(Thing thing, Map map, in IntVec3 cell)
			=> map.ItemCountGrid()[cell]++;

		public static void Decrement(Thing thing, Map map, in IntVec3 cell)
		{
			ref var itemCount = ref map.ItemCountGrid()[cell];
			itemCount--;
			
			if (itemCount < 0)
			{
				itemCount = 0;
				ErrorForInvalidCount(thing);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void ErrorForInvalidCount(Thing thing)
			=> Log.Error($"Tried decrementing item count at cell {thing.Position} for thing '{
				thing}', but it was already 0. This should never happen.");

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ReplacementBody(IntVec3 c, Map map) => map.ItemCountGrid()[c];
	}
}