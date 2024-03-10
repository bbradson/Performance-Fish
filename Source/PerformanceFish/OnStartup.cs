// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;
using PerformanceFish.Events;

namespace PerformanceFish;

[StaticConstructorOnStartup]
[UsedImplicitly]
public static class OnStartup
{
	static OnStartup()
	{
		// Log.Message($"LoadItem took {TextureLoadingPatches.Stopwatch.ElapsedMilliSecondsAccurate()} ms");
		
		// Log.Message($"Allowed defs for mothballing: {MothballOptimization.AllowedDefs.ToStringSafeEnumerable()}");
		// Log.Message($"Blocking defs for mothballing: {DefDatabase<HediffDef>.AllDefsListForReading.Where(static def
		// 	=> !MothballOptimization.AllowedDefs.Contains(def)).ToStringSafeEnumerable()}");
		
		Fields.Initialized = true;
		StaticEvents.OnStaticConstructorOnStartupCalled();
	}

	public static class State
	{
		public static bool Initialized => Fields.Initialized;
	}

	private static class Fields
	{
		internal static bool Initialized;
	}
}