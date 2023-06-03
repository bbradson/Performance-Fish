// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Prepatching;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors | ImplicitUseTargetFlags.WithMembers)]
public abstract class FishPrepatchBase : SingletonFactory<FishPrepatchBase>, IExposable, IHasDescription
{
	private bool _enabled;
	
	public virtual string? Description => null;

	public virtual string? Name => null;

	public virtual bool Enabled
	{
		get => _enabled;
		set => _enabled = value;
	}

	public virtual bool DefaultState
	{
		[Pure] get => true;
	}

	// ReSharper disable once VirtualMemberCallInConstructor
	protected FishPrepatchBase() => _enabled = DefaultState;
	
	public virtual void ExposeData() => Scribe_Values.Look(ref _enabled, "enabled", DefaultState);
}