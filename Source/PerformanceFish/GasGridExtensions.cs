// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;

namespace PerformanceFish;

[PublicAPI]
public static class GasGridExtensions
{
	public static GasGridOptimization.ParallelGasGrid ForDef(this GasGridOptimization.ParallelGasGrid[] grids,
		GasDef def)
		=> grids[def.index];
	
	public static GasGridOptimization.ParallelGasGrid ForDef(this GasGrid grid, GasDef def)
		=> grid.ParallelGasGrids().ForDef(def);

	public static void AddGas(this GasGrid grid, in IntVec3 cell, GasDef gas, int amount, bool canOverflow = true)
		=> grid.ForDef(gas).AddGas(cell, amount, canOverflow);
	
	public static void AddGas(this GasGrid grid, in IntVec3 cell, GasDef gas, float radius)
		=> grid.AddGas(cell, gas, 255 * GenRadial.NumCellsInRadius(radius));

	public static void SetDirect(this GasGrid grid, in IntVec3 cell, GasDef gas, byte density)
		=> grid.ForDef(gas).SetDirect(cell, density);

	public static byte DensityAt(this GasGrid grid, in IntVec3 cell, GasDef gas) => grid.ForDef(gas).DensityAt(cell);

	public static float DensityPercentAt(this GasGrid grid, in IntVec3 cell, GasDef gas)
		=> grid.DensityAt(cell, gas) / 255f;

	/// <summary>
	/// For modders. Performance Fish doesn't automatically scribe added custom defs.
	/// </summary>
	public static void ScribeForDef(this GasGrid grid, GasDef gas, string label)
	{
		if (gas == GasDefOf.BlindSmoke || gas == GasDefOf.ToxGas || gas == GasDefOf.RotStink)
		{
			Log.Error($"Tried scribing vanilla gas '{gas}'. This should never be manually scribed by a mod.");
			return;
		}

		grid.ForDef(gas).Scribe(label);
	}
}