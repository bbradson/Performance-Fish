// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Pools;

public static class FishPool<T> where T : IFishPoolable
{
	private static T?[] _freeItems = new T[4];
	private static int _freeItemCount;

	public static T Get()
	{
		if (_freeItemCount == 0)
			return Reflection.New<T>();
		
		var freeItem = _freeItems[--_freeItemCount];
		_freeItems[_freeItemCount] = default;
		return freeItem!;
	}

	public static void Return(T item)
	{
		item.Reset();
		
		if (++_freeItemCount >= _freeItems.Length)
			ExpandFreeItems();

		_freeItems[_freeItemCount - 1] = item;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ExpandFreeItems() => Array.Resize(ref _freeItems, _freeItems.Length << 1);
}

public interface IFishPoolable
{
	public void Reset();
}