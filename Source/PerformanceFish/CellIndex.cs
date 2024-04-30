// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public record struct CellIndex(int Value)
{
	public int Value = Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public CellIndex(IntVec3 cell, Map map) : this(cell, map.cellIndices.mapSizeX) {}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public CellIndex(IntVec3 cell, int mapSizeX) : this(cell.CellToIndex(mapSizeX)) {}

	public IntVec3 ToCell(Map map) => ToCell(map.cellIndices.mapSizeX);
	
	public IntVec3 ToCell(int mapSizeX) => CellIndicesUtility.IndexToCell(Value, mapSizeX);

	public List<Thing> GetThingList(Map map) => map.thingGrid.thingGrid[Value];

	public bool IsFogged(Map map) => map.fogGrid.fogGrid[Value];

	public float GetSnowDepth(Map map) => map.snowGrid.depthGrid[Value];

	public Building? GetEdifice(Map map) => map.edificeGrid[Value];
}