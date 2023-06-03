// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if false
using System.Runtime.InteropServices;

namespace PerformanceFish.Experimental;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct UnalignedPointer
{
	private fixed byte _data[6]; // 48bit
	
	// [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] // 48bit
	// private byte[] _data;

	public void* Value
	{
		get
		{
			fixed (void* value = _data)
			{
				return value;
			}
		}
	}

	public UnalignedPointer(void* pointer)
	{
		fixed (UnalignedPointer* instance = &this)
		{
			instance->_data = (byte*)pointer;
		}
	}
}

public unsafe struct TaggedPointer
{
	private void* _data;

	public void* Value
	{
		get
		{
			var value = _data;
			return value;
		}
	}

	public ushort UpperBits => (ushort)_data;

	public byte LowerBits => (byte)_data;

	public TaggedPointer(void* pointer) => _data = pointer;
}
#endif