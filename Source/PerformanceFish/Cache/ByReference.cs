// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;

public static class ByReference<T_first, T_result>
	where T_first : notnull
{
	private static Dictionary<T_first, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<T_first, T_result>? _getThreadStatic;

	public static Dictionary<T_first, T_result> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
			=> /*UnityData.IsInMainThread ? _get
			:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<T_first, T_result>>();
	}

	public static Dictionary<T_first, T_result> GetDirectly => _get;

	static ByReference() => Utility.All.Add(_get);

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T_first>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_first>.Default;
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {typeof(ByReference<T_first, T_result>).FullDescription()}\n{ex}");
		}
	}
}

public struct ByReference<T_first, T_second, T_result> : IEquatable<ByReference<T_first, T_second, T_result>>
	where T_first : notnull where T_second : notnull
{
	private static Dictionary<ByReference<T_first, T_second, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByReference<T_first, T_second, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByReference<T_first, T_second, T_result>, T_result> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
			=> /*UnityData.IsInMainThread ? _get
			:*/ _getThreadStatic ??= Utility.AddNew<Dictionary<ByReference<T_first, T_second, T_result>, T_result>>();
	}

	public static Dictionary<ByReference<T_first, T_second, T_result>, T_result> GetDirectly => _get;

	static ByReference() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T_first first, T_second second)
	{
		First = first;
		Second = second;
	}

	public static unsafe void Initialize()
	{
		try
		{
			_ = FisheryLib.FunctionPointers.Equals<T_first>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_second>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T_first, T_second, T_result>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_first>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_second>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T_first, T_second, T_result>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {typeof(ByReference<T_first, T_result>).FullDescription()}\n{ex}");
		}
	}

	public T_first First;

	public T_second Second;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T_first, T_second, T_result> other)
		=> First.Equals<T_first>(other.First)
		&& Second.Equals<T_second>(other.Second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode()
		=> HashCode.Combine(First, Second);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByReference<T_first, T_second, T_result> cache
		&& Equals(cache);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ByReference<T_first, T_second, T_result> left, ByReference<T_first, T_second, T_result> right)
		=> left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ByReference<T_first, T_second, T_result> left, ByReference<T_first, T_second, T_result> right)
		=> !(left == right);
}

public struct ByReference<T_first, T_second, T_third, T_result> : IEquatable<ByReference<T_first, T_second, T_third, T_result>>
	where T_first : notnull where T_second : notnull where T_third : notnull
{
	private static Dictionary<ByReference<T_first, T_second, T_third, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByReference<T_first, T_second, T_third, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByReference<T_first, T_second, T_third, T_result>, T_result> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
			/*UnityData.IsInMainThread ? _get
			:*/ => _getThreadStatic ??= Utility.AddNew<Dictionary<ByReference<T_first, T_second, T_third, T_result>, T_result>>();
	}

	public static Dictionary<ByReference<T_first, T_second, T_third, T_result>, T_result> GetDirectly => _get;

	static ByReference() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T_first first, T_second second, T_third third)
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
			_ = FisheryLib.FunctionPointers.Equals<T_first>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_second>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_third>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T_first, T_second, T_third, T_result>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_first>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_second>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_third>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T_first, T_second, T_third, T_result>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {typeof(ByReference<T_first, T_second, T_result>).FullDescription()}\n{ex}");
		}
	}

	public T_first First;

	public T_second Second;

	public T_third Third;

	private int _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T_first, T_second, T_third, T_result> other)
		=> First.Equals<T_first>(other.First)
		&& Second.Equals<T_second>(other.Second)
		&& Third.Equals<T_third>(other.Third);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByReference<T_first, T_second, T_third, T_result> cache
		&& Equals(cache);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ByReference<T_first, T_second, T_third, T_result> left, ByReference<T_first, T_second, T_third, T_result> right)
		=> left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ByReference<T_first, T_second, T_third, T_result> left, ByReference<T_first, T_second, T_third, T_result> right)
		=> !(left == right);
}

public struct ByReference<T_first, T_second, T_third, T_fourth, T_result> : IEquatable<ByReference<T_first, T_second, T_third, T_fourth, T_result>>
	where T_first : notnull where T_second : notnull where T_third : notnull where T_fourth : notnull
{
	private static Dictionary<ByReference<T_first, T_second, T_third, T_fourth, T_result>, T_result> _get = new();
	[ThreadStatic]
	private static Dictionary<ByReference<T_first, T_second, T_third, T_fourth, T_result>, T_result>? _getThreadStatic;

	public static Dictionary<ByReference<T_first, T_second, T_third, T_fourth, T_result>, T_result> Get
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
			/*UnityData.IsInMainThread ? _get
			:*/ => _getThreadStatic ??= Utility.AddNew<Dictionary<ByReference<T_first, T_second, T_third, T_fourth, T_result>, T_result>>();
	}

	public static Dictionary<ByReference<T_first, T_second, T_third, T_fourth, T_result>, T_result> GetDirectly => _get;

	static ByReference() => Utility.All.Add(_get);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ByReference(T_first first, T_second second, T_third third, T_fourth fourth)
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
			_ = FisheryLib.FunctionPointers.Equals<T_first>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_second>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_third>.Default;
			_ = FisheryLib.FunctionPointers.Equals<T_fourth>.Default;
			_ = FisheryLib.FunctionPointers.Equals<ByReference<T_first, T_second, T_third, T_fourth, T_result>>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_first>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_second>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_third>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<T_fourth>.Default;
			_ = FisheryLib.FunctionPointers.GetHashCode<ByReference<T_first, T_second, T_third, T_fourth, T_result>>.Default;
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize {typeof(ByReference<T_first, T_second, T_third, T_result>).FullDescription()}\n{ex}");
		}
	}

	public T_first First;

	public T_second Second;

	public T_third Third;

	public T_fourth Fourth;

	private int _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ByReference<T_first, T_second, T_third, T_fourth, T_result> other)
		=> First.Equals<T_first>(other.First)
		&& Second.Equals<T_second>(other.Second)
		&& Third.Equals<T_third>(other.Third)
		&& Fourth.Equals<T_fourth>(other.Fourth);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => _hashCode;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object obj)
		=> obj is ByReference<T_first, T_second, T_third, T_fourth, T_result> cache
		&& Equals(cache);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ByReference<T_first, T_second, T_third, T_fourth, T_result> left, ByReference<T_first, T_second, T_third, T_fourth, T_result> right)
		=> left.Equals(right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ByReference<T_first, T_second, T_third, T_fourth, T_result> left, ByReference<T_first, T_second, T_third, T_fourth, T_result> right)
		=> !(left == right);
}