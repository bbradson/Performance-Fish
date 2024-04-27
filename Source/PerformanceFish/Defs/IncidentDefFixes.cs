// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Defs;

public sealed class IncidentDefFixes : ClassWithFishPrepatches
{
	public sealed class WorkerPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "RimWorld has no checks to verify that IncidentDefs include valid classes with their actual "
			+ "implementation. This means whenever mods either don't provide one or it simply fails to load for any "
			+ "reason, entirely uninformative errors get spammed at the point the storyteller tries starting an "
			+ "incident at. This patch here adds a check, logging the erroneous def with mod source when possible and "
			+ "disabling the IncidentWorker from running to prevent the error spam.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(IncidentDef), nameof(IncidentDef.Worker));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(Replacement);

		public static IncidentWorker Replacement(IncidentDef instance)
		{
			if (instance.workerInt == null)
			{
				if (instance.workerClass is null)
					TryFixAndLogError(instance);

				instance.workerInt = (IncidentWorker)Activator.CreateInstance(instance.workerClass!);
				instance.workerInt.def = instance;
			}

			return instance.workerInt;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void TryFixAndLogError(IncidentDef instance)
		{
			instance.workerClass = typeof(IncidentWorker_AlwaysSkip);
			Log.Error($"Detected missing workerClass in IncidentDef '{
				instance.defName ?? "null"}' from mod '{
					instance.modContentPack?.Name ?? "null"}' for IncidentCategoryDef '{
						instance.category?.defName ?? "null"}'. Disabling now to avoid further issues.");
		}
	}

	public sealed class IncidentWorker_AlwaysSkip : IncidentWorker
	{
#if !V1_4
		public override float ChanceFactorNow(IIncidentTarget target) => 0f;
#endif

		public override float BaseChanceThisGame => 0f;

		protected override bool CanFireNowSub(IncidentParms parms) => true;

		protected override bool TryExecuteWorker(IncidentParms parms) => true;
	}
}