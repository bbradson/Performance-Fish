// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Text;
using JetBrains.Annotations;

namespace PerformanceFish.Pools;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[InterpolatedStringHandler]
public struct PooledStringBuilderHandler : IDisposable
{
	private PooledIList<List<char>> _chars;

	public PooledStringBuilderHandler(int literalLength, int formattedCount)
	{
		_chars = new();
		_chars.List.EnsureCapacity(literalLength + (formattedCount * 8));
	}

	public void AppendLiteral(string s) => _chars.List.Add(s);

	public void AppendFormatted<T>(T t)
	{
		if (t != null)
			_chars.List.Add(t.ToString());
	}

	public StringBuilder AppendToStringBuilder(StringBuilder destination)
	{
		destination.Append(_chars.List);
		Dispose();
		return destination;
	}

	public void Dispose() => _chars.Dispose();
}