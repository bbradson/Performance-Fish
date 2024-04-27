// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;

namespace PerformanceFish;

[DefOf]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
#pragma warning disable CS8618
public static class GasDefOf
{
	public static GasDef
		BlindSmoke,
		ToxGas,
		RotStink
#if !V1_4
		,
		DeadLifeDust
#endif
		;

	static GasDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GasDefOf));
}
#pragma warning restore CS8618