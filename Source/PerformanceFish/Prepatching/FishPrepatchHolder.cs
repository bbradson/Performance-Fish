// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Collections.Concurrent;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using MonoMod.Utils;
using nuget::JetBrains.Annotations;

namespace PerformanceFish.Prepatching;

[PublicAPI]
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public class FishPrepatchHolder : IExposable, IEnumerable<FishPrepatchBase>
{
	public ConcurrentDictionary<Type, FishPrepatchBase> All { get; } = new();

	public T Get<T>() where T : FishPrepatchBase => (T)All[typeof(T)];
	public void Add(FishPrepatchBase patch) => All[patch.GetType()] = patch;
	public void PatchAll(ModuleDefinition module)
	{
		var patches = All.Values;
		foreach (var patch in patches)
		{
			if (!patch.Enabled)
				continue;
			
			try
			{
				switch (patch)
				{
					case FishPrepatch methodPatch:
						ApplyMethodPatch(module, methodPatch);
						break;
					case FishClassPrepatch classPatch:
						ApplyClassPatch(module, classPatch);
						break;
				}
			}
			catch (Exception ex)
			{
				Log.Error($"Exception while applying prepatches for {PerformanceFishMod.NAME}:\n{ex}");
			}
		}
	}

	private static void ApplyMethodPatch(ModuleDefinition module, FishPrepatch patch)
	{
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
		var patchMethod = patch.TargetMethodBase
			?? ThrowHelper.ThrowInvalidOperationException<MethodBase>(
				$"Patch {patch.GetType().FullName} has no target method.");

		if (patchMethod.IsGenericMethod && patchMethod is MethodInfo info)
			patchMethod = info.GetGenericMethodDefinition();

		var patchType = patchMethod.ReflectedType
			?? ThrowHelper.ThrowInvalidOperationException<Type>(
				$"Method {patchMethod.FullDescription()} has no declaring type.");

		if (patchMethod.ReflectedType != patchMethod.DeclaringType)
		{
			ThrowHelper.ThrowInvalidOperationException($"Tried to apply patch on method {
				patchMethod.Name} for type {patchMethod.ReflectedType}, but it is only declared in {
					patchMethod.DeclaringType}.");
		}

		if (patchType.IsGenericType)
		{
			patchType = patchType.GetGenericTypeDefinition();
			patchMethod = patchMethod is MethodInfo
				? AccessTools.DeclaredMethod(patchType, patchMethod.Name, patchMethod.GetParameters().GetTypes())
				: AccessTools.Constructor(patchType, patchMethod.GetParameters().GetTypes(), patchMethod.IsStatic);
		}
		
		DebugLog.Message($"Applying prepatch {patch.GetType().FullName} on method {
			patchMethod.FullDescription()}");

		var targetType = module.GetTypeDefinition(patchType) ?? ThrowForFailureToFindType(patchType);
		var targetMethod = targetType.GetMethod(patchMethod.Name, patchMethod.GetParameters().GetTypes())
			?? ThrowForFailureToFindMethod(targetType, patchMethod);
		
		// PrepatchManager.AddSecurityAttributes(targetMethod.SecurityDeclarations, module, targetMethod.FullName);
		
		var ilProcessor = targetMethod.Body.GetILProcessor();
		
		try
		{
			patch.Transpiler(ilProcessor, module);
		}
		catch (Exception e)
		{
			Log.Error($"Exception while applying transpiler for {patch.GetType().FullName}\n{e}\n{
				new StackTrace() /*StackTraceUtility.ExtractStackTrace()*/}");
		}

		var patchVariables = new PatchVariables();

		if (patch.PrefixMethodInfo is { } prefixMethod)
		{
			try
			{
				ApplyPrefix(prefixMethod, ilProcessor, module, ref patchVariables);
			}
			catch (Exception e)
			{
				Log.Error($"Exception while applying prefix for {patch.GetType().FullName}\n{e}\n{
					new StackTrace() /*StackTraceUtility.ExtractStackTrace()*/}");
			}
		}

		if (patch.PostfixMethodInfo is { } postfixMethod)
		{
			try
			{
				ApplyPostfix(postfixMethod, ilProcessor, module, ref patchVariables);
			}
			catch (Exception e)
			{
				Log.Error($"Exception while applying postfix for {patch.GetType().FullName}\n{e}\n{
					new StackTrace() /*StackTraceUtility.ExtractStackTrace()*/}");
			}
		}

		targetMethod.Body.OptimizeMacros();
	}

	private static void ApplyPrefix(MethodInfo prefixMethodInfo, ILProcessor ilProcessor, ModuleDefinition module,
		ref PatchVariables patchVariables)
	{
		if (prefixMethodInfo.ReturnType != typeof(bool) && prefixMethodInfo.ReturnType != typeof(void))
		{
			ThrowHelper.ThrowArgumentException($"Prefix has invalid return type: {
				prefixMethodInfo.ReturnType.FullName}. Must be void or bool");
		}

		var instructions = ilProcessor.instructions;
		var targetMethod = ilProcessor.GetMethod();

		var genericParameters = GetGenericParameters(targetMethod);
		var prefixParameters = prefixMethodInfo.GetParameters();
		
		var resultParameter = prefixParameters.TryGetNamed("__result");
		VerifyResultParameter(module, resultParameter, targetMethod, true);

		var stateParameter = prefixParameters.TryGetNamed("__state");
		VerifyStateParameter(stateParameter);

		// var skipOriginalFlag = ilProcessor.DeclareLocal(typeof(bool));
		var resultVariable = GetResultVariable(ilProcessor, ref patchVariables, resultParameter, targetMethod);
		var stateVariable = GetStateVariable(ilProcessor, ref patchVariables, stateParameter);
		var startOfOriginal = instructions[0];

		var index = 0;
		foreach (var parameter in prefixParameters)
		{
			ilProcessor.InsertAt(index++, GetInstructionForParameter(module, parameter, resultParameter, resultVariable,
				stateParameter, stateVariable, targetMethod));
		}
		
		if (genericParameters != null)
		{
			ilProcessor.InsertAt(index++, OpCodes.Call,
				module.ImportMethodWithGenericArguments(prefixMethodInfo, genericParameters));
		}
		else
		{
			ilProcessor.InsertAt(index++, OpCodes.Call, prefixMethodInfo);
		}

		if (prefixMethodInfo.ReturnType == typeof(bool))
		{
			ilProcessor.InsertAt(index++, (OpCodes.Brtrue_S, startOfOriginal));
			
			if (resultVariable != null)
			{
				ilProcessor.InsertAt(index++, (OpCodes.Ldloc_S, resultVariable));
			}
			else if (targetMethod.ReturnType.FullName != module.TypeSystem.Void.FullName)
			{
				Log.Error($"Prefix {prefixMethodInfo.FullDescription()
				} with return type of bool must assign to a __result parameter");
			}

			ilProcessor.InsertAt(index, OpCodes.Ret);
		}
	}

	private static VariableDefinition? GetStateVariable(ILProcessor ilProcessor, ref PatchVariables patchVariables,
		ParameterInfo? stateParameter)
		=> stateParameter != null
			? patchVariables.State ??= ilProcessor.DeclareLocal(GetUnderlyingType(stateParameter.ParameterType))
			: null;

	private static VariableDefinition? GetResultVariable(ILProcessor ilProcessor, ref PatchVariables patchVariables,
		ParameterInfo? resultParameter, MethodDefinition targetMethod)
		=> resultParameter != null
			? patchVariables.Result ??= ilProcessor.DeclareLocal(targetMethod.ReturnType)
			: null;

	private static void VerifyStateParameter(ParameterInfo? stateParameter)
	{
		if (stateParameter is { ParameterType.IsByRef: false })
			ThrowHelper.ThrowArgumentException("__state parameter must be declared with ref or out keyword.");
	}

	private static Collection<GenericParameter>? GetGenericParameters(MethodDefinition targetMethod)
		=> targetMethod.HasGenericParameters ? targetMethod.GenericParameters
			: targetMethod.DeclaringType.HasGenericParameters ? targetMethod.DeclaringType.GenericParameters
			: null;

	private static Type GetUnderlyingType(Type type) => type.IsByRef ? type.GetElementType()! : type;

	private static void ApplyPostfix(MethodInfo postfixMethodInfo, ILProcessor ilProcessor, ModuleDefinition module,
		ref PatchVariables patchVariables)
	{
		var targetMethod = ilProcessor.GetMethod();
		
		if (postfixMethodInfo.ReturnType != typeof(void)
			&& !postfixMethodInfo.ReturnType.Is(targetMethod.ReturnType))
		{
			ThrowHelper.ThrowArgumentException($"Postfix has invalid return type: {
				postfixMethodInfo.ReturnType.FullName}. Must be void or {targetMethod.ReturnType}");
		}

		var instructions = ilProcessor.instructions;
		var genericParameters = GetGenericParameters(targetMethod);
		var postfixParameters = postfixMethodInfo.GetParameters();
		
		var resultParameter = postfixParameters.TryGetNamed("__result");
		VerifyResultParameter(module, resultParameter, targetMethod, false);

		var stateParameter = postfixParameters.TryGetNamed("__state");
		// Verify? It would make sense to match against prefix state

		// var skipOriginalFlag = ilProcessor.DeclareLocal(typeof(bool));
		var resultVariable = GetResultVariable(ilProcessor, ref patchVariables, resultParameter, targetMethod);
		var stateVariable = GetStateVariable(ilProcessor, ref patchVariables, stateParameter);

		var postfixInstructions = new Collection<object>();
		
		if (resultVariable != null)
			postfixInstructions.Add((OpCodes.Stloc_S, resultVariable));

		foreach (var parameter in postfixParameters)
		{
			postfixInstructions.Add(GetInstructionForParameter(module, parameter, resultParameter, resultVariable,
				stateParameter, stateVariable, targetMethod));
		}
		
		if (genericParameters != null)
		{
			postfixInstructions.Add((OpCodes.Call,
				module.ImportMethodWithGenericArguments(postfixMethodInfo, genericParameters)));
		}
		else
		{
			postfixInstructions.Add((OpCodes.Call, postfixMethodInfo));
		}

		if (postfixMethodInfo.ReturnType == typeof(void) && resultVariable != null)
			postfixInstructions.Add((OpCodes.Ldloc_S, resultVariable));
		
		// TODO: use label instead of inserting multiple times
		for (var i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].OpCode != OpCodes.Ret)
				continue;
				
			ilProcessor.InsertRange(i, postfixInstructions);

			i += postfixInstructions.Count;
		}
	}

	private static ITuple GetInstructionForParameter(ModuleDefinition module, ParameterInfo parameter,
		ParameterInfo? resultParameter, VariableDefinition? resultVariable, ParameterInfo? stateParameter,
		VariableDefinition? stateVariable, MethodDefinition targetMethod)
		=> parameter == resultParameter
			? (parameter.ParameterType.IsByRef ? OpCodes.Ldloca_S : OpCodes.Ldloc_S, resultVariable)
			: parameter == stateParameter
				? (parameter.ParameterType.IsByRef ? OpCodes.Ldloca_S : OpCodes.Ldloc_S, stateVariable)
				: GetMatchingTargetParameter(targetMethod, parameter, module);

	private static void VerifyResultParameter(ModuleDefinition module, ParameterInfo? resultParameter,
		MethodDefinition targetMethod, bool requiresByRef)
	{
		if (resultParameter is null)
			return;

		if (targetMethod.ReturnType.IsGeneric() ? !GetUnderlyingType(resultParameter.ParameterType).IsGeneric()
			: !GetUnderlyingType(resultParameter.ParameterType).Is(targetMethod.ReturnType))
		{
			// var firstType = targetMethod.ReturnType;
			// var secondType = GetUnderlyingType(resultParameter.ParameterType);
			// Log.Message($"Type FullName: {firstType.FullName
			// }: ContainsGenericParameter: {firstType.ContainsGenericParameter
			// }, HasGenericParameters :{firstType.HasGenericParameters
			// }, IsGenericInstance: {firstType.IsGenericInstance
			// }, Type FullName: {secondType.FullName
			// } ContainsGenericParameters: {secondType.ContainsGenericParameters
			// }, IsGenericType {secondType.IsGenericType
			// }, IsConstructedGenericType: {secondType.IsConstructedGenericType}");
			
			ThrowHelper.ThrowArgumentException($"__result parameter has invalid type: {
				resultParameter.ParameterType.FullName}. Must match the target's return type: {
					targetMethod.ReturnType.FullName}");
		}
		if (requiresByRef && !resultParameter.ParameterType.IsByRef)
		{
			ThrowHelper.ThrowArgumentException(
				"__result parameter must be declared with ref or out keyword.");
		}
	}

	private record struct PatchVariables
	{
		public VariableDefinition? Result, State;
	}

	private static ITuple GetMatchingTargetParameter(MethodDefinition targetMethod,
		ParameterInfo parameter, ModuleDefinition module)
	{
		var targetParameters = targetMethod.Parameters;
		var targetParameter = parameter.Name == "__instance"
			? targetMethod.Body.ThisParameter
			: targetParameters.TryGetNamed(parameter.Name);
		if (targetParameter is null)
		{
			return ThrowHelper.ThrowArgumentException<ITuple>($"No parameter named {
				parameter.Name} found in target method");
		}

		var targetParameterType = targetParameter.ParameterType;
		var patchParameterType = parameter.ParameterType;

		return GetUnderlyingType(targetParameter).Name != GetUnderlyingType(patchParameterType).Name
			? ThrowHelper.ThrowArgumentException<ITuple>($"Parameter has invalid type: {
				patchParameterType.FullName}. Must match the target method parameter's type: {
					targetParameterType.FullName}")
			: (patchParameterType.IsByRef && !targetParameter.ParameterType.IsByReference
				? OpCodes.Ldarga_S
				: OpCodes.Ldarg_S, targetParameter);
	}

	private static TypeReference GetUnderlyingType(ParameterInfo prefixParameter, ModuleDefinition module)
		=> module.ImportReference(GetUnderlyingType(prefixParameter.ParameterType));

	private static TypeReference GetUnderlyingType(ParameterDefinition prefixParameter)
		=> prefixParameter.ParameterType.IsByReference ? prefixParameter.ParameterType.GetElementType()!
			: prefixParameter.ParameterType;

	private static TypeDefinition ThrowForFailureToFindType(Type patchType)
		=> ThrowHelper.ThrowInvalidOperationException<TypeDefinition>(
			$"Failed to find type with namespace {patchType.Namespace} and name {patchType.Name}");

	private static MethodDefinition ThrowForFailureToFindMethod(TypeDefinition? type, MethodBase patchMethod)
		=> ThrowHelper.ThrowInvalidOperationException<MethodDefinition>(
			$"Failed to find method with type {type} and name {patchMethod.Name}");

	private static void ApplyClassPatch(ModuleDefinition module, FishClassPrepatch patch)
	{
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
		var patchType = patch.Type
			?? ThrowHelper.ThrowInvalidOperationException<Type>(
				$"Patch {patch.GetType().FullName} has no target type.");

		if (patchType.IsGenericType)
			patchType = patchType.GetGenericTypeDefinition();

		DebugLog.Message($"Applying prepatch {patch.GetType().FullName} on type {
			patchType.FullName}");

		var targetType = module.GetType(patchType.Namespace, patchType.Name) ?? ThrowForFailureToFindType(patchType);
		
		// PrepatchManager.AddSecurityAttributes(targetType.SecurityDeclarations, module);
		
		patch.FreePatch(targetType);
	}

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