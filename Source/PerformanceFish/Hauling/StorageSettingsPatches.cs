// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// #define StorageSettings_DEBUG

using System.Diagnostics;
using System.Linq;
using FisheryLib.Pools;

namespace PerformanceFish.Hauling;

public sealed class StorageSettingsPatches : ClassWithFishPatches
{
	public sealed class AllowedToAcceptPatch : FirstPriorityFishPatch
	{
		public override string Description { get; }
			= "Caches results of storage settings checks for things when hauling";

		protected internal override void OnPatchingCompleted()
			=> Events.ThingEvents.QualityChanging += NotifyQualityChanging;

		public static void NotifyQualityChanging(Thing thing)
		{
			if (!thing.IsItem() || thing.TryGetMap() is not { } map)
				return;

			var haulDestinations = map.haulDestinationManager.AllHaulDestinationsListForReading;
			for (var i = haulDestinations.Count; i-- > 0;)
				haulDestinations[i].GetStoreSettings().Cache().AllowedToAccept.Remove(thing.thingIDNumber);
		}

		public override Expression<Action> TargetMethod { get; }
			= static () => default(StorageSettings)!.AllowedToAccept(default(Thing));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(StorageSettings __instance, Thing t, ref bool __result, out bool __state)
		{
			ref var allowedToAcceptCacheValue
				= ref __instance.Cache().AllowedToAccept.GetOrAddReference(t.thingIDNumber);

			if (allowedToAcceptCacheValue.Dirty)
				return __state = true;

			__result = allowedToAcceptCacheValue.Value;

			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(StorageSettings __instance, Thing t, bool __result, bool __state)
		{
			if (!__state)
				return;

			UpdateCache(__instance, t, __result);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void UpdateCache(StorageSettings __instance, Thing t, bool __result)
			=> __instance.Cache().AllowedToAccept.GetOrAddReference(t.thingIDNumber)
				.Update(t, __result && t is { def: not null, thingIDNumber: not -1 });
	}

	public sealed class TryNotifyChanged_Patch : FishPatch
	{
		public override List<Type> LinkedPatches { get; } = [typeof(AllowedToAcceptPatch)];

		public static event Action<StorageSettings>? Changed;
		
		public override string Description { get; } = "StorageSettings cache resetting";

		public override MethodBase? TargetMethodInfo { get; }
			= AccessTools.DeclaredMethod(typeof(StorageSettings), nameof(StorageSettings.TryNotifyChanged));

		public static void Postfix(StorageSettings __instance)
		{
			__instance.Cache().OnSettingsChanged(__instance);
			
			if (__instance.owner is StorageGroup group)
				NotifyGroupMembers(group);
			
			Changed?.Invoke(__instance);
		}

		public static void NotifyGroupMembers(StorageGroup group)
		{
			var groupMembers = group.members;
			for (var i = 0; i < groupMembers.Count; i++)
			{
				var thingStoreSettings = groupMembers[i].ThingStoreSettings;
				thingStoreSettings.Cache().OnSettingsChanged(thingStoreSettings);
			}
		}
	}
	
	public static void InitializeSlotGroupParent(Thing thing, Map map)
	{
		var slotGroupParent = (ISlotGroupParent)thing;
			
		slotGroupParent.TryGetGroupStoreSettings()?.Cache().Initialize(null);
			
		if (slotGroupParent.GetThingStoreSettings() is not { } storeSettings)
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

	public record struct StorageSettingsCache()
	{
		public FishTable<int, AllowedToAcceptCacheValue> AllowedToAccept = new();

		private Thing? _parent;

		public int FreeSlots = 0xFFFFFFF;
		
		private int _storedThingCount;

		private FishTable<ushort, List<Thing>> _storedThingsByDef = new();

		private CompProperties? _lwmProps;

		private DefModExtension? _rimfactoryExtension;

		public event Action<StorageSettings>? SettingsChanged;

		public int TotalSlots => _parent?.TrueTotalSlots(_lwmProps, _rimfactoryExtension) ?? 0xFFFFFFF;

		public List<Thing> StoredThingsOfDef(ThingDef def) => _storedThingsByDef.GetOrAdd(def.shortHash);

		public void Initialize(Thing? slotGroupParent)
		{
			_parent = slotGroupParent;
			_storedThingCount = 0;
			_storedThingsByDef.Clear();
			AllowedToAccept.Clear();
			_lwmProps = slotGroupParent?.TryGetLwmCompProperties();
			_rimfactoryExtension = slotGroupParent?.TryGetRimfactoryExtension();
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
	}

	public record struct AllowedToAcceptCacheValue()
	{
		private int
			_value = int.MaxValue,
			_nextUpdateTick = -2;

		public bool Value => _value.AsBool();

		public bool Dirty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (_value == int.MaxValue) | TickHelper.Past(_nextUpdateTick);
		}

		public void Update(Thing thing, bool allowedToAccept)
		{
			_value = allowedToAccept.AsInt();
			_nextUpdateTick = TickHelper.Add(2048, thing.thingIDNumber, 512);
		}
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