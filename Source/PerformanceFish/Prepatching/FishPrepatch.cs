// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using MonoMod.Utils;

namespace PerformanceFish.Prepatching;

public abstract class FishPrepatch : FishPrepatchBase
{
	public abstract MethodBase TargetMethodBase { get; }
	
	public virtual MethodInfo? PrefixMethodInfo
		=> TryGetMethod("PREFIX", static m => m.HasAttribute<HarmonyPrefix>());

	public virtual MethodInfo? PostfixMethodInfo
		=> TryGetMethod("POSTFIX", static m => m.HasAttribute<HarmonyPostfix>());

	public virtual void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
	{
	}

	private MethodInfo? TryGetMethod(string nameCaseInsensitive, Predicate<MethodInfo>? predicate = null)
		=> GetType().TryGetMethod(nameCaseInsensitive, predicate);

	protected internal override void ApplyPatch(ModuleDefinition module)
	{
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
		var patchMethod = TargetMethodBase
			?? ThrowHelper.ThrowInvalidOperationException<MethodBase>(
				$"Patch {GetType().FullName} has no target method.");

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
		
		DebugLog.Message($"Applying prepatch {GetType().FullName} on method {
			patchMethod.FullDescription()}");

		var targetType = module.GetTypeDefinition(patchType) ?? ThrowForFailureToFindType(patchType);
		var targetMethod = targetType.GetMethod(patchMethod.Name, patchMethod.GetParameters().GetTypes())
			?? ThrowForFailureToFindMethod(targetType, patchMethod);
		
		// PrepatchManager.AddSecurityAttributes(targetMethod.SecurityDeclarations, module, targetMethod.FullName);

		var targetMethodBody = targetMethod.Body;
		var ilProcessor = targetMethodBody.GetILProcessor();
		
		try
		{
			Transpiler(ilProcessor, module);
		}
		catch (Exception e)
		{
			Log.Error($"Exception while applying transpiler for {GetType().FullName}\n{e}\n{new StackTrace(true)}");
		}

		var patchVariables = new PatchVariables();

		if (PrefixMethodInfo is { } prefixMethod)
		{
			try
			{
				ApplyPrefix(prefixMethod, ilProcessor, module, ref patchVariables);
			}
			catch (Exception e)
			{
				Log.Error($"Exception while applying prefix for {GetType().FullName}\n{e}\n{new StackTrace(true)}");
			}
		}

		if (PostfixMethodInfo is { } postfixMethod)
		{
			try
			{
				ApplyPostfix(postfixMethod, ilProcessor, module, ref patchVariables);
			}
			catch (Exception e)
			{
				Log.Error($"Exception while applying postfix for {GetType().FullName}\n{e}\n{new StackTrace(true)}");
			}
		}
		
		targetMethodBody.SimplifyMacros();
		targetMethodBody.OptimizeMacros();
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
			ilProcessor.InsertAt(index++, (OpCodes.Brtrue, startOfOriginal));
			
			if (resultVariable != null)
			{
				ilProcessor.InsertAt(index++, (OpCodes.Ldloc, resultVariable));
			}
			else if (targetMethod.ReturnType.FullName != module.TypeSystem.Void.FullName)
			{
				Log.Error($"Prefix {
					prefixMethodInfo.FullDescription()} with return type of bool must assign to a __result parameter");
			}

			ilProcessor.InsertAt(index, OpCodes.Ret);
		}
	}
	
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
			postfixInstructions.Add((OpCodes.Stloc, resultVariable));

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
			postfixInstructions.Add((OpCodes.Ldloc, resultVariable));
		
		Span<int> retInstructions = stackalloc int[instructions.Count];
		var retInstructionCount = 0;
		
		for (var i = 0; i < instructions.Count; i++)
		{
			if (instructions[i].OpCode == OpCodes.Ret)
				retInstructions[retInstructionCount++] = i;
		}

		var lastRetInstructionIndex = retInstructions[retInstructionCount - 1];
		ilProcessor.InsertRange(lastRetInstructionIndex, postfixInstructions);

		for (var i = 0; i < instructions.Count; i++)
		{
			var instruction = instructions[i];
			if (instruction.Operand is Instruction branchTarget && branchTarget.OpCode == OpCodes.Ret)
				instruction.Operand = instructions[lastRetInstructionIndex];
		}

		for (var i = 0; i < retInstructionCount - 1; i++)
		{
			var instruction = instructions[retInstructions[i]];
			instruction.OpCode = OpCodes.Br;
			instruction.Operand = instructions[lastRetInstructionIndex];
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

	private static ITuple GetInstructionForParameter(ModuleDefinition module, ParameterInfo parameter,
		ParameterInfo? resultParameter, VariableDefinition? resultVariable, ParameterInfo? stateParameter,
		VariableDefinition? stateVariable, MethodDefinition targetMethod)
		=> parameter == resultParameter
			? (parameter.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc, resultVariable)
			: parameter == stateParameter
				? (parameter.ParameterType.IsByRef ? OpCodes.Ldloca : OpCodes.Ldloc, stateVariable)
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

	private static Collection<GenericParameter>? GetGenericParameters(MethodDefinition targetMethod)
		=> targetMethod.HasGenericParameters ? targetMethod.GenericParameters
			: targetMethod.DeclaringType.HasGenericParameters ? targetMethod.DeclaringType.GenericParameters
			: null;

	private static Type GetUnderlyingType(Type type) => type.IsByRef ? type.GetElementType()! : type;

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
				? OpCodes.Ldarga
				: OpCodes.Ldarg, targetParameter);
	}

	private static TypeReference GetUnderlyingType(ParameterInfo prefixParameter, ModuleDefinition module)
		=> module.ImportReference(GetUnderlyingType(prefixParameter.ParameterType));

	private static TypeReference GetUnderlyingType(ParameterDefinition prefixParameter)
		=> prefixParameter.ParameterType.IsByReference ? prefixParameter.ParameterType.GetElementType()!
			: prefixParameter.ParameterType;

	private static MethodDefinition ThrowForFailureToFindMethod(TypeDefinition? type, MethodBase patchMethod)
		=> ThrowHelper.ThrowInvalidOperationException<MethodDefinition>(
			$"Failed to find method with type {type} and name {patchMethod.Name}");

	private record struct PatchVariables
	{
		public VariableDefinition? Result, State;
	}
}