// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Listers;

public sealed class Haulables : ClassWithFishPrepatches
{
	public sealed class CheckPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes the ListerHaulables by making frequent checks that happen every tick to only check for adding "
			+ "new haulables, skipping things that are already inside the lister. This generally results in them "
			+ "getting removed when actively checked by pawns or otherwise getting destroyed instead, which happens "
			+ "far less frequently. Hauling behaviour visible to the player remains identical";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerHaulables), nameof(ListerHaulables.Check));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(CheckAddPatch.ReplacementBody);
	}
	
	public sealed class CheckAddPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches haulables in a fast custom hash set to not have to look them up in a large list";

		public override List<Type> LinkedPatches { get; } = [typeof(TryRemovePatch)];

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerHaulables), nameof(ListerHaulables.CheckAdd));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(ListerHaulables __instance, Thing t)
		{
			if (!t.IsItem())
				return;
			
			var haulables = __instance.haulables;
			
			ref var cache = ref __instance.Cache();
			cache.CheckUpdate(haulables);

			var haulablesInCache = cache.ContainedThings;
			var thingKey = t.GetKey();
			if (haulablesInCache.Contains(thingKey))
				return;
			
			if (!__instance.ShouldBeHaulable(t))
				return;
			
			haulables.Add(t);
			haulablesInCache.Add(thingKey);
		}
	}
	
	public sealed class TryRemovePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches haulables in a fast custom hash set to not have to look them up in a large list";
		
		public override List<Type> LinkedPatches { get; } = [typeof(CheckAddPatch)];

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerHaulables), nameof(ListerHaulables.TryRemove));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(ListerHaulables __instance, Thing t)
		{
			if (t.def.category != ThingCategory.Item)
				return;

			var haulables = __instance.haulables;
			
			ref var cache = ref __instance.Cache();
			cache.CheckUpdate(haulables);

			if (cache.ContainedThings.Remove(t.GetKey()))
				haulables.Remove(t);
		}
	}

	public sealed class TickPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Essentially fixes a bug that was causing the method to tick cells multiple times for storages with less "
			+ "than 4 cells. Not ticking multiple times leads to a performance improvement";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ListerHaulables), nameof(ListerHaulables.ListerHaulablesTick));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var instructions = ilProcessor.instructions;
			
			var fourInstructionIndex = instructions.FirstIndexOf(static code
				=> code.OpCode == OpCodes.Ldc_I4_4 || (code.OpCode == OpCodes.Ldc_I4 && code.Operand is 4));

			ilProcessor.InsertRange(fourInstructionIndex + 1,
				(OpCodes.Ldloc,
					ilProcessor.Body.Variables.First(static local => local.VariableType.Is(typeof(SlotGroup)))),
				(OpCodes.Call, methodof(GetCellCountToTick)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetCellCountToTick(int original, SlotGroup slotGroup)
			=> Math.Min(original, slotGroup.CellsList.Count);
	}
	
	public record struct Cache
	{
		public readonly FishSet<int> ContainedThings = [];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CheckUpdate(List<Thing> haulables)
		{
			if (IsDirty(haulables))
				Update(haulables);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsDirty(List<Thing> haulables) => haulables.Count != ContainedThings.Count;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public void Update(List<Thing> haulables)
		{
			ContainedThings.Clear();
			
			for (var i = haulables.Count; i-- > 0;)
				ContainedThings.Add(haulables[i].GetKey());
		}

		public Cache()
		{
		}
	}
}

public static class ListerHaulablesExtensions
{
	public static void TryAddDirectly(this ListerHaulables lister, Thing t)
	{
		if (!t.IsItem())
			return;
			
		var haulables = lister.haulables;
			
		ref var cache = ref lister.Cache();
		cache.CheckUpdate(haulables);

		if (cache.ContainedThings.Add(t.GetKey()))
			haulables.Add(t);
	}
	
	public static void TryRemoveDirectly(this ListerHaulables lister, Thing t)
	{
		if (!t.IsItem())
			return;
			
		var haulables = lister.haulables;
			
		ref var cache = ref lister.Cache();
		cache.CheckUpdate(haulables);

		if (cache.ContainedThings.Remove(t.GetKey()))
			haulables.Remove(t);
	}
}