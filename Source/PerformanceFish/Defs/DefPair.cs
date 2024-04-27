// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;

namespace PerformanceFish.Defs;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record struct DefPair
{
	private uint _data;

	public ushort First
	{
		get => (ushort)(_data >> 16);
		set => _data = (_data & 0x00_00_FF_FF) | ((uint)value << 16);
	}

	public ushort Second
	{
		get => (ushort)_data;
		set => _data = (_data & 0xFF_FF_00_00) | value;
	}

	public DefPair(Def first, Def second) : this(first.shortHash, second.shortHash)
	{
	}

	public DefPair(ushort first, ushort second) => _data = Create(first, second);

	public bool Equals(DefPair other) => _data == other._data;

	public override int GetHashCode() => unchecked((int)_data);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Create(Def first, Def second) => Create(first.shortHash, second.shortHash);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static uint Create(ushort first, ushort second) => ((uint)first << 16) | second;
}