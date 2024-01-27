// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public static class GUIHelper
{
	/// <summary>
	/// Unity calls OnGUI with EventType.Layout at the beginning and EventType.Repaint at the end of every frame.
	/// Anything drawn within the Layout call in turn gets covered every time, making it a waste of CPU cycles.
	/// </summary>
	public static bool CanSkip
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Event.current.type == EventType.Layout;
	}
}