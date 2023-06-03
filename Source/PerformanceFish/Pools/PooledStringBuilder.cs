// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Security;
using System.Text;
using JetBrains.Annotations;

namespace PerformanceFish.Pools;

public struct PooledStringBuilder : IDisposable
{
	private FishPoolable _fishPoolable;
	
	public StringBuilder Builder => _fishPoolable.Builder;

	public StringBuilder Append(string? value) => Builder.Append(value);
	public StringBuilder Append(char value) => Builder.Append(value);
	public StringBuilder Append(char[] value) => Builder.Append(value);
	public StringBuilder Append(PooledStringBuilderHandler value) => value.AppendToStringBuilder(Builder);

	public StringBuilder AppendLine(string? value) => Builder.AppendLine(value);
	
	public StringBuilder Insert(int index, string? value) => Builder.Insert(index, value);
	public StringBuilder Insert(int index, char value) => Builder.Insert(index, value);
	
	public StringBuilder Remove(int startIndex, int length) => Builder.Remove(startIndex, length);

	public void Clear() => Builder.Clear();

	public override string ToString() => Builder.ToString();
	
	[SecuritySafeCritical]
	public PooledStringBuilder() => _fishPoolable = FishPool<FishPoolable>.Get();

	public PooledStringBuilder(int minimumSize) : this()
	{
		if (_fishPoolable.Builder.Capacity < minimumSize)
			_fishPoolable.Builder.Capacity = minimumSize;
	}

	[SecuritySafeCritical]
	public void Dispose()
	{
		if (_fishPoolable.Builder == null!)
			return;

		FishPool<FishPoolable>.Return(_fishPoolable);
		_fishPoolable.Builder = null!;
	}

	private struct FishPoolable : IFishPoolable
	{
		public StringBuilder Builder = new();
		
		public void Reset() => Builder.Clear();
		
		[UsedImplicitly]
		public FishPoolable() {}
	}
}