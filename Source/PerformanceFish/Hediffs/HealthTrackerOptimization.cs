// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if false
using PerformanceFish.Prepatching;

namespace PerformanceFish.Hediffs;

public sealed class HealthTrackerOptimization : ClassWithFishPrepatches
{
	public static void Initialize()
	{
		var allHediffDefs = DefDatabase<HediffDef>.AllDefsListForReading;
		for (var i = allHediffDefs.Count; i-- > 0;)
		{
			var hediffDef = allHediffDefs[i];

			if (hediffDef.stages is { Count: > 1})
				continue;

			SkippableHediffs.Add(hediffDef);
		}
	}
	
	public sealed class HealthTickPatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTick));
		
		
	}

	public static HashSet<HediffDef> SkippableHediffs = new();
}
#endif