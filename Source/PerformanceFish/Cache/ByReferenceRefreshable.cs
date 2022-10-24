// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;
public static class ByReferenceRefreshable<T_first, T_result>
	where T_first : notnull
	where T_result : IIsRefreshable<T_first, T_result>
{
	private static Dictionary<T_first, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<T_first, T_result>? _getThreadStatic;

	public static Dictionary<T_first, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<T_first, T_result>>();

	public static Dictionary<T_first, T_result> GetDirectly => _get;

	static ByReferenceRefreshable() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T_result GetValue(T_first key)
	{
		ref var cache = ref Get.TryGetReferenceUnsafe(key);

		if (Unsafe.IsNullRef(ref cache))
		{
			Get[key] = Reflection.New<T_result>();
			cache = ref Get.GetReference(key)!;
			goto RefreshCache;
		}

		if (!cache.ShouldRefreshNow)
			return ref cache!;

	RefreshCache:
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(T_first key, out T_result value)
		=> Get.TryGetValue(key, out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(T_first key)
	{
		var cache = Get.TryGetValue(key);
		cache ??= Reflection.New<T_result>();
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);
		return cache;
	}
}

public struct ByReferenceRefreshable<T_first, T_second, T_result> : IEquatable<ByReferenceRefreshable<T_first, T_second, T_result>>
	where T_first : notnull where T_second : notnull
	where T_result : IIsRefreshable<ByReferenceRefreshable<T_first, T_second, T_result>, T_result>
{
	internal ByReference<T_first, T_second, T_result> _innerValue;

	private static Dictionary<ByReferenceRefreshable<T_first, T_second, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByReferenceRefreshable<T_first, T_second, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByReferenceRefreshable<T_first, T_second, T_result>, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByReferenceRefreshable<T_first, T_second, T_result>, T_result>>();

	public static Dictionary<ByReferenceRefreshable<T_first, T_second, T_result>, T_result> GetDirectly => _get;

	static ByReferenceRefreshable() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref T_result GetValue(ref ByReferenceRefreshable<T_first, T_second, T_result> key)
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(ByReferenceRefreshable<T_first, T_second, T_result> key, out T_result value)
		=> Get.TryGetValue(key, out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(ByReferenceRefreshable<T_first, T_second, T_result> key)
	{
		var cache = Get.TryGetValue(ref key);
		cache ??= Reflection.New<T_result>();
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);
		return cache;
	}

	public ByReferenceRefreshable(T_first first, T_second second) => _innerValue = new(first, second);

	public T_first First => _innerValue.First;

	public T_second Second => _innerValue.Second;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReferenceRefreshable<T_first, T_second, T_result> other)
		=> _innerValue.Equals(other._innerValue);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _innerValue.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByReferenceRefreshable<T_first, T_second, T_result> cache
		&& Equals(cache);

	public static bool operator ==(ByReferenceRefreshable<T_first, T_second, T_result> left, ByReferenceRefreshable<T_first, T_second, T_result> right)
		=> left.Equals(right);

	public static bool operator !=(ByReferenceRefreshable<T_first, T_second, T_result> left, ByReferenceRefreshable<T_first, T_second, T_result> right)
		=> !(left == right);
}

public struct ByReferenceRefreshable<T_first, T_second, T_third, T_result> : IEquatable<ByReferenceRefreshable<T_first, T_second, T_third, T_result>>
	where T_first : notnull where T_second : notnull where T_third : notnull
	where T_result : IIsRefreshable<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result>
{
	private ByReference<T_first, T_second, T_third, T_result> _innerValue;

	private static Dictionary<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result> Get
		=> /*UnityData.IsInMainThread ? _get
		:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result>>();

	public static Dictionary<ByReferenceRefreshable<T_first, T_second, T_third, T_result>, T_result> GetDirectly => _get;

	static ByReferenceRefreshable() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref T_result GetValue(ref ByReferenceRefreshable<T_first, T_second, T_third, T_result> key)
	{
		ref var cache = ref Unsafe.AsRef<T_result>(Unsafe.AsPointer(ref Get.TryGetReferenceUnsafe(ref key)));

		if (Unsafe.IsNullRef(ref cache))
		{
			Get[key] = Activator.CreateInstance<T_result>();
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetValue(ByReferenceRefreshable<T_first, T_second, T_third, T_result> key, out T_result value)
		=> Get.TryGetValue(key, out value)
		&& !value.ShouldRefreshNow;

	public static T_result GetValueAndRefreshNow(ByReferenceRefreshable<T_first, T_second, T_third, T_result> key)
	{
		var cache = Get.TryGetValue(ref key);
		cache ??= Activator.CreateInstance<T_result>();
		cache.ShouldRefreshNow = false;
		Get[key] = cache.SetNewValue(key);
		return cache;
	}

	public ByReferenceRefreshable(T_first first, T_second second, T_third third)
		=> _innerValue = new(first, second, third);

	public T_first First => _innerValue.First;
	public T_second Second => _innerValue.Second;
	public T_third Third => _innerValue.Third;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReferenceRefreshable<T_first, T_second, T_third, T_result> other)
		=> _innerValue.Equals(other._innerValue);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _innerValue.GetHashCode();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByReferenceRefreshable<T_first, T_second, T_third, T_result> cache
		&& Equals(cache);

	public static bool operator ==(ByReferenceRefreshable<T_first, T_second, T_third, T_result> left, ByReferenceRefreshable<T_first, T_second, T_third, T_result> right)
		=> left.Equals(right);

	public static bool operator !=(ByReferenceRefreshable<T_first, T_second, T_third, T_result> left, ByReferenceRefreshable<T_first, T_second, T_third, T_result> right)
		=> !(left == right);
}