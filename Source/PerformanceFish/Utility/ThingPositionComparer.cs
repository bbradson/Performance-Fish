// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public sealed class ThingPositionComparer : IComparer<Thing>
{
	public IntVec3 rootCell;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int Compare(Thing x, Thing y)
		=> (x.Position - rootCell).LengthHorizontalSquared.CompareTo(
			(y.Position - rootCell).LengthHorizontalSquared);
}