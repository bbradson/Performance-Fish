// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;
public interface IIsRefreshable<T, V> where V : IIsRefreshable<T, V>
{
	public bool ShouldRefreshNow
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		set;
	}
	public V SetNewValue(T key);
}