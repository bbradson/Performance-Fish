// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using LwmCompCache = PerformanceFish.Cache.ByReference<Verse.Thing, PerformanceFish.Hauling.LwmCompCacheValue>;

namespace PerformanceFish.Hauling;

public record struct LwmCompCacheValue
{
	public CompProperties? CompProperties;

	public static CompProperties? TryGetLwmProps(Thing slotGroupParent)
		=> ModCompatibility.Types.LWM.CompProperties != null
			? slotGroupParent.def.comps?.Find(static props
				=> props.GetType() == ModCompatibility.Types.LWM.CompProperties)
			: null;

	static LwmCompCacheValue() => EnsureInitialized();

	public static void EnsureInitialized()
	{
		if (Interlocked.Exchange(ref _initialized, 1) == 0)
			LwmCompCache.ValueInitializer = static key => new() { CompProperties = TryGetLwmProps(key) };
	}

	private static volatile int _initialized;

	public static AccessTools.FieldRef<CompProperties, int>?
		LwmMaxNumberStacks = TryGetLwmField("maxNumberStacks"),
		LwmMinNumberStacks = TryGetLwmField("minNumberStacks");

	private static AccessTools.FieldRef<CompProperties, int>? TryGetLwmField(string name)
		=> ModCompatibility.Types.LWM.CompProperties is { } propsType
			? AccessTools.FieldRefAccess<CompProperties, int>(AccessTools.Field(propsType, name))
			: null;
}