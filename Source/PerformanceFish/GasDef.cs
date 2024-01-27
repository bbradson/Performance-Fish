// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;

namespace PerformanceFish;

[PublicAPI]
public class GasDef : Def
{
	public int dissipationRate;
	public bool diffuses;
	public Color color;

	public static GasDef? OfGasType(GasType gasType)
		=> gasType switch
		{
			GasType.BlindSmoke => GasDefOf.BlindSmoke,
			GasType.RotStink => GasDefOf.RotStink,
			GasType.ToxGas => GasDefOf.ToxGas,
			_ => null
		};
}