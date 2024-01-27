// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Defs;

public record struct DefThingPair(int DefShortHash, int ThingIDNumber)
{
	public int
		DefShortHash = DefShortHash,
		ThingIDNumber = ThingIDNumber;

	public bool Equals(DefThingPair other)
		=> Unsafe.As<DefThingPair, ulong>(ref this) == Unsafe.As<DefThingPair, ulong>(ref other);

	public override int GetHashCode() => HashCode.Combine(DefShortHash, ThingIDNumber);
}