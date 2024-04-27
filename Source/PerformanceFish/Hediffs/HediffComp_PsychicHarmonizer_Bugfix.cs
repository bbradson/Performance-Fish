// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if V1_4
using static System.Reflection.Emit.OpCodes;

namespace PerformanceFish.Hediffs;

public sealed class HediffComp_PsychicHarmonizer_Bugfix : ClassWithFishPatches
{
	public sealed class CompPostTick_Patch : FishPatch
	{
		public override string Description { get; }
			= "Fixes a bug in the psychic harmonizer tick method that was causing it to recalculate results on 149 "
			+ "out of 150 ticks instead of once every 150 ticks. Large impact.";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.Method(typeof(HediffComp_PsychicHarmonizer),
				nameof(HediffComp_PsychicHarmonizer.CompPostTick));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
		{
			var codes = codeInstructions.AsOrToList();
			var pawn_int_IsHashIntervalTick = FishTranspiler.Call<Func<Thing, int, bool>>(Gen.IsHashIntervalTick);
			var success = false;
			
			for (var i = 0; i < codes.Count; i++)
			{
				if (i - 2 >= 0
					&& codes[i - 2].opcode == Ldc_I4
					&& codes[i - 1] == pawn_int_IsHashIntervalTick
					&& codes[i].opcode == Brfalse_S)
				{
					codes[i].opcode = Brtrue_S;
					success = true;
				}
			}

			if (!success)
			{
				Log.Warning("Performance Fish failed to insert its Psychic Harmonizer Tick bugfix. This is "
					+ "probably harmless and can happen when toggling patches from mod settings. It could indicate "
					+ "an actual failure to patch if settings were not toggled however, which would mean some other "
					+ "patch or a vanilla change either messes with this or implements the same thing");
			}

			return codes;
		}
	}
}
#endif