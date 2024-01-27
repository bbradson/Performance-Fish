// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Threading.Tasks;

namespace PerformanceFish.Cache;

public interface ICacheKeyable
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetHashCode();
}

public interface IMemberCount<T1>
{
	public T1 First
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}
}

public interface IMemberCount<T1, T2> : IMemberCount<T1>
{
	public T2 Second
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}
}

public interface IMemberCount<T1, T2, T3> : IMemberCount<T1, T2>
{
	public T3 Third
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}
}

public interface IMemberCount<T1, T2, T3, T4> : IMemberCount<T1, T2, T3>
{
	public T4 Fourth
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}
}

public interface IInitializable<T> : IMemberCount<T>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Initialize(T key);
}

public interface IInitializable<T1, T2> : IMemberCount<T1, T2>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Initialize(T1 first, T2 second);
}

public interface IInitializable<T1, T2, T3> : IMemberCount<T1, T2, T3>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Initialize(T1 first, T2 second, T3 third);
}

public interface IInitializable<T1, T2, T3, T4> : IMemberCount<T1, T2, T3, T4>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Initialize(T1 first, T2 second, T3 third, T4 fourth);
}

public interface IDirtyable
{
	public bool Dirty
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
	}
}

public interface ICacheable<TKey> : IDirtyable
{
	public void Update(ref TKey key);
}

public interface ICacheable<TKey, in T2> : IDirtyable
{
	public void Update(ref TKey key, T2 second);
}

public interface ICacheable<TKey, in T2, in T3> : IDirtyable
{
	public void Update(ref TKey key, T2 second, T3 third);
}

public interface IAsyncCacheable<TResult> : IDirtyable
{
	public Task<TResult>? Task
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}

	public TResult? Result
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set;
	}
}

public interface IAsyncCacheable<in TKey, TResult> : IAsyncCacheable<TResult>
{
	public TResult? MakeResultAsync(TKey key);
}

public interface IAsyncCacheable<in TKey, in T2, TResult> : IAsyncCacheable<TResult>
{
	public ValueTask<TResult?> MakeResultAsync(TKey key, T2 second);
}

internal sealed record Countdown(int Value)
{
	public volatile int Value = Value;
}