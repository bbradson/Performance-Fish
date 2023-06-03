// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using JetBrains.Annotations;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace PerformanceFish.Cache;

#pragma warning disable CS9091, 9082
[PublicAPI]
public static class ByReferenceUnclearable<T, TResult>
	where T : notnull where TResult : new()
{
	public const int MINIMUM_CALL_COUNT_FOR_STARTING_TASK = 8;
	
	public static object SyncLock = new();

	private static FishTable<T, TResult> _get = Utility.AddNewUnclearable<T, TResult>();

	[ThreadStatic]
	private static FishTable<T, TResult>? _getThreadStatic;

	public static FishTable<T, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNewUnclearable<T, TResult>();
	}

	public static FishTable<T, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(T key) => ref Get.GetOrAddReference(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(ref T key) => ref Get.GetOrAddReference(ref key);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T key) => ref Get.GetReference(key);
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult>(T key) where VResult : TResult, ICacheable<T>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult, V2>(T key, V2 second) where VResult : TResult, ICacheable<T, V2>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult, V2, V3>(T key, V2 second, V3 third)
		where VResult : TResult, ICacheable<T, V2, V3>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second, third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult>(T key) where VResult : TResult, ICacheable<T>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult, V2>(T key, V2 second) where VResult : TResult, ICacheable<T, V2>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key, second);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult, V2, V3>(T key, V2 second, V3 third)
		where VResult : TResult, ICacheable<T, V2, V3>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key, second, third);

		return ref cache!;
	}

	public static bool UpdateAsyncCache<TCacheValue, TCacheResult>(ref TCacheValue cache, T key)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		if (cache.Task is null)
			return SyncWithCentralCache<TCacheValue, TCacheResult>(ref cache, key);

		if (cache.Task.IsCompletedSuccessfully)
			cache.Result = cache.Task.Result;

		return !cache.Result.Equals<TCacheResult>(default);
	}

	private static bool SyncWithCentralCache<TCacheValue, TCacheResult>(ref TCacheValue cache, T key)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		lock (SyncLock)
		{
			ref var centralCache = ref Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key));
			var cacheCopy = centralCache;

			centralCache.Task ??= Task.Run(() => UpdateCacheAsync<TCacheValue, TCacheResult>(key, cacheCopy));

			cache.Task = centralCache.Task;
			cache.Result = centralCache.Result;
			return !cache.Result.Equals<TCacheResult>(default);
		}
	}

	private static TCacheResult UpdateCacheAsync<TCacheValue, TCacheResult>(T key, TCacheValue cacheCopy)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		TCacheResult? result = default;
		try
		{
			result = cacheCopy.MakeResultAsync(key);

			lock (SyncLock)
				Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key)).Result = result;
		}
		catch (Exception e)
		{
			Log.Error($"{e}");
		}

		return result ?? throw new();
	}

	[SecuritySafeCritical]
	public static Task<TCacheResult>? RequestFromCacheAsync<TCacheValue, TArgument2, TCacheResult>(T key, TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		TCacheValue cacheCopy;
		lock (SyncLock)
		{
			ref var centralCache = ref Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key));
			cacheCopy = centralCache;

			if (centralCache.Task is null)
			{
				centralCache.Task = CreateTaskCountdown<TCacheResult>();
				cacheCopy.Task = null;
			}
			// ReSharper disable once SuspiciousTypeConversion.Global
			else if (centralCache.Task is Countdown taskCountdown)
			{
				Interlocked.Decrement(ref taskCountdown.Value);
				
				if (taskCountdown.Value <= 0)
				{
					cacheCopy.Task = centralCache.Task
						= StartTask<TCacheValue, TArgument2, TCacheResult>(key, second, cacheCopy);
				}
				else
				{
					cacheCopy.Task = null;
				}
			}
			else
			{
				cacheCopy.Task = centralCache.Task;
			}
		}

		return cacheCopy.Task;
	}

	private static Task<TCacheResult> StartTask<TCacheValue, TArgument2, TCacheResult>(T key, TArgument2 second,
		TCacheValue cacheCopy) where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
		=> Task.Run(() => UpdateCacheAsync<TCacheValue, TArgument2, TCacheResult>(key, cacheCopy, second));

	[SecuritySafeCritical]
	private static Task<TCacheResult> CreateTaskCountdown<TCacheResult>()
		=> Unsafe.As<Task<TCacheResult>>(new Countdown(MINIMUM_CALL_COUNT_FOR_STARTING_TASK));

	public static bool UpdateAsyncCache<TCacheValue, TArgument2, TCacheResult>(ref TCacheValue cache, T key,
		TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		cache.Task ??= RequestFromCacheAsync<TCacheValue, TArgument2, TCacheResult>(key, second);

		if (cache.Task is null)
			return false;
		
		if (cache.Task.IsCompletedSuccessfully)
			cache.Result = cache.Task.Result;

		return !cache.Result.Equals<TCacheResult>(default);
	}

	private static async Task<TCacheResult> UpdateCacheAsync<TCacheValue, TArgument2, TCacheResult>(T key, TCacheValue cacheCopy,
		TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		TCacheResult? result = default;
		try
		{
			result = await cacheCopy.MakeResultAsync(key, second).ConfigureAwait(false);
				
			lock (SyncLock)
				Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key)).Result = result;
		}
		catch (Exception e)
		{
			Log.Error($"{e}");
		}

		return result ?? throw new();
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T>.Default;
		}
		catch (Exception ex)
		{
			Log.Error(
				$"Performance Fish encountered an exception while trying to initialize {
					typeof(ByReferenceUnclearable<T, TResult>).FullName}\n{ex}");
		}
	}
}

[PublicAPI]
public static class ByReference<T, TResult>
	where T : notnull where TResult : new()
{
	public static object SyncLock = new();
	
	private static FishTable<T, TResult> _get = Utility.AddNew<T, TResult>();

	[ThreadStatic]
	private static FishTable<T, TResult>? _getThreadStatic;

	public static FishTable<T, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNew<T, TResult>();
	}

	public static FishTable<T, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(T key) => ref Get.GetOrAddReference(key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(ref T key) => ref Get.GetOrAddReference(ref key);

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(T key) => ref Get.GetReference(key);
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult>(T key) where VResult : TResult, ICacheable<T>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult, V2>(T key, V2 second) where VResult : TResult, ICacheable<T, V2>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Update<VResult, V2, V3>(T key, V2 second, V3 third)
		where VResult : TResult, ICacheable<T, V2, V3>
		=> Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second, third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult>(T key) where VResult : TResult, ICacheable<T>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult, V2>(T key, V2 second) where VResult : TResult, ICacheable<T, V2>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key, second);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref VResult GetAndCheck<VResult, V2, V3>(T key, V2 second, V3 third)
		where VResult : TResult, ICacheable<T, V2, V3>
	{
		ref var cache = ref Unsafe.As<TResult, VResult>(ref GetOrAddReference(key));
		if (cache.Dirty)
			cache.Update(ref key, second, third);

		return ref cache!;
	}

	public static bool UpdateAsyncCache<TCacheValue, TCacheResult>(ref TCacheValue cache, T key)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		if (cache.Task is null)
			return SyncWithCentralCache<TCacheValue, TCacheResult>(ref cache, key);

		if (cache.Task.IsCompletedSuccessfully)
			cache.Result = cache.Task.Result;

		return !cache.Result.Equals<TCacheResult>(default);
	}

	private static bool SyncWithCentralCache<TCacheValue, TCacheResult>(ref TCacheValue cache, T key)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		lock (SyncLock)
		{
			ref var centralCache = ref Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key));
			var cacheCopy = centralCache;

			centralCache.Task ??= Task.Run(() => UpdateCacheAsync<TCacheValue, TCacheResult>(key, cacheCopy));

			cache.Task = centralCache.Task;
			cache.Result = centralCache.Result;
			return !cache.Result.Equals<TCacheResult>(default);
		}
	}

	private static TCacheResult UpdateCacheAsync<TCacheValue, TCacheResult>(T key, TCacheValue cacheCopy)
		where TCacheValue : TResult, IAsyncCacheable<T, TCacheResult>
	{
		TCacheResult? result = default;
		try
		{
			result = cacheCopy.MakeResultAsync(key);

			lock (SyncLock)
				Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key)).Result = result;
		}
		catch (Exception e)
		{
			Log.Error($"{e}");
		}

		return result ?? throw new();
	}

	public static Task<TCacheResult> RequestFromCacheAsync<TCacheValue, TArgument2, TCacheResult>(T key, TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		TCacheValue cacheCopy;
		lock (SyncLock)
		{
			ref var centralCache = ref Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key));
			cacheCopy = centralCache;

			centralCache.Task ??= Task.Run(async ()
				=> await UpdateCacheAsync<TCacheValue, TArgument2, TCacheResult>(key, cacheCopy, second)
					.ConfigureAwait(false));

			cacheCopy.Task = centralCache.Task;
		}

		return cacheCopy.Task;
	}

	public static bool UpdateAsyncCache<TCacheValue, TArgument2, TCacheResult>(ref TCacheValue cache, T key,
		TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		cache.Task ??= RequestFromCacheAsync<TCacheValue, TArgument2, TCacheResult>(key, second);
		
		if (cache.Task.IsCompletedSuccessfully)
			cache.Result = cache.Task.Result;

		return !cache.Result.Equals<TCacheResult>(default);
	}

	private static async ValueTask<TCacheResult> UpdateCacheAsync<TCacheValue, TArgument2, TCacheResult>(T key, TCacheValue cacheCopy,
		TArgument2 second)
		where TCacheValue : TResult, IAsyncCacheable<T, TArgument2, TCacheResult>
	{
		TCacheResult? result = default;
		try
		{
			result = await cacheCopy.MakeResultAsync(key, second).ConfigureAwait(false);
				
			lock (SyncLock)
				Unsafe.As<TResult, TCacheValue>(ref GetDirectly.GetOrAddReference(key)).Result = result;
		}
		catch (Exception e)
		{
			Log.Error($"{e}");
		}

		return result ?? throw new();
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T>.Default;
		}
		catch (Exception ex)
		{
			Log.Error(
				$"Performance Fish encountered an exception while trying to initialize {
					typeof(ByReference<T, TResult>).FullName}\n{ex}");
		}
	}
}

[PublicAPI]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct ByReference<T1, T2, TResult> : IInitializable<T1, T2>
	where T1 : notnull where T2 : notnull where TResult : new()
{
	private static FishTable<ByReference<T1, T2, TResult>, TResult> _get
		= Utility.AddNew<ByReference<T1, T2, TResult>, TResult>();

	[ThreadStatic]
	private static FishTable<ByReference<T1, T2, TResult>, TResult>? _getThreadStatic;

	public static FishTable<ByReference<T1, T2, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNew<ByReference<T1, T2, TResult>, TResult>();
	}

	public static FishTable<ByReference<T1, T2, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(T1 first, T2 second)
		=> ref Get.GetOrAddReference(new() { First = first, Second = second });

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByReference<T1, T2, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref TResult GetExistingReference(T1 first, T2 second)
	{
		var key = new ByReference<T1, T2, TResult>(first, second);
		return ref Get.GetReference(ref key);
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(in ByReference<T1, T2, TResult> key)
		=> ref Get.GetReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult>(T1 first, T2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>>
	{
		var key = new ByReference<T1, T2, TResult>(first, second);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2>(T1 key1, T2 key2, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>, V2>
	{
		var key = new ByReference<T1, T2, TResult>(key1, key2);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2, V3>(T1 key1, T2 key2, V2 second, V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, TResult>(key1, key2);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second, third);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult>(T1 first, T2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>>
	{
		var key = new ByReference<T1, T2, TResult>(first, second);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2>(T1 key1, T2 key2, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>, V2>
	{
		var key = new ByReference<T1, T2, TResult>(key1, key2);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2, V3>(T1 key1, T2 key2, V2 second, V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, TResult>(key1, key2);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second, third);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T1 first, T2 second)
	{
		First = first;
		Second = second;
	}

	public void Initialize(T1 first, T2 second)
	{
		First = first;
		Second = second;
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T1>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T2>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T1, T2, TResult>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T1>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T2>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T1, T2, TResult>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error(
				$"Performance Fish encountered an exception while trying to initialize {
					typeof(ByReference<T1, T2, TResult>).FullName}\n{ex}");
		}
	}

	public T1 First;

	public T2 Second;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T1, T2, TResult> other)
		=> First.Equals<T1>(other.First)
			&& Second.Equals<T2>(other.Second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(First, Second);

	T1 IMemberCount<T1>.First
	{
		get => First;
		set => First = value;
	}

	T2 IMemberCount<T1, T2>.Second
	{
		get => Second;
		set => Second = value;
	}
}

[PublicAPI]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct ByReference<T1, T2, T3, TResult>
	where T1 : notnull where T2 : notnull where T3 : notnull where TResult : new()
{
	// TODO: Switch to FishTable. This is used by some of the reflection patches and currently crashes when not used
	// with standard dictionaries
	
	private static Dictionary<ByReference<T1, T2, T3, TResult>, TResult> _get
		= Utility.AddNew<Dictionary<ByReference<T1, T2, T3, TResult>, TResult>>();

	[ThreadStatic]
	private static Dictionary<ByReference<T1, T2, T3, TResult>, TResult>? _getThreadStatic;

	public static Dictionary<ByReference<T1, T2, T3, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNew<Dictionary<ByReference<T1, T2, T3, TResult>, TResult>>();
	}

	public static Dictionary<ByReference<T1, T2, T3, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref TResult GetOrAddReference(T1 first, T2 second, T3 third)
	{
		var key = new ByReference<T1, T2, T3, TResult> { First = first, Second = second, Third = third };
		return ref Get.GetOrAddReference(ref key);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByReference<T1, T2, T3, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref TResult GetExistingReference(T1 first, T2 second, T3 third)
	{
		var key = new ByReference<T1, T2, T3, TResult>(first, second, third);
		return ref Get.GetReference(ref key);
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(in ByReference<T1, T2, T3, TResult> key)
		=> ref Get.GetReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult>(T1 first, T2 second, T3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>>
	{
		var key = new ByReference<T1, T2, T3, TResult>(first, second, third);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2>(T1 key1, T2 key2, T3 key3, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>, V2>
	{
		var key = new ByReference<T1, T2, T3, TResult>(key1, key2, key3);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2, V3>(T1 key1, T2 key2, T3 key3, V2 second, V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, T3, TResult>(key1, key2, key3);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second, third);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult>(T1 first, T2 second, T3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>>
	{
		var key = new ByReference<T1, T2, T3, TResult>(first, second, third);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2>(T1 key1, T2 key2, T3 key3, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>, V2>
	{
		var key = new ByReference<T1, T2, T3, TResult>(key1, key2, key3);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2, V3>(T1 key1, T2 key2, T3 key3, V2 second, V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, T3, TResult>(key1, key2, key3);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second, third);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T1 first, T2 second, T3 third)
	{
		First = first;
		Second = second;
		Third = third;
		_hashCode = HashCode.Combine(first, second, third);
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T1>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T2>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T3>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T1, T2, T3, TResult>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T1>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T2>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T3>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T1, T2, T3, TResult>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {
				typeof(ByReference<T1, T2, T3, TResult>).FullName}\n{ex}");
		}
	}

	public T1 First;

	public T2 Second;

	public T3 Third;

	private int _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T1, T2, T3, TResult> other)
		=> First.Equals<T1>(other.First)
			&& Second.Equals<T2>(other.Second)
			&& Third.Equals<T3>(other.Third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _hashCode;
}

[PublicAPI]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public record struct ByReference<T1, T2, T3, T4, TResult>
	where T1 : notnull where T2 : notnull where T3 : notnull where T4 : notnull where TResult : new()
{
	private static FishTable<ByReference<T1, T2, T3, T4, TResult>, TResult> _get
		= Utility.AddNew<ByReference<T1, T2, T3, T4, TResult>, TResult>();

	[ThreadStatic]
	private static FishTable<ByReference<T1, T2, T3, T4, TResult>, TResult>? _getThreadStatic;

	public static FishTable<ByReference<T1, T2, T3, T4, TResult>, TResult> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNew<ByReference<T1, T2, T3, T4, TResult>, TResult>();
	}

	public static FishTable<ByReference<T1, T2, T3, T4, TResult>, TResult> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref TResult GetOrAddReference(T1 first, T2 second, T3 third, T4 fourth)
	{
		var key = new ByReference<T1, T2, T3, T4, TResult> { First = first, Second = second, Third = third, Fourth = fourth};
		return ref Get.GetOrAddReference(ref key);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TResult GetOrAddReference(in ByReference<T1, T2, T3, T4, TResult> key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref TResult GetExistingReference(T1 first, T2 second, T3 third, T4 fourth)
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(first, second, third, fourth);
		return ref Get.GetReference(ref key);
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TResult GetExistingReference(in ByReference<T1, T2, T3, T4, TResult> key)
		=> ref Get.GetReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult>(T1 first, T2 second, T3 third, T4 fourth)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(first, second, third, fourth);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2>(T1 key1, T2 key2, T3 key3, T4 key4, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>, V2>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(key1, key2, key3, key4);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Update<VResult, V2, V3>(T1 key1, T2 key2, T3 key3, T4 key4, V2 second, V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(key1, key2, key3, key4);
		Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key)).Update(ref key, second, third);
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult>(T1 first, T2 second, T3 third, T4 fourth)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(first, second, third, fourth);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2>(T1 key1, T2 key2, T3 key3, T4 key4, V2 second)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>, V2>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(key1, key2, key3, key4);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static unsafe ref VResult GetAndCheck<VResult, V2, V3>(T1 key1, T2 key2, T3 key3, T4 key4, V2 second,
		V3 third)
		where VResult : TResult, ICacheable<ByReference<T1, T2, T3, T4, TResult>, V2, V3>
	{
		var key = new ByReference<T1, T2, T3, T4, TResult>(key1, key2, key3, key4);
		ref var cache = ref Unsafe.As<TResult, VResult>(ref Get.GetOrAddReference(ref key));
		if (cache.Dirty)
			cache.Update(ref key, second, third);

		return ref cache!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T1 first, T2 second, T3 third, T4 fourth)
	{
		First = first;
		Second = second;
		Third = third;
		Fourth = fourth;
		_hashCode = HashCode.Combine(first, second, third, fourth);
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T1>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T2>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T3>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T4>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T1, T2, T3, T4, TResult>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T1>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T2>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T3>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T4>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T1, T2, T3, T4, TResult>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error(
				$"Performance Fish encountered an exception while trying to initialize {
					typeof(ByReference<T1, T2, T3, T4, TResult>).FullName}\n{ex}");
		}
	}

	public T1 First;

	public T2 Second;

	public T3 Third;

	public T4 Fourth;

	private int _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T1, T2, T3, T4, TResult> other)
		=> First.Equals<T1>(other.First)
			&& Second.Equals<T2>(other.Second)
			&& Third.Equals<T3>(other.Third)
			&& Fourth.Equals<T4>(other.Fourth);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _hashCode;
}
#pragma warning restore CS9091