// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using RimWorld.Planet;

namespace PerformanceFish.Planet;

public sealed class MothballOptimization : ClassWithFishPrepatches
{
	public const string BASIC_DESCRIPTION
		= "Mothballing means to \"take something out of operation but maintain it so that it can be used in the "
		+ "future.\" RimWorld has this system for world pawns along with a number of conditions which can block it "
		+ "from taking effect.";
	
	public sealed class WorldPawnsDefPreventingMothball : FishPrepatch
	{
		public override string? Description { get; }
			= BASIC_DESCRIPTION + " This patch essentially serves as a whitelist to allow mothballing a lot more "
			+ "pawns. Large performance impact, but not entirely side-effect free. Certain slowly progressing and not "
			+ "lethal conditions like addictions or pregnancy do not progress on mothballed pawns.";

		public static bool MothballEverything => FishSettings.MothballEverything;
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldPawns), nameof(WorldPawns.DefPreventingMothball));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var instructions = ilProcessor.instructions;
			for (var i = 0; i < instructions.Count; i++)
			{
				if (instructions[i].Operand is not MethodReference { Name: "IsPermanent" })
					continue;

				ilProcessor.InsertAt(i++, OpCodes.Dup);
				i++; // bool IsPermanent(hediff)
				
				ilProcessor.InsertRange(i,
					OpCodes.Ldarg_0,
					OpCodes.Ldarg_1,
					(OpCodes.Call, methodof(AllowMothball)));
				
				break;
			}
		}

		public static bool AllowMothball(Hediff hediff, bool isPermanent, WorldPawns worldPawns, Pawn pawn)
			=> MothballEverything
				|| isPermanent
				|| !hediff.Visible
				|| (/*IsLowPriority(worldPawns, pawn)
					&&*/ AllowedDefs.Contains(hediff.def)
					&& hediff is
					{
						CurStage.lifeThreatening: false, SummaryHealthPercentImpact: < 0.01f, Bleeding: false
					}
					&& (!HasBlockingComp(hediff) || !HasLifeThreateningStage(hediff.def)));

		private static bool HasBlockingComp(Hediff hediff)
			=> hediff.def.minSeverity <= 0f
				&& hediff is HediffWithComps hediffWithComps
				&& hediffWithComps.comps.ExistsAndNotNull(static comp
					=> comp is HediffComp_TendDuration
					&& comp.props is not HediffCompProperties_TendDuration { showTendQuality: false });

		private static bool IsLowPriority(WorldPawns worldPawns, Pawn pawn)
			=> WorldPawns.lowPrioritySituations.Contains(worldPawns.GetSituation(pawn));
	}

	public static HashSet<HediffDef> AllowedDefs => _allowedDefs ??= PrepareAllowedDefs();

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static HashSet<HediffDef> PrepareAllowedDefs()
		=> DefDatabase<HediffDef>.AllDefsListForReading.Where(static def
			=> def.lethalSeverity < 0f
				// && !def.displayWound
				&& !CausesBleeding(def)
				&& !LosesSeverityPerDay(def)
				&& (!HasLifeThreateningStage(def) || !HasBlockingCompProperties(def)))
			.ToHashSet();

	private static bool HasBlockingCompProperties(HediffDef def)
		=> def.minSeverity <= 0f
			&& def.comps.ExistsAndNotNull(static props
			=> (props.compClass?.IsAssignableTo(typeof(HediffComp_TendDuration)) ?? false)
			&& props is not HediffCompProperties_TendDuration { showTendQuality: false });
	
	private static bool LosesSeverityPerDay(HediffDef def)
		=> def.minSeverity <= 0f
		&& def.comps.ExistsAndNotNull(static props
		=> props is HediffCompProperties_SeverityPerDay severityProps
			&& (severityProps.severityPerDay <= -0.35f || severityProps.severityPerDayRange.max <= -0.35f));

	private static bool HasLifeThreateningStage(HediffDef def)
		=> def.stages.ExistsAndNotNull(static stage => stage.lifeThreatening);

	private static bool CausesBleeding(HediffDef def) => def.injuryProps is { bleedRate: > 0.001f };

	private static HashSet<HediffDef>? _allowedDefs;
}