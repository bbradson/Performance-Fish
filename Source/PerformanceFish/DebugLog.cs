// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;

namespace PerformanceFish;

/// <summary>
/// Log Functions that only do something when compiling in Debug Mode
/// </summary>
public static class DebugLog
{
	[Conditional("DEBUG")]
	public static void Message(string text) => Log.Message(text);

	[Conditional("DEBUG")]
	public static void Warning(string text) => Log.Warning(text);

	[Conditional("DEBUG")]
	public static void Error(string text) => Log.Error(text);
}