// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using ThingRequestCache = PerformanceFish.Cache.ByReferenceRefreshable<int, byte, System.RuntimeTypeHandle, PerformanceFish.ThingRequestInfo>;

namespace PerformanceFish;
public class ThingOwnerUtilityCaching : ClassWithFishPatches
{
	public class GetAllThingsRecursively_Patch : FishPatch
	{
		public override string Description => "Caching of information about mostly unspawned pawns and things. Relatively large performance impact";
		public override Delegate TargetMethodGroup => ThingOwnerUtility.GetAllThingsRecursively<Thing>;

		public static bool Prefix(Map map, ThingRequest request, IList outThings, bool allowUnreal, Predicate<IThingHolder> passCheck, bool alsoGetSpawnedThings, out bool __state)
		{
			if (request.singleDef != null || request.group == 0 || !allowUnreal || passCheck != null || alsoGetSpawnedThings || outThings == null)
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
			var cache = GetAndCheckCache(map, request.group, type);
			if (cache.count == listByGroup.Count)
			{
				if (ThingHolderCount(map) != cache.thingHolderCount)
					return __state = true;

				outThings.Clear();
				outThings.TryAddRange(cache.things, type);
				return __state = false;
			}

			return __state = true;
		}

		public static void Postfix(Map map, ThingRequest request, IList outThings, bool __state)
		{
			if (!__state)
				return;

			var type = outThings.GetType().GetGenericArguments()[0];
			var cache = ThingRequestCache.Get[new(map.uniqueID, (byte)request.group, type.TypeHandle)];
			if (cache.things is null)
			{
				cache.things = FisheryLib.CollectionExtensions.TryNew(outThings, type);
			}
			else
			{
				cache.things.Clear();
				cache.things.TryAddRange(outThings, type);
			}
			cache.ShouldRefreshNow = false;
			cache.count = map.listerThings.listsByGroup[(int)request.group]?.Count ?? -1;
			cache.thingHolderCount = ThingHolderCount(map);

			ThingRequestCache.Get[new(map.uniqueID, (byte)request.group, type.TypeHandle)] = cache;
		}

		public static ref ThingRequestInfo GetAndCheckCache(Map map, ThingRequestGroup group, Type type)
		{
			var key = new ThingRequestCache(map.uniqueID, (byte)group, type.TypeHandle);
			ref var cache = ref ThingRequestCache.Get.TryGetReferenceUnsafe(ref key);

			if (Unsafe.IsNullRef(ref cache))
			{
				ThingRequestCache.Get[key] = new();
				cache = ref ThingRequestCache.Get.TryGetReferenceUnsafe(ref key);
			}

			if (cache.ShouldRefreshNow)
			{
				cache.count = map.listerThings.listsByGroup[(int)group]?.Count ?? -1;
				cache.thingHolderCount = ThingHolderCount(map);
				cache.ShouldRefreshNow = false;
				ThingRequestCache.Get[new(map.uniqueID, (byte)group, type.TypeHandle)] = cache;
				cache.count = -1;
			}

			unsafe
			{
				return ref Unsafe.AsRef<ThingRequestInfo>(Unsafe.AsPointer(ref cache));
			}
		}
	}

	public static int ThingHolderCount(Map map) => (map.passingShipManager?.passingShips.Count ?? 0) + map.components.Count + (map.listerThings.listsByGroup[(int)ThingRequestGroup.ThingHolder]?.Count ?? 0);

	/*public static void GetAllThingsRecursively(IThingHolder holder, List<Thing> outThings)
	{
		outThings.Clear();

		ThingOwnerUtility.tmpStack.Clear();
		ThingOwnerUtility.tmpStack.Push(holder);
		while (ThingOwnerUtility.tmpStack.Count != 0)
		{
			IThingHolder thingHolder = ThingOwnerUtility.tmpStack.Pop();
			ThingOwner directlyHeldThings = thingHolder.GetDirectlyHeldThings();
			if (directlyHeldThings != null)
			{
				outThings.AddRange(directlyHeldThings);
			}

			ThingOwnerUtility.tmpHolders.Clear();
			thingHolder.GetChildHolders(ThingOwnerUtility.tmpHolders);
			for (int i = 0; i < ThingOwnerUtility.tmpHolders.Count; i++)
			{
				ThingOwnerUtility.tmpStack.Push(ThingOwnerUtility.tmpHolders[i]);
			}
		}

		ThingOwnerUtility.tmpStack.Clear();
		ThingOwnerUtility.tmpHolders.Clear();
	}

	public static void AppendThingHoldersFromThings(List<IThingHolder> outThingsHolders, IList<Thing> container)
	{
		if (container == null)
		{
			return;
		}

		int i = 0;
		for (int count = container.Count; i < count; i++)
		{
			IThingHolder thingHolder = container[i] as IThingHolder;
			if (thingHolder != null)
			{
				outThingsHolders.Add(thingHolder);
			}

			ThingWithComps thingWithComps = container[i] as ThingWithComps;
			if (thingWithComps == null)
			{
				continue;
			}

			List<ThingComp> allComps = thingWithComps.AllComps;
			for (int j = 0; j < allComps.Count; j++)
			{
				IThingHolder thingHolder2 = allComps[j] as IThingHolder;
				if (thingHolder2 != null)
				{
					outThingsHolders.Add(thingHolder2);
				}
			}
		}
	}*/
}

public struct ThingRequestInfo : Cache.IIsRefreshable<ThingRequestCache, ThingRequestInfo>
{
	public int nextRefreshTick;
	public int count;
	public int thingHolderCount;
	public IList things;

	public bool ShouldRefreshNow
	{
		get => nextRefreshTick < Current.Game.tickManager.TicksGame;
		set => nextRefreshTick = value ? 0 : Current.Game.tickManager.TicksGame + 3072 + Math.Abs(Rand.Int % 2048);
	}

	public ThingRequestInfo SetNewValue(ThingRequestCache key) => throw new NotImplementedException();
}