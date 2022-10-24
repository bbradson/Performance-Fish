// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*using System.Threading.Tasks;
using Verse.AI;

namespace PerformanceFish.JobSystem;

public class GenClosestPatches : ClassWithFishPatches
{*/
#if disabledForNow
	public class ClosestThing_Global_Patch : FishPatch
	{
		public override string Description => "Threading for job scanning. Disabled and unstable.";
		public override bool DefaultState => false;
		public static bool ThreadingEnabled { get; set; }
		public override Expression<Action> TargetMethod => () => GenClosest.ClosestThing_Global(default, null, default, null, null);
		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(ClosestThing_Global_Replacement);
		private struct Variables
		{
			public IntVec3 center;
			public float maxDistanceSquared;
			public Func<Thing, float>? priorityGetter;
			public float closestDistSquared;
			public Predicate<Thing>? validator;
		}
		public static Thing? ClosestThing_Global_Replacement(IntVec3 center, IEnumerable searchSet, float maxDistance = 99999f, Predicate<Thing>? validator = null, Func<Thing, float>? priorityGetter = null)
		{
			if (searchSet == null)
				return null;
			var closestDistSquared = 2.14748365E+09f;
			Thing? chosen = null;
			var bestPrio = float.MinValue;
			//float maxDistanceSquared = maxDistance * maxDistance;
			Variables variables = new() { center = center, maxDistanceSquared = maxDistance * maxDistance, validator = validator, priorityGetter = priorityGetter, closestDistSquared = closestDistSquared };

			var predicate = GenClosest_Predicate.GetValidator(validator?.Target)?.Target ?? validator?.Target;
			if (ThreadingEnabled && validator != null && JobGiver_Work_Predicate.GetScanner(predicate) is WorkGiver_DoBill /*or WorkGiver_ConstructDeliverResources or WorkGiver_ConstructFinishFrames*/)
			{
				if (searchSet is IList<Thing> thingList)
				{
					Log.Message($"Running threading code for {validator.Target.GetType()}");
					Exception? exception = null;
					try
					{
						Parallel.For<(IList<Thing> list, Variables vars)>(0, thingList.Count,
						   () => (thingList, variables),
							(i, loop, listAndVars) => (listAndVars.list, ProcessThreaded(listAndVars.list[i], listAndVars.vars)),
							 _ => { });
					}
					catch (Exception ex)
					{
						exception = ex;
						Log.Error($"Caught error while threading:\n{ex}");
					}
					if (exception is null)
						Log.Message("Finished threading code without errors");

					return chosen;
				}
			}

			switch (searchSet)
			{
				case IList<Thing> thingList:
				{
					for (var i = 0; i < thingList.Count; i++)
						Process(thingList[i], variables);
					break;
				}
				case IList<Pawn> pawnList:
				{
					for (var j = 0; j < pawnList.Count; j++)
						Process(pawnList[j], variables);
					break;
				}
				case IList<Building> buildingList:
				{
					for (var k = 0; k < buildingList.Count; k++)
						Process(buildingList[k], variables);
					break;
				}
				case IList<IAttackTarget> attackTargetList:
				{
					for (var l = 0; l < attackTargetList.Count; l++)
						Process((Thing)attackTargetList[l], variables);
					break;
				}
				default:
				{
					foreach (Thing item in searchSet)
						Process(item, variables);
					break;
				}
			}

			return chosen;
			void Process<T>(T t, Variables variables) where T : Thing
			{
				if (t.Spawned)
				{
					float num = (variables.center - t.Position).LengthHorizontalSquared;
					if (!(num > variables.maxDistanceSquared)
						&& (variables.priorityGetter != null || num < closestDistSquared)
						&& (variables.validator == null || variables.validator(t)))
					{
						var num2 = 0f;
						if (variables.priorityGetter != null)
						{
							num2 = variables.priorityGetter(t);
							if (num2 < bestPrio || (num2 == bestPrio && num >= closestDistSquared))
								return;
						}
						chosen = t;
						closestDistSquared = variables.closestDistSquared = num;
						bestPrio = num2;
					}
				}
			}
			Variables ProcessThreaded<T>(T t, Variables variables) where T : Thing
			{
				if (t.Spawned)
				{
					float num = (variables.center - t.Position).LengthHorizontalSquared;
					if (!(num > variables.maxDistanceSquared)
						&& (variables.priorityGetter != null || (num < variables.closestDistSquared && num < (variables.closestDistSquared = closestDistSquared)))
						&& (variables.validator == null || variables.validator(t)))
					{
						lock (Lock)
						{
							var num2 = 0f;
							if (variables.priorityGetter != null)
							{
								num2 = variables.priorityGetter(t);
								if (num2 < bestPrio || (num2 == bestPrio && num >= closestDistSquared))
									return variables;
							}
							else
							{
								if (num >= closestDistSquared)
									return variables;
							}
							chosen = t;
							closestDistSquared = variables.closestDistSquared = num;
							bestPrio = num2;
						}
					}
				}
				return variables;
			}
		}
		private static object Lock { get; } = new();
		private static Type JobGiverWorkPredicateType { get; } = AccessTools.FirstInner(typeof(JobGiver_Work),
			t => AccessTools.Field(t, "scanner") != null && AccessTools.FirstMethod(t, m => m.Name.Contains("TryIssueJobPackage")) != null);
		private static JobGiver_Work_PredicateAccess JobGiver_Work_Predicate { get; }
			= (JobGiver_Work_PredicateAccess)Activator.CreateInstance(typeof(JobGiver_Work_PredicateAccessImplementation<>).MakeGenericType(JobGiverWorkPredicateType));
		private static Type GenClosestPredicateType { get; } = AccessTools.FirstInner(typeof(GenClosest),
			t => AccessTools.Field(t, "validator") != null && AccessTools.FirstMethod(t, m => m.Name.Contains("ClosestThingReachable")) != null);
		private static GenClosest_PredicateAccess GenClosest_Predicate { get; }
			= (GenClosest_PredicateAccess)Activator.CreateInstance(typeof(GenClosest_PredicateAccessImplementation<>).MakeGenericType(GenClosestPredicateType));
		private abstract class JobGiver_Work_PredicateAccess
		{
			public abstract WorkGiver_Scanner? GetScanner(object? obj);
		}
		private class JobGiver_Work_PredicateAccessImplementation<T> : JobGiver_Work_PredicateAccess
		{
			public override WorkGiver_Scanner? GetScanner(object? obj) => obj is T t ? Scanner(t) : null;
			public static AccessTools.FieldRef<T, WorkGiver_Scanner> Scanner { get; } = AccessTools.FieldRefAccess<T, WorkGiver_Scanner>("scanner");
		}
		private abstract class GenClosest_PredicateAccess
		{
			public abstract Predicate<Thing>? GetValidator(object? obj);
		}
		private class GenClosest_PredicateAccessImplementation<T> : GenClosest_PredicateAccess
		{
			public override Predicate<Thing>? GetValidator(object? obj) => obj is T t ? Validator(t) : null;
			public static AccessTools.FieldRef<T, Predicate<Thing>> Validator { get; } = AccessTools.FieldRefAccess<T, Predicate<Thing>>("validator");
		}
	}
#endif

#if threadingEnabled
	public class CanReach_Patch : FishPatch
	{
		protected CanReach_Patch() => FishSettings.ThreadingPatches.Add(this);
		public override bool DefaultState => false;
		public override string Description => "Lock for threading. Only needed with threading enabled and off by default";
		public override Expression<Action> TargetMethod => () => default(Reachability)!.CanReach(default, null, default, default);
		public static void Prefix()
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Enter(Lock);
		}

		public static Exception Finalizer(Exception __exception)
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Exit(Lock);
			return __exception;
		}
		private static object Lock { get; } = new();
	}
#endif
//}