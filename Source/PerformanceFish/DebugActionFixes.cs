// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if !V1_4
using LudeonTK;
#endif
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class DebugActionFixes : ClassWithFishPrepatches
{
	public sealed class InitActionsPatch : FishPrepatch
	{
		// useless without the other, as they initialize simultaneously
		public override List<Type> LinkedPatches { get; } = [typeof(InitOutputsPatch)];

		public override string? Description { get; }
			= "Adds exception handling to the debug actions menu, allowing it to still generate and open when having "
			+ "one mod throw errors. Also logs where that error gets thrown";

		public override MethodBase TargetMethodBase { get; } = AccessTools.DeclaredMethod(typeof(DebugTabMenu_Actions),
			nameof(DebugTabMenu_Actions.InitActions));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static DebugActionNode ReplacementBody(DebugTabMenu_Actions instance, DebugActionNode absRoot)
		{
			var root = instance.myRoot = new("Actions");
			absRoot.AddChild(root);
			root.AddChild(instance.moreActionsNode = new("Show more actions")
			{
				category = "More debug actions",
				displayPriority = -999999
			});
			
			foreach (var type in GenTypes.AllTypes)
			{
				try
				{
					var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var methodInfo in methods)
					{
						if (methodInfo.TryGetAttribute<DebugActionAttribute>(out var customAttribute))
							instance.GenerateCacheForMethod(methodInfo, customAttribute);
					
						if (!methodInfo.TryGetAttribute<DebugActionYielderAttribute>(out _))
							continue;
					
						foreach (var item in (IEnumerable<DebugActionNode>)methodInfo.Invoke(null, null))
							root.AddChild(item);
					}
				}
				catch (Exception ex)
				{
					Log.Error($"Exception caught while trying to initialize debug actions for type '{
						type.FullDescription()}': {ex}");
				}
			}
			
			root.TrySort();
			return root;
		}
	}
	
	public sealed class InitOutputsPatch : FishPrepatch
	{
		public override List<Type> LinkedPatches { get; } = [typeof(InitActionsPatch)];
		
		public override string? Description { get; }
			= "Adds exception handling to the debug outputs menu, allowing it to still generate and open when having "
			+ "one mod throw errors. Also logs where that error gets thrown";

		public override MethodBase TargetMethodBase { get; } = AccessTools.DeclaredMethod(typeof(DebugTabMenu_Output),
			nameof(DebugTabMenu_Output.InitActions));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static DebugActionNode ReplacementBody(DebugTabMenu_Output instance, DebugActionNode absRoot)
		{
			var root = instance.myRoot = new("Outputs");
			absRoot.AddChild(root);
			
			foreach (var type in GenTypes.AllTypes)
			{
				try
				{
					var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var methodInfo in methods)
					{
						if (methodInfo.TryGetAttribute<DebugOutputAttribute>(out var customAttribute))
							instance.GenerateCacheForMethod(methodInfo, customAttribute);
					}
				}
				catch (Exception ex)
				{
					Log.Error($"Exception caught while trying to initialize debug outputs for type '{
						type.FullDescription()}': {ex}");
				}
			}
			
			root.TrySort();
			return root;
		}
	}
}