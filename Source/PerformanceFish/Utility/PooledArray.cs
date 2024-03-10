// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public record struct PooledArray<T> : IDisposable, IEnumerable<T>
{
	private T[] _array;
	private int _length;

	public T[] BackingArray => _array;
	public int Length => _length;
	public int Capacity => _array.Length;
	
	[ThreadStatic]
	private static T[]?[]? _buffersThreadStatic;

	[ThreadStatic]
	private static int _bufferCountThreadStatic;

	private static object _lockObject = new();
	private static T[]?[] _buffersGlobal = new T[]?[16];
	private static int _bufferCountGlobal;
	
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
		if (_bufferCountThreadStatic == 0)
			FetchThroughLock();

		ref var bucket = ref _buffersThreadStatic![--_bufferCountThreadStatic];
		_array = bucket!;
		bucket = null;
		if (_array.Length < length)
			ResizeBuffer(length);

		_length = length;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ResizeBuffer(int length) => Array.Resize(ref _array, Mathf.NextPowerOfTwo(length));

	public PooledArray(ICollection<T> collection) : this(collection.Count) => collection.CopyTo(_array, 0);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ref T GetReference(int index)
	{
		Guard.IsLessThan(index, Length);
		return ref _array[index];
	}

	public void Dispose()
	{
		if (_length > 0)
			Array.Clear(_array, 0, _length);
		
		var buffersThreadStatic = _buffersThreadStatic ??= InitializeBuffersThreadStatic();
		
		if (_bufferCountThreadStatic == 16)
			PushThroughLock();

		buffersThreadStatic[_bufferCountThreadStatic++] = _array;
	}
	
	public void Sort(IComparer<T> comparer) => _array.AsSpan()[.._length].Sort(comparer);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void FetchThroughLock()
	{
		var buffersThreadStatic = _buffersThreadStatic ??= InitializeBuffersThreadStatic();
		var createNew = false;
		
		lock (_lockObject)
		{
			if (_bufferCountGlobal != 0)
			{
				for (var i = 8; i-- > 0;)
				{
					buffersThreadStatic[i] = _buffersGlobal[--_bufferCountGlobal];
					_buffersGlobal[_bufferCountGlobal] = default;
				}
			}
			else
			{
				createNew = true;
			}
		}

		if (createNew)
			Array.Fill(buffersThreadStatic, Array.Empty<T>(), 0, 8);
		
		_bufferCountThreadStatic = 8;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void PushThroughLock()
	{
		var buffersThreadStatic = _buffersThreadStatic!;
		
		lock (_lockObject)
		{
			if (_bufferCountGlobal + 8 > _buffersGlobal.Length)
				ExpandGlobalBuffers();
			
			for (var i = 16; i-- > 8;)
			{
				ref var bucket = ref buffersThreadStatic[i];
				var buffer = bucket;
				bucket = default;

				_buffersGlobal[_bufferCountGlobal++] = buffer;
			}
		}
		
		_bufferCountThreadStatic = 8;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ExpandGlobalBuffers() => Array.Resize(ref _buffersGlobal, _buffersGlobal.Length << 1);
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static T[]?[] InitializeBuffersThreadStatic() => new T[]?[16];

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < _length; i++)
			yield return _array[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}