// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using static System.Reflection.Emit.OpCodes;
using static PerformanceFish.TranspilerHelpers;

namespace PerformanceFish.JobSystem;

public class JobGiver_WorkPatches : ClassWithFishPatches
{
	public class TryIssueJobPackage_InnerDelegate_Patch : FishPatch
	{
		public override string Description => "Work scanning optimization. Splits the IsForbidden and HasJobOnThing checks into separate loops, then threads the IsForbidden loop, sorts it, caches the result and skips any following duplicate checks";
		public override MethodBase TargetMethodInfo => AccessTools.FindIncludingInnerTypes(typeof(JobGiver_Work),
			t => AccessTools.Field(t, "scanner") != null ? AccessTools.FirstMethod(t, m => m.Name.Contains("TryIssueJobPackage") && m.ReturnType == typeof(bool)) : null);
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
		{
			var shouldRemove = true;
			foreach (var code in CodeInstructions)
			{
				if (shouldRemove)
				{
					if (code.Branches(out _))
						shouldRemove = false;
				}
				else
				{
					if (code.opcode == Ret)
						shouldRemove = true;
					yield return code;
				}
			}
			//Replaces
			//(Thing t) => !t.IsForbidden(pawn) && scanner.HasJobOnThing(pawn, t);
			//with
			//(Thing t) => scanner.HasJobOnThing(pawn, t);
		}
	}

	public class TryIssueJobPackage_Patch : FishPatch
	{
		public override string Description => "Work scanning optimization. Splits the IsForbidden and HasJobOnThing checks into separate loops, then threads the IsForbidden loop, sorts it, caches the result and skips any following duplicate checks";
		public override MethodBase TargetMethodInfo => AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage));
		public static void Prefix(Pawn pawn)
		{
			PrefilteredPotentialWorkThingsGlobal.Clear();
			FishCache.ClearAll();
			CurrentPawn = pawn;
		}
		public static void Postfix()
		{
			PrefilteredPotentialWorkThingsGlobal.Clear();
			FishCache.ClearAll();
			CurrentPawn = null;
		}
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions, MethodBase original)
		{
			var pawn = GetLdarg(original, "pawn");

			var scanner_pawn_PotentialWorkThingsGlobal = GetCallvirt(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
			var potentialWorkThingsGlobal_pawn_TryGetPrefilteredPotentialWorkThingsAnyType = GetCall(() => TryGetPrefilteredPotentialWorkThingsAnyType(null, null));

			var scanner_PotentialWorkThingRequest = GetGetPropertyCallvirt(typeof(WorkGiver_Scanner), nameof(WorkGiver_Scanner.PotentialWorkThingRequest));
			var scanner_pawn_GetCachedThingRequest = GetCall(() => GetCachedThingRequest(null, null));

			foreach (var code in CodeInstructions)
			{
				if (code == scanner_pawn_PotentialWorkThingsGlobal)
				{
					yield return code;
					yield return pawn;
					yield return potentialWorkThingsGlobal_pawn_TryGetPrefilteredPotentialWorkThingsAnyType;
				}
				else if (code == scanner_PotentialWorkThingRequest)
				{
					yield return pawn;
					yield return scanner_pawn_GetCachedThingRequest;
				}
				else
				{
					yield return code;
				}
			}
		}

		public static ThingRequest GetCachedThingRequest(WorkGiver_Scanner scanner, Pawn pawn)
		{
			var originalRequest = scanner.PotentialWorkThingRequest;
			if (originalRequest is { singleDef: null, group: ThingRequestGroup.Undefined })
				return originalRequest;

			var listerThings = pawn.Map.listerThings.ThingsMatching(originalRequest);
			var collection = listerThings.Count > 100 ? (scanner.PotentialWorkThingsGlobal(pawn) as ICollection<Thing>) : null;

			var processedThings = TryGetPrefilteredPotentialWorkThingsAnyType(collection is null || listerThings.Count < collection.Count * 4 ? listerThings : collection, pawn);
			var fishCache = originalRequest.singleDef != null ? FishCache.ForDef(originalRequest.singleDef) : FishCache.ForGroup(originalRequest.group);
			fishCache.FilteredThings = processedThings as List<Thing> ?? new(processedThings);
			return new ThingRequest() { group = originalRequest.group, singleDef = fishCache };
		}
		public static IEnumerable<Thing> TryGetPrefilteredPotentialWorkThingsAnyType(IEnumerable<Thing> originalWorkThings, Pawn pawn)
			=> originalWorkThings switch
			{
				List<Thing> => Generic<Thing>.TryGetPrefilteredPotentialWorkThingsForList(originalWorkThings, pawn),
				List<Pawn> => Generic<Pawn>.TryGetPrefilteredPotentialWorkThingsForList(originalWorkThings, pawn),
				_ => GetPrefilteredPotentialWorkThingsUncached(originalWorkThings, pawn)
			};
		public static IEnumerable<Thing> GetPrefilteredPotentialWorkThingsUncached(IEnumerable<Thing> originalWorkThings, Pawn pawn)
			=> (originalWorkThings is not ICollection<Thing> collection || collection.Count < 2048)
			? CheckForbidden(originalWorkThings, pawn)
			: Generic<Thing>.Threaded_CheckForbiddenAndAddToAnyCollection(collection, pawn);

		public static IEnumerable<Thing> CheckForbidden(IEnumerable<Thing> things, Pawn pawn)
		{
			if (things is null)
				yield break;

			foreach (var thing in things)
			{
				if (!thing.IsForbidden(pawn))
					yield return thing;
			}
		}

		public static class Generic<T> where T : Thing
		{
			public static IEnumerable<Thing> TryGetPrefilteredPotentialWorkThingsForList(IEnumerable<Thing> originalWorkThings, Pawn pawn)
			{
				if (!originalWorkThings.Any())
					return originalWorkThings;

				if (!PrefilteredPotentialWorkThingsGlobal.TryGetValue(originalWorkThings, out var cache))
					cache = RecalculatePrefilteredPotentialWorkThings(originalWorkThings, pawn);

				return cache;
			}
			public static T[] RecalculatePrefilteredPotentialWorkThings(IEnumerable<Thing> originalWorkThings, Pawn pawn)
			{
				var inputList = originalWorkThings as List<T>;
				if (inputList.Count < 512)
					Sequential_CheckForbiddenAndAddToList(inputList, pawn);
				else
					Threaded_CheckForbiddenAndAddToList(inputList, pawn);

				var cache = _tempThingList.ToArray();
				if (cache.Length < 1024)
				{
					var pawnPosition = pawn.Position;
					Array.Sort(cache, (a, b) => (pawnPosition - a.Position).LengthHorizontalSquared.CompareTo((pawnPosition - b.Position).LengthHorizontalSquared));
				}

				PrefilteredPotentialWorkThingsGlobal[originalWorkThings] = cache;
				_tempThingList.Clear();

				return cache;
			}
			private static void Sequential_CheckForbiddenAndAddToList(List<T> inputList, Pawn pawn)
			{
				_tempThingList.Clear();
				var tempThingList = _tempThingList;
				var count = inputList.Count;
				for (var i = 0; i < count; i++)
				{
					if (!inputList[i].IsForbidden(pawn))
						tempThingList.Add(inputList[i]);
				}
			}
			private static void Threaded_CheckForbiddenAndAddToList(List<T> inputList, Pawn pawn)
			{
				_tempThingList.Clear();
#if DEBUG
				DebugLog.Message("Running threading code");
				var stopwatch = new System.Diagnostics.Stopwatch();
				stopwatch.Start();
#endif
				var partitioner = Partitioner.Create(0, inputList.Count);

				Parallel.ForEach<Tuple<int, int>, (List<T> localList, List<T> sourceList, Pawn pawn)>(partitioner,
					() => new(new(), inputList, pawn),
					(range, state, vars) =>
					{
						for (int i = range.Item1; i < range.Item2; i++)
						{
							var item = vars.sourceList[i];
							if (!item.IsForbidden(vars.pawn))
								vars.localList.Add(item);
						}
						return vars;
					},
				vars =>
				{
					lock (_lock)
					{
						_tempThingList.AddRange(vars.localList);
					}
				});
#if DEBUG
				stopwatch.Stop();
				DebugLog.Message($"Finished threading code without errors in {stopwatch.Elapsed}");
#endif
			}
			public static List<T> Threaded_CheckForbiddenAndAddToAnyCollection(ICollection<T> inputCollection, Pawn pawn)
			{
				_tempThingList.Clear();
#if DEBUG
				DebugLog.Message("Running threading code");
				var stopwatch = new System.Diagnostics.Stopwatch();
				stopwatch.Start();
#endif

				Parallel.ForEach<T, (List<T> localList, Pawn pawn)>(inputCollection,
					() => new(new(), pawn),
					(thing, state, vars) =>
					{
						if (!thing.IsForbidden(vars.pawn))
							vars.localList.Add(thing);
						return vars;
					},
				vars =>
				{
					lock (_lock)
					{
						_tempThingList.AddRange(vars.localList);
					}
				});
#if DEBUG
				stopwatch.Stop();
				DebugLog.Message($"Finished threading code without errors in {stopwatch.Elapsed}");
#endif
				return _tempThingList;
			}

			private static object _lock { get; } = new();
			private static List<T> _tempThingList { get; } = new();
		}

		public static Dictionary<IEnumerable<Thing>, IEnumerable<Thing>> PrefilteredPotentialWorkThingsGlobal { get; } = new();
	}

	public class ThingRequest_IsUndefined_Patch : FishPatch
	{
		public override string Description => "Part of the work scanning optimization";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.IsUndefined));
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(ThingRequest __instance, ref bool __result)
		{
			if (__instance.singleDef is not FishCache)
				return;

			__result = NewResult(__instance);
		}
		public static bool NewResult(ThingRequest __instance)
			=> __instance.group == ThingRequestGroup.Undefined && !((FishCache)__instance.singleDef).IsSingleDef;
	}

	public class ThingRequest_CanBeFoundInRegion_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Part of the work scanning optimization";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.CanBeFoundInRegion));
		public static bool Prefix(ThingRequest __instance, ref bool __result)
		{
			if (__instance.singleDef is not FishCache)
				return true;

			__result = NewResult(__instance);
			return false;
		}
		public static bool NewResult(ThingRequest __instance)
			=> ((FishCache)__instance.singleDef).IsSingleDef
			|| (__instance.group != ThingRequestGroup.Undefined && (__instance.group == ThingRequestGroup.Nothing || __instance.group.StoreInRegion()));
	}

	public class ListerThings_ThingsMatching_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Part of the work scanning optimization";
		public override Expression<Action> TargetMethod => () => new ListerThings(default).ThingsMatching(default);
		public static bool Prefix(ListerThings __instance, ThingRequest req, ref List<Thing> __result)
		{
			if (req.singleDef is not FishCache)
				return true;

			__result = GetCache(__instance, req);
			return false;
		}
		public static List<Thing> GetCache(ListerThings __instance, ThingRequest req)
		{
			var cachedThings = ((FishCache)req.singleDef).FilteredThings ?? ListerThings.EmptyList;
			if (__instance.use == ListerThingsUse.Region && cachedThings.Count > 20)
			{
				var regionalThings = GetForRegion(__instance, req);
				return regionalThings.Count < cachedThings.Count ? regionalThings : cachedThings;
			}
			else
			{
				return cachedThings;
			}
		}
		public static List<Thing> GetForRegion(ListerThings __instance, ThingRequest req)
			=> new(TryIssueJobPackage_Patch.Generic<Thing>.TryGetPrefilteredPotentialWorkThingsForList(
				(((FishCache)req.singleDef) is var cache && cache.IsSingleDef
				? __instance.listsByDef.TryGetValue(cache.Def, out var value) ? value : null
				: __instance.listsByGroup.TryGetItem((int)req.group, out var item) ? item : null) ?? ListerThings.EmptyList, CurrentPawn));
	}

	public class FishCache : ThingDef
	{
		public bool IsSingleDef => Def != null;
		public ThingDef Def { get; private set; }
		public List<Thing> FilteredThings { get; set; }
		public static FishCache ForDef(ThingDef def)
		{
			if (_dictOfDefs.TryGetValue(def, out var value))
				return value;
			
			_dictOfDefs[def] = value = new(def.defName) { Def = def };
			return value;
		}
		public static FishCache ForGroup(ThingRequestGroup group)
		{
			if (_dictOfGroups.TryGetValue(group, out var value))
				return value;

			_dictOfGroups[group] = value = new(group.ToString());
			return value;
		}
		public static void ClearAll()
		{
			_dictOfGroups.Clear();
			_dictOfDefs.Clear();
		}
		private FishCache(string name) => defName = name;
		private static Dictionary<ThingRequestGroup, FishCache> _dictOfGroups { get; } = new();
		private static Dictionary<ThingDef, FishCache> _dictOfDefs { get; } = new();
	}

	private static Pawn CurrentPawn { get; set; }
}*/