// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Prepatching;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors | ImplicitUseTargetFlags.WithMembers)]
public abstract class ClassWithFishPrepatches : SingletonFactory<ClassWithFishPrepatches>
{
	public virtual FishPrepatchHolder Patches { get; }

	public virtual bool ShowSettings
	{
		get
		{
			foreach (var patch in Patches)
			{
				if (patch.ShowSettings)
					return true;
			}

			return false;
		}
	}
	
	public virtual bool Enabled => true;

	protected ClassWithFishPrepatches() => Patches = new(GetType());
}