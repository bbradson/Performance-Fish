// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.System;
public class EqualityComparerOptimization : ClassWithFishPatches, IHasDescription
{
	public string Description => "Specialized EqualityComparers for slightly faster collection lookups on ValueTypes. No settings for this one. Can reduce memory usage by preventing unnecessary boxing into object.";

	public static void Initialize()
	{
		foreach (var type in AccessTools.AllTypes())
		{
			if (type.IsValueType
				&& type != typeof(void)
				&& typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type)
				&& type != typeof(int)
				&& type != typeof(ushort)
				&& !type.IsGenericTypeDefinition)
			{
				SetValueForDefaultComparer(type, Activator.CreateInstance(typeof(EquatableValueTypeEqualityComparer<>).MakeGenericType(type)));
			}
		}

		SetValueForDefaultComparer(typeof(int), new IntEqualityComparer());
		SetValueForDefaultComparer(typeof(ushort), new UshortEqualityComparer());
		SetValueForDefaultComparer(typeof(string), new StringEqualityComparer());

		// below aren't IEquatable, so why don't we just go fix that
		SetValueForDefaultComparer(typeof(IntPtr), new IntPtrEqualityComparer());
		SetValueForDefaultComparer(typeof(RuntimeFieldHandle), new RuntimeFieldHandleEqualityComparer());
		SetValueForDefaultComparer(typeof(RuntimeTypeHandle), new RuntimeTypeHandleEqualityComparer());
		SetValueForDefaultComparer(typeof(RuntimeMethodHandle), new RuntimeMethodHandleEqualityComparer());
	}

	private static void SetValueForDefaultComparer(Type type, object value)
		=> typeof(EqualityComparer<>).MakeGenericType(type).GetField("defaultComparer", AccessTools.allDeclared).SetValue(null, value);
}

[Serializable]
public class EquatableValueTypeEqualityComparer<T> : EqualityComparer<T> where T : struct, IEquatable<T>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(T x, T y) => x.Equals(y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(T obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is EquatableValueTypeEqualityComparer<T>;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class StringEqualityComparer : EqualityComparer<string>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(string x, string y) => string.Equals(x, y); // already does a nullcheck, so this skips the extra check that'd otherwise happen
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(string obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is StringEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class IntPtrEqualityComparer : EqualityComparer<IntPtr>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(IntPtr x, IntPtr y) => x == y;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(IntPtr obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is IntPtrEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class RuntimeFieldHandleEqualityComparer : EqualityComparer<RuntimeFieldHandle>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(RuntimeFieldHandle x, RuntimeFieldHandle y) => x.Value == y.Value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(RuntimeFieldHandle obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is RuntimeFieldHandleEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class RuntimeTypeHandleEqualityComparer : EqualityComparer<RuntimeTypeHandle>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(RuntimeTypeHandle x, RuntimeTypeHandle y) => x.Value == y.Value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(RuntimeTypeHandle obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is RuntimeTypeHandleEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class RuntimeMethodHandleEqualityComparer : EqualityComparer<RuntimeMethodHandle>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(RuntimeMethodHandle x, RuntimeMethodHandle y) => x.Value == y.Value;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(RuntimeMethodHandle obj) => obj.GetHashCode();

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is RuntimeMethodHandleEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class IntEqualityComparer : EqualityComparer<int>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(int x, int y) => x == y;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(int obj) => obj;

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is IntEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class UshortEqualityComparer : EqualityComparer<ushort>
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(ushort x, ushort y) => x == y;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(ushort obj) => obj;

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is UshortEqualityComparer;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}

[Serializable]
public class ReferenceEqualityComparer<T> : EqualityComparer<T>
{
	public static new readonly ReferenceEqualityComparer<T> Default = new();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(T x, T y) => ReferenceEquals(x, y);
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);

	/// <summary>
	/// Equals method for the comparer itself.
	/// </summary>
	public override bool Equals(object obj) => obj is ReferenceEqualityComparer<T>;
	public override int GetHashCode() => GetType().Name.GetHashCode();
}