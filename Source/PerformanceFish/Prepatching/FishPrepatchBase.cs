// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Linq;
using Mono.Cecil;
using nuget::JetBrains.Annotations;
using PerformanceFish.ModCompatibility;

namespace PerformanceFish.Prepatching;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors | ImplicitUseTargetFlags.WithMembers)]
public abstract class FishPrepatchBase : SingletonFactory<FishPrepatchBase>, IExposable, IHasDescription
{
	private bool
		_enabled,
		_isActive;

	private string? _descriptionWithNotes;

	private FishPrepatchBase[]? _resolvedLinkedPrepatches;
	private FishPatch[]? _resolvedLinkedHarmonyPatches;

	internal int IDNumber { get; }

	public virtual int Priority => HarmonyLib.Priority.Normal;
	
	public virtual List<string> IncompatibleModIDs { get; } = [];

	public virtual List<Type> LinkedPatches { get; } = [];

	private FishPrepatchBase[] ResolvedLinkedPrepatches
		=> _resolvedLinkedPrepatches ??= LinkedPatches
			.Where(static type => type.IsAssignableTo(typeof(FishPrepatchBase))).Select(static type => Get(type))
			.ToArray();

	private FishPatch[] ResolvedLinkedHarmonyPatches
		=> _resolvedLinkedHarmonyPatches ??= LinkedPatches
			.Where(static type => type.IsAssignableTo(typeof(FishPatch))).Select(static type => FishPatch.Get(type))
			.ToArray();

	public bool DisabledForModCompatibility => ActiveMods.ContainsAnyOf(IncompatibleModIDs);

	public virtual string? Description => null;

	public string? DescriptionWithNotes
		=> _descriptionWithNotes ??= !DisabledForModCompatibility
			? Description
			: Description
			+ $"{(Description?.EndsWith(".") ?? true ? "" : ".")} Disabled for compatibility with {
				IncompatibleModIDs.Select(static id => ActiveMods.TryGetModMetaData(id)?.Name)
					.Where(Is.NotNull).ToCommaList(true)}";

	public virtual string? Name => null;

	public virtual bool Enabled
	{
		get => _enabled && !DisabledForModCompatibility;
		set
		{
			if (_enabled == value)
				return;

			_enabled = value;

			for (var i = 0; i < ResolvedLinkedPrepatches.Length; i++)
				ResolvedLinkedPrepatches[i].Enabled = value;

			for (var i = 0; i < ResolvedLinkedHarmonyPatches.Length; i++)
				ResolvedLinkedHarmonyPatches[i].Enabled = value;
		}
	}

	public virtual bool DefaultState
	{
		[Pure] get => true;
	}

	public virtual bool ShowSettings => true;

	public bool IsActive => _isActive;

	// ReSharper disable once VirtualMemberCallInConstructor
	protected FishPrepatchBase()
	{
		_enabled = DefaultState;
		IDNumber = Interlocked.Increment(ref _idCounter);

		if (OnAssembliesLoaded.Loaded)
			_isActive = FishStash.Get.IsPatchActive(this);
	}

	protected internal abstract void ApplyPatch(ModuleDefinition module);

	protected internal virtual void OnPatchingCompleted()
	{
	}

	protected internal virtual void BeforeHarmonyPatching()
	{
	}
	
	public virtual void ExposeData() => Scribe_Values.Look(ref _enabled, "enabled", DefaultState);

	internal static Comparer<FishPrepatchBase> PriorityComparer
		= Comparer<FishPrepatchBase>.Create(static (x, y) => y.Priority.CompareTo(x.Priority));

	protected static TypeDefinition ThrowForFailureToFindType(Type patchType)
		=> ThrowHelper.ThrowInvalidOperationException<TypeDefinition>(
			$"Failed to find type with namespace {patchType.Namespace} and name {patchType.Name}");
	
	private static volatile int _idCounter;
}