// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;

namespace PerformanceFish.Cache;
public struct ByInt<T_in, T_result>
	where T_in : notnull
	where T_result : IIsRefreshable<T_in, T_result>
{
	private static Dictionary<ByInt<T_in, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByInt<T_in, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByInt<T_in, T_result>, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByInt<T_in, T_result>, T_result>>();

	public static Dictionary<ByInt<T_in, T_result>, T_result> GetDirectly => _get;

	static ByInt() => Utility.All.Add(_get);

	public int Key;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T_in key) => Key = FunctionPointers.IndexGetter<T_in>.Default(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T_result GetValue(T_in key)
	{
		ref var cache = ref Get.TryGetReferenceUnsafe(new(key));

		if (Unsafe.IsNullRef(ref cache))
		{
			Get[new(key)] = Reflection.New<T_result>();
			cache = ref Get.GetReference(new(key))!;
			goto RefreshCache;
		}

		if (!cache.ShouldRefreshNow)
			return ref cache!;

	RefreshCache:
		cache.ShouldRefreshNow = false;
		Get[new(key)] = cache.SetNewValue(key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceUnsafe(int key)
		=> ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByInt<T_in, T_result>>(ref key))));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceChecked(int key, out bool result)
	{
		ref var value = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByInt<T_in, T_result>>(ref key))));
		result = !Unsafe.IsNullRef(ref value) && !value.ShouldRefreshNow;
		return ref value!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(T_in key, [MaybeNullWhen(false)] out T_result value)
		=> Get.TryGetValue(new(key), out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(T_in key)
	{
		var cache = Get.TryGetValue(new(key));
		cache ??= Reflection.New<T_result>();
		cache.ShouldRefreshNow = false;
		Get[new(key)] = cache.SetNewValue(key);
		return cache;
	}
}

public struct ByIntRefreshable<T_first, T_second, T_result> : IEquatable<ByIntRefreshable<T_first, T_second, T_result>>
	where T_first : notnull where T_second : notnull
	where T_result : IIsRefreshable<ByIntRefreshable<T_first, T_second, T_result>, T_result>
{
	private static EqualityComparer<ByIntRefreshable<T_first, T_second, T_result>> _comparer = new Comparer();
	private class Comparer : EqualityComparer<ByIntRefreshable<T_first, T_second, T_result>>
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(ByIntRefreshable<T_first, T_second, T_result> x, ByIntRefreshable<T_first, T_second, T_result> y)
			=> x.First == y.First
			&& x.Second == y.Second;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode(ByIntRefreshable<T_first, T_second, T_result> obj)
			=> HashCode.Combine(obj.First, obj.Second);
	}

	private static Dictionary<ByIntRefreshable<T_first, T_second, T_result>, T_result> _get = new(_comparer);
	[ThreadStatic]
	private static Dictionary<ByIntRefreshable<T_first, T_second, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByIntRefreshable<T_first, T_second, T_result>, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByIntRefreshable<T_first, T_second, T_result>, T_result>>(_comparer);

	public static Dictionary<ByIntRefreshable<T_first, T_second, T_result>, T_result> GetDirectly => _get;

	static ByIntRefreshable() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref T_result GetValue(ref ByIntRefreshable<T_first, T_second, T_result> key)
	{
		// CS8347 without this crap
		ref var cache = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref key)));

		if (Unsafe.IsNullRef(ref cache))
		{
			Get[key] = Reflection.New<T_result>();
			cache = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref key)))!;
			goto RefreshCache;
		}

		if (!cache.ShouldRefreshNow)
			return ref cache!;

	RefreshCache:
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceUnsafe(int second, int first)
		=> ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByIntRefreshable<T_first, T_second, T_result>>(ref first))));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceChecked(int second, int first, out bool result)
	{
		ref var value = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByIntRefreshable<T_first, T_second, T_result>>(ref first))));
		result = !Unsafe.IsNullRef(ref value) && !value.ShouldRefreshNow;
		return ref value!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(ByIntRefreshable<T_first, T_second, T_result> key, [MaybeNullWhen(false)] out T_result value)
		=> Get.TryGetValue(key, out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(ByIntRefreshable<T_first, T_second, T_result> key)
	{
		var cache = Get.TryGetValue(ref key);
		cache ??= Reflection.New<T_result>();
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);
		return cache;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByIntRefreshable(T_first first, T_second second)
	{
		First = FunctionPointers.IndexGetter<T_first>.Default(first);
		Second = FunctionPointers.IndexGetter<T_second>.Default(second);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByIntRefreshable(int first, int second)
	{
		First = first;
		Second = second;
	}

	public int First;
	public int Second;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByIntRefreshable<T_first, T_second, T_result> other)
		=> First == other.First
		&& Second == other.Second;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
		=> HashCode.Combine(First, Second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByIntRefreshable<T_first, T_second, T_result> cache
		&& Equals(cache);

	public static bool operator ==(ByIntRefreshable<T_first, T_second, T_result> left, ByIntRefreshable<T_first, T_second, T_result> right)
		=> left.Equals(right);

	public static bool operator !=(ByIntRefreshable<T_first, T_second, T_result> left, ByIntRefreshable<T_first, T_second, T_result> right)
		=> !(left == right);
}

public struct ByInt<T_first, T_second, T_third, T_result> : IEquatable<ByInt<T_first, T_second, T_third, T_result>>
	where T_first : notnull where T_second : notnull where T_third : notnull
	where T_result : IIsRefreshable<ByInt<T_first, T_second, T_third, T_result>, T_result>
{
	private static Dictionary<ByInt<T_first, T_second, T_third, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByInt<T_first, T_second, T_third, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByInt<T_first, T_second, T_third, T_result>, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByInt<T_first, T_second, T_third, T_result>, T_result>>();

	public static Dictionary<ByInt<T_first, T_second, T_third, T_result>, T_result> GetDirectly => _get;

	static ByInt() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref T_result GetValue(ByInt<T_first, T_second, T_third, T_result> key)
	{
		ref var cache = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref key)));

		if (Unsafe.IsNullRef(ref cache))
		{
			Get[key] = Reflection.New<T_result>();
			cache = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref key)))!;
			goto RefreshCache;
		}

		if (!cache.ShouldRefreshNow)
			return ref cache!;

	RefreshCache:
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceUnsafe(int second, int first, int third)
		=> ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByInt<T_first, T_second, T_third, T_result>>(ref first))));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref T_result TryGetReferenceChecked(int second, int first, out bool result)
	{
		ref var value = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref Unsafe.As<int, ByInt<T_first, T_second, T_third, T_result>>(ref first))));
		result = !Unsafe.IsNullRef(ref value) && !value.ShouldRefreshNow;
		return ref value!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(ByInt<T_first, T_second, T_third, T_result> key, out T_result value)
		=> Get.TryGetValue(key, out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(ByInt<T_first, T_second, T_third, T_result> key)
	{
		var cache = Get.TryGetValue(ref key);
		cache ??= Reflection.New<T_result>();
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);
		return cache;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T_first first, T_second second, T_third third)
	{
		First = FunctionPointers.IndexGetter<T_first>.Default(first);
		Second = FunctionPointers.IndexGetter<T_second>.Default(second);
		Third = FunctionPointers.IndexGetter<T_third>.Default(third);
	}

	public int First;
	public int Second;
	public int Third;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByInt<T_first, T_second, T_third, T_result> other)
		=> First == other.First
		&& Second == other.Second
		&& Third == other.Third;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
		=> HashCode.Combine(First, Second, Third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByInt<T_first, T_second, T_third, T_result> cache
		&& Equals(cache);

	public static bool operator ==(ByInt<T_first, T_second, T_third, T_result> left, ByInt<T_first, T_second, T_third, T_result> right)
		=> left.Equals(right);

	public static bool operator !=(ByInt<T_first, T_second, T_third, T_result> left, ByInt<T_first, T_second, T_third, T_result> right)
		=> !(left == right);
}