// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

global using RimfactoryExtensionCache
	= PerformanceFish.Cache.ByReference<Verse.Thing, PerformanceFish.Hauling.RimfactoryExtensionCacheValue>;
using RimfactoryTypes = PerformanceFish.ModCompatibility.Types.Rimfactory;

namespace PerformanceFish.Hauling;

public record struct RimfactoryExtensionCacheValue
{
	public DefModExtension? ModExtension;

	public static DefModExtension? TryGetExtension(Thing slotGroupParent)
		=> RimfactoryTypes.ModExtension != null
			? slotGroupParent.def.modExtensions?.Find(_extensionPredicate)
			: null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasExtension(Thing slotGroupParent)
		=> RimfactoryTypes.ModExtension != null && TestExtension(slotGroupParent);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TestExtension(Thing slotGroupParent) => slotGroupParent.TryGetRimfactoryExtension() != null;

	static RimfactoryExtensionCacheValue() => EnsureInitialized();

	public static void EnsureInitialized()
	{
		if (Interlocked.Exchange(ref _initialized, 1) == 0)
			RimfactoryExtensionCache.ValueInitializer = static key => new() { ModExtension = TryGetExtension(key) };
	}

	private static volatile int _initialized;

	public static AccessTools.FieldRef<DefModExtension, int>? Limit = TryGetExtensionField("limit");

	private static Predicate<DefModExtension> _extensionPredicate = static extension
		=> extension.GetType() == RimfactoryTypes.ModExtension;

	private static AccessTools.FieldRef<DefModExtension, int>? TryGetExtensionField(string name)
		=> RimfactoryTypes.ModExtension is { } extensionType
			? AccessTools.FieldRefAccess<DefModExtension, int>(AccessTools.Field(extensionType, name))
			: null;
}