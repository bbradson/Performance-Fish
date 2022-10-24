// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Threading.Tasks;
using SharedAltarCache = PerformanceFish.Cache.ByIndex<RimWorld.Ideo, PerformanceFish.ThoughtWorker_Precept_AltarSharingOptimization.SharedAltarCacheValue>;

namespace PerformanceFish;

public class ThoughtWorker_Precept_AltarSharingOptimization : ClassWithFishPatches
{
	public class SharedAltar_Patch : FirstPriorityFishPatch
	{
		public override string Description => "Caches results of ideology's shared altar thoughtworker, throttles it to wait a minimum of 128 ticks between refreshes and makes refreshing happen async on a different thread. Quite impactful on performance";
		public override Expression<Action> TargetMethod => () => default(ThoughtWorker_Precept_AltarSharing)!.SharedAltar(null);
		public static void AsyncThoughtWorker(ThoughtWorker_Precept_AltarSharing __instance, Pawn pawn)
		{
			var allThings = pawn.Map.listerThings.listsByGroup[(int)ThingRequestGroup.BuildingArtificial].ToArray();
			Task.Run(() =>
			{
				var newThing = SharedAltarParallel(pawn, allThings);
				lock (Lock)
					SharedAltarCache.GetDirectly[pawn.Ideo] = new(pawn, newThing);
			});
		}
		public static bool Prefix(ThoughtWorker_Precept_AltarSharing __instance, Pawn pawn, ref Thing? __result, out bool __state)
		{
			if (!pawn.Spawned)
			{
				__result = null;
				return __state = false;
			}
			SharedAltarCacheValue cache;
			var shouldStartRefreshing = false;
			lock (Lock)
			{
				if (!SharedAltarCache.GetDirectly.TryGetValue(pawn.Ideo, out cache) || (!FishSettings.ThreadingEnabled && cache.ShouldRefreshNow))
				{
					return __state = true;
				}
				else if (cache.ShouldRefreshNow)
				{
					cache.currentlyRefreshing = true;
					SharedAltarCache.GetDirectly[pawn.Ideo] = cache;
					shouldStartRefreshing = true;
				}
			}

			__result = cache.thing;
			if (shouldStartRefreshing)
				AsyncThoughtWorker(__instance, pawn);
			return __state = false;
		}
		public static void Postfix(Pawn pawn, Thing __result, bool __state)
		{
			if (!__state)
				return;
			lock (Lock)
				SharedAltarCache.GetDirectly[pawn.Ideo] = new(pawn, __result);
		}
		public static Thing? SharedAltarParallel(Pawn pawn, Thing[] allThings)
		{
			if (!pawn.Spawned || pawn.Ideo == null)
				return null;

			for (var i = 0; i < allThings.Length; i++)
			{
				var item = allThings[i];
				var compStyleable = item.TryGetComp<CompStyleable>();
				if (compStyleable == null || compStyleable.SourcePrecept == null || compStyleable.SourcePrecept.ideo != pawn.Ideo)
					continue;

				var room = item.GetRoom();
				if (room == null || room.TouchesMapEdge)
					continue;

				var containedAndAdjacentThings = room.ContainedAndAdjacentThings;
				for (var k = 0; k < containedAndAdjacentThings.Count; k++)
				{
					var containedAndAdjacentThing = containedAndAdjacentThings[k];
					if (containedAndAdjacentThing != item)
					{
						var compStyleable2 = containedAndAdjacentThing.TryGetComp<CompStyleable>();
						if (compStyleable2 != null && compStyleable2.SourcePrecept != null && compStyleable2.SourcePrecept.ideo != pawn.Ideo)
						{
							return item;
						}
					}
				}
			}
			return null;
		}
		private static object Lock { get; } = new();
	}

	public struct SharedAltarCacheValue : Cache.IIsRefreshable<Ideo, SharedAltarCacheValue>
	{
		public Thing? thing;
		public bool currentlyRefreshing;
		private int _nextLateRefreshTick;
		private int _nextEarlyRefreshTick;
		private int _allStructuresListVersion;
		private ListerThings _lister;

		public SharedAltarCacheValue(Pawn pawn, Thing? result)
		{
			thing = result;
			var ticks = Current.Game.tickManager.TicksGame;
			_nextLateRefreshTick = ticks + 3072 + Math.Abs(Rand.Int % 2048);
			_nextEarlyRefreshTick = ticks + 128 + Math.Abs(Rand.Int % 128);
			_lister = pawn.Map.listerThings;
			_allStructuresListVersion = _lister.listsByGroup[(int)ThingRequestGroup.BuildingArtificial].Version();
			currentlyRefreshing = false;
		}

		public bool ShouldRefreshNow
		{
			get
			{
				var ticks = Current.Game.tickManager.TicksGame;
				return !currentlyRefreshing && (_nextLateRefreshTick < ticks
					|| (_nextEarlyRefreshTick < ticks && _allStructuresListVersion != _lister.listsByGroup[(int)ThingRequestGroup.BuildingArtificial].Version()));
			}
			set
			{
				var ticks = Current.Game.tickManager.TicksGame;
				_nextLateRefreshTick = value ? 0 : ticks + 3072 + Math.Abs(Rand.Int % 2048);
				_nextEarlyRefreshTick = value ? 0 : ticks + 128 + Math.Abs(Rand.Int % 128);
			}
		}

		public SharedAltarCacheValue SetNewValue(Ideo key) => throw new NotImplementedException();
	}
}