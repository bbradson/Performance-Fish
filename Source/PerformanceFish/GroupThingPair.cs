// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using JetBrains.Annotations;
// ReSharper disable InconsistentNaming

namespace PerformanceFish;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record struct GroupThingPair
{
	public const int MaxValue = 0xFF_FF_FF;
	
	private uint _data;

	public ThingRequestGroup ThingRequestGroup
	{
		get => (ThingRequestGroup)(byte)(_data >> 24); // fewer hash collisions this way than in rightmost bit
		set => _data = (_data & 0x00_FF_FF_FF) | ((uint)value << 24);
	}

	public int ThingIDNumber
	{
		get => (int)(_data & 0x00_FF_FF_FF); // 24 bit, 16.8m MaxValue
		set => _data = (_data & 0xFF_00_00_00) | (uint)(value & 0x00_FF_FF_FF);
	}

	public GroupThingPair(ThingRequestGroup thingRequestGroup, int thingIDNumber)
		=> _data = ((uint)thingRequestGroup << 24) | (0x00_FF_FF_FF & (uint)thingIDNumber);

	public bool Equals(GroupThingPair other) => _data == other._data;

	public override int GetHashCode() => unchecked((int)_data);
}

// [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
// public record struct GroupThingPair
// {
// 	private uint _data;
//
// 	public ThingRequestGroup ThingRequestGroup
// 	{
// 		get => (ThingRequestGroup)(byte)_data;
// 		set => _data = (_data & 0xFF_FF_FF_00) | (byte)value;
// 	}
//
// 	public int ThingIDNumber
// 	{
// 		get => (int)(_data >> 8); // 24 bit, 16.8m MaxValue
// 		set => _data = (_data & 0xFF) | (uint)(value << 8);
// 	}
//
// 	public GroupThingPair(ThingRequestGroup thingRequestGroup, int thingIDNumber)
// 		=> _data = (uint)thingRequestGroup | ((uint)thingIDNumber << 8);
//
// 	public bool Equals(GroupThingPair other) => _data == other._data;
//
// 	public override int GetHashCode() => Unsafe.As<uint, int>(ref _data);
// }