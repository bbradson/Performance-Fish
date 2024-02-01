// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Diagnostics;
using System.Linq;
using nuget::JetBrains.Annotations;
using PerformanceFish.Cache;
using PerformanceFish.ModCompatibility;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Patching;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors | ImplicitUseTargetFlags.WithMembers)]
public abstract class FishPatch : SingletonFactory<FishPatch>, IExposable, IHasDescription
{
	private bool _enabled;

	private string? _descriptionWithNotes;

	private FishPrepatchBase[]? _resolvedLinkedPrepatches;
	
	private FishPatch[]? _resolvedLinkedHarmonyPatches;
	
	public virtual List<string> IncompatibleModIDs { get; } = [];

	public virtual List<Type> LinkedPatches { get; } = [];

	private FishPrepatchBase[] ResolvedLinkedPrepatches
		=> _resolvedLinkedPrepatches ??= LinkedPatches
			.Where(static type => type.IsAssignableTo(typeof(FishPrepatchBase)))
			.Select(static type => FishPrepatchBase.Get(type))
			.ToArray();

	private FishPatch[] ResolvedLinkedHarmonyPatches
		=> _resolvedLinkedHarmonyPatches ??= LinkedPatches
			.Where(static type => type.IsAssignableTo(typeof(FishPatch))).Select(static type => Get(type))
			.ToArray();

	public bool DisabledForModCompatibility => ActiveMods.ContainsAnyOf(IncompatibleModIDs);
	
	public virtual bool ShouldBenchmark => false;

	public virtual bool Enabled
	{
		get => _enabled && !DisabledForModCompatibility;
		set
		{
			if (value == _enabled)
				return;
			
			_enabled = value;
			if (value)
			{
				if (ShouldBePatched)
					TryPatch();
			}
			else
			{
				var shouldBePatched = ShouldBePatched;
				TryUnpatch();
				ShouldBePatched = shouldBePatched;
			}

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

	public virtual int PrefixMethodPriority => TryGetPriority(PrefixMethodInfo);
	public virtual int PostfixMethodPriority => TryGetPriority(PostfixMethodInfo);
	public virtual int TranspilerMethodPriority => TryGetPriority(TranspilerMethodInfo);
	public virtual int FinalizerMethodPriority => TryGetPriority(FinalizerMethodInfo);
	public MethodInfo? HarmonyMethodInfo => HarmonyMethodInfos.FirstOrDefault();
	public List<MethodInfo> HarmonyMethodInfos { get; } = [];
	public MethodInfo? BenchmarkHarmonyMethodInfo => BenchmarkHarmonyMethodInfos.FirstOrDefault();
	public List<MethodInfo> BenchmarkHarmonyMethodInfos { get; } = [];
	private bool Patched { get; set; }
	private bool ShouldBePatched { get; set; }

	public virtual string? Name => null;
	
	public virtual string? Description => null;

	public string? DescriptionWithNotes
		=> _descriptionWithNotes ??= !DisabledForModCompatibility
			? Description
			: Description
			+ $"{(Description?.EndsWith(".") ?? true ? "" : ".")} Disabled for compatibility with {
				IncompatibleModIDs.Select(static id => ActiveMods.TryGetModMetaData(id)?.Name)
					.Where(Is.NotNull).ToCommaList(true)}";

	public virtual Delegate? TargetMethodGroup => null;
	public virtual IEnumerable<Delegate>? TargetMethodGroups => null;
	public virtual Expression<Action>? TargetMethod => null;
	public virtual IEnumerable<Expression<Action>>? TargetMethods => null;

	public virtual MethodBase? TargetMethodInfo
		=> TargetMethod != null
			? SymbolExtensions.GetMethodInfo(TargetMethod)
			: TargetMethodGroup?.Method;

	public virtual IEnumerable<MethodBase> TargetMethodInfos
		=> _targetMethodInfos
			??= (TargetMethods?.Select(SymbolExtensions.GetMethodInfo)
					?? TargetMethodGroups?.Select(static m => m.Method))?.Cast<MethodBase>().ToArray()
				?? (TargetMethodInfo != null!
					? [TargetMethodInfo]
				: Array.Empty<MethodBase>());

	private MethodBase[]? _targetMethodInfos;

	public virtual MethodInfo? ReversePatchMethodInfo
		=> TryGetMethod("REVERSEPATCH",
			static m => m.HasAttribute<HarmonyReversePatch>() && !m.HasAttribute<HarmonyTranspiler>());

	public virtual MethodInfo? ReversePatchTranspilerMethodInfo
		=> TryGetMethod("REVERSEPATCHTRANSPILER",
			static m => m.HasAttribute<HarmonyTranspiler>() && m.HasAttribute<HarmonyReversePatch>());

	public virtual MethodInfo? PrefixMethodInfo
		=> TryGetMethod("PREFIX", static m => m.HasAttribute<HarmonyPrefix>());

	public virtual MethodInfo? PostfixMethodInfo
		=> TryGetMethod("POSTFIX", static m => m.HasAttribute<HarmonyPostfix>());

	public virtual MethodInfo? TranspilerMethodInfo
		=> TryGetMethod("TRANSPILER",
			static m => m.HasAttribute<HarmonyTranspiler>() && !m.HasAttribute<HarmonyReversePatch>());

	public virtual MethodInfo? FinalizerMethodInfo
		=> TryGetMethod("FINALIZER", static m => m.HasAttribute<HarmonyFinalizer>());

	internal Type ParentType
		=> GetType().DeclaringType
			?? ThrowHelper.ThrowArgumentNullException<Type>($"{this} is missing parent class. This is not supported");

	internal IHasFishPatch? ParentClass
		=> PerformanceFishMod.AllPatchClasses?.FirstOrDefault(patchClass => patchClass.GetType() == ParentType);

	internal bool IsBenchmarking => ShouldBenchmark && !FinishedBenchmarking;
	internal bool FinishedBenchmarking;

	// ReSharper disable once VirtualMemberCallInConstructor
	protected FishPatch() => _enabled = DefaultState;

	public virtual MethodInfo? TryPatch()
	{
		if ((ParentClass?.RequiresLoadedGameForPatching ?? false) && Current.ProgramState != ProgramState.Playing)
			return HarmonyMethodInfo;
		
		ShouldBePatched = true;
		if (!Enabled || Patched)
			return HarmonyMethodInfo;
		
		if (!TargetMethodInfos.Any())
		{
			Log.Error($"Tried to apply patches for {GetType().Name}, but it lacks a target method for patching.");
			return null;
		}

		if (PrefixMethodInfo is null
			&& PostfixMethodInfo is null
			&& TranspilerMethodInfo is null
			&& FinalizerMethodInfo is null
			&& ReversePatchMethodInfo is null)
		{
			Log.Error($"Tried to apply patches for {
				GetType().Name}, but there are none. This is likely not intended.");
			return null;
		}
		
		ApplyPatch();

		return HarmonyMethodInfo;
	}

	protected virtual void ApplyPatch()
	{
		Patched = true;
		foreach (var method in TargetMethodInfos)
		{
			DebugLog.Message($"Performance Fish is applying {GetType().Name} on {method.FullDescription()}");

			try
			{
				if (IsBenchmarking)
				{
					Patched = false;

					if (AccessTools.EnumeratorMoveNext(method) is { } moveNext)
					{
						BenchmarkHarmonyMethodInfos.Add(PerformanceFishMod.Harmony!.Patch(moveNext,
							prefix: new(methodof(Benchmarking.Prefix), Priority.First + 1),
							postfix: new(methodof(Benchmarking.Postfix), Priority.Last - 1)));
					}

					BenchmarkHarmonyMethodInfos.Add(PerformanceFishMod.Harmony!.Patch(method,
						prefix: new(methodof(Benchmarking.Prefix), Priority.First + 1),
						postfix: new(methodof(Benchmarking.Postfix), Priority.Last - 1)));
				}
				else
				{
					if (ReversePatchMethodInfo != null)
					{
						HarmonyMethodInfos.Add(Harmony.ReversePatch(method, new(ReversePatchMethodInfo),
							ReversePatchTranspilerMethodInfo));
					}

					if (PrefixMethodInfo != null
						|| PostfixMethodInfo != null
						|| TranspilerMethodInfo != null
						|| FinalizerMethodInfo != null)
					{
						HarmonyMethodInfos.Add(PerformanceFishMod.Harmony!.Patch(method,
							prefix: TryMakeHarmonyMethod(PrefixMethodInfo, PrefixMethodPriority),
							postfix: TryMakeHarmonyMethod(PostfixMethodInfo, PostfixMethodPriority),
							transpiler: TryMakeHarmonyMethod(TranspilerMethodInfo, TranspilerMethodPriority),
							finalizer: TryMakeHarmonyMethod(FinalizerMethodInfo, FinalizerMethodPriority)));
					}
				}
			}
			catch (Exception e)
			{
				Log.Error($"Performance Fish encountered an exception while trying to patch {
					method.FullDescription()} with {PrefixMethodInfo.FullDescription()}, {
						PostfixMethodInfo.FullDescription()}, {TranspilerMethodInfo.FullDescription()}, {
							FinalizerMethodInfo.FullDescription()}:\n{e}");
			}
		}
	}

	private static HarmonyMethod? TryMakeHarmonyMethod(MethodInfo? method, int priority = -1, string[]? before = null,
		string[]? after = null, bool? debug = null)
		=> method != null ? new(method, priority, before, after, debug) : null;

	public virtual void TryUnpatch()
	{
		ShouldBePatched = false;
		if ( /*!Enabled &&*/ Patched)
			ApplyUnpatching();
	}

	protected virtual void ApplyUnpatching()
	{
		Patched = false;
		foreach (var method in TargetMethodInfos)
		{
			for (var i = 0; i < 4; i++)
			{
				TryUnpatchMethod(method, i switch
				{
					0 => PrefixMethodInfo,
					1 => PostfixMethodInfo,
					2 => TranspilerMethodInfo,
					_ => FinalizerMethodInfo
				});
			}
		}
	}

	private static void TryUnpatchMethod(MethodBase targetMethod, MethodInfo? patchMethod)
	{
		if (patchMethod is null)
			return;

		try
		{
			PerformanceFishMod.Harmony.Unpatch(targetMethod, patchMethod);
		}
		catch (Exception e)
		{
			Log.Error($"Performance Fish encountered an exception when unpatching {
				patchMethod.FullDescription()} from {targetMethod.FullDescription()}:\n{e}");
		}
	}

	private MethodInfo? TryGetMethod(string nameCaseInsensitive, Predicate<MethodInfo>? predicate = null)
		=> GetType().TryGetMethod(nameCaseInsensitive, predicate);

	private static int TryGetPriority(MethodInfo? info)
		=> info?.TryGetAttribute<HarmonyPriority>()?.info.priority ?? Priority.Normal;
	
	protected internal virtual void OnPatchingCompleted()
	{
	}

	public virtual void ExposeData() => Scribe_Values.Look(ref _enabled, "enabled", DefaultState);

	public override string ToString() => GetType().FullName ?? GetType().Name;
}

internal static class Benchmarking
{
	internal static int SwitchFrame = int.MaxValue;
	internal static int EndFrame = int.MaxValue;

	public static void Prefix(MethodBase __originalMethod, out Results __state)
	{
		__state = ByReference<MethodBase, Results>.Get.GetOrAdd(__originalMethod);
		if (__state.Stopwatch is null)
			ByReference<MethodBase, Results>.Get.GetReference(__originalMethod).Setup(__originalMethod);

		__state.Stopwatch!.Start();
	}

	public static void Postfix(Results __state)
	{
		__state.Stopwatch!.Stop();
		__state.Count++;

		//Log.Message($"SwitchFrame is {SwitchFrame}, EndFrame is {EndFrame}, CurrentFrame is {Time.frameCount}");

		//if (__state.Stopwatch.ElapsedMilliseconds > 100 || __state.Count > 100000)
		if (__state.Switched ? GenTicks.TicksGame > EndFrame : GenTicks.TicksGame > SwitchFrame)
			PrintResultsAndStop(__state);
	}

	public static void PrintResultsAndStop(Results results)
	{
		var stopwatch = results.Stopwatch!;
		Log.Message($"{results.Method.FullDescription()} took {stopwatch.ElapsedMilliSecondsAccurate()} ms for {
			results.Count} calls. This equals {
			stopwatch.ElapsedNanoSeconds() / results.Count} ns per call on average.");

		results.Count = 0;
		results.Switched = true;
		stopwatch.Reset();

		var fishPatch = PerformanceFishMod.AllPatchClasses!
			.SelectMany(static patchClass => patchClass.Patches)
			.First(patch => patch.TargetMethodInfos.Contains(results.Method));

		if (fishPatch.FinishedBenchmarking)
		{
			foreach (var target in fishPatch.TargetMethodInfos)
			{
				if (AccessTools.EnumeratorMoveNext(target) is { } moveNext)
				{
					PerformanceFishMod.Harmony!.Unpatch(moveNext, methodof(Prefix));
					PerformanceFishMod.Harmony.Unpatch(moveNext, methodof(Postfix));
				}

				PerformanceFishMod.Harmony!.Unpatch(target, methodof(Prefix));
				PerformanceFishMod.Harmony.Unpatch(target, methodof(Postfix));
			}
		}
		else
		{
			fishPatch.FinishedBenchmarking = true;
			fishPatch.TryPatch();
		}
	}

	public sealed class Results
	{
		public int Count;
		public Stopwatch? Stopwatch;
		public MethodBase? Method;
		public bool Switched;

		public void Setup(MethodBase method)
		{
			Stopwatch = new();
			Method = method;
		}
	}
}