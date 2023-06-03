// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Patching;

public abstract class FirstPriorityFishPatch : FishPatch
{
	public override int PrefixMethodPriority => Priority.First;
	public override int PostfixMethodPriority => Priority.First;
	public override int TranspilerMethodPriority => Priority.First;
	public override int FinalizerMethodPriority => Priority.First;
}