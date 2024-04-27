// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class GeneTrackerOptimization : ClassWithFishPrepatches
{
	public sealed class GeneTrackerTickPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Every pawn has a gene tracker, which is responsible for ticking each of their genes. Normally it ticks "
			+ "all of them equally, including those don't change or affect anything through ticking, like skin colors "
			+ "or basic stat modifiers. This patch improves the gene tracker to determine genes that need ticking in "
			+ "advance, cache the list of them, and only tick those, skipping all the others.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(Pawn_GeneTracker), nameof(Pawn_GeneTracker.GeneTrackerTick));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(GeneTrackerTick);

		public static void GeneTrackerTick(Pawn_GeneTracker instance)
		{
			if (!ModLister.BiotechInstalled)
				return;

			var genesToTick = instance.GenesToTick();
			if (Dirty(instance, genesToTick))
				Update(instance, genesToTick);
			
			for (var i = genesToTick.Count - 1; i >= 0; i--)
			{
				if (genesToTick[i].Active)
					genesToTick[i].Tick();
			}

			if (instance.pawn.IsSpawned()
				&& instance.Xenotype != XenotypeDefOf.Baseliner
				&& instance.pawn.IsHashIntervalTick(300))
			{
				LessonAutoActivator.TeachOpportunity(ConceptDefOf.GenesAndXenotypes, OpportunityType.Important);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Dirty(Pawn_GeneTracker instance, List<Gene> genesToTick)
			=> GetListVersion(instance) != genesToTick._version;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void Update(Pawn_GeneTracker instance, List<Gene> genesToTick)
		{
			genesToTick.Clear();

			AddTickableGenes(genesToTick, instance.xenogenes);
			AddTickableGenes(genesToTick, instance.endogenes);

			genesToTick._version = GetListVersion(instance);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetListVersion(Pawn_GeneTracker instance)
			=> (instance.xenogenes._version << 16) | (instance.endogenes._version & 0b1111_1111_1111_1111);

		private static void AddTickableGenes(List<Gene> genesToTick, List<Gene> genes)
		{
			var count = genes.Count;
			for (var i = 0; i < count; i++)
			{
				var gene = genes[i];
				if (SkippableTypes.Contains(gene.GetType())
					&& (gene.def.mentalBreakMtbDays <= 0f || gene.def.mentalBreakDef == null))
				{
					continue;
				}

				genesToTick.Add(gene);
			}
		}
		
		public static HashSet<Type> SkippableTypes
			= typeof(Gene).SubclassesWithNoMethodOverrideAndSelf(nameof(Gene.Tick)).ToHashSet();
	}
}