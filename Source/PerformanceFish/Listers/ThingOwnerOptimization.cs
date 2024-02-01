// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Listers;

public sealed class ThingOwnerOptimization : ClassWithFishPrepatches
{
	public sealed class ExposeDataPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Required to keep the ThingOwner cache synced";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.ExposeData));
		
		public static void Postfix<T>(ThingOwner<T> __instance) where T : Thing
		{
			if (Scribe.mode != LoadSaveMode.PostLoadInit)
				return;
			
			var indexMap = __instance.IndexMap();
			var innerList = __instance.innerList;
			innerList.RemoveAll(static thing => thing is null);
			
			indexMap.Clear();
			for (var i = innerList.Count; i-- > 0;)
				indexMap[innerList[i].GetKey()] = i;
		}
	}

	public sealed class TryAddPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Required to keep the ThingOwner cache synced";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.TryAdd),
				[typeof(Thing), typeof(bool)]);

		public static void Postfix<T>(ThingOwner<T> __instance, Thing? item, bool canMergeWithExistingStacks,
			bool __result) where T : Thing
		{
			if (!__result
				|| item is null
				|| (canMergeWithExistingStacks && (item.Destroyed || item.stackCount == 0)))
			{
				return;
			}

			__instance.IndexMap()[item.GetKey()] = __instance.innerList.Count - 1;
		}
	}

	/*public sealed class IndexOfPatch : FishPrepatch // not really used enough to be worth patching
	{
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.IndexOf));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Thing>);

		public static int ReplacementBody<T>(ThingOwner<T> instance, Thing? item) where T : Thing
			=> item is not T itemOfT || item.holdingOwner != instance
				? -1
				: instance.innerList is var innerList
				&& instance.IndexMap().TryGetValue(GetKey(itemOfT), out var knownIndex)
				&& knownIndex < innerList.Count
				&& innerList[knownIndex] == item
					? knownIndex
					: innerList.IndexOf(itemOfT);
	}*/

	public sealed class RemovePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes ThingOwner to keep a cache of thing indices, to greatly speed up removal and in turn improve "
			+ "performance when despawning happens. Late game colonies and maps in biomes with large amounts of plants "
			+ "benefit most from this";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingOwner<Thing>), nameof(ThingOwner<Thing>.Remove));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var codes = ilProcessor.instructions;
			
			var lastIndexOfIndex = codes.FirstIndexOf(static code
				=> code.Operand is MethodReference { Name: "LastIndexOf" });
			
			for (var i = lastIndexOfIndex; i-- > 0;)
			{
				if (codes[i].OpCode != OpCodes.Ldfld || !codes[i - 1].LoadsArgument(0))
					continue;

				codes.RemoveAt(i);
				lastIndexOfIndex--;
				break;
			}

			var removeAtIndex = codes.FirstIndexOf(static code
				=> code.Operand is MethodReference { Name: "RemoveAt" });

			for (var i = removeAtIndex + 1; i-- > 0;)
			{
				if (i == lastIndexOfIndex)
					break;
				
				codes.RemoveAt(i);
			}

			codes[lastIndexOfIndex].OpCode = OpCodes.Call;
			codes[lastIndexOfIndex].Operand
				= module.ImportMethodWithGenericArguments(methodof(Remove<Thing>).GetGenericMethodDefinition(),
					ilProcessor.Body.Method.DeclaringType.GenericParameters);

			var variables = ilProcessor.Body.Variables;
			variables.RemoveAt(variables.FirstIndexOf(variable => variable.VariableType == module.TypeSystem.Int32));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Remove<T>(ThingOwner<T> instance, Thing item) where T : Thing
		{
			var indexMap = instance.IndexMap();
			var innerList = instance.innerList;
			var itemKey = item.GetKey();
			
			var index = indexMap.TryGetValue(itemKey, out var knownIndex)
				&& knownIndex < innerList.Count
				&& innerList[knownIndex] == item
					? knownIndex
					: innerList.LastIndexOf((T)item);
			
			if (index >= 0)
				innerList.RemoveAtFastUnorderedUnsafe(index);
			
			indexMap.Remove(itemKey);
			
			if (index < innerList.Count && index >= 0)
				indexMap[innerList[index].GetKey()] = index;
		}
	}
}