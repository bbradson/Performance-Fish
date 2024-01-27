// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class UtilityPrepatches : ClassWithFishPrepatches
{
	public sealed class CreateModClassesPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook for very early loading of patches, right after the game finishes loading up game assemblies into "
			+ "its process and before it launches any processes on them. Certain patches require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

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
					Environment.StackTrace /*StackTraceUtility.ExtractStackTrace()*/}");
			}
		}
	}
}