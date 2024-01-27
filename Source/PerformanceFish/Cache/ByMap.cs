// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;

public static class ByMap<T> where T : new()
{
	private static List<(Map map, T value)> _cache = Utility.AddNew<List<(Map map, T value)>>();

	[ThreadStatic]
	private static List<(Map map, T value)>? _cacheThreadStatic;

	public static List<(Map map, T value)> GetCache
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _cacheThreadStatic ??= Utility.AddNew<List<(Map map, T value)>>();
	}

	public static List<(Map map, T value)> GetCacheDirectly
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _cache;
	}

	static ByMap() => Utility.All.Add(_cache);

	private static Func<Map, T> Initialize { get; }
		= typeof(MapComponent).IsAssignableFrom(typeof(T))
			? AccessTools.MethodDelegate<Func<Map, T>>(AccessTools.Method(typeof(Map), nameof(Map.GetComponent),
				Type.EmptyTypes, [typeof(T)]))
			// missing T constraints cause the compiler to complain when trying to just use map => map.GetComponent<T>
			: static _ => Reflection.New<T>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T GetFor(Map map)
	{
		var cache = GetCache;
		for (var i = 0; i < cache.Count; i++)
		{
			ref var cacheEntry = ref cache.GetReferenceUnsafe(i);
			if (cacheEntry.map == map)
				return cacheEntry.value;

			if (EntryValid(i, cacheEntry.map))
				continue;

			cache.RemoveAt(i);
			i--;
		}

		return AddEntry(map, cache);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref T GetReferenceFor(Map map)
	{
		var cache = GetCache;
		for (var i = 0; i < cache.Count; i++)
		{
			ref var cacheEntry = ref cache.GetReferenceUnsafe(i);
			if (cacheEntry.map == map)
				return ref cacheEntry.value;

			if (EntryValid(i, cacheEntry.map))
				continue;

			cache.RemoveAt(i);
			i--;
		}

		return ref AddEntryByRef(map, cache);
	}

	private static bool EntryValid(int i, Map map)
	{
		var maps = Current.Game.Maps;
		return maps.Count > i && maps.Contains(map);
	}

	private static T AddEntry(Map map, List<(Map map, T value)> cache)
	{
		cache.Add((map, Initialize(map)));
		return cache[^1].value;
	}
	
	private static ref T AddEntryByRef(Map map, List<(Map map, T value)> cache)
	{
		cache.Add((map, Initialize(map)));
		return ref cache.GetReferenceUnsafe(cache.Count - 1).value;
	}
}