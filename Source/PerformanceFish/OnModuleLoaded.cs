// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

/// <summary>
/// Prepatcher's FreePatch invoke, the Log patch and if neither of the two happened the Mod ctor invoke call this.
/// Generally runs twice with Prepatcher active as a result and doesn't really enable running anything earlier than
/// without this initializer.
/// </summary>
#pragma warning disable CA2255
public static class OnModuleLoaded
{
	[ModuleInitializer]
	public static void Start()
	{
		
	}
}
#pragma warning restore CA2255