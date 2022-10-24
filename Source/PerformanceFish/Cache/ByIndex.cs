// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PerformanceFish.Cache;
public class ByIndex<T_out> : IList<T_out?>, ICollection
{
	private static ByIndex<T_out> _get = new();
	[ThreadStatic]
	private static ByIndex<T_out>? _getThreadStatic;

	public static ByIndex<T_out> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<ByIndex<T_out>>();

	public static ByIndex<T_out> GetDirectly => _get;

	static ByIndex() => Utility.All.Add(_get);

	protected T_out?[] _items = Array.Empty<T_out>();
	protected object? _syncRoot;

	public T_out? this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			ResizeIfNecessary(index);
			return _items[index];
		}
		set
		{
			ResizeIfNecessary(index);
			_items[index] = value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[return: MaybeNull]
	public ref T_out GetReference(int index)
	{
		ResizeIfNecessary(index);
		return ref _items[index];
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(int index, out T_out? value)
	{
		value = this[index];
		return !value!.Equals<T_out>(default!);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void ResizeIfNecessary(int checkedIndex)
	{
		if (checkedIndex >= _items.Length)
			ResizeFor(checkedIndex);
	}

	protected void ResizeFor(int index)
	{
		Array.Resize(ref _items, Mathf.NextPowerOfTwo(index + 1));
		ResizeIfNecessary(index); // just in case
	}

	public int Count => _items.Length;

	public bool IsReadOnly => false;

	object ICollection.SyncRoot
		=> _syncRoot
		??= Interlocked.CompareExchange<object>(ref _syncRoot!, new(), null!);

	bool ICollection.IsSynchronized => false;

	[DoesNotReturn]
	public void Add(T_out? item) => throw new NotSupportedException();

	public void Clear()
	{
		if (_items.Length > 0)
			Array.Clear(_items, 0, _items.Length);
	}

	public bool Contains(T_out? item) => Array.IndexOf(_items, item) >= 0;

	public void CopyTo(T_out?[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

	public IEnumerator<T_out?> GetEnumerator() => _items.AsEnumerable().GetEnumerator();

	public int IndexOf(T_out? item) => Array.IndexOf(_items, item);

	public void Insert(int index, T_out? item) => this[index] = item;

	[DoesNotReturn]
	public bool Remove(T_out? item) => throw new NotSupportedException();

	public void RemoveAt(int index)
	{
		if (index < Count)
			_items[index] = default;
	}

	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
	void ICollection.CopyTo(Array array, int index) => _items.CopyTo(array, index);
}

public class ByIndex<T_in, T_out> : ByIndex<T_out>
	where T_in : notnull
{
	private static ByIndex<T_in, T_out> _get = new();
	[ThreadStatic]
	private static ByIndex<T_in, T_out>? _getThreadStatic;

	public new static ByIndex<T_in, T_out> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<ByIndex<T_in, T_out>>();

	public new static ByIndex<T_in, T_out> GetDirectly => _get;

	static ByIndex() => Utility.All.Add(_get);

	public unsafe T_out? this[T_in key]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this[FunctionPointers.IndexGetter<T_in>.Default(key)];
		set => this[FunctionPointers.IndexGetter<T_in>.Default(key)] = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(T_in key, [NotNullWhen(true)] out T_out? value)
	{
		value = this[key];
		return !value!.Equals<T_out>(default!);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[return: MaybeNull]
	public unsafe ref T_out GetReference(T_in key)
		=> ref GetReference(FunctionPointers.IndexGetter<T_in>.Default(key))!;

}

/// <summary>
/// Seems slower than the dictionary based Cache.ByInt.
/// </summary>
public class ByIndex<T_first, T_second, T_out> : ByIndex<T_first, ByIndex<T_second, T_out>>
	where T_first : notnull where T_second : notnull
{
	private static ByIndex<T_first, T_second, T_out> _get = new();
	[ThreadStatic]
	private static ByIndex<T_first, T_second, T_out>? _getThreadStatic;

	public new static ByIndex<T_first, T_second, T_out> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<ByIndex<T_first, T_second, T_out>>();

	public new static ByIndex<T_first, T_second, T_out> GetDirectly => _get;

	static ByIndex() => Utility.All.Add(_get);

	public T_out? this[T_first first, T_second second]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => GetInner(first)[second];
		set => GetInner(first)[second] = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private unsafe ByIndex<T_second, T_out> GetInner(T_first first)
	{
		var firstIndex = FunctionPointers.IndexGetter<T_first>.Default(first);
		ResizeIfNecessary(firstIndex);

		ref var inner = ref _items[firstIndex];
		inner ??= new();

		return inner!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[return: MaybeNull]
	public ref T_out GetReference(T_first first, T_second second)
		=> ref GetInner(first).GetReference(second)!;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(T_first first, T_second second, [NotNullWhen(true)] out T_out? value)
	{
		value = this[first, second];
		return !value!.Equals<T_out>(default!);
	}
}