// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using static System.Reflection.Emit.OpCodes;

namespace PerformanceFish;
public class MiscOptimizations : ClassWithFishPatches
{
	public class GenList_ListFullCopy_Patch : FishPatch
	{
		public override void TryUnpatch() { } // prevent unpatching

		public override string Description => "Minor optimization. Requires a full restart to toggle off due to a bug in harmony";
		public override Delegate TargetMethodGroup => GenList.ListFullCopy<object>; // harmony patches for all T types

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(IList source, ref IList __result)
			=> source is null || source.Count == 0
			|| (__result = FisheryLib.CollectionExtensions.TryNew(source, source.GetType().GetGenericArguments()[0])) is null;
	}

	public class WindManager_WindManagerTick_Patch : FishPatch
	{
		public override string Description => "Throttles plant sway to update at most once per frame instead of every tick. Also fixes the plant sway setting to actually disable plant sway calculations when it should. " +
			"By default it simply sets values to 0 but then still goes through everything, sacrificing tps for no reason";
		public override Expression<Action> TargetMethod => () => default(WindManager)!.WindManagerTick();

		public static CodeInstructions? Transpiler(CodeInstructions codes)
		{
			var find_CurrentMap = FishTranspiler.CallPropertyGetter(typeof(Find), nameof(Find.CurrentMap));
			var this_map = FishTranspiler.Field(typeof(WindManager), nameof(WindManager.map));

			return codes.ReplaceAt(
				(codes, i)
					=> i - 3 >= 0
					&& codes[i - 3] == find_CurrentMap
					&& codes[i - 1] == this_map
					&& codes[i].operand is Label, // goto return;

				code
					=> new[]
					{
						code,
						FishTranspiler.Call(ShouldSway),
						FishTranspiler.IfFalse_Short((Label)code.operand)
					}
				);
		}

		public static bool ShouldSway()
		{
			var currentFrame = Time.frameCount;
			if (_lastSwayFrame >= currentFrame)
				return false;

			if (Prefs.PlantWindSway != _lastPrefValue)
				_lastPrefValue = Prefs.PlantWindSway;
			else if (!Prefs.PlantWindSway)
				return false;

			_lastSwayFrame = currentFrame;
			return true;
		}

		private static int _lastSwayFrame;
		private static bool _lastPrefValue;
	}

	public class DrawBatch_Flush_Patch : FishPatch
	{
		public override string Description => "Optimization of a fleck rendering method. Low - medium performance impact";
		public override Expression<Action> TargetMethod => () => default(DrawBatch)!.Flush(default);

		public static CodeInstructions? Transpiler(CodeInstructions CodeInstructions, ILGenerator generator)
		{
			var codes = CodeInstructions.ToList();

			var instance_tmpPropertyBlocks = FishTranspiler.Field(typeof(DrawBatch), nameof(DrawBatch.tmpPropertyBlocks));
			var instance_tmpPropertyBlocks_instance_propertyBlockCache_AddRange = FishTranspiler.Call<Action<HashSet<DrawBatchPropertyBlock>, List<DrawBatchPropertyBlock>>>(GenCollection.AddRange);
			var instance_tmpPropertyBlocks_key_propertyBlock_Add = FishTranspiler.Call(new HashSet<DrawBatchPropertyBlock>().Add);

			var label_inFrontOf_instance_batches_Clear = generator.DefineLabel();
			var instance_batches_Clear = FishTranspiler.Call(new Dictionary<DrawBatch.BatchKey, List<DrawBatch.BatchData>>().Clear);

			codes[
				   codes.FindIndex(c => c == instance_batches_Clear)
				   - 2
				   ].labels.Add(label_inFrontOf_instance_batches_Clear);

			var instance_myPropertyBlocks = FishTranspiler.Field(typeof(DrawBatch), nameof(DrawBatch.myPropertyBlocks));
			var instance_myPropertyBlocks_GetEnumerator = FishTranspiler.Call(new HashSet<DrawBatchPropertyBlock>().GetEnumerator);

			var addRangeRemoved = false;
			var addRemoved = false;
			var checkFleckLeaksInserted = false;

			for (var i = 0; i < codes.Count; i++)
			{
				if (i + 4 < codes.Count
					&& codes[i + 1] == instance_tmpPropertyBlocks
					&& codes[i + 4] == instance_tmpPropertyBlocks_instance_propertyBlockCache_AddRange)
				{
					i += 4;
					addRangeRemoved = true;
				}
				else if (i + 5 < codes.Count
					&& codes[i + 1] == instance_tmpPropertyBlocks
					&& codes[i + 4] == instance_tmpPropertyBlocks_key_propertyBlock_Add
					&& codes[i + 5].opcode == Pop)
				{
					i += 5;
					addRemoved = true;
				}
				else if (i + 2 < codes.Count
					&& codes[i + 1] == instance_myPropertyBlocks
					&& codes[i + 2] == instance_myPropertyBlocks_GetEnumerator)
				{
					yield return codes[i];
					yield return FishTranspiler.Call(CheckFleckLeaks);
					yield return FishTranspiler.IfFalse(label_inFrontOf_instance_batches_Clear);
					yield return FishTranspiler.This;
					checkFleckLeaksInserted = true;
				}
				else
				{
					yield return codes[i];
				}
			}

			if (!addRangeRemoved || !addRemoved || !checkFleckLeaksInserted)
				Log.Error($"Performance Fish failed to apply its DrawBatch.Flush() patch. Mod Settings has an option to disable this");
		}

		public static bool CheckFleckLeaks(DrawBatch instance)
		{
			if (instance.myPropertyBlocks.Count != instance.propertyBlockCache.Count)
			{
				instance.tmpPropertyBlocks.AddRange(instance.propertyBlockCache);
				return true;
			}
			return false;
		}

#if whatItsGonnaLookLikeAfterTranspiling
		public static void Flush(DrawBatch __instance, bool draw = true)
		{
			if (__instance.tmpPropertyBlock == null)
				__instance.tmpPropertyBlock = new MaterialPropertyBlock();
			__instance.tmpPropertyBlocks.Clear();
//			__instance.tmpPropertyBlocks.AddRange(__instance.propertyBlockCache); // <--------------------------------------------------------- removed
			try
			{
				foreach (var batch in __instance.batches)
				{
					var key = batch.Key;
					try
					{
						foreach (var item in batch.Value)
						{
							var batchData = item;
							if (draw)
							{
								__instance.tmpPropertyBlock.Clear();
								if (key.propertyBlock != null)
									key.propertyBlock.Write(__instance.tmpPropertyBlock);
								if (key.renderInstanced)
								{
									key.material.enableInstancing = true;
									if (batchData.hasAnyColors)
										__instance.tmpPropertyBlock.SetVectorArray("_Color", batchData.colors);
									Graphics.DrawMeshInstanced(key.mesh, 0, key.material, item.matrices, item.ptr, __instance.tmpPropertyBlock, UnityEngine.Rendering.ShadowCastingMode.On, receiveShadows: true, key.layer);
								}
								else
								{
									for (var i = 0; i < batchData.ptr; i++)
									{
										var matrix = batchData.matrices[i];
										var vector = batchData.colors[i];
										if (batchData.hasAnyColors)
											__instance.tmpPropertyBlock.SetColor("_Color", vector);
										Graphics.DrawMesh(key.mesh, matrix, key.material, key.layer, null, 0, __instance.tmpPropertyBlock);
									}
								}
							}
							batchData.Clear();
							__instance.batchDataListCache.Add(batchData);
						}
					}
					finally
					{
						if (key.propertyBlock != null && __instance.myPropertyBlocks.Contains(key.propertyBlock))
						{
//							__instance.tmpPropertyBlocks.Add(key.propertyBlock); // <---------------------------------------------------------- removed
							key.propertyBlock.Clear();
							__instance.propertyBlockCache.Add(key.propertyBlock);
						}
						__instance.batchListCache.Add(batch.Value);
						batch.Value.Clear();
					}
				}
			}
			finally
			{
				if (CheckFleckLeaks(__instance)) // <------------------------------------------------------------------------------------------ added
				{
					foreach (var myPropertyBlock in __instance.myPropertyBlocks)
					{
						if (!__instance.tmpPropertyBlocks.Contains(myPropertyBlock))
							Log.Warning("Property block from FleckDrawBatch leaked!" + ((myPropertyBlock.leakDebugString == null) ? null : ("Leak debug information: \n" + myPropertyBlock.leakDebugString)));
					}
					var hashSet = __instance.myPropertyBlocks;
					__instance.myPropertyBlocks = __instance.tmpPropertyBlocks;
					__instance.tmpPropertyBlocks = hashSet;
				}

				__instance.batches.Clear();
				__instance.lastBatchKey = default;
				__instance.lastBatchList = null;
			}
		}
#endif
	}
}