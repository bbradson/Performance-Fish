// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

public static class CellIndexExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this in IntVec3 cell, Map map) => cell.CellToIndex(map.cellIndices);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this in IntVec3 cell, CellIndices cellIndices)
		=> cell.CellToIndex(cellIndices.mapSizeX);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this in IntVec3 cell, int mapSizeX) => (cell.z * mapSizeX) + cell.x;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this IntVec2 cell, Map map) => cell.CellToIndex(map.cellIndices);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this IntVec2 cell, CellIndices cellIndices)
		=> cell.CellToIndex(cellIndices.mapSizeX);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(this IntVec2 cell, int mapSizeX) => (cell.z * mapSizeX) + cell.x;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(int x, int z, Map map) => CellToIndex(x, z, map.cellIndices);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(int x, int z, CellIndices cellIndices) => CellToIndex(x, z, cellIndices.mapSizeX);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int CellToIndex(int x, int z, int mapSizeX) => (z * mapSizeX) + x;
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static List<Thing> ThingsListAtFast(this ThingGrid thingGrid, CellIndex cellIndex)
		=> thingGrid.thingGrid[cellIndex.Value];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsFogged(this FogGrid fogGrid, CellIndex cellIndex) => fogGrid.fogGrid[cellIndex.Value];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetDepth(this SnowGrid snowGrid, CellIndex cellIndex) => snowGrid.depthGrid[cellIndex.Value];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static float GetDepth(this SnowGrid snowGrid, int cellIndex) => snowGrid.depthGrid[cellIndex];
}