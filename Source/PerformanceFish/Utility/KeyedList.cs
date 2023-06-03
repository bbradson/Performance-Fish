// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace PerformanceFish.Utility;

[PublicAPI]
public class KeyedList<TKey, TValue> : List<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>
{
	public IList<TKey> Keys
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
	}

	public IList<TValue> Values
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
	}

	public KeyedList()
	{
		Keys = new KeyCollection(this);
		Values = new ValueCollection(this);
	}

	public KeyedList(int capacity) : base(capacity)
	{
		Keys = new KeyCollection(this);
		Values = new ValueCollection(this);
	}

	public KeyedList(IEnumerable<KeyValuePair<TKey, TValue>> collection) : base(collection)
	{
		Keys = new KeyCollection(this);
		Values = new ValueCollection(this);
	}

	public TValue this[TKey key]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			Guard.IsNotNull(key);

			var items = _items;
			for (var i = Count - 1; i >= 0; i--)
			{
				if (items[i].Key.Equals<TKey>(key))
					return items[i].Value;
			}

			return ThrowHelperF.ThrowKeyNotFoundException<TKey, TValue>(key);
		}
		set
		{
			Guard.IsNotNull(key);

			var items = _items;
			for (var i = Count - 1; i >= 0; i--)
			{
				ref var item = ref items[i];
				if (!item.Key.Equals<TKey>(key))
					continue;

				item = new(item.Key, value);
				return;
			}

			Add(new(key, value));
		}
	}

	public void Add(TKey key, TValue value) => Add(new(key, value));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsKey(TKey item)
	{
		Guard.IsNotNull(item);

		var items = _items;
		for (var i = Count - 1; i >= 0; i--)
		{
			if (items[i].Key.Equals<TKey>(item))
				return true;
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool ContainsValue(TValue item)
	{
		if (item is null)
			return false;

		var items = _items;
		for (var i = Count - 1; i >= 0; i--)
		{
			if (items[i].Value.Equals<TValue>(item))
				return true;
		}

		return false;
	}

#pragma warning disable CS8767 // missing nullable on IDictionary interface
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetValue(TKey key, out TValue? value)
#pragma warning restore CS8767
	{
		Guard.IsNotNull(key);
		
		var items = _items;
		for (var i = Count - 1; i >= 0; i--)
		{
			if (!items[i].Key.Equals<TKey>(key))
				continue;

			value = items[i].Value;
			return true;
		}

		value = default;
		return false;
	}

	public void CopyTo(TKey[] array, int arrayIndex)
	{
		CanCopyTo(array, arrayIndex);

		var items = _items;
		for (var i = 0; i < Count; i++)
			array[i + arrayIndex] = items[i].Key;
	}

	public void CopyTo(TValue[] array, int arrayIndex)
	{
		CanCopyTo(array, arrayIndex);

		var items = _items;
		for (var i = 0; i < Count; i++)
			array[i + arrayIndex] = items[i].Value;
	}

	protected void CanCopyTo<W>(W[] array, int arrayIndex)
	{
		Guard.IsNotNull(array, nameof(array));
		Guard.IsGreaterThanOrEqualTo(arrayIndex, 0, nameof(arrayIndex));
		Guard.HasSizeGreaterThanOrEqualTo(array, Count + arrayIndex, nameof(array));
	}

	public bool Remove(TKey item)
	{
		Guard.IsNotNull(item);

		var index = IndexOfKey(item);
		if (index < 0)
			return false;

		RemoveAt(index);
		return true;
	}

	public bool RemoveValue(TValue item)
	{
		var index = IndexOfValue(item);
		if (index < 0)
			return false;

		RemoveAt(index);
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int IndexOfKey(TKey key)
	{
		Guard.IsNotNull(key);

		var items = _items;
		for (var i = Count - 1; i >= 0; i--)
		{
			if (items[i].Key.Equals<TKey>(key))
				return i;
		}

		return -1;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int IndexOfValue(TValue value)
	{
		var items = _items;
		for (var i = Count - 1; i >= 0; i--)
		{
			if (items[i].Value.Equals<TValue>(value))
				return i;
		}

		return -1;
	}

	protected class KeyCollection : IList<TKey>
	{
		private readonly KeyedList<TKey, TValue> _list;

		public KeyCollection(KeyedList<TKey, TValue> list) => _list = list;

		public TKey this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _list[index].Key;
			// ReSharper disable once ValueParameterNotUsed
			set => ThrowHelper.ThrowNotSupportedException();
		}

		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _list.Count;
		}

		public bool IsReadOnly => false;

		public void Add(TKey item) => ThrowHelper.ThrowNotSupportedException();

		public void Clear() => _list.Clear();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(TKey item) => _list.ContainsKey(item);

		public void CopyTo(TKey[] array, int arrayIndex)
		{
			_list.CanCopyTo(array, arrayIndex);

			var items = _list._items;
			for (var i = 0; i < Count; i++)
				array[i + arrayIndex] = items[i].Key;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<TKey> GetEnumerator()
		{
			var items = _list._items;
			for (var i = 0; i < Count; i++)
				yield return items[i].Key;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(TKey item) => _list.IndexOfKey(item);

		public void Insert(int index, TKey item) => ThrowHelper.ThrowNotSupportedException();

		public bool Remove(TKey item) => _list.Remove(item);

		public void RemoveAt(int index) => _list.RemoveAt(index);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	protected class ValueCollection : IList<TValue>
	{
		private readonly KeyedList<TKey, TValue> _list;

		public ValueCollection(KeyedList<TKey, TValue> list) => _list = list;

		public TValue this[int index]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _list[index].Value;
			set
			{
				ref var item = ref _list._items[index];
				item = new(item.Key, value);
			}
		}

		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _list.Count;
		}

		public bool IsReadOnly => false;

		public void Add(TValue item) => ThrowHelper.ThrowNotSupportedException();

		public void Clear() => _list.Clear();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(TValue item) => _list.ContainsValue(item);

		public void CopyTo(TValue[] array, int arrayIndex)
		{
			_list.CanCopyTo(array, arrayIndex);

			var items = _list._items;
			for (var i = 0; i < Count; i++)
				array[i + arrayIndex] = items[i].Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<TValue> GetEnumerator()
		{
			var items = _list._items;
			for (var i = 0; i < Count; i++)
				yield return items[i].Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(TValue item) => _list.IndexOfValue(item);

		public void Insert(int index, TValue item) => ThrowHelper.ThrowNotSupportedException();

		public bool Remove(TValue item) => _list.RemoveValue(item);

		public void RemoveAt(int index) => _list.RemoveAt(index);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;
	ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

	protected static class StringResources
	{
		internal const string ARG_KEY_NOT_FOUND_WITH_KEY = "The given key '{0}' was not present in the dictionary.";
	}

	protected static class ThrowHelperF
	{
		[DoesNotReturn]
		internal static V ThrowKeyNotFoundException<T, V>(T key) => throw GetKeyNotFoundException(key);

		[DoesNotReturn]
		internal static void ThrowKeyNotFoundException<T>(T key)
			// Generic key to move the boxing to the right hand side of throw
			=> throw GetKeyNotFoundException(key);

		private static KeyNotFoundException GetKeyNotFoundException(object? key)
			=> new(string.Format(StringResources.ARG_KEY_NOT_FOUND_WITH_KEY, key));
	}
}