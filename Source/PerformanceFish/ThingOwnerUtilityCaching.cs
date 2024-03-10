// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;
/*using ThingRequestCache
	= PerformanceFish.Cache.ByReference<int, byte, System.RuntimeTypeHandle,
		PerformanceFish.ThingRequestInfo>;*/

namespace PerformanceFish;

public sealed class ThingOwnerUtilityPreCaching : ClassWithFishPrepatches
{
	public sealed class GetAllThingsRecursively_Patch : FishPrepatch
	{
		// https://github.com/jptrrs/HumanResources/blob/master/Source/Harmony/ThingOwnerUtility_GetAllThingsRecursively.cs
		// Harmony patches on generics like there strip out information from the generic T argument, due to harmony
		// replacing the method with one made with Reflection.Emit
		// noted down as known edge case on the harmony wiki here:
		// https://harmony.pardeike.net/articles/patching-edgecases.html#generics
		public override List<string> IncompatibleModIDs { get; }
			= [ModCompatibility.PackageIDs.HUMAN_RESOURCES];

		public override string Description { get; }
			= "Caching of information about mostly unspawned pawns and things. Relatively large performance impact";

		public override MethodBase TargetMethodBase { get; }
			= methodof(ThingOwnerUtility.GetAllThingsRecursively<Thing>);

		public static bool Prefix<T>(Map map, ThingRequest request, List<T>? outThings, bool allowUnreal,
			Predicate<IThingHolder>? passCheck, bool alsoGetSpawnedThings, out bool __state)
			where T : Thing
		{
			if (request.singleDef != null
				|| request.group == 0
				|| !allowUnreal
				|| passCheck != null
				|| alsoGetSpawnedThings
				|| outThings == null)
			{
				__state = false;
				return true;
			}

			var listsByGroup = map.listerThings.listsByGroup[(int)request.group];
			if (listsByGroup is null)
			{
				__state = false;
				return true;
			}

			var key = new Cache.ByInt<Map, ThingRequestGroup, ThingRequestInfo<T>>(map.uniqueID, (byte)request.group);
			ref var cache = ref Cache.ByInt<Map, ThingRequestGroup, ThingRequestInfo<T>>.Get.GetOrAddReference(ref key);

			if (cache.Dirty
				|| cache.ListsByGroupCount != listsByGroup.Count
				|| cache.ThingHolderCount != ThingHolderCount(map))
			{
				return __state = true;
			}

			outThings.ReplaceContentsWith(cache.Things);
			return __state = false;
		}

		public static void Postfix<T>(Map map, ThingRequest request, List<T> outThings, bool __state)
			where T : Thing
		{
			if (!__state)
				return;

			ref var cache = ref Cache.ByInt<Map, ThingRequestGroup, ThingRequestInfo<T>>.Get.GetReference(new(map.uniqueID,
				(byte)request.group));

			cache.Things.ReplaceContentsWith(outThings);
			cache.ListsByGroupCount = map.listerThings.listsByGroup[(int)request.group]?.Count ?? -1;
			cache.ThingHolderCount = ThingHolderCount(map);
			cache.SetDirty(false, map.uniqueID + (int)request.group + cache.ListsByGroupCount + cache.ThingHolderCount);
		}
	}
	
	public static int ThingHolderCount(Map map)
		=> (map.passingShipManager?.passingShips.Count ?? 0)
			+ map.components.Count
			+ (map.listerThings.listsByGroup[(int)ThingRequestGroup.ThingHolder]?.Count ?? 0);
}

/*public sealed class ThingOwnerUtilityCaching : ClassWithFishPatches
{
	public sealed class GetAllThingsRecursively_Patch : FishPatch
	{
		public override string Description { get; }
			= "Caching of information about mostly unspawned pawns and things. Relatively large performance impact";

		public override Delegate TargetMethodGroup { get; } = ThingOwnerUtility.GetAllThingsRecursively<Thing>;

		public static bool Prefix(Map map, ThingRequest request, IList? outThings, bool allowUnreal,
			Predicate<IThingHolder>? passCheck, bool alsoGetSpawnedThings, out bool __state)
		{
			if (request.singleDef != null
				|| request.group == 0
				|| !allowUnreal
				|| passCheck != null
				|| alsoGetSpawnedThings
				|| outThings == null)
			{
				__state = false;
				return true;
			}

			var listByGroup = map.listerThings.listsByGroup[(int)request.group];
			if (listByGroup is null)
			{
				__state = false;
				return true;
			}

			var type = outThings.GetType().GetGenericArguments()[0];
			
			var key = new ThingRequestCache(map.uniqueID, (byte)request.group, type.TypeHandle);
			ref var cache = ref ThingRequestCache.Get.GetOrAddReference(ref key);

			if (cache.Dirty
				|| cache.count != listByGroup.Count
				|| cache.thingHolderCount != ThingHolderCount(map))
			{
				return __state = true;
			}

			outThings.Clear();
			outThings.TryAddRange(cache.things!, type);
			return __state = false;
		}

		public static void Postfix(Map map, ThingRequest request, IList outThings, bool __state)
		{
			if (!__state)
				return;

			var type = outThings.GetType().GetGenericArguments()[0];
			ref var cache
				= ref ThingRequestCache.Get.GetReference(new(map.uniqueID, (byte)request.group, type.TypeHandle));
			
			if (cache.things is null)
			{
				cache.things = FisheryLib.CollectionExtensions.TryNew(outThings, type);
			}
			else
			{
				cache.things.Clear();
				cache.things.TryAddRange(outThings, type);
			}

			cache.count = map.listerThings.listsByGroup[(int)request.group]?.Count ?? -1;
			cache.thingHolderCount = ThingHolderCount(map);
			cache.SetDirty(false, map.uniqueID + (int)request.group + cache.count + cache.thingHolderCount);
		}
	}

	public static int ThingHolderCount(Map map)
		=> (map.passingShipManager?.passingShips.Count ?? 0)
			+ map.components.Count
			+ (map.listerThings.listsByGroup[(int)ThingRequestGroup.ThingHolder]?.Count ?? 0);
}*/

/*public record struct ThingRequestInfo
{
	private int _nextRefreshTick;
	public int count;
	public int thingHolderCount;
	public IList? things;

	public void SetDirty(bool value, int offset)
		=> _nextRefreshTick = value ? 0 : TickHelper.Add(3072, offset, 2048);

	public bool Dirty => _nextRefreshTick < Current.Game.tickManager.TicksGame;
}*/

public record struct ThingRequestInfo<T>() where T : Thing
{
	private int _nextUpdateTick = -2;
	public int ListsByGroupCount = -2;
	public int ThingHolderCount = -2;
	public readonly List<T> Things = [];

	public void SetDirty(bool value, int offset)
		=> _nextUpdateTick = value ? 0 : TickHelper.Add(3072, offset, 2048);

	public bool Dirty => TickHelper.Past(_nextUpdateTick);
}