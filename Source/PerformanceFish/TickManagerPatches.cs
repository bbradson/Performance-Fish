// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if unused
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class TickManagerPatches : ClassWithFishPrepatches
{
	public sealed class SetCurTimeSpeedPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertySetter(typeof(TickManager), nameof(TickManager.CurTimeSpeed));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(TickManager __instance, TimeSpeed value) => TimeSpeedChanging(__instance, value);
	}

	public sealed class TogglePausedPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(TickManager), nameof(TickManager.TogglePaused));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(TickManager __instance) => TimeSpeedChanging(__instance, TimeSpeed.Paused);
	}

	public static void TimeSpeedChanging(TickManager tickManager, TimeSpeed value)
	{
		if (value == tickManager.curTimeSpeed)
			return;
		
		if (value == TimeSpeed.Paused)
			GamePausing();
		else if (tickManager.curTimeSpeed == TimeSpeed.Paused)
			GameResuming();
	}

	public static void GamePausing()
	{
		
	}

	public static void GameResuming()
	{
		
	}
}
#endif