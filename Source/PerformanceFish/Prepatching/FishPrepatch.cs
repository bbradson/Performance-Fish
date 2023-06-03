// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;

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
}