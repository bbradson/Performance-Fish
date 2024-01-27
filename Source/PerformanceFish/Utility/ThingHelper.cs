// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public static class ThingHelper
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int GetKey(this Thing thing)
		=> thing.thingIDNumber != -1 ? thing.thingIDNumber : RuntimeHelpers.GetHashCode(thing);
}