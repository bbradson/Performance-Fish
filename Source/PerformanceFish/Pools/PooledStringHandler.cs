// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;

namespace PerformanceFish.Pools;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[InterpolatedStringHandler]
public struct PooledStringHandler : IDisposable
{
	private PooledStringBuilder _builder;

	public PooledStringHandler(int literalLength, int formattedCount)
		=> _builder = new(literalLength + (formattedCount * 8));

	public void AppendLiteral(string s) => _builder.Append(s);

	public void AppendFormatted<T>(T t)
	{
		if (typeof(T) == typeof(PooledStringBuilderHandler))
			_builder.Append(Unsafe.As<T, PooledStringBuilderHandler>(ref t));
		else
			_builder.Append(t?.ToString());
	}

	public string GetFormattedText()
	{
		var result = _builder.ToString();
		Dispose();
		return result;
	}

	public void Dispose() => _builder.Dispose();
}