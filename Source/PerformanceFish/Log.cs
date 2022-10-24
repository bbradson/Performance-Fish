// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public static class Log
{
	public static void Message(string message) => _message(message);
	public static void Warning(string message) => _warning(message);
	public static void Error(string message) => _error(message);

	public static class Config
	{
		public static Action<string> Message { set => _message = value; }
		public static Action<string> Warning { set => _warning = value; }
		public static Action<string> Error { set => _error = value; }
	}

	private static Action<string> _message = Verse.Log.Message;
	private static Action<string> _warning = Verse.Log.Warning;
	private static Action<string> _error = Verse.Log.Error;
}