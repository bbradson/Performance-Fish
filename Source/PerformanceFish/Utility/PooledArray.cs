// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;

namespace PerformanceFish.Utility;

public record struct PooledArray<T> : IDisposable, IEnumerable<T>
{
	private static T[]?[] _buffers = new T[]?[4];
	private static int _bufferCount;
	
	private T[] _array;
	private int _length;

	public T[] BackingArray => _array;
	public int Length => _length;
	public int Capacity => _array.Length;
	
	public T this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			Guard.IsLessThan(index, Length);
			return _array[index];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			Guard.IsLessThan(index, Length);
			_array[index] = value;
		}
	}

	public PooledArray() : this(0)
	{
	}

	public PooledArray(int length)
	{
		if (_bufferCount < 1)
		{
			NewBuffer(length);
		}
		else
		{
			ref var bucket = ref _buffers[--_bufferCount];
			_array = bucket!;
			bucket = null;
			if (_array.Length < length)
				ResizeBuffer(length);
		}

		_length = length;
	}

	public PooledArray(ICollection<T> collection) : this(collection.Count) => collection.CopyTo(_array, 0);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ref T GetReference(int index)
	{
		Guard.IsLessThan(index, Length);
		return ref _array[index];
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ResizeBuffer(int length) => Array.Resize(ref _array, Mathf.NextPowerOfTwo(length));

	[MemberNotNull(nameof(_array))]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void NewBuffer(int length) => _array = new T[Mathf.NextPowerOfTwo(length)];

	public void Dispose()
	{
		if (++_bufferCount > _buffers.Length)
			ExpandBuffers(_bufferCount);

		if (_length > 0)
			Array.Clear(_array, 0, _length);
		
		_buffers[_bufferCount - 1] = _array;
	}
	
	public void Sort(IComparer<T> comparer) => _array.AsSpan()[.._length].Sort(comparer);

	private static void ExpandBuffers(int minLength)
	{
		var newBuffers = new T[]?[Mathf.NextPowerOfTwo(minLength)];

		Array.Copy(_buffers, newBuffers, _buffers.Length);

		_buffers = newBuffers;
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < _length; i++)
			yield return _array[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}