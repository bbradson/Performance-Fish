// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class DeepResourceGridOptimization : ClassWithFishPrepatches
{
	public sealed class AnyActiveDeepScannersOnMapPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes lookups for active deep scanners to directly return the result of cached data instead of "
			+ "iterating over all buildings on the map trying to find any matches";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(DeepResourceGrid), nameof(DeepResourceGrid.AnyActiveDeepScannersOnMap));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(DeepResourceGrid __instance)
		{
			foreach (var item in __instance.map.listerBuildings.Cache().ColonistDeepScanners)
			{
				var compDeepScanner = item.TryGetComp<CompDeepScanner>();
				if (compDeepScanner != null && compDeepScanner.ShouldShowDeepResourceOverlay())
					return true;
			}
			
			return false;
		}
	}
}