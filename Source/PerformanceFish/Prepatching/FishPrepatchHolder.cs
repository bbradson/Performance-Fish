// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Collections.Concurrent;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Prepatching;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public class FishPrepatchHolder : IExposable, IEnumerable<FishPrepatchBase>
{
	public ConcurrentDictionary<Type, FishPrepatchBase> All { get; } = new();

	public T Get<T>() where T : FishPrepatchBase => (T)All[typeof(T)];
	public void Add(FishPrepatchBase patch) => All[patch.GetType()] = patch;

	public FishPrepatchBase this[Type type] => All[type];

	public FishPrepatchHolder(Type type)
	{
		_type = type;
		AddPatchesRecursively(type);
	}

	public void Scribe() => ExposeData(); // Exposable.Scribe(this, _type.FullName);
	// directly calling ExposeData prevents creating nested nodes in the config file. Looks cleaner imo.

	public void ExposeData()
	{
		foreach (var (type, patch) in All)
			Exposable.Scribe(patch, type.FullName ?? type.Name);
	}

	private void AddPatchesRecursively(Type type)
	{
		if (typeof(FishPrepatchBase).IsAssignableFrom(type) && !All.ContainsKey(type))
		{
			if (PerformanceFishMod.AllPrepatchClasses is { } allPatches)
				RemoveDupes(allPatches, type);

			All.TryAdd(type, FishPrepatchBase.Get(type));
		}

		foreach (var nestedType in type.GetNestedTypes(AccessTools.all))
			AddPatchesRecursively(nestedType);
	}

	private void RemoveDupes(ClassWithFishPrepatches[] patches, Type type)
	{
		foreach (var patchClass in patches)
		{
			if (patchClass.GetType() == _type
				|| !patchClass.Patches.All.ContainsKey(type))
			{
				continue;
			}

			patchClass.Patches.All.TryRemove(type, out _);
			Log.Warning($"Performance Fish removed a duplicate patch from {
				patchClass.GetType().FullName}. This is likely caused by no longer valid mod configs");
		}
	}

	public IEnumerator<FishPrepatchBase> GetEnumerator() => All.Values.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => All.Values.GetEnumerator();

	private Type _type;
}