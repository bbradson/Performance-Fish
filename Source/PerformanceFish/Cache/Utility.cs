// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Linq;

namespace PerformanceFish.Cache;
public static class Utility
{
	public static ConcurrentBag<ICollection> All { get; } = new();

	public static void Clear()
	{
		foreach (var cache in All)
		{
			cache.GetType()
				.GetMethods()
				.Where(m
					=> m.Name == nameof(Array.Clear)
					&& m.GetParameters().Length == 0)
				.First()
				.Invoke(cache, null);
		}
	}

	public static T AddNew<T>() where T : ICollection, new()
	{
		var newCollection = new T();
		All.Add(newCollection);
		return newCollection;
	}

	public static T AddNew<T>(IEqualityComparer comparer) where T : ICollection, new()
	{
		var newCollection = (T)Activator.CreateInstance(typeof(T), comparer);
		All.Add(newCollection);
		return newCollection;
	}
}

public delegate int IndexGetter<T>(T item);