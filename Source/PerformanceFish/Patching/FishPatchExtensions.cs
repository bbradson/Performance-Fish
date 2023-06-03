// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Patching;

public static class FishPatchExtensions
{
	public static void PatchAll(this IEnumerable<FishPatch> patches)
	{
		foreach (var patch in patches)
			patch.TryPatch();
	}

	public static void UnpatchAll(this IEnumerable<FishPatch> patches)
	{
		foreach (var patch in patches)
			patch.TryUnpatch();
	}
}