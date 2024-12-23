// Copyright (c) 2024 bradson, missionz3r0
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using Verse.AI;

namespace PerformanceFish.JobSystem;

public sealed class Toils_BedOptimization : ClassWithFishPatches
{
	public sealed class FailOnBedNoLongerUsable_Patch : FishPatch
	{
		public override string Description { get; }
			= "Adds a HashOffset to skip BedNoLongerUsable calls every tick.";

		public override MethodBase TargetMethodInfo { get; }
			= FindDelegateMethod(
				AccessTools.Method(typeof(Toils_Bed), nameof(Toils_Bed.FailOnBedNoLongerUsable), [typeof(Toil), typeof(TargetIndex), typeof(TargetIndex)]),
				"FailOn"
			);

		public static bool Prefix(ref bool __result)
		{
			if (TickHelper.MatchesModulo(64))
				return true;

			return __result = false;
		}
	}

	private static MethodBase FindDelegateMethod(
		MethodBase containingMethod,
		string secondOrderMethod
	)
	{
			var body = PatchProcessor.ReadMethodBody(containingMethod).ToList();

			for (var i = 0; i < body.Count; i++)
			{
					var (opcode, operand) = body[i];
					if (operand is not MethodInfo info || info.Name != secondOrderMethod)
							continue;

					while (--i >= 0)
					{
							(opcode, operand) = body[i];
							if (opcode == OpCodes.Ldftn)
									return (MethodBase)operand;
					}

					break;
			}

			throw new InvalidOperationException(
				$"Performance Fish failed to find a delegate called from method {secondOrderMethod} within method {containingMethod}"
			);
	}
}
