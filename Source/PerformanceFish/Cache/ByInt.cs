// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable WithExpressionModifiesAllMembers

namespace PerformanceFish.Cache;

#pragma warning disable CS9091
[PublicAPI]
public record struct ByInt<T, TResult> where T : notnull where TResult : new()
{
	public int Key;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T key) => Key = FunctionPointers.IndexGetter<T>.Default(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByInt(int key) => Key = key;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByInt<T, TResult> other) => Key == other.Key;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Key;
	
	private static readonly object _valueInitializerLock = new();

	private static readonly List<IDictionary> _allCaches = [];

	private static readonly FishTable<ByInt<T, TResult>, TResult> _get = InitializeNew();

	[ThreadStatic]
	private static FishTable<ByInt<T, TResult>, TResult>? _getThreadStatic;

	private static Func<ByInt<T, TResult>, TResult>? _valueInitializer;
	
	public static Func<ByInt<T, TResult>, TResult>? ValueInitializer
	{
		get
		{
			lock (_valueInitializerLock)
				return _valueInitializer;
		}
		set
		{
			lock (_valueInitializerLock)
			{
				value ??= static _ => Reflection.New<TResult>();
				_valueInitializer = value;
				_get.ValueInitializer = value;
			}
		}
	}

	public static FishTable<ByInt<T, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= InitializeNew();
	}

	public static FishTable<ByInt<T, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(int key)
		=> ref Get.GetOrAddReference(Unsafe.As<int, ByInt<T, TResult>>(ref key));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(ByInt<T, TResult> key) => ref Get.GetOrAddReference(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TResult GetOrAdd(int key) => Get.GetOrAdd(Unsafe.As<int, ByInt<T, TResult>>(ref key));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TResult GetOrAdd(ByInt<T, TResult> key) => Get.GetOrAdd(key);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(int key)
		=> ref Get.GetReference(Unsafe.As<int, ByInt<T, TResult>>(ref key));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T key) => ref Get.GetReference(new(key));

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FishTable<ByInt<T, TResult>, TResult> InitializeNew()
		=> Utility.AddNew(_allCaches, ValueInitializer);

	public static void Clear() => Utility.Internal.ClearIn(_allCaches);
}

[PublicAPI]
public record struct ByInt<T1, T2, TResult>
	where T1 : notnull where T2 : notnull where TResult : new()
{
	public int First, Second;
	
	private static readonly object _valueInitializerLock = new();
	
	private static readonly List<IDictionary> _allCaches = [];
	
	private static readonly FishTable<ByInt<T1, T2, TResult>, TResult> _get = InitializeNew();

	[ThreadStatic]
	private static FishTable<ByInt<T1, T2, TResult>, TResult>? _getThreadStatic;

	private static Func<ByInt<T1, T2, TResult>, TResult>? _valueInitializer;
	
	public static Func<ByInt<T1, T2, TResult>, TResult>? ValueInitializer
	{
		get
		{
			lock (_valueInitializerLock)
				return _valueInitializer;
		}
		set
		{
			lock (_valueInitializerLock)
			{
				value ??= static _ => Reflection.New<TResult>();
				_valueInitializer = value;
				_get.ValueInitializer = value;
			}
		}
	}

	public static FishTable<ByInt<T1, T2, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= InitializeNew();
	}

	private static ref FishTable<ByInt<T1, T2, TResult>, TResult> GetCacheRef
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			_getThreadStatic ??= Utility.AddNew<ByInt<T1, T2, TResult>, TResult>();
			return ref _getThreadStatic!;
		}
	}

	public static FishTable<ByInt<T1, T2, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(int first, int second)
		=> ref Get.GetOrAddReference(default(ByInt<T1, T2, TResult>) with { First = first, Second = second });

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByInt<T1, T2, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TResult GetOrAdd(int first, int second)
		=> Get.GetOrAdd(default(ByInt<T1, T2, TResult>) with { First = first, Second = second });

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TResult GetOrAdd(in ByInt<T1, T2, TResult> key) => Get.GetOrAdd(ref Unsafe.AsRef(in key));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(int first, int second)
		=> ref Get.GetReference(default(ByInt<T1, T2, TResult>) with { First = first, Second = second });

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T1 first, T2 second)
		=> ref Get.GetReference(new(first, second));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T1 first, T2 second)
	{
		First = FunctionPointers.IndexGetter<T1>.Default(first);
		Second = FunctionPointers.IndexGetter<T2>.Default(second);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByInt(int first, int second)
	{
		First = first;
		Second = second;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByInt<T1, T2, TResult> other)
		=> Unsafe.As<ByInt<T1, T2, TResult>, long>(ref this) == Unsafe.As<ByInt<T1, T2, TResult>, long>(ref other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(First, Second);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FishTable<ByInt<T1, T2, TResult>, TResult> InitializeNew()
		=> Utility.AddNew(_allCaches, ValueInitializer);

	public static void Clear() => Utility.Internal.ClearIn(_allCaches);
}

[PublicAPI]
public record struct ByInt<T1, T2, T3, TResult>
	where T1 : notnull where T2 : notnull where T3 : notnull where TResult : new()
{
	public int First, Second, Third;
	
	private static readonly object _valueInitializerLock = new();
	
	private static readonly List<IDictionary> _allCaches = [];
	
	private static FishTable<ByInt<T1, T2, T3, TResult>, TResult> _get = InitializeNew();

	[ThreadStatic]
	private static FishTable<ByInt<T1, T2, T3, TResult>, TResult>? _getThreadStatic;

	private static Func<ByInt<T1, T2, T3, TResult>, TResult>? _valueInitializer;
	
	public static Func<ByInt<T1, T2, T3, TResult>, TResult>? ValueInitializer
	{
		get
		{
			lock (_valueInitializerLock)
				return _valueInitializer;
		}
		set
		{
			lock (_valueInitializerLock)
			{
				value ??= static _ => Reflection.New<TResult>();
				_valueInitializer = value;
				_get.ValueInitializer = value;
			}
		}
	}

	public static FishTable<ByInt<T1, T2, T3, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= InitializeNew();
	}

	public static FishTable<ByInt<T1, T2, T3, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref TResult GetOrAddReference(int first, int second, int third)
	{
		var key = default(ByInt<T1, T2, T3, TResult>) with { First = first, Second = second, Third = third };
		return ref Get.GetOrAddReference(ref key);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByInt<T1, T2, T3, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(int first, int second, int third)
		=> ref Get.GetReference(default(ByInt<T1, T2, T3, TResult>) with
		{
			First = first, Second = second, Third = third
		});

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T1 first, T2 second, T3 third)
		=> ref Get.GetReference(new(first, second, third));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T1 first, T2 second, T3 third)
	{
		First = FunctionPointers.IndexGetter<T1>.Default(first);
		Second = FunctionPointers.IndexGetter<T2>.Default(second);
		Third = FunctionPointers.IndexGetter<T3>.Default(third);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByInt<T1, T2, T3, TResult> other)
		=> (Unsafe.As<ByInt<T1, T2, T3, TResult>, long>(ref this)
			== Unsafe.As<ByInt<T1, T2, T3, TResult>, long>(ref other))
		& (Third == other.Third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(First, Second, Third);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FishTable<ByInt<T1, T2, T3, TResult>, TResult> InitializeNew()
		=> Utility.AddNew(_allCaches, ValueInitializer);

	public static void Clear() => Utility.Internal.ClearIn(_allCaches);
}

[PublicAPI]
public record struct ByInt<T1, T2, T3, T4, TResult>
	where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull
	where TResult : new()
{
	public int First, Second, Third, Fourth;
	
	private static readonly object _valueInitializerLock = new();
	
	private static readonly List<IDictionary> _allCaches = [];
	
	private static FishTable<ByInt<T1, T2, T3, T4, TResult>, TResult> _get = InitializeNew();

	[ThreadStatic]
	private static FishTable<ByInt<T1, T2, T3, T4, TResult>, TResult>? _getThreadStatic;

	private static Func<ByInt<T1, T2, T3, T4, TResult>, TResult>? _valueInitializer;
	
	public static Func<ByInt<T1, T2, T3, T4, TResult>, TResult>? ValueInitializer
	{
		get
		{
			lock (_valueInitializerLock)
				return _valueInitializer;
		}
		set
		{
			lock (_valueInitializerLock)
			{
				value ??= static _ => Reflection.New<TResult>();
				_valueInitializer = value;
				_get.ValueInitializer = value;
			}
		}
	}

	public static FishTable<ByInt<T1, T2, T3, T4, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= InitializeNew();
	}

	public static FishTable<ByInt<T1, T2, T3, T4, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref TResult GetOrAddReference(int first, int second, int third, int fourth)
	{
		var key = default(ByInt<T1, T2, T3, T4, TResult>) with
		{
			First = first, Second = second, Third = third, Fourth = fourth
		};
		return ref Get.GetOrAddReference(ref key);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByInt<T1, T2, T3, T4, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(int first, int second, int third, int fourth)
		=> ref Get.GetReference(default(ByInt<T1, T2, T3, T4, TResult>) with
		{
			First = first, Second = second, Third = third, Fourth = fourth
		});

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T1 first, T2 second, T3 third, T4 fourth)
		=> ref Get.GetReference(new(first, second, third, fourth));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public unsafe ByInt(T1 first, T2 second, T3 third, T4 fourth)
	{
		First = FunctionPointers.IndexGetter<T1>.Default(first);
		Second = FunctionPointers.IndexGetter<T2>.Default(second);
		Third = FunctionPointers.IndexGetter<T3>.Default(third);
		Fourth = FunctionPointers.IndexGetter<T4>.Default(fourth);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByInt<T1, T2, T3, T4, TResult> other)
		=> (Unsafe.As<ByInt<T1, T2, T3, T4, TResult>, long>(ref this)
				== Unsafe.As<ByInt<T1, T2, T3, T4, TResult>, long>(ref other))
			& (Unsafe.As<int, long>(ref Third) == Unsafe.As<int, long>(ref other.Third));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(First, Second, Third, Fourth);

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FishTable<ByInt<T1, T2, T3, T4, TResult>, TResult> InitializeNew()
		=> Utility.AddNew(_allCaches, ValueInitializer);

	public static void Clear() => Utility.Internal.ClearIn(_allCaches);
}
#pragma warning restore CS9091