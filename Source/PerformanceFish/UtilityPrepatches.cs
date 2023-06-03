// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public class UtilityPrepatches : ClassWithFishPrepatches
{
	public class CreateModClassesPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook for very early loading of patches, right after the game finishes loading up game assemblies into "
			+ "its process and before it launches any processes on them. Certain patches require this.";

		public override bool Enabled => true;

		public override MethodBase TargetMethodBase { get; }
			= methodof(LoadedModManager.CreateModClasses);

		public static void Prefix()
		{
			try
			{
				OnAssembliesLoaded.Start();
			}
			catch (Exception e)
			{
				Log.Error($"Exception caught within OnAssembliesLoaded:\n{e}\n{
					new StackTrace() /*StackTraceUtility.ExtractStackTrace()*/}");
			}
		}
	}

	public class ThingDestroy : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things get destroyed. Certain functions require this.";

		public override bool Enabled => true;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.Destroy));

		public static void Postfix(Thing __instance) => Cache.Utility.NotifyThingDestroyed(__instance);
	}
}