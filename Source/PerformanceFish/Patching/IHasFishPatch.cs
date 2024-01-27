// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Patching;

[PublicAPI]
public interface IHasFishPatch
{
	public FishPatchHolder Patches { get; }
	public bool RequiresLoadedGameForPatching { get; }
	public bool ShowSettings { get; }
}