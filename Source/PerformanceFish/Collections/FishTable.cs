// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;

namespace PerformanceFish.Collections;

#pragma warning disable CS8766, CS8767
public class FishTable<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary, IReadOnlyDictionary<TKey, TValue>
{
	//private int[] _buckets;

	internal Entry[] _entries;

	private int _count;

	private int _version;

	//private unsafe delegate*<TKey, int> _keyGetHashCode = FunctionPointers.GetHashCode<TKey>.Default;
	//private unsafe delegate*<TKey, TKey, bool> _keyEquals = FunctionPointers.Equals<TKey>.Default;

	//private int _freeList;

	//private int _freeCount;

	//private KeyCollection _keys;

	//private ValueCollection _values;

	public ICollection<TKey> Keys => throw new NotImplementedException();

	public ICollection<TValue> Values => throw new NotImplementedException();

	public int Version
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _version;
	}

	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _count;
	}

	public bool IsReadOnly => false;

	ICollection IDictionary.Keys => throw new NotImplementedException();

	ICollection IDictionary.Values => throw new NotImplementedException();

	bool IDictionary.IsFixedSize => false;
	object ICollection.SyncRoot => this; // matching System.Collections.Generic.Dictionary
	bool ICollection.IsSynchronized => false;

	IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => throw new NotImplementedException();

	IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => throw new NotImplementedException();

	object? IDictionary.this[object key]
	{
		get
			=> key is TKey tkey
			? this[tkey]
			: ThrowHelper.ThrowKeyNotFoundException<object, TValue>(key);

		set
		{
			if (key is TKey tkey)
			{
				if (value is TValue tvalue)
					this[tkey] = tvalue;
				else if (value is null)
					this[tkey] = default;
				else
					ThrowHelper.ThrowWrongValueTypeArgumentException(value);
			}
			else
			{
				ThrowHelper.ThrowWrongKeyTypeArgumentException(key);
			}
		}
	}

	public TValue? this[TKey key]
	{
		get
		{
			var keyCode = (uint)HashCode.Get(key);
			var entry = _entries[FastModulo(keyCode, (uint)_entries.Length)];

			while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
			{
				if (entry.Next != EntryReference.Empty && entry.Next != null)
					entry = entry.Next.Entry;
				else
					ThrowHelper.ThrowKeyNotFoundException(key);
			}

			if (entry.Next is null)
				ThrowHelper.ThrowKeyNotFoundException(key);

			return entry.Value;
		}
		set
		{
			ref var reference = ref TryGetReferenceUnsafe(key);
			if (!Unsafe.IsNullRef(ref reference))
			{
				reference = value;
				_version++;
				return;
			}

			Add(key, value!);
		}
	}

	public FishTable() : this(0) { }

	public FishTable(int minimumCapacity)
		=> _entries = new Entry[
			minimumCapacity <= 1 ? 1 // Minimum size to not throw on indexing into _entries. Length is not checked beforehand
			: Mathf.NextPowerOfTwo(minimumCapacity)]; // System.Collections.Dictionary starts at 3. So wasteful.

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static uint FastModulo(uint first, uint second)
		=> first & (second - 1);

	public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
	{
		var keyCode = (uint)HashCode.Get(key);
		var entry = _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
			{
				entry = entry.Next.Entry;
			}
			else
			{
				value = default;
				return false;
			}
		}

		value = entry.Value;
		return entry.Next != null;
	}

	public TValue? TryGetValue(TKey key)
	{
		var keyCode = (uint)HashCode.Get(key);
		var entry = _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
				entry = entry.Next.Entry;
			else
				return default;
		}

		return entry.Value;
	}

	public ref TValue GetReference(TKey key)
	{
		var keyCode = (uint)HashCode.Get(key);
		ref var entry = ref _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
				entry = ref entry.Next.Entry;
			else
				ThrowHelper.ThrowKeyNotFoundException(key);
		}

		if (entry.Next == null)
			ThrowHelper.ThrowKeyNotFoundException(key);
		
		return ref entry.Value;
	}

	/// <summary>
	/// Returns a reference to the value field of an entry if the key exists and an Unsafe.NullRef{TValue} if not.
	/// Must be checked with Unsafe.IsNullRef before assigning to a regular var or field without ref.
	/// </summary>
	/// <param name="key">The key</param>
	/// <returns>ref TValue or Unsafe.NullRef{TValue}</returns>
	public ref TValue TryGetReferenceUnsafe(TKey key)
	{
		var keyCode = (uint)HashCode.Get(key);
		ref var entry = ref _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
				entry = ref entry.Next.Entry;
			else
				return ref Unsafe.NullRef<TValue>()!;
		}

		return ref entry.Next == null
			? ref Unsafe.NullRef<TValue>()
			: ref entry.Value;
	}

	public bool ContainsKey(TKey key)
	{
		var keyCode = (uint)HashCode.Get(key);
		var entry = _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
				entry = entry.Next.Entry;
			else
				return false;
		}

		return entry.Next != null;
	}

	public void Add(TKey key, TValue value)
	{
		Guard.IsNotNull(key);

		if (_count == _entries.Length) // throwing DuplicateKeyExceptions happens later, so Expanding occurs based on expected
			Expand();                  // behaviour instead of matching actual results like in System.Collections.Generic.

		var keyCode = (uint)HashCode.Get(key);
		ref var entry = ref _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.Next != null)
		{
			if (entry.HashCode == keyCode && entry.Key.Equals<TKey>(key))
				ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(key);

			if (entry.Next == EntryReference.Empty)
				break;

			entry = ref entry.Next.Entry;
		}

		if (entry.Next == null) // means the entry is empty
			entry = new(keyCode, key, value);
		else // if (entry.Next == _freeSlot) // occupied entry with an empty slot in entry.Next
			entry.Next = new(keyCode, key, value);
		// entry.Next != _freeSlot would be a threading issue, as this was just checked in the loop. Unhandled.

		_count++;
		_version++;
	}

	private void Expand()
	{
		var newLength = Mathf.NextPowerOfTwo(_entries.Length + 1);
		var newArray = new Entry[newLength];

		for (var i = 0; i < _entries.Length; i++)
		{
			var entry = _entries[i];
			if (entry.Next is null)
				continue;

		BeforeAdding:
			ref var newEntry = ref newArray[FastModulo(entry.HashCode, (uint)newLength)];

			while (newEntry.Next != null)
			{
				if (newEntry.HashCode == entry.HashCode && newEntry.Key.Equals<TKey>(entry.Key))
					ThrowHelper.ThrowAddingDuplicateWithKeyArgumentException(entry.Key);

				if (newEntry.Next == EntryReference.Empty)
					break;

				newEntry = ref newEntry.Next.Entry;
			}

			var nextEntry = entry.Next;
			entry.Next = EntryReference.Empty;

			if (newEntry.Next == null)
				newEntry = entry;
			else
				newEntry.Next = new(entry);

			if (nextEntry != EntryReference.Empty)
			{
				entry = nextEntry.Entry;
				goto BeforeAdding;
			}
		}

		_entries = newArray;
	}

	private ref Entry TryFindEntry(TKey key)
	{
		var keyCode = (uint)HashCode.Get(key);
		ref var entry = ref _entries[FastModulo(keyCode, (uint)_entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals<TKey>(key))
		{
			if (entry.Next != EntryReference.Empty && entry.Next != null)
				entry = ref entry.Next.Entry;
			else
				return ref Unsafe.NullRef<Entry>();
		}

		return ref entry;
	}

	private void RemoveEntry(ref Entry entry)
	{
		entry = entry.Next != EntryReference.Empty && entry.Next != null
			? entry.Next.Entry
			: default;

		_count--;
		_version++;
	}

	public bool Remove(TKey key)
	{
		ref var entry = ref TryFindEntry(key);
		if (Unsafe.IsNullRef(ref entry))
			return false;

		RemoveEntry(ref entry);
		return true;
	}

	public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

	public void Clear()
	{
		Array.Clear(_entries, 0, _entries.Length);
		_count = 0;
		_version++;
	}

	public bool Contains(KeyValuePair<TKey, TValue> item)
		=> TryGetValue(item.Key, out var value)
		&& value.Equals<TValue>(item.Value);

	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		var length = _entries.Length;
		for (var i = 0; i < length; i++)
		{
			var entry = _entries[i];
			if (entry.Next == null)
				continue;

			array[arrayIndex] = new(entry.Key, entry.Value);
			arrayIndex++;

			while (entry.Next != EntryReference.Empty)
			{
				entry = entry.Next.Entry;
				array[arrayIndex] = new(entry.Key, entry.Value);
				arrayIndex++;
			}
		}
	}

	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		ref var entry = ref TryFindEntry(item.Key);

		if (Unsafe.IsNullRef(ref entry)
			|| !entry.Value.Equals<TValue>(item.Value))
		{
			return false;
		}

		RemoveEntry(ref entry);
		return true;
	}

	public Enumerator GetEnumerator()
		=> new(this, Enumerator.KEY_VALUE_PAIR);

	IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
		=> new Enumerator(this, Enumerator.KEY_VALUE_PAIR);

	IEnumerator IEnumerable.GetEnumerator()
		=> new Enumerator(this, Enumerator.KEY_VALUE_PAIR);

	bool IDictionary.Contains(object key)
	{
		Guard.IsNotNull(key);

		return key is TKey tkey
			&& ContainsKey(tkey);
	}

	void IDictionary.Add(object key, object value) => throw new NotImplementedException();

	IDictionaryEnumerator IDictionary.GetEnumerator()
		=> new Enumerator(this, Enumerator.DICT_ENTRY);

	void IDictionary.Remove(object key)
	{
		Guard.IsNotNull(key);

		if (key is TKey tkey)
			Remove(tkey);
	}

	void ICollection.CopyTo(Array array, int index) => throw new NotImplementedException();

	internal struct Entry
	{
		public uint HashCode;
		public TKey Key;
		public EntryReference Next;
		public TValue Value;

		internal Entry(uint hashCode, TKey key, TValue value)
		{
			HashCode = hashCode;
			Key = key;
			Value = value;
			Next = EntryReference.Empty;
		}
	}

	internal class EntryReference
	{
		public static readonly EntryReference Empty = new();

		public Entry Entry;

		public EntryReference(uint hashCode, TKey key, TValue value) => Entry = new(hashCode, key, value);

		public EntryReference(Entry entry) => Entry = entry;

		private EntryReference() { }
	}

	public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
	{
		private readonly FishTable<TKey, TValue> _dictionary;
		private readonly int _version;
		private int _index;
		private EntryReference _next;
		private KeyValuePair<TKey, TValue> _current;
		private readonly int _getEnumeratorRetType;  // What should Enumerator.Current return?

		internal const int DICT_ENTRY = 1;
		internal const int KEY_VALUE_PAIR = 2;

		internal Enumerator(FishTable<TKey, TValue> dictionary, int getEnumeratorRetType)
		{
			_dictionary = dictionary;
			_version = dictionary._version;
			_index = 0;
			_getEnumeratorRetType = getEnumeratorRetType;
			_current = default;
			_next = EntryReference.Empty;
		}

		public bool MoveNext()
		{
			if (_version != _dictionary._version)
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

			if (_next != EntryReference.Empty)
			{
				_current = new(_next.Entry.Key, _next.Entry.Value);
				_next = _next.Entry.Next;
				return true;
			}

			// Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
			// dictionary.count+1 could be negative if dictionary.count is int.MaxValue
			while ((uint)_index < (uint)_dictionary._entries.Length)
			{
				ref var entry = ref _dictionary._entries[_index++];

				if (entry.Next != null)
				{
					_current = new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
					_next = entry.Next;
					return true;
				}
			}

			_index = _dictionary._entries.Length + 1;
			_current = default;
			return false;
		}

		public KeyValuePair<TKey, TValue> Current => _current;

		public void Dispose() { }

		object? IEnumerator.Current
		{
			get
			{
				if (_index == 0 || (_index == _dictionary._entries.Length + 1))
					ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

				return _getEnumeratorRetType == DICT_ENTRY
					? new DictionaryEntry(_current.Key, _current.Value)
					: new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
			}
		}

		void IEnumerator.Reset()
		{
			if (_version != _dictionary._version)
				ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion();

			_index = 0;
			_current = default;
		}

		DictionaryEntry IDictionaryEnumerator.Entry
		{
			get
			{
				if (_index == 0 || (_index == _dictionary._entries.Length + 1))
					ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

				return new DictionaryEntry(_current.Key, _current.Value);
			}
		}

		object? IDictionaryEnumerator.Key
		{
			get
			{
				if (_index == 0 || (_index == _dictionary._entries.Length + 1))
					ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

				return _current.Key;
			}
		}

		object? IDictionaryEnumerator.Value
		{
			get
			{
				if (_index == 0 || (_index == _dictionary._entries.Length + 1))
					ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();

				return _current.Value;
			}
		}
	}

	private static class ThrowHelper
	{
		[DoesNotReturn]
		internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion()
			=> throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

		[DoesNotReturn]
		internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen()
			=> throw new InvalidOperationException("Enumeration has either not started or has already finished.");

		[DoesNotReturn]
		internal static void ThrowKeyNotFoundException<T>(T key)
		{
			Guard.IsNotNull(key);
			ThrowKeyNotFoundException((object)key);
		}

		[DoesNotReturn]
		internal static V ThrowKeyNotFoundException<T, V>(T key)
		{
			Guard.IsNotNull(key);
			ThrowKeyNotFoundException((object)key);
			return default;
		}

		[DoesNotReturn]
		internal static void ThrowKeyNotFoundException(object key)
			=> throw new KeyNotFoundException(
				string.Format("The given key '{0}' was not present in the dictionary.", key));

		[DoesNotReturn]
		internal static void ThrowWrongValueTypeArgumentException(object value)
			=> throw new ArgumentException(
				string.Format("The value \"{0}\" is not of type \"{1}\" and cannot be used in this generic collection.",
				 value, typeof(TValue)),
				nameof(value));

		[DoesNotReturn]
		internal static void ThrowWrongKeyTypeArgumentException(object? key)
		{
			Guard.IsNotNull(key);
			throw new ArgumentException(
			 string.Format("The value \"{0}\" is not of type \"{1}\" and cannot be used in this generic collection.",
					key, typeof(TKey)),
				nameof(key));
		}

		[DoesNotReturn]
		internal static void ThrowAddingDuplicateWithKeyArgumentException<T>(T key)
		{
			Guard.IsNotNull(key);
			ThrowAddingDuplicateWithKeyArgumentException((object)key);
		}

		[DoesNotReturn]
		internal static void ThrowAddingDuplicateWithKeyArgumentException(object key)
			=> throw new ArgumentException(
				string.Format("An item with the same key has already been added. Key: {0}", key));
	}
}
public static class Extensions
{
	public static ref TValue TryGetReferenceUnsafe<TKey, TValue>(this FishTable<TKey, TValue> fishTable, ref TKey key)
		where TKey : struct, IEquatable<TKey>
	{
		var keyCode = (uint)HashCode.Get(ref key);
		ref var entry = ref fishTable._entries[FishTable<TKey, TValue>.FastModulo(keyCode, (uint)fishTable._entries.Length)];

		while (entry.HashCode != keyCode || !entry.Key.Equals(ref key))
		{
			if (entry.Next != FishTable<TKey, TValue>.EntryReference.Empty && entry.Next != null)
				entry = ref entry.Next.Entry;
			else
				return ref Unsafe.NullRef<TValue>()!;
		}

		return ref entry.Next == null
			? ref Unsafe.NullRef<TValue>()
			: ref entry.Value;
	}
}
#pragma warning restore CS8766, CS8767