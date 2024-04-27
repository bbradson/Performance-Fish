// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using RoomOwnersCache
	= PerformanceFish.Cache.ByInt<Verse.Room, PerformanceFish.RoomOptimizations.Owners_Patch.CacheValue>;
using RoomRoleCache
	= PerformanceFish.Cache.ByInt<Verse.Room, PerformanceFish.RoomOptimizations.Role_Patch.CacheValue>;
using ContainedAndAdjacentThingsCache
	= PerformanceFish.Cache.ByInt<Verse.Room,
		PerformanceFish.RoomOptimizations.ContainedAndAdjacentThings_Patch.CacheValue>;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace PerformanceFish;

public sealed class RoomOptimizations : ClassWithFishPatches, IHasDescription
{
	public string Description { get; } = "Room stats related optimizations";

	public sealed class Role_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Throttles room stats and role updating to happen at most once every 512 or so ticks per room, "
			+ "instead of every time anything at all changes or moves";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Room), nameof(Room.Role));

		public static CodeInstructions Transpiler(CodeInstructions instructions)
		{
			var this_statsAndRoleDirty = FishTranspiler.Field(typeof(Room), nameof(Room.statsAndRoleDirty));

			try
			{
				return instructions.ReplaceAt((codes, i)
						=> i - 1 > 0
						&& codes[i - 1] == this_statsAndRoleDirty
						&& codes[i].opcode == OpCodes.Brfalse_S
						&& codes[i].operand is Label, static code =>
					[
						code,
						FishTranspiler.This,
						FishTranspiler.Call(ShouldUpdate),
						FishTranspiler.IfFalse_Short((Label)code.operand)
					]);
			}
			catch (Exception ex)
			{
				Log.Error("Performance Fish failed to patch Room.Role. This just means that specific patch "
					+ $"will be doing nothing.\n{ex}");
				return null!;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ShouldUpdate(Room __instance)
		{
			if (__instance.role is not null || !RoomRoleCache.GetOrAddReference(__instance.ID).Dirty)
				return false;

			UpdateCache(__instance);
			return true;

		}

		private static void UpdateCache(Room __instance)
			=> RoomRoleCache.GetOrAddReference(__instance.ID).SetDirty(__instance);

		public record struct CacheValue()
		{
			private int _nextUpdateTick = -2;
			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextUpdateTick);
			}
			public void SetDirty(Room room)
				=> _nextUpdateTick = TickHelper.Add(384, room.ID, 256);
		}
	}

	public sealed class Owners_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Caches room owner data and throttles them to only update at most once every ~512 or so ticks. "
			+ "RimWorld calculates owners by checking ownership of all items contained in a room, which can get "
			+ "quite expensive";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Room), nameof(Room.Owners));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Room __instance, ref IEnumerable<Pawn> __result, out bool __state)
		{
			ref var cache = ref RoomOwnersCache.GetOrAddReference(__instance.ID);

			if (cache.Dirty)
			{
				if (CanCache(__instance))
					return __state = true;
				else
					cache.Update(__instance, null);
			}

			__result = cache.Owners;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool CanCache(Room __instance)
			=> __instance is { TouchesMapEdge: false, IsHuge: false }
				&& (!__instance.statsAndRoleDirty || __instance.ContainedBeds.Any());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Room __instance, bool __state, ref IEnumerable<Pawn> __result)
		{
			if (!__state)
				return;

			UpdateCache(__instance, ref __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void UpdateCache(Room __instance, ref IEnumerable<Pawn> __result)
		{
			ref var cache = ref RoomOwnersCache.GetOrAddReference(__instance.ID);
			
			cache.Update(__instance, __result);
			
			__result = cache.Owners;
		}

		public record struct CacheValue()
		{
			private int _nextRefreshTick = -2;
			public readonly List<Pawn> Owners = [];

			public void Update(Room room, IEnumerable<Pawn>? result)
			{
				Owners.Clear();
				
				if (result != null)
					Owners.AddRange(result);
				
				_nextRefreshTick = TickHelper.Add(384, room.ID, 256);
			}

			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextRefreshTick);
			}
		}
	}

	public sealed class ContainedBeds_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "So RimWorld has a list of beds and a list of all things. For some reason Ludeon chose to have "
			+ "ContainedBeds retrieve its beds by looping through all things. This fixes that. Helps a lot "
			+ "against spikes";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Room), nameof(Room.ContainedBeds));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
			=> Reflection.GetCodeInstructions(ContainedBeds_Replacement, generator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Building_Bed> ContainedBeds_Replacement(Room instance)
		{
			var uniqueContainedBeds = instance.UniqueContainedBeds();
			uniqueContainedBeds.Clear();
			
			var regions = instance.Regions;
			for (var i = regions.Count; i-- > 0;)
			{
				var beds = regions[i].ListerThings.listsByGroup[(int)ThingRequestGroup.Bed];
				if (beds is null)
					continue;

				for (var j = beds.Count; j-- > 0;)
					uniqueContainedBeds.Add((Building_Bed)beds[j]);
			}

			return uniqueContainedBeds;
		}
	}

	public sealed class ContainedAndAdjacentThings_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Caching of room content info. Variable impact";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Room), nameof(Room.ContainedAndAdjacentThings));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(ContainedAndAdjacentThings_Replacement);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<Thing> ContainedAndAdjacentThings_Replacement(Room instance)
		{
			instance.uniqueContainedThings.Clear();

			var regions = instance.Regions;

			ref var cache = ref ContainedAndAdjacentThingsCache.GetOrAddReference(instance.ID);

			if (cache.ListVersions.Count == regions.Count && !cache.Dirty)
			{
				var cacheIsAccurate = true;
				
				for (var i = regions.Count; i-- > 0;)
				{
					if (cache.ListVersions[i] != (regions[i]?.listerThings.AllThings?._version ?? -1))
						cacheIsAccurate = false;
				}

				if (cacheIsAccurate)
				{
					instance.uniqueContainedThings.AddRangeFast(cache.Things);
					return instance.uniqueContainedThings;
				}
			}

			return RefreshContainedAndAdjacentThings(instance, regions, ref cache);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static List<Thing> RefreshContainedAndAdjacentThings(Room instance, List<Region> regions,
			ref CacheValue cache)
		{
			var uniqueContainedThings = instance.uniqueContainedThings;
			var uniqueContainedThingsSet = instance.uniqueContainedThingsSet;
			uniqueContainedThingsSet.Clear();
			cache.ListVersions.Clear();

			for (var i = 0; i < regions.Count; i++)
			{
				if (regions[i]?.ListerThings.AllThings is not { } allThings)
				{
					cache.ListVersions.Add(-1);
					continue;
				}

				var count = allThings.Count;
				for (var j = 0; j < count; j++)
					uniqueContainedThingsSet.Add(allThings[j]);

				cache.ListVersions.Add(allThings._version);
			}

			uniqueContainedThings.AddRange(uniqueContainedThingsSet);
			
			cache.Things.ReplaceContentsWith(uniqueContainedThings);
			cache.SetDirty(false, instance);

			uniqueContainedThingsSet.Clear();
			return uniqueContainedThings;
		}

		/*[MethodImpl(MethodImplOptions.NoInlining)]
		private static List<Thing> RefreshContainedAndAdjacentThingsAlternative(Room instance, List<Region> regions,
			ref CacheValue cache)
		{
			var uniqueContainedThings = instance.uniqueContainedThings;
			cache.ListVersions.Clear();
			var map = instance.Map;
			var regionGrid = map.regionGrid.regionGrid;
			var mapSizeX = map.cellIndices.mapSizeX;

			using var pooledAdjacentThings = new Pools.PooledIList<List<Thing>>();
			var adjacentThings = pooledAdjacentThings.List;
			var regionCount = regions.Count;
			using var pooledRegionIDs = new PooledArray<int>(regionCount);
			var regionIDs = pooledRegionIDs.BackingArray;
			
			for (var i = regionCount; i-- > 0;)
				regionIDs[i] = regions[i].id;

			for (var i = 0; i < regionCount; i++)
			{
				var region = regions[i];
				if (region.ListerThings.AllThings is not { } allThings)
				{
					cache.ListVersions.Add(-1);
					continue;
				}

				var count = allThings.Count;
				for (var j = 0; j < count; j++)
				{
					var thing = allThings[j];
					var thingRegionId = regionGrid[thing.Position.CellToIndex(mapSizeX)].id;
					
					if (thingRegionId == region.id)
						uniqueContainedThings.Add(thing);
					else if (!regionIDs.Contains(thingRegionId) && !adjacentThings.Contains(thing))
						adjacentThings.Add(thing);
				}

				cache.ListVersions.Add(allThings._version);
			}

			for (var i = adjacentThings.Count; i-- > 0;)
				uniqueContainedThings.Add(adjacentThings[i]);

			cache.Things.ReplaceContentsWith(uniqueContainedThings);
			cache.SetDirty(false, instance);

			return uniqueContainedThings;
		}*/

		public record struct CacheValue()
		{
			private int _nextRefreshTick = -2;
			public readonly List<int> ListVersions = [];
			public readonly List<Thing> Things = [];
			
			public void SetDirty(bool value, Room room)
				=> _nextRefreshTick = value ? 0 : TickHelper.Add(3072, room.ID, 2048);

			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextRefreshTick);
			}
		}
	}

	public sealed class RoomAt_Patch : FirstPriorityFishPatch
	{
		public override string? Description { get; }
			= "Minor optimization through inlining ond cleaning up of duplicate calls in the method. About 1/4 "
			+ "faster, which isn't much, but it likely won't break anything either.";

		public override Delegate TargetMethodGroup { get; } = RegionAndRoomQuery.RoomAt;

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(RoomAt);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Room? RoomAt(IntVec3 c, Map map, RegionType allowedRegionTypes = RegionType.Set_All)
		{
			var cellIndices = map.cellIndices;
			if (((uint)c.x >= (uint)cellIndices.mapSizeX) | ((uint)c.z >= (uint)cellIndices.mapSizeZ))
				return null;

			var regionAndRoomUpdater = map.regionAndRoomUpdater;
			
			if (regionAndRoomUpdater.Enabled)
				regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
			else if (regionAndRoomUpdater.AnythingToRebuild)
				LogIncorrectResultWarning(c);

			var region = map.regionGrid.regionGrid[(c.z * cellIndices.mapSizeX) + c.x];
			return region is null || !region.valid || (region.type & allowedRegionTypes) == 0
				? null
				: region.District?.Room;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void LogIncorrectResultWarning(IntVec3 c)
			=> Log.Warning($"Trying to get valid region at {
				c.ToString()} but RegionAndRoomUpdater is disabled. The result may be incorrect.\nThis commonly "
				+ $"happens when incorrectly accessing a region within a method that itself gets accessed by the "
				+ $"RegionAndRoomUpdater when computing the region grid. The updater gets disabled to avoid a stack "
				+ $"overflow.\n{new StackTrace(1, true)}");
	}
}

public sealed class RoomPrepatches : ClassWithFishPrepatches
{
	public sealed class Regions_Patch : FishPrepatch
	{
		public override string Description { get; } = "Minor optimization by simplifying instructions";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(Room), nameof(Room.Regions));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(Regions_Replacement);

		public static List<Region> Regions_Replacement(Room instance)
		{
			var tmpRegions = instance.tmpRegions;
			var districts = instance.districts;

			tmpRegions.Clear();
			for (var i = 0; i < districts.Count; i++)
				tmpRegions.AddRangeFast(districts[i].Regions);

			return tmpRegions;
		}
	}
}