// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;
public static class ByMap<T> where T : new()
{
	private static List<(Map map, T value)> _cache = new();
	[ThreadStatic]
	private static List<(Map map, T value)>? _cacheThreadStatic;

	public static List<(Map map, T value)> GetCache
		=> UnityData.IsInMainThread ? _cache
		: _cacheThreadStatic ??= Utility.AddNew<List<(Map map, T value)>>();

	public static List<(Map map, T value)> GetCacheDirectly => _cache;

	static ByMap() => Utility.All.Add(_cache);

	private static Func<Map, T> Initialize { get; }
		= typeof(MapComponent).IsAssignableFrom(typeof(T))
		? AccessTools.MethodDelegate<Func<Map, T>>(AccessTools.Method(typeof(Map), nameof(Map.GetComponent), Array.Empty<Type>(), new[] { typeof(T) }))
		// missing T constraints cause the compiler to complain when trying to just use map => map.GetComponent<T>
		: _ => new();

	public static T GetFor(Map map)
	{
		var cache = GetCache;
		for (var i = 0; i < cache.Count; i++)
		{
			var cacheEntry = cache[i];
			if (cacheEntry.map == map)
				return cacheEntry.value;

			if (Find.Maps.Count <= i + 1
				&& !Find.Maps.Contains(cacheEntry.map))
			{
				cache.RemoveAt(i);
				i--;
			}
		}
		cache.Add((map, Initialize(map)));
		return cache[^1].value;
	}
}