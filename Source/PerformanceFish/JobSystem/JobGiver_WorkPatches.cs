// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*using System.Linq;
using static System.Reflection.Emit.OpCodes;

namespace PerformanceFish.JobSystem;

public sealed class JobGiver_WorkPatches : ClassWithFishPatches
{
	public sealed class TryIssueJobPackage_InnerDelegate_Patch : FishPatch
	{
		public override string Description { get; }
			= "Work scanning optimization. Splits the IsForbidden and HasJobOnThing checks into separate loops, then "
			+ "sorts the IsForbidden loop, caches the result and skips any following duplicate checks";

		public override MethodBase? TargetMethodInfo { get; }
			= AccessTools.FindIncludingInnerTypes(typeof(JobGiver_Work), static t
				=> AccessTools.Field(t, "scanner") != null
					? AccessTools.FirstMethod(t,
						static m => m.Name.Contains("TryIssueJobPackage") && m.ReturnType == typeof(bool))
					: null);

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

	public sealed class TryIssueJobPackage_Patch : FishPatch
	{
		public override string Description { get; }
			= "Work scanning optimization. Splits the IsForbidden and HasJobOnThing checks into separate loops, "
			+ "then sorts it, caches the result and skips any following duplicate checks";

		public override MethodBase TargetMethodInfo
			=> AccessTools.Method(typeof(JobGiver_Work), nameof(JobGiver_Work.TryIssueJobPackage));

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
			var pawn = FishTranspiler.Argument(original, "pawn");

			var scanner_pawn_PotentialWorkThingsGlobal = FishTranspiler.Call(typeof(WorkGiver_Scanner),
				nameof(WorkGiver_Scanner.PotentialWorkThingsGlobal));
			var potentialWorkThingsGlobal_pawn_TryGetPrefilteredPotentialWorkThingsAnyType
				= FishTranspiler.Call(static () => TryGetPrefilteredPotentialWorkThingsAnyType(null!, null!));

			var scanner_PotentialWorkThingRequest = FishTranspiler.PropertyGetter(typeof(WorkGiver_Scanner),
				nameof(WorkGiver_Scanner.PotentialWorkThingRequest));
			var scanner_pawn_GetCachedThingRequest
				= FishTranspiler.Call(static () => GetCachedThingRequest(null!, null!));

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
			var collection = listerThings.Count > 100
				? scanner.PotentialWorkThingsGlobal(pawn) as ICollection<Thing>
				: null;

			var processedThings = TryGetPrefilteredPotentialWorkThingsAnyType(
				collection is null || listerThings.Count < collection.Count * 4 ? listerThings : collection, pawn);
			var fishCache = originalRequest.singleDef != null
				? FishCache.ForDef(originalRequest.singleDef)
				: FishCache.ForGroup(originalRequest.group);
			fishCache.FilteredThings = processedThings as List<Thing> ?? [..processedThings];
			return new() { group = originalRequest.group, singleDef = fishCache };
		}

		public static IEnumerable<Thing> TryGetPrefilteredPotentialWorkThingsAnyType(
			IEnumerable<Thing> originalWorkThings, Pawn pawn)
			=> originalWorkThings switch
			{
				List<Thing> => Generic<Thing>.TryGetPrefilteredPotentialWorkThingsForList(originalWorkThings, pawn),
				List<Pawn> => Generic<Pawn>.TryGetPrefilteredPotentialWorkThingsForList(originalWorkThings, pawn),
				_ => GetPrefilteredPotentialWorkThingsUncached(originalWorkThings, pawn)
			};

		public static IEnumerable<Thing>
			GetPrefilteredPotentialWorkThingsUncached(IEnumerable<Thing>? originalWorkThings, Pawn pawn)
		{
			if (originalWorkThings is null)
				yield break;

			foreach (var thing in originalWorkThings)
			{
				if (!thing.IsForbidden(pawn))
					yield return thing;
			}
		}

		public static class Generic<T> where T : Thing
		{
			public static IEnumerable<Thing> TryGetPrefilteredPotentialWorkThingsForList(
				IEnumerable<Thing> originalWorkThings, Pawn pawn)
			{
				if (!originalWorkThings.Any())
					return originalWorkThings;

				if (!PrefilteredPotentialWorkThingsGlobal.TryGetValue(originalWorkThings, out var cache))
					cache = RecalculatePrefilteredPotentialWorkThings(originalWorkThings, pawn);

				return cache;
			}

			public static T[] RecalculatePrefilteredPotentialWorkThings(IEnumerable<Thing> originalWorkThings,
				Pawn pawn)
			{
				var inputList = (List<T>)originalWorkThings;
				Sequential_CheckForbiddenAndAddToList(inputList, pawn);

				var cache = _tempThingList.ToArray();
				if (cache.Length < 1024)
				{
					var pawnPosition = pawn.Position;
					Array.Sort(cache,
						(a, b) => (pawnPosition - a.Position).LengthHorizontalSquared.CompareTo(
							(pawnPosition - b.Position).LengthHorizontalSquared));
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
			private static readonly List<T> _tempThingList = [];
		}

		public static Dictionary<IEnumerable<Thing>, IEnumerable<Thing>> PrefilteredPotentialWorkThingsGlobal { get; }
			= [];
	}

	public sealed class ThingRequest_IsUndefined_Patch : FishPatch
	{
		public override string Description => "Part of the work scanning optimization";

		public override MethodBase TargetMethodInfo
			=> AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.IsUndefined));

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

	public sealed class ThingRequest_CanBeFoundInRegion_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Part of the work scanning optimization";

		public override MethodBase TargetMethodInfo
			=> AccessTools.PropertyGetter(typeof(ThingRequest), nameof(ThingRequest.CanBeFoundInRegion));

		public static bool Prefix(ThingRequest __instance, ref bool __result)
		{
			if (__instance.singleDef is not FishCache)
				return true;

			__result = NewResult(__instance);
			return false;
		}

		public static bool NewResult(ThingRequest __instance)
			=> ((FishCache)__instance.singleDef).IsSingleDef
				|| (__instance.group != ThingRequestGroup.Undefined
					&& (__instance.group == ThingRequestGroup.Nothing || __instance.group.StoreInRegion()));
	}

	public sealed class ListerThings_ThingsMatching_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Part of the work scanning optimization";

		public override MethodBase? TargetMethodInfo { get; }
			= AccessTools.DeclaredMethod(typeof(ListerThings), nameof(ListerThings.ThingsMatching));

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
			=>
			[
				..TryIssueJobPackage_Patch.Generic<Thing>.TryGetPrefilteredPotentialWorkThingsForList(
					((FishCache)req.singleDef is { IsSingleDef: true } cache
						? __instance.listsByDef.TryGetValue(cache.Def, out var value) ? value : null
						: __instance.listsByGroup.TryGetItem((int)req.group, out var item)
							? item
							: null)
					?? ListerThings.EmptyList, CurrentPawn)
			];
	}

	public class FishCache : ThingDef
	{
		public bool IsSingleDef => Def != null;
		public ThingDef? Def { get; private init; }
		public List<Thing>? FilteredThings { get; set; }

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
		private static readonly Dictionary<ThingRequestGroup, FishCache> _dictOfGroups = new();
		private static readonly Dictionary<ThingDef, FishCache> _dictOfDefs = new();
	}

	private static Pawn? CurrentPawn { get; set; }
}*/