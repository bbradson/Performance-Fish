// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Collections.Concurrent;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Patching;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public class FishPatchHolder : IExposable, IEnumerable<FishPatch>
{
	private volatile int _completedPatchingOnce;
	
	public ConcurrentDictionary<Type, FishPatch> All { get; } = new();

	public T Get<T>() where T : FishPatch => (T)All[typeof(T)];
	public void Add(FishPatch patch) => All[patch.GetType()] = patch;
	
	public void PatchAll()
	{
		using var patches = new PooledArray<FishPatch>(All.Values);

		for (var i = 0; i < patches.Length; i++)
			patches[i].TryPatch();

		if (Interlocked.Exchange(ref _completedPatchingOnce, 1) == 0)
		{
			for (var i = 0; i < patches.Length; i++)
				patches[i].OnPatchingCompleted();
		}
	}

	public void UnpatchAll()
	{
		foreach (var patch in All.Values)
			patch.TryUnpatch();
	}

	public FishPatch this[Type type] => All[type];

	public FishPatchHolder(Type type)
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
		if (typeof(FishPatch).IsAssignableFrom(type) && !All.ContainsKey(type))
		{
			if (PerformanceFishMod.AllPatchClasses is { } allPatches)
				RemoveDupes(allPatches, type);

			All.TryAdd(type, FishPatch.Get(type));
		}

		foreach (var nestedType in type.GetNestedTypes(AccessTools.all))
			AddPatchesRecursively(nestedType);
	}

	private void RemoveDupes(IHasFishPatch[] patches, Type type)
	{
		foreach (var patchClass in patches)
		{
			if (patchClass.GetType() == _type
				|| !patchClass.Patches.All.ContainsKey(type))
			{
				continue;
			}

			patchClass.Patches[type].Enabled = false;
			patchClass.Patches.All.TryRemove(type, out _);
			Log.Warning($"Performance Fish removed a duplicate patch from {
				patchClass.GetType().FullName}. This is likely caused by no longer valid mod configs");
		}
	}

	public IEnumerator<FishPatch> GetEnumerator() => All.Values.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => All.Values.GetEnumerator();

	private Type _type;
}