// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if false
using System.Runtime.InteropServices;

namespace PerformanceFish.Experimental;

#pragma warning disable CS8500
/// <summary>
/// References within fixed size buffers don't get tracked by the GC. This leads to memory corruption. Don't use
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public unsafe struct InlineArray256<T> where T : class
{
	[FieldOffset(0)]
	private T _firstEntry;
	
	[FieldOffset(0)]
	private fixed ulong _entries[256];

	public T this[int index]
	{
		get => (&_firstEntry)[index];
		set => (&_firstEntry)[index] = value;
	}
}
#pragma warning restore CS8500
#endif