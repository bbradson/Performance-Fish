// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

extern alias nuget;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using nuget::JetBrains.Annotations;
using PerformanceFish.ModCompatibility;
using CollectionExtensions = PerformanceFish.Utility.CollectionExtensions;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace PerformanceFish.Prepatching;

public static class PrepatchManager
{
	public static bool PatchingFinished { get; private set; }
	
	[UsedImplicitly]
	public static void Start(ModuleDefinition module)
	{
		if (PatchingFinished)
		{
			ThrowHelper.ThrowInvalidOperationException("Tried running free patches a second time. This should "
				+ "never happen. Cancelling now to avoid further issues.");
		}
		
		var gasFields = module.GetTypeDefinition(typeof(Gas))!.Fields;
		if (gasFields.Any(static field => field.Name == VERIFICATION_FIELD_NAME))
		{ // TODO: fix unity logger breaking when getting here
			UnityEngine.Debug.Log("Detected second prepatch attempt. This is known to happen after using "
				+ "Prepatcher's mod manager. Cancelling and restarting now to prevent issues");
			GenCommandLine.Restart();
		}
		else
		{
			gasFields.Add(new(VERIFICATION_FIELD_NAME,
				FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal,
				module.TypeSystem.Byte));
		}

		Log.Message(LogPatches.PERFORMANCE_FISH_WELCOME_MESSAGE);
		if (!ActiveMods.Prepatcher)
			Log.Warning("Prepatches running without prepatcher active. This should normally not happen.");

		var stopwatch = new Stopwatch();
		stopwatch.Start();
		
		// if (Type.GetType("Prepatcher.Process.FreePatcher, PrepatcherImpl") is { } freePatcherType
		// 	&& AccessTools.Method(freePatcherType, "AddSkipVerificationAttribute") is { } addSkipVerificationMethod
		// 	/*Type.GetType("HugsLib.Patches.MainMenuDrawer_Quickstart_Patch, HugsLib") is { } freePatcherType
		// 	&& AccessTools.Method(freePatcherType, "QuicktestButtonUsesQuickstarter") is { } addSkipVerificationMethod*/)
		// {
		// 	var rimworldAssembly = typeof(Root).Assembly;
		// 	var harmonyAssembly = typeof(Harmony).Assembly;
		// 	var prepatcherAssembly = freePatcherType.Assembly;
		// 	var fishAssembly = typeof(PerformanceFishMod).Assembly;
		// 	var monoModAssembly = typeof(MonoMod.Utils.Cil.ILGeneratorShim).Assembly;
		// 	
		// 	SetReflectionOnly(rimworldAssembly, false, out var previousRimworldValue);
		// 	SetReflectionOnly(harmonyAssembly, false, out var previousHarmonyValue);
		// 	SetReflectionOnly(prepatcherAssembly, false, out var previousPrepatcherValue);
		// 	SetReflectionOnly(monoModAssembly, false, out var previousMonoModValue);
		// 	SetReflectionOnly(fishAssembly, false, out var previousFishValue);
		// 	
		// 	PerformanceFishMod.Harmony.Patch(addSkipVerificationMethod,
		// 		prefix: new(methodof(FreePatcherSkipVerificationPatch)));
		// 	
		// 	SetReflectionOnly(rimworldAssembly, previousRimworldValue, out _);
		// 	SetReflectionOnly(harmonyAssembly, previousHarmonyValue, out _);
		// 	SetReflectionOnly(prepatcherAssembly, previousPrepatcherValue, out _);
		// 	SetReflectionOnly(fishAssembly, previousMonoModValue, out _);
		// 	SetReflectionOnly(fishAssembly, previousFishValue, out _);
		// }

		Application.logMessageReceivedThreaded -= Verse.Log.Notify_MessageReceivedThreadedInternal;

		AddAttributes(module);

		ModifyAllTypes(module);

		var allPrepatchClasses
			= PerformanceFishMod.AllPrepatchClasses
				= PerformanceFishMod.InitializeAllPatchClasses<ClassWithFishPrepatches>();

		_ = FishSettings.Instance;
		
		allPrepatchClasses.ApplyPatches(module);

		FishStash.Get.InitializeActivePrepatchIDs();
		PatchingFinished = true;
		stopwatch.Stop();
		Verse.Log.Message($"Performance Fish finished applying prepatches in {
			stopwatch.ElapsedSecondsAccurate():N7} seconds");
	}

	private const string VERIFICATION_FIELD_NAME = "PerformanceFishS1ngl3Pr3p4tchV3r1f1c4t10nF13ld";

	// private static readonly FieldInfo? _monoAssemblyField = AccessTools.Field(typeof(Assembly), "_mono_assembly");
	//
	// internal static unsafe void SetReflectionOnly(Assembly asm, bool value, out bool previousValue)
	// {
	// 	var refOnlyField = (int*)((IntPtr)_monoAssemblyField!.GetValue(asm) + 0x74);
	// 	previousValue = *refOnlyField == 1 || (*refOnlyField == 0 ? false : throw new());
	// 	*refOnlyField = value ? 1 : 0;
	// }
	//
	// public static bool FreePatcherSkipVerificationPatch() => false;

	private static void ModifyAllTypes(ModuleDefinition module)
	{
		var types = module.Types;
		
		// Parallel.For(0, types.Count, // about 20% faster than sequential for in testing
		// 	i => ModifyAllMethodsIn(types[i]));

		for (var i = 0; i < types.Count; i++)
			ModifyAllMethodsIn(types[i]);
	}

	private static void ModifyAllMethodsIn(TypeDefinition type)
	{
		if (type.Methods is not { } methods)
			return;
		
		for (var i = methods.Count; i-- > 0;)
		{
			if (methods[i].Body is { } body)
				ModifyMethodBody(body);
		}

		if (type.NestedTypes is not { } nestedTypes)
			return;

		for (var i = nestedTypes.Count; i-- > 0;)
			ModifyAllMethodsIn(nestedTypes[i]);
	}

	private static void ModifyMethodBody(MethodBody body)
	{
		ApplySkipLocalsInit(body);
		ModifyInstructions(body.Method, body.Instructions);
	}

	[UsedImplicitly(ImplicitUseTargetFlags.Members)]
	private enum LinqMethod
	{
		None,
		First,
		FirstOrDefault,
		Where,
		Any,
		Select,
		Count,
		Contains,
		Sum
	}

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	[SuppressMessage("ReSharper", "UnusedMember.Local")]
	private static class LinqStrings
	{
		public const string
			First = "First",
			FirstOrDefault = "FirstOrDefault",
			Where = "Where",
			Any = "Any",
			Select = "Select",
			Count = "Count",
			Contains = "Contains",
			Sum = "Sum",
			DeclaringType = "System.Linq.Enumerable";
	}

	private static void ModifyInstructions(MethodDefinition method, Collection<Instruction> instructions)
	{
		for (var i = 0; i < instructions.Count; i++)
		{
			var instruction = instructions[i];

			if (instruction.OpCode != OpCodes.Call)
				continue;

			if (i - 4 < 0)
				continue;

			if (!(i + 1 < instructions.Count
					&& instruction.Operand is MethodReference
					{
						Name: LinqStrings.Where, DeclaringType.FullName: LinqStrings.DeclaringType
					}
					&& instructions[i + 1].Operand is MethodReference
					{
						Name: LinqStrings.FirstOrDefault or LinqStrings.First,
						DeclaringType.FullName: LinqStrings.DeclaringType
					})
				&& instruction.Operand is not MethodReference
				{
					Name: LinqStrings.FirstOrDefault or LinqStrings.First,
					DeclaringType.FullName: LinqStrings.DeclaringType
				})
			{
				continue;
			}

			var linqTarget = LinqMethod.FirstOrDefault;

			if (((MethodReference)instruction.Operand).Name == LinqStrings.First
				|| (i + 1 < instructions.Count
					&& ((MethodReference)instruction.Operand).Name == LinqStrings.Where
					&& ((MethodReference)instructions[i + 1].Operand).Name == LinqStrings.First))
			{
				linqTarget = LinqMethod.First;
			}

			if (!(instructions[i - 1].OpCode == OpCodes.Newobj && instructions[i - 2].OpCode == OpCodes.Ldftn)
				&& !(instructions[i - 1].OpCode == OpCodes.Stsfld && instructions[i - 2].OpCode == OpCodes.Dup
				&& instructions[i - 3].OpCode == OpCodes.Newobj && instructions[i - 4].OpCode == OpCodes.Ldftn))
				continue;

			var inputMethodInstruction = instructions[i - 4];
			var inputMethod = inputMethodInstruction.OpCode == OpCodes.Call
				? inputMethodInstruction.Operand as MethodReference
				: null;
			
			if (inputMethod is null && i - 6 < 0)
				continue;

			if ((inputMethod ??= (inputMethodInstruction = instructions[i - 6]).Operand as MethodReference) is null)
				continue;
			
			var module = method.Module;

			if (inputMethod!.Name == "get_AllDefs")
			{
				inputMethodInstruction.Operand
					= inputMethod
					= module.ImportReference((inputMethod.DeclaringType.ContainsGenericParameter
						? typeof(DefDatabase<>).GetProperty(nameof(DefDatabase<Def>.AllDefsListForReading))
						: typeof(DefDatabase<>)
							.MakeGenericType(AccessTools.TypeByName(((GenericInstanceType)inputMethod.DeclaringType)
								.GenericArguments[0].FullName))
							.GetProperty(nameof(DefDatabase<Def>.AllDefsListForReading)))!.GetMethod, inputMethod);
			}

			var inputReturnType = inputMethod.ReturnType;
			if (inputReturnType.Name == "List`1")
			{
				var containsGenericParameter = inputReturnType.ContainsGenericParameter
					&& (inputReturnType.TryGetGenericArguments()
						?.Any(a => a.TryGetGenericParameterType(inputMethod) is null) ?? true);
				
				var listFindMethod = linqTarget == LinqMethod.FirstOrDefault
					? containsGenericParameter
						? typeof(List<>).GetMethod(nameof(List<object>.Find))
						: typeof(List<>)
							.MakeGenericType(AccessTools.TypeByName(inputReturnType
								.TryGetGenericArguments(inputMethod)![0].FullName)).GetMethod(nameof(List<object>.Find))
					: methodof((Func<List<object>, Predicate<object>, object>)CollectionExtensions.First)
						.GetGenericMethodDefinition();

				if (linqTarget == LinqMethod.First && !containsGenericParameter)
				{
					listFindMethod = listFindMethod!.MakeGenericMethod(AccessTools.TypeByName(inputReturnType
						.TryGetGenericArguments(inputMethod)![0].FullName));
				}

				var newOperand = containsGenericParameter
					? module.ImportReference(listFindMethod, method)
					: module.ImportReference(listFindMethod);
				
				// Log.Message($"Performance Fish successfully optimized {
				// 	((MethodReference)instruction.Operand).FullName} within method {
				// 		method.FullName} by applying new operand {newOperand.FullName}");
				
				var instructionsToRemove = 0;
				if (instruction.Operand is MethodReference { Name: LinqStrings.Where })
					instructionsToRemove++;
				
				instruction.Operand = newOperand;
				
				while (instructionsToRemove-- > 0)
					instructions.RemoveAt(i + 1);
			}
		}
	}

	private static void ApplySkipLocalsInit(MethodBody body) => body.InitLocals = false;

	private static void AddAttributes(ModuleDefinition module)
	{
		AddModuleAttributes(module);
		AddAssemblyAttributes(module);
		// AddSecurityAttributes(module.Assembly.SecurityDeclarations, module);
	}

	private static void AddAssemblyAttributes(ModuleDefinition module)
	{
		var rimworldCustomAttributes = module.Assembly.CustomAttributes;

		if (TryAddAttribute(module, rimworldCustomAttributes, typeof(IgnoresAccessChecksToAttribute),
			typeof(string)) is { } ignoresAccessChecksToAttribute)
		{
			ignoresAccessChecksToAttribute.ConstructorArguments.Add(new(module.TypeSystem.String, "mscorlib"));
		}

		TryAddAttribute(module, rimworldCustomAttributes, typeof(AllowPartiallyTrustedCallersAttribute));
		
		TryAddAttribute(module, rimworldCustomAttributes, typeof(SecurityTransparentAttribute));

		if (TryAddAttribute(module, rimworldCustomAttributes, typeof(SecurityRulesAttribute),
			typeof(SecurityRuleSet)) is { } securityRulesAttribute)
		{
			securityRulesAttribute.ConstructorArguments.Add(new(module.ImportReference(typeof(SecurityRuleSet)),
				SecurityRuleSet.Level2));
		
			securityRulesAttribute.Properties.Add(new(nameof(SecurityRulesAttribute.SkipVerificationInFullTrust),
				new(module.TypeSystem.Boolean, true)));
		}

		var debuggableAttribute = rimworldCustomAttributes.First(static a
			=> a.AttributeType.Name == nameof(DebuggableAttribute));
		
		debuggableAttribute.Constructor
			= module.ImportReference(typeof(DebuggableAttribute)
				.GetConstructor([typeof(bool), typeof(bool)]));
		
		debuggableAttribute.ConstructorArguments.Clear();
		debuggableAttribute.ConstructorArguments.Add(new(module.TypeSystem.Boolean, false));
		debuggableAttribute.ConstructorArguments.Add(new(module.TypeSystem.Boolean, false));
	}

	private static void AddModuleAttributes(ModuleDefinition module)
		=> TryAddAttribute(module, module.CustomAttributes, typeof(UnverifiableCodeAttribute));

	// internal static void AddSecurityAttributes(Collection<SecurityDeclaration> securityDeclarations,
	// 	ModuleDefinition module, [CallerArgumentExpression(nameof(securityDeclarations))] string? targetName = null)
	// {
	// 	if (securityDeclarations.Any(static s => s.SecurityAttributes
	// 		.Any(static a => a.AttributeType.Name == nameof(SecurityPermissionAttribute)
	// 			&& a.Properties.Any(static p => p.Name == nameof(SecurityPermissionAttribute.SkipVerification)))))
	// 	{
	// 		return;
	// 	}
	// 	
	// 	if (targetName != null)
	// 		Log.Message($"Added skipVerification attribute to {targetName}");
	//
	// 	securityDeclarations.Add(new(Mono.Cecil.SecurityAction.RequestMinimum)
	// 	{
	// 		SecurityAttributes =
	// 		{
	// 			new SecurityAttribute(module.ImportReference(typeof(SecurityPermissionAttribute)))
	// 			{
	// 				Properties =
	// 				{
	// 					new(nameof(SecurityPermissionAttribute.SkipVerification),
	// 						new(module.TypeSystem.Boolean, true))
	// 				}
	// 			}
	// 		}
	// 	});
	// }

	public static CustomAttribute? TryAddAttribute(ModuleDefinition module, IList<CustomAttribute> attributes,
		Type attributeType, params Type[] parameters)
	{
		if (ContainsAttribute(attributes, attributeType.Name))
			return null;

		var attribute = ImportAttribute(module, attributeType, parameters);
		attributes.Add(attribute);
		return attribute;
	}

	private static CustomAttribute ImportAttribute(ModuleDefinition module, Type attributeType,
		params Type[] parameters)
		=> new(module.ImportReference(AccessTools.Constructor(attributeType, parameters)));

	private static bool ContainsAttribute(IList<CustomAttribute> attributes, string name)
	{
		for (var i = attributes.Count; i-- > 0;)
		{
			if (attributes[i].AttributeType.Name == name)
				return true;
		}

		return false;
	}
}