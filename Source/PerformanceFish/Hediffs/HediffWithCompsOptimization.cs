// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if useless

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Hediffs;

public sealed class HediffWithCompsOptimization : ClassWithFishPrepatches
{
	public sealed class ShouldRemovePatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(HediffWithComps), nameof(HediffWithComps.ShouldRemove));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static unsafe bool ReplacementBody(HediffWithComps instance)
		{
			if (instance.comps is { } comps)
			{
				for (var i = 0; i < comps.Count; i++)
				{
					if (comps[i].CompShouldRemove)
						return true;
				}
			}
			
			return BaseShouldRemove(instance);
		}

		public static unsafe delegate*<HediffWithComps, bool> BaseShouldRemove
			= (delegate*<HediffWithComps, bool>)AccessTools
				.DeclaredPropertyGetter(typeof(Hediff), nameof(Hediff.ShouldRemove)).GetFunctionPointer();
	}

	public sealed class VisiblePatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(HediffWithComps), nameof(HediffWithComps.Visible));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static unsafe bool ReplacementBody(HediffWithComps instance)
		{
			if (instance.comps is { } comps)
			{
				for (var i = 0; i < comps.Count; i++)
				{
					if (comps[i].CompDisallowVisible())
						return false;
				}
			}
			
			return BaseVisible(instance);
		}

		public static unsafe delegate*<HediffWithComps, bool> BaseVisible
			= (delegate*<HediffWithComps, bool>)AccessTools
				.DeclaredPropertyGetter(typeof(Hediff), nameof(Hediff.Visible)).GetFunctionPointer();
	}

	public sealed class PostTickPatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(HediffWithComps), nameof(HediffWithComps.PostTick));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static unsafe void ReplacementBody(HediffWithComps instance)
		{
			BasePostTick(instance);

			if (instance.comps is not { } comps)
				return;

			var severityAdjustment = 0f;
			for (var i = 0; i < comps.Count; i++)
				comps[i].CompPostTick(ref severityAdjustment);

			if (severityAdjustment != 0f)
				instance.Severity += severityAdjustment;
		}

		public static unsafe delegate*<HediffWithComps, void> BasePostTick
			= (delegate*<HediffWithComps, void>)AccessTools.DeclaredMethod(typeof(Hediff), nameof(Hediff.PostTick))
				.GetFunctionPointer();
	}
}

#endif