// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;

#pragma warning disable CS9091
public static class Database<TCache, TValue> where TCache : ICacheKeyable where TValue : new()
{
	public static object SyncLock = new();
	
	private static Dictionary<TCache, TValue> _get = Utility.AddNew<Dictionary<TCache, TValue>>();

	[ThreadStatic]
	private static Dictionary<TCache, TValue>? _getThreadStatic;

	public static Dictionary<TCache, TValue> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _getThreadStatic ??= Utility.AddNew<Dictionary<TCache, TValue>>();
	}

	public static Dictionary<TCache, TValue> GetDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _get;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TValue GetOrAddReference<VCache, T1, T2>(T1 first, T2 second)
		where VCache : IMemberCount<T1, T2>, new()	// new as generic constraint requires at least function pointers,
													// and can't be inlined. Makes this slow, unfortunately
		=> ref Unsafe.As<Dictionary<VCache, TValue>>(Get)
			.GetOrAddReference(Reflection.New<VCache, T1, T2>(first, second));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TValue GetOrAddReference(in TCache key)
		=> ref Get.GetOrAddReference(ref Unsafe.AsRef(in key));
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static unsafe ref TValue GetExistingReference<VCache, T1, T2>(T1 first, T2 second)
		where VCache : IMemberCount<T1, T2>, new()
	{
		var key = Reflection.New<VCache, T1, T2>(first, second);
		return ref Get.GetReference(ref Unsafe.As<VCache, TCache>(ref key));
	}
	
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ref TValue GetExistingReference(in TCache key)
		=> ref Get.GetReference(ref Unsafe.AsRef(in key));
}
#pragma warning restore CS9091