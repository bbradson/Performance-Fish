// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace PerformanceFish.Utility;

public static class CollectionExtensions
{
	public static bool Any<T>(this T[] array, Predicate<T> predicate)
	{
		var length = array.Length;
		return length != 0 && Any(ref array[0], predicate, (nuint)length);
	}

	// overlaps with method in GenCollection
	// public static bool Any<T>(this List<T> list, Predicate<T> predicate)
	// {
	// 	var count = list.Count;
	// 	return count != 0 && Any(ref list._items[0], predicate, count);
	// }

	public static bool Any<T>(this IList<T> iList, Predicate<T> predicate)
	{
		var count = iList.Count;
		for (var i = 0; i < count; i++)
		{
			if (predicate(iList[i]))
				return true;
		}

		return false;
	}
	
	public static unsafe bool Any<T>(this T[] array, delegate*<T, bool> predicate)
	{
		var length = array.Length;
		return length != 0 && Any(ref array[0], predicate, (nuint)length);
	}

	public static unsafe bool Any<T>(this List<T> list, delegate*<T, bool> predicate)
	{
		var count = list.Count;
		return count != 0 && Any(ref list._items[0], predicate, (nuint)count);
	}

	public static bool ExistsAndNotNull<T>(this List<T>? list, Predicate<T> match)
	{
		if (list is null)
			return false;
		
		var count = list.Count;
		return count != 0 && Any(ref list._items[0], match, (nuint)count);
	}

	public static bool Contains<T>(this T[] array, T item)
	{
		var length = array.Length;
		return length != 0 && Contains(ref array[0], item, (nuint)length);
	}

	public static bool Contains<T>(this IList<T> iList, T item)
	{
		var count = iList.Count;
		for (var i = 0; i < count; i++)
		{
			if (iList[i].Equals<T>(item))
				return true;
		}

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? FirstOrDefault<T>(this T[] array, Predicate<T> predicate)
	{
		var length = array.Length;
		return length == 0 ? default : FirstOrDefault(ref array[0], predicate, (nuint)length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? FirstOrDefaultFast<T>(this List<T> list, Predicate<T> predicate)
	{
		var count = list.Count;
		return count == 0 ? default : FirstOrDefault(ref list._items[0], predicate, (nuint)count);
	}

	public static T? FirstOrDefault<T>(this IList<T> iList, Predicate<T> predicate)
	{
		var count = iList.Count;
		for (var i = 0; i < count; i++)
		{
			if (predicate(iList[i]))
				return iList[i];
		}

		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T? FirstOrDefault<T>(this T[] array, delegate*<T, bool> predicate)
	{
		var length = array.Length;
		return length == 0 ? default : FirstOrDefault(ref array[0], predicate, (nuint)length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T? FirstOrDefault<T>(this List<T> list, delegate*<T, bool> predicate)
	{
		var count = list.Count;
		return count == 0 ? default : FirstOrDefault(ref list._items[0], predicate, (nuint)count);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? FirstOrDefault<T>(this T[] array) => array.Length != 0 ? array[0] : default;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? FirstOrDefault<T>(this List<T> list) => list.Count != 0 ? list[0] : default;

	public static T? FirstOrDefault<T>(this IList<T> iList) => iList.Count != 0 ? iList[0] : default;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T First<T>(this T[] array, Predicate<T> predicate)
	{
		var length = array.Length;
		return length == 0 ? ThrowForNoElements<T>() : First(ref array[0], predicate, (nuint)length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T First<T>(this List<T> list, Predicate<T> predicate)
	{
		var count = list.Count;
		return count == 0 ? ThrowForNoElements<T>() : First(ref list._items[0], predicate, (nuint)count);
	}

	public static T First<T>(this IList<T> iList, Predicate<T> predicate)
	{
		var count = iList.Count;
		for (var i = 0; i < count; i++)
		{
			if (predicate(iList[i]))
				return iList[i];
		}

		return ThrowForNoElements<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T First<T>(this T[] array, delegate*<T, bool> predicate)
	{
		var length = array.Length;
		return length == 0 ? ThrowForNoElements<T>() : First(ref array[0], predicate, (nuint)length);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe T First<T>(this List<T> list, delegate*<T, bool> predicate)
	{
		var count = list.Count;
		return count == 0 ? ThrowForNoElements<T>() : First(ref list._items[0], predicate, (nuint)count);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T First<T>(this T[] array) => array.Length != 0 ? array[0] : ThrowForNoElements<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T First<T>(this List<T> list) => list.Count != 0 ? list[0] : ThrowForNoElements<T>();

	public static T First<T>(this IList<T> iList) => iList.Count != 0 ? iList[0] : ThrowForNoElements<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe bool Any<T>(ref T reference, Predicate<T> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return true;

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe bool Any<T>(ref T reference, delegate*<T, bool> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return true;

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe bool Contains<T>(ref T reference, T item, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (Unsafe.AddByteOffset(ref reference, i).Equals<T>(item))
				return true;

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe T? FirstOrDefault<T>(ref T reference, Predicate<T> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return Unsafe.AddByteOffset(ref reference, i);

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe T First<T>(ref T reference, Predicate<T> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return Unsafe.AddByteOffset(ref reference, i);

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return ThrowForNoElements<T>();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe T? FirstOrDefault<T>(ref T reference, delegate*<T, bool> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return Unsafe.AddByteOffset(ref reference, i);

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static unsafe T First<T>(ref T reference, delegate*<T, bool> predicate, nuint length)
	{
		nuint i = 0;
		length *= (nuint)sizeof(T);

		do
		{
			if (predicate(Unsafe.AddByteOffset(ref reference, i)))
				return Unsafe.AddByteOffset(ref reference, i);

			i += (nuint)sizeof(T);
		}
		while (i < length);

		return ThrowForNoElements<T>();
	}
	
	public static unsafe void RemoveAtFastUnorderedUnsafe<T>(this List<T> list, int index)
	{
		fixed (T* lastBucket = &list._items[list._size - 1])
		{
			list[index] = *lastBucket;
			*lastBucket = default;
		}

		list._size--;
	}

	public static List<T> AsOrToList<T>(this IEnumerable<T> enumerable) => enumerable as List<T> ?? enumerable.ToList();

	public static unsafe ref T GetReference<T>(this NativeArray<T> nativeArray, int index) where T : struct
		=> ref Unsafe.AsRef<T>((T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(nativeArray) + index);

	public static void UnwrapArray<T>(this List<T> list, out T[] array, out int count)
	{
		array = list._items;
		count = list._size;
		Guard.IsLessThanOrEqualTo((uint)count, (uint)array.Length);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void SetSize<T>(this List<T> list, int size) => list._size = size;

	public static void AddRange<T>(this List<T> list, IndexedFishSet<T> set)
	{
		list.EnsureCapacity(list._size + set.Count);
		set.CopyTo(list._items, list._size);
		list._size += set.Count;
		list._version++;
	}
	
	[DoesNotReturn]
	private static T ThrowForNoElements<T>()
		=> throw new InvalidOperationException("Sequence contains no elements");
}