// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public static class TickHelper
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Past(int tick) => tick < TicksGame;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool MatchesModulo(int powerOfTwo) => MatchesModulo(powerOfTwo, 0);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool MatchesModulo(int powerOfTwo, int offset)
		=> ((TicksGame + offset) & (powerOfTwo - 1)) == 0;
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Add(int ticks) => ticks + TicksGame;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Add(int ticks, int randomOffset)
		=> ticks + (randomOffset & 63) + TicksGame;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int Add(int ticks, int randomOffset, int powerOfTwo)
		=> ticks + (randomOffset & (powerOfTwo - 1)) + TicksGame;

	public static int TicksGame
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Current.gameInt.tickManager.TicksGame;
	}
}