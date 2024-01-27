// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;

namespace PerformanceFish.Prepatching;

public abstract class FishClassPrepatch : FishPrepatchBase
{
	public abstract Type Type { get; }

	public abstract void FreePatch(TypeDefinition typeDefinition);

	protected internal override void ApplyPatch(ModuleDefinition module)
	{
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
		var patchType = Type
			?? ThrowHelper.ThrowInvalidOperationException<Type>(
				$"Patch {GetType().FullName} has no target type.");

		if (patchType.IsGenericType)
			patchType = patchType.GetGenericTypeDefinition();

		DebugLog.Message($"Applying prepatch {GetType().FullName} on type {
			patchType.FullName}");

		var targetType = module.GetType(patchType.Namespace, patchType.Name) ?? ThrowForFailureToFindType(patchType);
		
		// PrepatchManager.AddSecurityAttributes(targetType.SecurityDeclarations, module);
		
		FreePatch(targetType);
	}
}