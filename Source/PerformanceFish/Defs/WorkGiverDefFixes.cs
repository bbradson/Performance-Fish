// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Defs;

public sealed class WorkGiverDefFixes : ClassWithFishPrepatches
{
	public sealed class WorkerPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "RimWorld has no checks to verify that WorkGiverDefs include valid classes with their actual "
			+ "implementation. This means whenever mods either don't provide one or it simply fails to load for any "
			+ "reason, entirely uninformative errors get spammed at the point pawns try starting a job at. This patch "
			+ "here adds a check, logging the erroneous def with mod source when possible and disabling the WorkGiver "
			+ "from running to prevent the error spam.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(WorkGiverDef), nameof(WorkGiverDef.Worker));
		
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(Replacement);

		public static WorkGiver Replacement(WorkGiverDef instance)
		{
			if (instance.workerInt == null)
			{
				if (instance.giverClass is null)
					TryFixAndLogError(instance);

				instance.workerInt = (WorkGiver)Activator.CreateInstance(instance.giverClass!);
				instance.workerInt.def = instance;
			}

			return instance.workerInt;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void TryFixAndLogError(WorkGiverDef instance)
		{
			instance.giverClass = typeof(WorkGiver_AlwaysSkip);
			Log.Error($"Detected missing giverClass in WorkGiverDef '{
				instance.defName ?? "null"}' from mod '{
					instance.modContentPack?.Name ?? "null"}' for WorkTypeDef '{
						instance.workType?.defName ?? "null"}'. Disabling now to avoid further issues.");
		}
	}

	public sealed class WorkGiver_AlwaysSkip : WorkGiver
	{
		public override bool ShouldSkip(Pawn pawn, bool forced = false) => true;
	}
}