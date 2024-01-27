// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// #define StorageSettings_DEBUG

using System.Diagnostics;
using System.Linq;
using FisheryLib.Pools;
using PerformanceFish.Events;

namespace PerformanceFish.Hauling;

public sealed class StorageSettingsPatches : ClassWithFishPatches
{
	public sealed class AllowedToAcceptPatch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "StorageSettings and capacity caching. High impact in colonies with multiple full storage buildings.";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(StorageSettings)!.AllowedToAccept(default(Thing));

		protected internal override void OnPatchingCompleted() => ThingEvents.Initialized += TryRegister;

		public static void TryRegister(Thing thing)
		{
			var thingEvents = thing.Events();

			if (thing is ISlotGroupParent)
				thingEvents.Spawned += InitializeSlotGroupParent;
			
			if (!thing.IsItem())
				return;

			thingEvents.RegisteredAtThingGrid += TryNotifyReceivedThing;
			thingEvents.DeregisteredAtThingGrid += TryNotifyLostThing;
		}

		public static void TryNotifyReceivedThing(Thing thing, Map map, in IntVec3 cell)
		{
			if (cell.GetSlotGroupParent(map) is { } parent and Thing)
				parent.GetStoreSettings()?.Cache().Notify_ReceivedThing(thing);
		}

		public static void TryNotifyLostThing(Thing thing, Map map, in IntVec3 cell)
		{
			if (cell.GetSlotGroupParent(map) is { } parent and Thing)
				parent.GetStoreSettings()?.Cache().Notify_LostThing(thing);
		}

		public static void InitializeSlotGroupParent(Thing thing, Map map)
		{
			var slotGroupParent = (ISlotGroupParent)thing;
			
			if (slotGroupParent.GetStoreSettings() is not { } storeSettings)
				return;

			ref var cache = ref storeSettings.Cache();
			cache.Initialize(thing);

			var slotCellsList = slotGroupParent.AllSlotCellsList();
			for (var i = slotCellsList.Count; i-- > 0;)
			{
				var thingsAtCell = slotCellsList[i].GetThingListUnchecked(map);
				for (var j = thingsAtCell.Count; j-- > 0;)
				{
					var storedThing = thingsAtCell[j];
					if (!storedThing.IsItem() || cache.Contains(storedThing))
						continue;

					cache.Notify_ReceivedThing(storedThing);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(StorageSettings __instance, Thing t, ref bool __result, out bool __state)
		{
			var cachedAllowedToAcceptValue = __instance.Cache().AllowedToAccept.GetOrAdd(t.thingIDNumber);

			if (StorageSettingsCache.IsDirty(cachedAllowedToAcceptValue))
				return __state = true;

			__result = cachedAllowedToAcceptValue.AsBool() && CapacityAllows(__instance, t);

			return __state = false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static bool CapacityAllows(StorageSettings __instance, Thing t)
		{
			if (__instance.owner is not Thing ownerThing)
				return true;

			ref var cache = ref __instance.Cache();
			Debug.VerifyItemCount(ownerThing, ref cache);
			
			return cache.FreeSlots > 0
				|| (t.TryGetMap() is { } map && t.Position.GetSlotGroupParent(map) == ownerThing)
				|| AcceptsForStacking(ref cache, t);
		}

		public static bool AcceptsForStacking(ref StorageSettingsCache cache, Thing t)
			=> cache.StoredThingsOfDef(t.def).ContainsThingStackableWith(t);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(StorageSettings __instance, Thing t, bool __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(__instance, t, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void UpdateCache(StorageSettings __instance, Thing t, bool __result)
			=> __instance.Cache().Update(t.thingIDNumber, __result && t is { def: not null, thingIDNumber: not -1 });
	}

	public sealed class TryNotifyChanged_Patch : FishPatch
	{
		public static event Action<StorageSettings>? Changed;
		
		public override string Description { get; } = "StorageSettings cache resetting";

		public override Expression<Action> TargetMethod { get; }
			= static () => default(StorageSettings)!.TryNotifyChanged();

		public static void Postfix(StorageSettings __instance)
		{
			__instance.Cache().OnSettingsChanged(__instance);
			Changed?.Invoke(__instance);
		}
	}

	public record struct StorageSettingsCache
	{
		public FishTable<int, int> AllowedToAccept = new();

		private Thing? _parent;

		public int FreeSlots = 0xFFFFFFF;
		
		private int _storedThingCount;

		private FishTable<ushort, List<Thing>> _storedThingsByDef = new();

		private CompProperties? _lwmProps;

		private DefModExtension? _rimfactoryExtension;

		public event Action<StorageSettings>? SettingsChanged;

		public int TotalSlots => _parent!.TrueTotalSlots(_lwmProps, _rimfactoryExtension);

		public List<Thing> StoredThingsOfDef(ThingDef def) => _storedThingsByDef.GetOrAdd(def.shortHash);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsDirty(int allowedToAcceptValue) => allowedToAcceptValue == int.MaxValue;

		public void Update(int thingIDNumber, bool allowedToAccept)
			=> AllowedToAccept[thingIDNumber] = allowedToAccept.AsInt();

		public void Initialize(Thing slotGroupParent)
		{
			_parent = slotGroupParent;
			_storedThingsByDef.Clear();
			AllowedToAccept.Clear();
			_lwmProps = slotGroupParent.TryGetLwmCompProperties();
			_rimfactoryExtension = slotGroupParent.TryGetRimfactoryExtension();
			FreeSlots = TotalSlots;
		}

		public bool Contains(Thing thing) => StoredThingsOfDef(thing.def).Contains(thing);

		public void Notify_ReceivedThing(Thing thing)
		{
			if (_parent is null)
				return;
			
			StoredThingsOfDef(thing.def).Add(thing);
			FreeSlots = TotalSlots - ++_storedThingCount;
			Debug.VerifyItemCount(_parent, ref this);
		}

		public void Notify_LostThing(Thing thing)
		{
			if (_parent is null)
				return;
			
			StoredThingsOfDef(thing.def).Remove(thing);
			FreeSlots = TotalSlots - --_storedThingCount;
			
			Debug.VerifyItemCount(_parent, ref this);
		}

		public void OnSettingsChanged(StorageSettings storageSettings)
		{
			AllowedToAccept.Clear();
				
			if (_parent?.IsSpawned() ?? false)
				FreeSlots = TotalSlots - _storedThingCount;
			
			SettingsChanged?.Invoke(storageSettings);
		}

		public StorageSettingsCache() => AllowedToAccept.ValueInitializer = static _ => int.MaxValue;
	}

	public static class Debug
	{
		[Conditional("StorageSettings_DEBUG")]
		public static void LogMessage(PooledStringHandler stringHandler) => Log.Message(stringHandler);
		
		[Conditional("StorageSettings_DEBUG")]
		public static void VerifyItemCount(Thing storageThing, ref StorageSettingsCache cache)
		{
			if (storageThing.TryGetMap() is not { } ownerMap || storageThing is not ISlotGroupParent slotGroup)
				return;

			var realTotalSlots = cache.TotalSlots;
			var realTotalItemCount = slotGroup.AllSlotCellsList().Sum(cell => cell.GetItemCount(ownerMap));
			if (cache.FreeSlots == realTotalSlots - realTotalItemCount)
				return;

			Log.Error($"StorageSettings cache for '{storageThing}' has incorrect free slots count of '{
				cache.FreeSlots}'. Real total item count: '{realTotalItemCount}', real total slots: '{
					realTotalSlots}', real free slots: '{realTotalSlots - realTotalItemCount}'");
					
			Current.Game.tickManager.Pause();
		}
	}
}