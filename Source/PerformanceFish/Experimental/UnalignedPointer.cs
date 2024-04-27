// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if false
using System.Runtime.InteropServices;
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace PerformanceFish.Experimental;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct UnalignedPointer
{
	// 48 bit
	private ushort
		_top,
		_mid,
		_bot;

	public void* Value
	{
		get
		{
			var result = (void*)(Unsafe.As<ushort, ulong>(ref _top) & 0x0000_FFFF_FFFF_FFFF);
			return result;
		}
	}

	public UnalignedPointer(void* pointer)
	{
		Unsafe.As<ushort, uint>(ref _top) = (uint)(ulong)pointer;
		_bot = (ushort)((ulong)pointer >> 32);
	}
}

public unsafe struct TaggedPointer
{
	private const ulong
		LowerBitMask = ~(ulong.MaxValue >> 3),
		UpperBitMask = 0x0000_0000_0000_FFFF,
		ValueBitMask = ~LowerBitMask & ~UpperBitMask;
	
	private ulong _data;

	public void* Value
	{
		get
		{
			var value = (void*)(_data & ValueBitMask);
			return value;
		}
		set => _data = (_data & ~ValueBitMask) | ((ulong)value & ValueBitMask);
	}

	public ushort UpperBits
	{
		get => (ushort)(_data & UpperBitMask);
		set => _data = (_data & ~UpperBitMask) | (value & UpperBitMask);
	}

	public byte LowerBits
	{
		get => (byte)((_data & LowerBitMask) >> 61);
		set => _data = (_data & ~LowerBitMask) | (((ulong)value << 61) & LowerBitMask);
	}

	public TaggedPointer(void* pointer) => Value = pointer;

	public TaggedPointer(void* pointer, ushort upperBits, byte lowerBits)
		=> _data = ((ulong)pointer & ValueBitMask)
			| (upperBits & UpperBitMask)
			| (((ulong)lowerBits << 61) & LowerBitMask);
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct UInt48 : IComparable<UInt48>, IEquatable<UInt48>
{
	public const ulong MaxValue = ulong.MaxValue >> 16;
	
	private ushort
		_top,
		_mid,
		_bot;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public UInt48(ulong value)
	{
		Unsafe.As<ushort, uint>(ref _top) = (uint)value;
		_bot = (ushort)(value >> 32);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(UInt48 other) => this == other;

	public override bool Equals(object? obj) => obj is UInt48 value && this == value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(UInt48 other) => ((ulong)this).CompareTo(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Unsafe.As<ushort, int>(ref _top) ^ _bot;

	public override string ToString() => ((ulong)this).ToString();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(UInt48 left, UInt48 right)
	{
		var x = left;
		var y = right;
		return (Unsafe.As<ushort, uint>(ref x._top) == Unsafe.As<ushort, uint>(ref y._top)) & (x._bot == y._bot);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(UInt48 left, UInt48 right) => !(left == right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(UInt48 left, UInt48 right) => (ulong)left < right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(UInt48 left, UInt48 right) => (ulong)left <= right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(UInt48 left, UInt48 right) => (ulong)left > right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(UInt48 left, UInt48 right) => (ulong)left >= right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ulong(UInt48 value)
	{
		var temp = value;
		return Unsafe.As<ushort, ulong>(ref temp._top) & MaxValue;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator long(UInt48 value)
	{
		var temp = value;
		return Unsafe.As<ushort, long>(ref temp._top) & (long)MaxValue;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator UInt48(ulong value)
	{
		Unsafe.SkipInit(out UInt48 result);
		Unsafe.As<ushort, uint>(ref result._top) = (uint)value;
		result._bot = (ushort)(value >> 32);
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator UInt48(long value) => (UInt48)(ulong)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator UInt48(uint value)
	{
		Unsafe.SkipInit(out UInt48 result);
		Unsafe.As<ushort, uint>(ref result._top) = value;
		result._bot = 0;
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator UInt48(int value) => (UInt48)(uint)value;
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public struct Int48 : IComparable<Int48>, IEquatable<Int48>
{
	public const long MaxValue = long.MaxValue >> 16;
	
	private ushort
		_top,
		_mid,
		_bot;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Int48(long value)
	{
		Unsafe.As<ushort, uint>(ref _top) = (uint)value;
		_bot = (ushort)((((ulong)value >> 32) & (ulong)short.MaxValue)
			| ((ulong)(value & long.MinValue) >> 48));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(Int48 other) => this == other;

	public override bool Equals(object? obj) => obj is Int48 value && this == value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int CompareTo(Int48 other) => ((long)this).CompareTo(other);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => Unsafe.As<ushort, int>(ref _top) ^ _bot;

	public override string ToString() => ((long)this).ToString();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(Int48 left, Int48 right)
	{
		var x = left;
		var y = right;
		return (Unsafe.As<ushort, uint>(ref x._top) == Unsafe.As<ushort, uint>(ref y._top)) & (x._bot == y._bot);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(Int48 left, Int48 right) => !(left == right);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <(Int48 left, Int48 right) => (long)left < right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator <=(Int48 left, Int48 right) => (long)left <= right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >(Int48 left, Int48 right) => (long)left > right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator >=(Int48 left, Int48 right) => (long)left >= right;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator ulong(Int48 value) => (ulong)(long)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator long(Int48 value)
	{
		var temp = value;
		return (Unsafe.As<ushort, long>(ref temp._top) & MaxValue)
			| (long)(((ulong)temp._bot << 48) & unchecked((ulong)long.MinValue));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator Int48(ulong value) => (Int48)(long)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator Int48(long value)
	{
		Unsafe.SkipInit(out Int48 result);
		Unsafe.As<ushort, uint>(ref result._top) = (uint)value;
		result._bot = (ushort)((((ulong)value >> 32) & (ulong)short.MaxValue)
			| ((ulong)(value & long.MinValue) >> 48));
		return result;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator Int48(uint value) => (Int48)(int)value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator Int48(int value)
	{
		Unsafe.SkipInit(out Int48 result);
		Unsafe.As<ushort, uint>(ref result._top) = (uint)(value & int.MaxValue);
		result._bot = (ushort)((uint)(value & int.MinValue) >> 16);
		return result;
	}
}
#endif