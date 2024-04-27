// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class IdeoBuildingPresenceDemandOptimization : ClassWithFishPrepatches
{
	public sealed class BuildingPresentPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimization to use cached ListerBuildings data instead of scanning through all colonist buildings, "
			+ "frequently";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(IdeoBuildingPresenceDemand),
				nameof(IdeoBuildingPresenceDemand.BuildingPresent));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static bool ReplacementBody(IdeoBuildingPresenceDemand __instance, Map map)
		{
			var parentBuilding = __instance.parent;
			var buildings = map.listerBuildings.AllBuildingsColonistOfDef(parentBuilding.ThingDef);
#if !V1_4
			for (var i = buildings.Count; i-- > 0;)
			{
				if (buildings[i].StyleSourcePrecept == parentBuilding)
					return true;
			}

			return false;
#else
			if (buildings is IndexedFishSet<Building> fishSet)
			{
				var list = fishSet.ReadOnlyList;
				for (var i = list.Count; i-- > 0;)
				{
					if (list[i].StyleSourcePrecept == parentBuilding)
						return true;
				}

				return false;
			}
			else
			{
				return FallbackCheck(__instance, buildings);
			}
#endif
		}

#if V1_4
		public static bool FallbackCheck(IdeoBuildingPresenceDemand __instance, IEnumerable<Building> buildings)
		{
			var parentBuilding = __instance.parent;
			foreach (var building in buildings)
			{
				if (building.StyleSourcePrecept == parentBuilding)
					return true;
			}

			return false;
		}
#endif
	}
}