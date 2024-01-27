// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Cache;

public sealed class BitCellGrid
{
	private long[] _innerArray;
	private CellIndices _cellIndices;

	public BitCellGrid(Map map)
	{
		_innerArray = null!;
		_cellIndices = null!;
		map.InvokeWhenCellIndicesReady(Initialize);
	}

	private void Initialize(Map map)
	{
		_cellIndices = map.cellIndices;
		_innerArray = new long[(Math.Max(0, _cellIndices.NumGridCells - 1) >> 6) + 1];
	}

	public bool this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => (_innerArray[index >> 6] & (1L << (index & 63))) != 0L;
		set
		{
			ref var bucket = ref _innerArray[index >> 6];
			bucket ^= (-(long)value.AsInt() ^ bucket) & (1L << (index & 63));
		}
	}

	public bool this[in IntVec3 c]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this[c.CellToIndex(_cellIndices)];
		set => this[c.CellToIndex(_cellIndices)] = value;
	}
}