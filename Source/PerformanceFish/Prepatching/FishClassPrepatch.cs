// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;

namespace PerformanceFish.Prepatching;

public abstract class FishClassPrepatch : FishPrepatchBase
{
	public abstract Type Type { get; }

	public virtual void FreePatch(TypeDefinition typeDefinition)
	{
	}
}