// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace PerformanceFish;

public abstract class FishPatch : SingletonFactory<FishPatch>, IExposable, IHasDescription
{
	public virtual bool ShouldBenchmark => false;

	public virtual bool Enabled
	{
		get => _enabled;
		set
		{
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
		}
	}
	private bool _enabled = true;
	public virtual bool DefaultState => true;
	public virtual int PrefixMethodPriority => TryGetPriority(PrefixMethodInfo);
	public virtual int PostfixMethodPriority => TryGetPriority(PostfixMethodInfo);
	public virtual int TranspilerMethodPriority => TryGetPriority(TranspilerMethodInfo);
	public virtual int FinalizerMethodPriority => TryGetPriority(FinalizerMethodInfo);
	public MethodInfo? HarmonyMethodInfo => HarmonyMethodInfos.FirstOrDefault();
	public List<MethodInfo> HarmonyMethodInfos { get; } = new();
	public MethodInfo? BenchmarkHarmonyMethodInfo => BenchmarkHarmonyMethodInfos.FirstOrDefault();
	public List<MethodInfo> BenchmarkHarmonyMethodInfos { get; } = new();
	private bool Patched { get; set; }
	private bool ShouldBePatched { get; set; }

	public virtual string? Name => null;
	public virtual string? Description => null;

	public virtual Delegate? TargetMethodGroup => null;
	public virtual IEnumerable<Delegate>? TargetMethodGroups => null;
	public virtual Expression<Action>? TargetMethod => null;
	public virtual IEnumerable<Expression<Action>>? TargetMethods => null;
	public virtual MethodBase TargetMethodInfo
		=> TargetMethod != null
		? SymbolExtensions.GetMethodInfo(TargetMethod)
		: TargetMethodGroup?.Method
		?? throw new MissingMethodException(GetType().ToString());

	public virtual IEnumerable<MethodBase> TargetMethodInfos
		=> _targetMethodInfos
		??= (TargetMethods?.Select(m
			=> SymbolExtensions.GetMethodInfo(m))
		?? TargetMethodGroups?.Select(m => m.Method)
		?? (TargetMethodInfo != null
		? new MethodBase[] { TargetMethodInfo }.AsEnumerable()
		: throw new MissingMethodException(GetType().ToString()))).ToArray();

	private MethodBase[]? _targetMethodInfos;

	public virtual MethodInfo ReversePatchMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "REVERSEPATCH" || (m.HasAttribute<HarmonyReversePatch>() && !m.HasAttribute<HarmonyTranspiler>()));
	public virtual MethodInfo ReversePatchTranspilerMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "REVERSEPATCHTRANSPILER" || (m.HasAttribute<HarmonyTranspiler>() && m.HasAttribute<HarmonyReversePatch>()));
	public virtual MethodInfo PrefixMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "PREFIX" || m.HasAttribute<HarmonyPrefix>());
	public virtual MethodInfo PostfixMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "POSTFIX" || m.HasAttribute<HarmonyPostfix>());
	public virtual MethodInfo TranspilerMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "TRANSPILER" || (m.HasAttribute<HarmonyTranspiler>() && !m.HasAttribute<HarmonyReversePatch>()));
	public virtual MethodInfo FinalizerMethodInfo => GetType().GetMethods(AccessTools.all)
		.FirstOrDefault(m => m.Name.ToUpperInvariant() == "FINALIZER" || m.HasAttribute<HarmonyFinalizer>());

	internal bool IsBenchmarking => ShouldBenchmark && !FinishedBenchmarking;
	internal bool FinishedBenchmarking = false;

	protected FishPatch() => _enabled = DefaultState;

	public virtual MethodInfo? TryPatch()
	{
		if (!TargetMethodInfos?.Any() ?? true)
		{
			Log.Error($"Tried to apply patches for {GetType().Name}, but it lacks a target method for patching.");
			return null;
		}
		if (PrefixMethodInfo is null && PostfixMethodInfo is null && TranspilerMethodInfo is null && FinalizerMethodInfo is null && ReversePatchMethodInfo is null)
		{
			Log.Error($"Tried to apply patches for {GetType().Name}, but there are none. This is likely not intended.");
			return null;
		}

		ShouldBePatched = true;
		if (Enabled && !Patched)
			ApplyPatch();

		return HarmonyMethodInfo;
	}

	protected virtual void ApplyPatch()
	{
		Patched = true;
		foreach (var method in TargetMethodInfos!)
		{
			DebugLog.Message($"Performance Fish is applying {GetType().Name} on {method.FullDescription()}");

			try
			{
				if (IsBenchmarking)
				{
					Patched = false;

					if (AccessTools.EnumeratorMoveNext(method) is { } moveNext)
					{
						BenchmarkHarmonyMethodInfos.Add(PerformanceFishMod.Harmony.Patch(moveNext,
							prefix: new(methodof(Benchmarking.Prefix), Priority.First + 1),
							postfix: new(methodof(Benchmarking.Postfix), Priority.Last - 1)));
					}

					BenchmarkHarmonyMethodInfos.Add(PerformanceFishMod.Harmony.Patch(method,
						prefix: new(methodof(Benchmarking.Prefix), Priority.First + 1),
						postfix: new(methodof(Benchmarking.Postfix), Priority.Last - 1)));
				}
				else
				{
					if (ReversePatchMethodInfo != null)
						HarmonyMethodInfos.Add(Harmony.ReversePatch(method, new(ReversePatchMethodInfo), ReversePatchTranspilerMethodInfo));

					if (PrefixMethodInfo != null || PostfixMethodInfo != null || TranspilerMethodInfo != null || FinalizerMethodInfo != null)
					{
						HarmonyMethodInfos.Add(PerformanceFishMod.Harmony.Patch(method,
							prefix: PrefixMethodInfo != null ? new(PrefixMethodInfo, PrefixMethodPriority) : null,
							postfix: PostfixMethodInfo != null ? new(PostfixMethodInfo, PostfixMethodPriority) : null,
							transpiler: TranspilerMethodInfo != null ? new(TranspilerMethodInfo, TranspilerMethodPriority) : null,
							finalizer: FinalizerMethodInfo != null ? new(FinalizerMethodInfo, FinalizerMethodPriority) : null));
					}
				}
			}
			catch (Exception e)
			{
				Log.Error($"Performance Fish encountered an exception while trying to patch {method.FullDescription()} with {PrefixMethodInfo.FullDescription()}" +
					$", {PostfixMethodInfo.FullDescription()}, {TranspilerMethodInfo.FullDescription()}, {FinalizerMethodInfo.FullDescription()}:\n{e}");
			}
		}
	}

	public virtual void TryUnpatch()
	{
		ShouldBePatched = false;
		if (/*!Enabled &&*/ Patched)
			ApplyUnpatching();
	}

	protected virtual void ApplyUnpatching()
	{
		Patched = false;
		foreach (var method in TargetMethodInfos)
		{
			if (PrefixMethodInfo != null)
			{
				try
				{
					PerformanceFishMod.Harmony.Unpatch(method, PrefixMethodInfo);
				}
				catch (Exception e)
				{
					Log.Error($"Performance Fish encountered an exception when unpatching {PrefixMethodInfo.FullDescription()} from {method.FullDescription()}:\n{e}");
				}
			}
			if (PostfixMethodInfo != null)
			{
				try
				{
					PerformanceFishMod.Harmony.Unpatch(method, PostfixMethodInfo);
				}
				catch (Exception e)
				{
					Log.Error($"Performance Fish encountered an exception when unpatching {PostfixMethodInfo.FullDescription()} from {method.FullDescription()}:\n{e}");
				}
			}
			if (TranspilerMethodInfo != null)
			{
				try
				{
					PerformanceFishMod.Harmony.Unpatch(method, TranspilerMethodInfo);
				}
				catch (Exception e)
				{
					Log.Error($"Performance Fish encountered an exception when unpatching {TranspilerMethodInfo.FullDescription()} from {method.FullDescription()}:\n{e}");
				}
			}
			if (FinalizerMethodInfo != null)
			{
				try
				{
					PerformanceFishMod.Harmony.Unpatch(method, FinalizerMethodInfo);
				}
				catch (Exception e)
				{
					Log.Error($"Performance Fish encountered an exception when unpatching {FinalizerMethodInfo.FullDescription()} from {method.FullDescription()}:\n{e}");
				}
			}
		}
	}

	private static int TryGetPriority(MethodInfo info) => info?.TryGetAttribute<HarmonyPriority>()?.info.priority ?? Priority.Normal;

	public virtual void ExposeData()
		=> Scribe_Values.Look(ref _enabled, "enabled", DefaultState);
}

internal static class Benchmarking
{
	internal static int SwitchFrame = int.MaxValue;
	internal static int EndFrame = int.MaxValue;

	public static void Prefix(MethodBase __originalMethod, out Results __state)
	{
		if (!Cache.ByReference<MethodBase, Results>.Get.TryGetValue(__originalMethod, out __state))
			Cache.ByReference<MethodBase, Results>.Get[__originalMethod] = __state = new(__originalMethod);

		__state.Stopwatch.Start();
	}

	public static void Postfix(Results __state)
	{
		__state.Stopwatch.Stop();
		__state.Count++;

		//Log.Message($"SwitchFrame is {SwitchFrame}, EndFrame is {EndFrame}, CurrentFrame is {Time.frameCount}");

		//if (__state.Stopwatch.ElapsedMilliseconds > 100 || __state.Count > 100000)
		if (__state.Switched ? GenTicks.TicksGame > EndFrame : GenTicks.TicksGame > SwitchFrame)
			PrintResultsAndStop(__state);
	}

	public static void PrintResultsAndStop(Results results)
	{
		var ticks = results.Stopwatch.ElapsedTicks;
		Log.Message($"{results.Method.FullDescription()} took {ticks} ticks for {results.Count} calls. This equals {ticks * 1000L / results.Count} ticks per 1000 calls on average.");

		results.Count = 0;
		results.Switched = true;
		results.Stopwatch.Reset();

		var fishPatch = PerformanceFishMod.AllPatchClasses
			.SelectMany(patchClass
				=> patchClass.Patches)
			.First(patch
				=> patch.TargetMethodInfos.Contains(results.Method));

		if (fishPatch.FinishedBenchmarking)
		{
			foreach (var target in fishPatch.TargetMethodInfos)
			{
				if (AccessTools.EnumeratorMoveNext(target) is { } moveNext)
				{
					PerformanceFishMod.Harmony.Unpatch(moveNext, methodof(Prefix));
					PerformanceFishMod.Harmony.Unpatch(moveNext, methodof(Postfix));
				}

				PerformanceFishMod.Harmony.Unpatch(target, methodof(Prefix));
				PerformanceFishMod.Harmony.Unpatch(target, methodof(Postfix));
			}
		}
		else
		{
			fishPatch.FinishedBenchmarking = true;
			fishPatch.TryPatch();
		}
	}

	public class Results
	{
		public int Count;
		public Stopwatch Stopwatch;
		public MethodBase Method;
		public bool Switched;

		public Results(MethodBase method)
		{
			Stopwatch = new();
			Method = method;
		}
	}
}

public abstract class FirstPriorityFishPatch : FishPatch
{
	public override int PrefixMethodPriority => Priority.First;
	public override int PostfixMethodPriority => Priority.First;
	public override int TranspilerMethodPriority => Priority.First;
	public override int FinalizerMethodPriority => Priority.First;
}

public static class FishPatchExtensions
{
	public static void PatchAll(this IEnumerable<FishPatch> patches)
	{
		foreach (var patch in patches)
			patch.TryPatch();
	}
	public static void UnpatchAll(this IEnumerable<FishPatch> patches)
	{
		foreach (var patch in patches)
			patch.TryUnpatch();
	}
}

public class FishPatchHolder : IExposable, IEnumerable<FishPatch>
{
	public ConcurrentDictionary<Type, FishPatch> All => _all;
	public T Get<T>() where T : FishPatch => (T)All[typeof(T)];
	public void Add(FishPatch patch) => All[patch.GetType()] = patch;
	public void PatchAll() => All.Values.PatchAll();
	public void UnpatchAll() => All.Values.UnpatchAll();
	public FishPatch this[Type type] => All[type];

	public FishPatchHolder(Type type)
	{
		_type = type;
		AddPatchesRecursively(type);
	}

	public void Scribe() => ExposeData(); // Exposable.Scribe(this, _type.FullName); // directly calling ExposeData prevents creating nested nodes in the config file. Looks cleaner imo.

	public void ExposeData()
	{
		foreach (var (type, patch) in All)
			Exposable.Scribe(patch, type.FullName);
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
		var dupeClasses = patches
			.Where(patchClass
				=> patchClass.GetType() != _type
				&& patchClass.Patches.All.ContainsKey(type));

		if (!dupeClasses.Any())
			return;

		foreach (var patchClass in dupeClasses)
		{
			patchClass.Patches[type].Enabled = false;
			patchClass.Patches.All.TryRemove(type, out _);
			Log.Warning($"Performance Fish removed a duplicate patch from {patchClass.GetType().FullName}. This is likely caused by no longer valid mod configs");
		}
	}

	public IEnumerator<FishPatch> GetEnumerator() => All.Values.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => All.Values.GetEnumerator();

	private ConcurrentDictionary<Type, FishPatch> _all = new();
	private Type _type;
}

public interface IHasFishPatch
{
	public FishPatchHolder Patches { get; }
	public bool RequiresLoadedGameForPatching { get; }
}

public interface IHasDescription
{
	public string? Description { get; }
}

public abstract class ClassWithFishPatches : SingletonFactory<ClassWithFishPatches>, IHasFishPatch
{
	public virtual FishPatchHolder Patches { get; }
	public virtual bool RequiresLoadedGameForPatching => false;

	protected ClassWithFishPatches() => Patches = new(GetType());
}