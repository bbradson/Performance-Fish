// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using RoomOwnersCache
	= PerformanceFish.Cache.ByIndex<Verse.Room, PerformanceFish.RoomOptimizations.Owners_Patch.CacheValue>;
using RoomRoleCache
	= PerformanceFish.Cache.ByIndex<Verse.Room, PerformanceFish.RoomOptimizations.Role_Patch.CacheValue>;
using ContainedAndAdjacentThingsCache
	= PerformanceFish.Cache.ByIndex<Verse.Room,
		PerformanceFish.RoomOptimizations.ContainedAndAdjacentThings_Patch.CacheValue>;

namespace PerformanceFish;

public class RoomOptimizations : ClassWithFishPatches, IHasDescription
{
	public string Description { get; } = "Room stats related optimizations";

	public class Role_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Throttles room stats and role updating to happen at most once every 512 or so ticks per room, "
			+ "instead of every time anything at all changes or moves";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Room), nameof(Room.Role));

		public static CodeInstructions Transpiler(CodeInstructions instructions)
		{
			var this_statsAndRoleDirty = FishTranspiler.Field(typeof(Room), nameof(Room.statsAndRoleDirty));

			try
			{
				return instructions.ReplaceAt((codes, i)
						=> i - 1 > 0
						&& codes[i - 1] == this_statsAndRoleDirty
						&& codes[i].opcode == OpCodes.Brfalse_S
						&& codes[i].operand is Label, static code => new[]
					{
						code,
						FishTranspiler.This,
						FishTranspiler.Call(ShouldUpdate),
						FishTranspiler.IfFalse_Short((Label)code.operand)
					});
			}
			catch (Exception ex)
			{
				Log.Error("Performance Fish failed to patch Room.Role. This just means that specific patch "
					+ $"will be doing nothing.\n{ex}");
				return null!;
			}
		}

		public static bool ShouldUpdate(Room __instance)
		{
			if (__instance.role is not null
				|| !RoomRoleCache.Get[__instance.ID].Dirty)
			{
				return false;
			}

			UpdateCache(__instance);
			return true;

		}

		private static void UpdateCache(Room __instance)
			=> RoomRoleCache.Get.GetReference(__instance).SetDirty(__instance);

		public record struct CacheValue
		{
			private int _nextUpdateTick;
			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextUpdateTick);
			}
			public void SetDirty(Room room)
				=> _nextUpdateTick = TickHelper.Add(384, room.ID, 256);
		}
	}

	public class Owners_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Caches room owner data and throttles them to only update at most once every ~512 or so ticks. "
			+ "RimWorld calculates owners by checking ownership of all items contained in a room, which can get "
			+ "quite expensive";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Room), nameof(Room.Owners));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Room __instance, ref IEnumerable<Pawn> __result, out bool __state)
		{
			ref var cache = ref RoomOwnersCache.Get.GetReference(__instance.ID);

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

		private static bool CanCache(Room __instance)
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
			ref var cache = ref RoomOwnersCache.Get.GetReference(__instance);
			
			cache.Update(__instance, __result);
			
			__result = cache.Owners;
		}

		public record struct CacheValue
		{
			private int _nextRefreshTick;
			public List<Pawn> Owners;

			public void Update(Room room, IEnumerable<Pawn>? result)
			{
				if (result is null)
				{
					(Owners ??= new(0)).Clear();
				}
				else
				{
					if (Owners != null)
					{
						Owners.Clear();
						Owners.AddRange(result);
					}
					else
					{
						Owners = new(result);
					}
				}
				
				_nextRefreshTick = TickHelper.Add(384, room.ID, 256);
			}

			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextRefreshTick);
			}
		}
	}

	public class Regions_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Minor optimization";
		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Room), nameof(Room.Regions));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(Regions_Replacement);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

	public class ContainedBeds_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "So RimWorld has a list of beds and a list of all things. For some reason Ludeon chose to have "
			+ "ContainedBeds retrieve its beds by looping through all things. This fixes that. Helps a lot "
			+ "against spikes";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Room), nameof(Room.ContainedBeds));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions, ILGenerator generator)
			=> Reflection.GetCodeInstructions(ContainedBeds_Replacement, generator);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<Building_Bed> ContainedBeds_Replacement(Room instance)
		{
			var regions = instance.Regions;
			for (var i = 0; i < regions.Count; i++)
			{
				var beds = regions[i].ListerThings.listsByGroup[(int)ThingRequestGroup.Bed];
				if (beds is null)
					continue;

				for (var j = 0; j < beds.Count; j++)
					yield return (Building_Bed)beds[j];
			}
		}
	}

	public class ContainedAndAdjacentThings_Patch : FirstPriorityFishPatch
	{
		public override string Description { get; } = "Caching of room content info. Variable impact";

		public override MethodBase TargetMethodInfo { get; }
			= AccessTools.PropertyGetter(typeof(Room), nameof(Room.ContainedAndAdjacentThings));

		public static CodeInstructions Transpiler(CodeInstructions codeInstructions)
			=> Reflection.MakeReplacementCall(ContainedAndAdjacentThings_Replacement);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<Thing> ContainedAndAdjacentThings_Replacement(Room instance)
		{
			instance.uniqueContainedThings.Clear();

			var regions = instance.Regions;

			ref var cache = ref ContainedAndAdjacentThingsCache.Get.GetReference(instance.ID);

			if (cache.ListVersions != null
				&& cache.ListVersions.Count == regions.Count
				&& !cache.Dirty)
			{
				var cacheIsAccurate = true;
				for (var i = 0; i < regions.Count; i++)
				{
					var allThings = regions[i].listerThings.AllThings;
					if (allThings == null)
					{
						if (cache.ListVersions[i] != -1)
							cacheIsAccurate = false;
					}
					else if (cache.ListVersions[i] != allThings._version)
					{
						cacheIsAccurate = false;
					}
				}

				if (cacheIsAccurate)
				{
					instance.uniqueContainedThings.AddRangeFast(cache.Things);
					return instance.uniqueContainedThings;
				}
			}

			return RefreshContainedAndAdjacentThings(instance, regions, ref cache);
		}

		private static List<Thing> RefreshContainedAndAdjacentThings(Room instance, List<Region> regions,
			ref CacheValue cache)
		{
			var uniqueContainedThings = instance.uniqueContainedThings;
			var uniqueContainedThingsSet = instance.uniqueContainedThingsSet;
			uniqueContainedThingsSet.Clear();
			(cache.ListVersions ??= new()).Clear();

			for (var i = 0; i < regions.Count; i++)
			{
				var allThings = regions[i].ListerThings.AllThings;
				if (allThings == null)
				{
					cache.ListVersions.Add(-1);
					continue;
				}

				var count = allThings.Count;
				for (var j = 0; j < count; j++)
					uniqueContainedThingsSet.Add(allThings[j]);

				cache.ListVersions.Add(allThings.Version());
			}

			uniqueContainedThings.AddRange(uniqueContainedThingsSet);
			if (cache.Things is { } list)
				list.ReplaceContentsWith(uniqueContainedThings);
			else
				cache.Things = new(uniqueContainedThings);
			
			cache.SetDirty(false, instance);

			uniqueContainedThingsSet.Clear();
			return uniqueContainedThings;
		}

		/*private static List<Thing> RefreshContainedAndAdjacentThingsAlternative(Room instance, List<Region> regions,
			FishThingsInfo cache)
		{
			var uniqueContainedThings = instance.uniqueContainedThings;
			(cache.listVersions ??= new()).Clear();
			var map = instance.Map;
			var regionGrid = map.regionGrid.regionGrid;
			var mapSizeX = map.cellIndices.mapSizeX;

			var adjacentThings = new List<Thing>();

			for (var i = 0; i < regions.Count; i++)
			{
				var region = regions[i];
				var allThings = region.ListerThings.AllThings;
				if (allThings == null)
				{
					cache.listVersions.Add(-1);
					continue;
				}

				var count = allThings.Count;
				for (var j = 0; j < count; j++)
				{
					if (regionGrid[CellIndicesUtility.CellToIndex(allThings[j].Position, mapSizeX)] == region)
						uniqueContainedThings.Add(allThings[j]);
					else
						adjacentThings.Add(allThings[j]);
				}

				cache.listVersions.Add(allThings._version);
			}

			for (var i = 0; i < adjacentThings.Count; i++)
			{
				if (!uniqueContainedThings.Contains(adjacentThings[i]))
					uniqueContainedThings.Add(adjacentThings[i]);
			}

			if (cache.things is List<Thing> list)
				list.ReplaceContentsWith(uniqueContainedThings);
			else
				cache.things = new List<Thing>(uniqueContainedThings);
			cache.ShouldRefreshNow = false;

			CachedThingLists[instance] = cache;

			return uniqueContainedThings;
		}*/

		public record struct CacheValue
		{
			private int _nextRefreshTick;
			public List<int>? ListVersions;
			public List<Thing> Things;
			
			public void SetDirty(bool value, Room room)
				=> _nextRefreshTick = value ? 0 : TickHelper.Add(3072, room.ID, 2048);

			public bool Dirty
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => TickHelper.Past(_nextRefreshTick);
			}
		}
	}

	public class RoomAt_Patch : FirstPriorityFishPatch
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
			var mapSize = map.info.Size;
			if ((uint)c.x >= (uint)mapSize.x || (uint)c.z >= (uint)mapSize.z)
				return null;

			var regionAndRoomUpdater = map.regionAndRoomUpdater;
			if (regionAndRoomUpdater.Enabled)
			{
				regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
			}
			else if (regionAndRoomUpdater.AnythingToRebuild)
			{
				Log.Warning($"Trying to get valid region at {
						c.ToString()} but RegionAndRoomUpdater is disabled. The result may be incorrect.");
			}

			var region = map.regionGrid.regionGrid[(c.z * mapSize.x) + c.x];
			return region is null || !region.valid || (region.type & allowedRegionTypes) == 0
				? null
				: region.District?.Room;
		}
	}
}