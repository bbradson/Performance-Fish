// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;
using RoomOwnersCache = PerformanceFish.Cache.ByIndex<Verse.Room, PerformanceFish.RoomOptimizations.Owners_Patch.FishRoomOwnersInfo>;

namespace PerformanceFish;
public class RoomOptimizations : ClassWithFishPatches, IHasDescription
{
	public string Description => "Room stats related optimizations";
	public class Role_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Throttles room stats and role updating to happen at most once every 512 or so ticks per room, instead of every time anything at all changes or moves";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(Room), nameof(Room.Role));
		public static CodeInstructions Transpiler(CodeInstructions codes)
		{
			var this_statsAndRoleDirty = FishTranspiler.Field(typeof(Room), nameof(Room.statsAndRoleDirty));

			try
			{
				return codes.ReplaceAt((codes, i)
					=> i - 1 > 0 && codes[i - 1] == this_statsAndRoleDirty
					&& codes[i].opcode == OpCodes.Brfalse_S && codes[i].operand is Label,
					code => new[]
					{
						code,
						FishTranspiler.This,
						FishTranspiler.Call(ShouldUpdate),
						FishTranspiler.IfFalse_Short((Label)code.operand)
					});
			}
			catch (Exception ex)
			{
				Log.Error($"Performance Fish failed to patch Room.Role. This just means that specific patch will be doing nothing.\n{ex}");
				return null!;
			}
		}
		public static bool ShouldUpdate(Room __instance)
		{
			if (__instance.role is null || !RoomThrottleInfo.TryGetValue(__instance, out var value) || value < Current.Game.tickManager.TicksGame)
			{
				RoomThrottleInfo[__instance] = Current.Game.tickManager.TicksGame + 384 + Math.Abs(Rand.Int % 256);
				return true;
			}
			return false;
		}
		public static Dictionary<Room, int> RoomThrottleInfo { get; } = new();
		static Role_Patch() => Cache.Utility.All.Add(RoomThrottleInfo);
	}

	public class Owners_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches room owner data and throttles them to only update at most once every ~512 or so ticks. RimWorld calculates owners by checking ownership of all items contained in a room, which can get quite expensive";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(Room), nameof(Room.Owners));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Room __instance, ref IEnumerable<Pawn> __result, out bool __state)
		{
			ref var cache = ref RoomOwnersCache.Get.GetReference(__instance);

			if (cache.ShouldRefreshNow)
			{
				if (__instance.TouchesMapEdge || __instance.IsHuge || (__instance.statsAndRoleDirty && !__instance.ContainedBeds.Any()))
				{
					cache.ShouldRefreshNow = false;
					cache.owners = Array.Empty<Pawn>();
					RoomOwnersCache.Get[__instance] = cache;
					__result = cache.owners;
					return __state = false;
				}

				return __state = true;
			}

			__result = cache.owners;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Room __instance, bool __state, ref IEnumerable<Pawn> __result)
		{
			if (!__state)
				return;

			if (RoomOwnersCache.Get.TryGetValue(__instance, out var cache) && cache.owners is List<Pawn> list)
			{
				list.Clear();
				foreach (var pawn in __result)
					list.Add(pawn);
			}
			else
			{
				cache.owners = new List<Pawn>(__result);
			}

			cache.ShouldRefreshNow = false;
			RoomOwnersCache.Get[__instance] = cache;
			__result = cache.owners;
		}

		public struct FishRoomOwnersInfo
		{
			public int nextRefreshTick;
			public IEnumerable<Pawn> owners;

			public bool ShouldRefreshNow
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => nextRefreshTick < Current.Game.tickManager.TicksGame;
				set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 384 + Math.Abs(Rand.Int % 256);
			}
		}
	}

	public class Regions_Patch : FishPatch
	{
		public override string Description => "Minor optimization";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(Room), nameof(Room.Regions));

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(Regions_Replacement);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<Region> Regions_Replacement(Room instance)
		{
			var tmpRegions = instance.tmpRegions;
			var districts = instance.districts;
			lock (_lock)
			{
				tmpRegions.Clear();
				for (var i = 0; i < districts.Count; i++)
					tmpRegions.AddRange(districts[i].Regions);
			}
			return tmpRegions;
		}

		private static readonly object _lock = new();
	}

	public class ContainedBeds_Patch : FishPatch
	{
		public override string Description => "So RimWorld has a list of beds and a list of all things. For some reason ludeon chose to have ContainedBeds retrieve its beds by looping through all things. This fixes that. Helps a lot against spikes";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(Room), nameof(Room.ContainedBeds));

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(ContainedBeds_Replacement);

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

	public class ContainedAndAdjacentThings_Patch : FishPatch
	{
		public override string Description => "Caching of room content info. Variable impact";
		public override MethodBase TargetMethodInfo => AccessTools.PropertyGetter(typeof(Room), nameof(Room.ContainedAndAdjacentThings));

		public static CodeInstructions Transpiler(CodeInstructions CodeInstructions)
			=> Reflection.MakeReplacementCall(ContainedAndAdjacentThings_Replacement);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<Thing> ContainedAndAdjacentThings_Replacement(Room instance)
		{
			lock (_lock)
			{
				var uniqueContainedThings = instance.uniqueContainedThings;
				uniqueContainedThings.Clear();

				var regions = instance.Regions;

				if (CachedThingLists.TryGetValue(instance, out var cache) && cache.listVersions.Count == regions.Count && !cache.ShouldRefreshNow)
				{
					var cacheIsAccurate = true;
					for (var i = 0; i < regions.Count; i++)
					{
						var allThings = regions[i].listerThings.AllThings;
						if (allThings == null)
						{
							if (cache.listVersions[i] != -1)
								cacheIsAccurate = false;
						}
						else if (cache.listVersions[i] != allThings.Version())
						{
							cacheIsAccurate = false;
						}
					}
					if (cacheIsAccurate)
					{
						uniqueContainedThings.AddRange(cache.things);
						return uniqueContainedThings;
					}
				}

				return RefreshContainedAndAdjacentThings(instance, regions, cache);
			}
		}

		private static List<Thing> RefreshContainedAndAdjacentThings(Room instance, List<Region> regions, FishThingsInfo cache)
		{
			var uniqueContainedThings = instance.uniqueContainedThings;
			var uniqueContainedThingsSet = instance.uniqueContainedThingsSet;
			uniqueContainedThingsSet.Clear();
			(cache.listVersions ??= new()).Clear();

			for (var i = 0; i < regions.Count; i++)
			{
				var allThings = regions[i].ListerThings.AllThings;
				if (allThings == null)
				{
					cache.listVersions.Add(-1);
					continue;
				}
				var count = allThings.Count;
				for (var j = 0; j < count; j++)
					uniqueContainedThingsSet.Add(allThings[j]);
				
				cache.listVersions.Add(allThings.Version());
			}
			uniqueContainedThings.AddRange(uniqueContainedThingsSet);
			if (cache.things is List<Thing> list)
				list.ReplaceContentsWith(uniqueContainedThings);
			else
				cache.things = new List<Thing>(uniqueContainedThings);
			cache.ShouldRefreshNow = false;

			CachedThingLists[instance] = cache;

			uniqueContainedThingsSet.Clear();
			return uniqueContainedThings;
		}

		/*private static List<Thing> RefreshContainedAndAdjacentThingsAlternative(Room instance, List<Region> regions, FishThingsInfo cache)
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

				cache.listVersions.Add(Utility<Thing>.getListVersion(allThings));
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

		private static readonly object _lock = new();

		public static Dictionary<Room, FishThingsInfo> CachedThingLists { get; } = new();
		static ContainedAndAdjacentThings_Patch() => Cache.Utility.All.Add(CachedThingLists);

		public struct FishThingsInfo
		{
			public int nextRefreshTick;
			public List<int> listVersions;
			public IEnumerable<Thing> things;

			public bool ShouldRefreshNow
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => nextRefreshTick < Current.Game.tickManager.TicksGame;
				set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
			}
		}
	}

	public class RoomAt_Patch : FishPatch
	{
		public override string? Description => "Minor optimization through inlining ond cleaning up of duplicate calls in the method. About 1/4 faster, which isn't much, but it likely won't break anything either.";

		public override Delegate? TargetMethodGroup => RegionAndRoomQuery.RoomAt;

		public static CodeInstructions Transpiler(CodeInstructions codes)
			=> Reflection.MakeReplacementCall(RoomAt);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Room? RoomAt(IntVec3 c, Map map, RegionType allowedRegionTypes = RegionType.Set_All)
		{
			var mapSize = map.info.Size;
			if ((uint)c.x >= mapSize.x || (uint)c.z >= mapSize.z)
				return null;

			var regionAndRoomUpdater = map.regionAndRoomUpdater;
			if (regionAndRoomUpdater.Enabled)
				regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
			else if (regionAndRoomUpdater.AnythingToRebuild)
				Log.Warning($"Trying to get valid region at {c} but RegionAndRoomUpdater is disabled. The result may be incorrect.");

			var region = map.regionGrid.regionGrid[(c.z * mapSize.x) + c.x];
			return region is null || !region.valid || (region.type & allowedRegionTypes) == 0 ? null
				: region.District?.Room;
		}
	}
}