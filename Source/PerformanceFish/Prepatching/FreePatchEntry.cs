// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;
using Mono.Cecil;
using PerformanceFish.ModCompatibility;
using Prepatcher;

namespace PerformanceFish.Prepatching;

public static class FreePatchEntry
{
	[FreePatch]
	[UsedImplicitly]
	public static void Start(ModuleDefinition module)
	{
		ActiveMods.CheckRequirements();
		PrepatchManager.Start(module);
	}
}