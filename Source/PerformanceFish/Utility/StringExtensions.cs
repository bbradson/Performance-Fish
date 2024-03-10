// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public static class StringExtensions
{
	public static bool NameEqualsCaseInsensitive(this MethodInfo methodInfo, string name)
		=> methodInfo.Name.Equals(name, StringComparison.OrdinalIgnoreCase);

	public static string AppendNewLineWhen(this string instance, bool predicate, string text)
		=> predicate ? instance.AppendNewLine(text) : instance;

	public static string AppendNewLineWhenNotNull(this string instance, object? value)
		=> value != null ? instance.AppendNewLine(value.ToString()) : instance;

	public static string AppendNewLine(this string instance, string text)
		=> string.Concat(instance, "\n", text);

	public static string AppendWhen(this string instance, bool predicate, string text)
		=> predicate ? instance + text : instance;

	public static string AppendWhenNotNull(this string instance, object? value)
		=> value != null ? instance + value : instance;
}