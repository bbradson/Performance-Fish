// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using PerformanceFish.Events;
using PerformanceFish.Prepatching;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodImplAttributes = Mono.Cecil.MethodImplAttributes;

namespace PerformanceFish.Defs;

public sealed class ThingDefPatches : ClassWithFishPrepatches
{
	public const string FISH_HASH_FIELD_NAME = "FishHash";
	
	public sealed class GetHashCodePatch : FishClassPrepatch
	{
		public override string? Description { get; }
			= "Def.GetHashCode is overridden to return defName.GetHashCode, which is rather slow as that's a string. "
			+ "This patch caches the hashcode in a field added through prepatcher and simply returns that instead.";

		public override Type Type { get; } = typeof(BuildableDef);

		public override void FreePatch(TypeDefinition typeDefinition)
		{
			var module = typeDefinition.Module;

			var fishHashField = new FieldDefinition(FISH_HASH_FIELD_NAME, FieldAttributes.Private,
				module.TypeSystem.Int32);
			
			typeDefinition.Fields.Insert(0, fishHashField);

			var constructorBody = typeDefinition.GetConstructors().First().Body;
			
			constructorBody.GetILProcessor().InsertRange(constructorBody.Instructions.Count - 2,
				OpCodes.Ldarg_0,
				(OpCodes.Ldc_I4, -42),
				(OpCodes.Stfld, fishHashField));
			
			constructorBody.OptimizeMacros();
			
			var getHashCodeBody = typeDefinition.GetMethod(nameof(BuildableDef.GetHashCode))!.Body;

			var variables = getHashCodeBody.Variables;
			variables.Clear();
			
			var hashVariable = new VariableDefinition(module.TypeSystem.Int32);
			variables.Add(hashVariable);

			var instructions = getHashCodeBody.Instructions;
			instructions.Clear();

			var ilProcessor = getHashCodeBody.GetILProcessor();
			ilProcessor.InsertRange(0,
				OpCodes.Ldarg_0,
				(OpCodes.Ldfld, fishHashField),
				(OpCodes.Stloc, hashVariable),
				(OpCodes.Ldloc, hashVariable),
				(OpCodes.Ldc_I4, -42),
				// 5 bne_un_s ^2
				OpCodes.Ldarg_0,
				(OpCodes.Call, methodof(CreateLongHash)),
				OpCodes.Ret,
				(OpCodes.Ldloc, hashVariable),
				OpCodes.Ret);
			
			ilProcessor.InsertAt(5, OpCodes.Bne_Un_S, instructions[^2]);

			getHashCodeBody.MaxStackSize = 2;
			getHashCodeBody.Method.ImplAttributes |= MethodImplAttributes.AggressiveInlining;
			getHashCodeBody.OptimizeMacros();
		}

		// [MethodImpl(MethodImplOptions.AggressiveInlining)]
		// public static int ReplacementBody(BuildableDef instance)
		// {
		// 	var hash = instance.FishHash;
		// 	return hash != -42 ? hash : CreateLongHash(instance);
		// }

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static int CreateLongHash(BuildableDef instance)
		{
			var defName = instance.defName;
			if (defName is null)
				return 0;
			
			var defHash = defName.GetHashCode();
			if (defName is "" or Def.DefaultDefName)
				return defHash;

			ref var cacheHash = ref Lazy.FishHashField(instance);
			cacheHash = defHash != -42 ? defHash : defHash + 1;
			return cacheHash;
		}

		private static class Lazy
		{
			internal static readonly AccessTools.FieldRef<BuildableDef, int> FishHashField
				= AccessTools.FieldRefAccess<BuildableDef, int>(FISH_HASH_FIELD_NAME);
		
			static Lazy()
			{
				// prevent beforefieldinit
			}
		}
	}

	public sealed class BaseMarketValuePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches the frequently accessed and never changing ThingDef.BaseMarketValue property. Also adds proper "
			+ "exception handling in case of errors in the market value calculation.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(ThingDef), nameof(ThingDef.BaseMarketValue));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReplacementBody(ThingDef instance)
		{
			var cacheValue = instance.BaseStatsCache()[(int)Stats.MarketValue];
			
			return !float.IsNaN(cacheValue) ? cacheValue : GetUpdatedValue(instance);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float GetUpdatedValue(ThingDef instance)
			=> Current.Game is null
				? GetValueSafely(instance)
				: instance.BaseStatsCache()[(int)Stats.MarketValue] = GetValueSafely(instance);

		public static float GetValueSafely(ThingDef instance)
			=> ThingDefPatches.GetValueSafely(instance, StatDefOf.MarketValue);
		
		public static float LogAndFixMarketValue(BuildableDef def, ThingDef? stuff, Exception exception)
		{
			Guard.IsNotNull(def);
			Log.Error($"Exception thrown while calculating {StatDefOf.MarketValue.label} for def '{
				def.defName}' from mod '{
					def.modContentPack?.Name ?? "null"}'. Attempting to fix this now so it doesn't happen again.\n{
						exception}");

			RecipeDef? recipe;
			try
			{
				recipe = StatWorker_MarketValue.CalculableRecipe(def);
				// this is slow, but gets cached by perf optimizer. Might be worth replicating
			}
			catch (Exception ex)
			{
				Log.Error($"Exception thrown while calculating recipe for def '{def.defName}' from mod '{
					def.modContentPack?.Name ?? "null"}'.\n{ex}");
				recipe = null;
			}

			if (recipe?.ingredients is { } ingredients)
				ingredients.RemoveAll(static ingredientCount => ingredientCount is null);
			
			if (def.CostList is { } costList)
				costList.RemoveAll(static thingDefCountClass => thingDefCountClass?.thingDef is null);

			try
			{
				return def.GetStatValueAbstract(StatDefOf.MarketValue, stuff);
			}
			catch (Exception ex)
			{
				Log.Error($"Exception thrown while calculating market value for def '{def.defName}' from mod '{
					def.modContentPack?.Name ?? "null"}' again. Fix failed, F.\n{ex}");
				
				return 0f;
			}
		}

		static BaseMarketValuePatch()
			=> StaticEvents.StaticConstructorOnStartupCalled
				+= static () => StatCaching.StatExceptionHandlers[StatDefOf.MarketValue] = LogAndFixMarketValue;
	}

	public sealed class BaseMassPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches the frequently accessed and never changing ThingDef.BaseMass property. Also adds basic "
			+ "exception handling in case of errors in the mass calculation.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(ThingDef), nameof(ThingDef.BaseMass));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReplacementBody(ThingDef instance)
		{
			var cacheValue = instance.BaseStatsCache()[(int)Stats.Mass];
			
			return !float.IsNaN(cacheValue) ? cacheValue : GetUpdatedValue(instance);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float GetUpdatedValue(ThingDef instance)
			=> Current.Game is null
				? GetValueSafely(instance)
				: instance.BaseStatsCache()[(int)Stats.Mass] = GetValueSafely(instance);

		public static float GetValueSafely(ThingDef instance)
			=> ThingDefPatches.GetValueSafely(instance, StatDefOf.Mass);
	}

	public sealed class BaseFlammabilityPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches the frequently accessed and never changing ThingDef.BaseFlammability property. Also adds basic "
			+ "exception handling in case of errors in the mass calculation.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(ThingDef), nameof(ThingDef.BaseFlammability));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ReplacementBody(ThingDef instance)
		{
			var cacheValue = instance.BaseStatsCache()[(int)Stats.Flammability];
			
			return !float.IsNaN(cacheValue) ? cacheValue : GetUpdatedValue(instance);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float GetUpdatedValue(ThingDef instance)
			=> Current.Game is null
				? GetValueSafely(instance)
				: instance.BaseStatsCache()[(int)Stats.Flammability] = GetValueSafely(instance);

		public static float GetValueSafely(ThingDef instance)
			=> ThingDefPatches.GetValueSafely(instance, StatDefOf.Flammability);
	}

	public sealed class BaseMaxHitPointsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches the frequently accessed and never changing ThingDef.BaseMaxHitPoints property. Also adds basic "
			+ "exception handling in case of errors in the mass calculation.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(ThingDef), nameof(ThingDef.BaseMaxHitPoints));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ReplacementBody(ThingDef instance)
		{
			var cacheValue = instance.BaseStatsCache()[(int)Stats.MaxHitPoints];
			
			return Mathf.RoundToInt(!float.IsNaN(cacheValue) ? cacheValue : GetUpdatedValue(instance));
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static float GetUpdatedValue(ThingDef instance)
			=> Current.Game is null
				? GetValueSafely(instance)
				: instance.BaseStatsCache()[(int)Stats.MaxHitPoints] = GetValueSafely(instance);

		public static float GetValueSafely(ThingDef instance)
			=> ThingDefPatches.GetValueSafely(instance, StatDefOf.MaxHitPoints);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static float GetValueSafely(ThingDef instance, StatDef stat)
	{
		try
		{
			return instance.GetStatValueAbstract(stat);
		}
		catch (Exception ex)
		{
			return StatCaching.StatExceptionHandlers.GetOrAdd(stat)(instance, null, ex);
		}
	}

	public enum Stats
	{
		MarketValue,
		Mass,
		Flammability,
		MaxHitPoints
	}
}