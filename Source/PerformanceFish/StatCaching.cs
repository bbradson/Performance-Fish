// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Defs;
using PerformanceFish.Prepatching;
using StatsCache = PerformanceFish.Cache.ByInt<uint, ushort, PerformanceFish.StatCaching.CachedStatValue>;

namespace PerformanceFish;

public sealed class StatCaching : ClassWithFishPrepatches
{
	public sealed class SetStatBaseValuePatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; } = methodof(StatExtension.SetStatBaseValue);

		public static void Postfix(BuildableDef def, StatDef stat)
		{
			if (def is ThingDef thingDef)
				ResetToDefaults(thingDef.BaseStatsCache());
			
			ResetToDefaults(def.StatsCache());
			StatsCache.Clear();
		}
	}

	public sealed class GetStatValueAbstractPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caching for stats calculated from defs, which can be expected to not change during regular gameplay. "
			+ "Also adds basic exception handling in case of errors in their calculation.";

		public override MethodBase TargetMethodBase { get; }
			= methodof((Func<BuildableDef, StatDef, ThingDef, float>)StatExtension.GetStatValueAbstract);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static float ReplacementBody(BuildableDef def, StatDef stat, ThingDef? stuff = null)
		{
			var statsCache = def.StatsCache();
			if (statsCache.Length > stat.index)
			{
				var cacheValue = stuff is null ? statsCache[stat.index] : GetStuffed(def, stat, stuff);

				if (!float.IsNaN(cacheValue))
					return cacheValue;
			}

			return TryGetUpdatedValue(def, stat, stuff);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float GetStuffed(BuildableDef def, StatDef stat, ThingDef stuff)
			=> StatsCache.GetOrAdd(unchecked((int)DefPair.Create(def, stat)), stuff.shortHash).Value;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float TryGetUpdatedValue(BuildableDef def, StatDef stat, ThingDef? stuff)
			=> Current.Game is null ? GetValueSafely(def, stat, stuff) : GetUpdatedValue(def, stat, stuff);

		private static float GetUpdatedValue(BuildableDef def, StatDef stat, ThingDef? stuff)
		{
			ref var statsCache = ref def.StatsCache();
			if (statsCache.Length != DefDatabase<StatDef>.DefCount)
				statsCache = InitializeBuildableDefStatCache();

			ref var statBucket = ref stuff is null
				? ref statsCache[stat.index]
				: ref StatsCache.GetOrAddReference(unchecked((int)DefPair.Create(def, stat)), stuff.shortHash).Value;
			
			return statBucket = GetValueSafely(def, stat, stuff);
		}
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static float GetValueSafely(BuildableDef def, StatDef stat, ThingDef? stuff = null)
	{
		try
		{
			return stat.Worker.GetValueAbstract(def, stuff);
		}
		catch (Exception ex)
		{
			return StatExceptionHandlers.GetOrAdd(stat)(def, stuff, ex);
		}
	}
	
	public static float LogErrorInStat(BuildableDef def, ThingDef? stuff, Exception exception, string name)
	{
		Guard.IsNotNull(def);
		Log.Error($"Exception thrown while calculating {name} for def '{def.defName}' from mod '{
			def.modContentPack?.Name ?? "null"}'{(stuff != null ? $" with stuff '{stuff.defName}' from mod '{
				stuff.modContentPack?.Name ?? "null"}'" : "")}.\n{exception}");

		return 0f;
	}

	public static float[] InitializeBuildableDefStatCache()
	{
		TryVerifyIndices();
		var array = new float[DefDatabase<StatDef>.DefCount];
		ResetToDefaults(array);
		return array;
	}

	public static void ResetToDefaults(float[] statsCache) => Array.Fill(statsCache, float.NaN);

	public static void ResetAllStatsCaches()
	{
		var resetAllStatsCachesForDefTypeBaseMethod
			= methodof(ResetAllStatsCachesForDefType<BuildableDef>).GetGenericMethodDefinition();
		
		foreach (var defType in GenDefDatabase.AllDefTypesWithDatabases())
		{
			if (defType.IsAssignableTo(typeof(BuildableDef)))
				resetAllStatsCachesForDefTypeBaseMethod.MakeGenericMethod(defType).Invoke(null, null);
		}
		
		StatsCache.Clear();
	}

	public static void ResetAllStatsCachesForDefType<T>() where T : BuildableDef
	{
		var defsList = DefDatabase<T>.AllDefsListForReading;
		for (var i = 0; i < defsList.Count; i++)
		{
			var def = defsList[i];
			ResetToDefaults(def.StatsCache());
			
			if (def is ThingDef thingDef)
				ResetToDefaults(thingDef.BaseStatsCache());
		}
	}

	private static void TryVerifyIndices()
	{
		if (_verifiedIndices)
			return;

		VerifyIndices();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void VerifyIndices()
	{
		lock (_verifyIndicesLock)
		{
			if (Volatile.Read(ref _verifiedIndices))
				return;
			
			try
			{
				DefDatabase<StatDef>.SetIndices();
			}
			finally
			{
				Volatile.Write(ref _verifiedIndices, true);
			}
		}
	}

	private static bool _verifiedIndices;
	private static object _verifyIndicesLock = new();

	public static FishTable<StatDef, Func<BuildableDef, ThingDef?, Exception, float>> StatExceptionHandlers
		= new() { ValueInitializer = static stat => (def, stuff, ex) => LogErrorInStat(def, stuff, ex, stat.label) };

	public record struct CachedStatValue()
	{
		public float Value = float.NaN;
	}
}