// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish;

public class StoreUtilityPatches : ClassWithFishPatches
{
	public class IsInValidBestStorage_Patch : FishPatch
	{
		public override string Description => "Patches IsInValidBestStorage to immediately remove things from haulables when they're found to be in the best storage. Default often has them checked several more times before removal happens";
		public override Delegate TargetMethodGroup => StoreUtility.IsInValidBestStorage;
		public static void Postfix(Thing t, bool __result)
		{
			if (__result)
			{
				t.Map.listerHaulables.TryRemove(t);
			}
			/*else if (!t.IsInValidStorage())
			{
				if (!StoreUtility.TryFindBestBetterStorageFor(t, null, t.Map, StoragePriority.Unstored, Faction.OfPlayerSilentFail, out IntVec3 _, out IHaulDestination _, needAccurateResult: false))
				{

				}
			}*/
		}
	}

#if threadingEnabled
	public class IsGoodStoreCell_Patch : FishPatch
	{
		protected IsGoodStoreCell_Patch() => FishSettings.ThreadingPatches.Add(this);
		public override bool DefaultState => false;
		public override string Description => "Lock for threading. Only needed with threading enabled and off by default";
		public override Delegate TargetMethodGroup => StoreUtility.IsGoodStoreCell;
		public static void Prefix()
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Enter(Lock);
		}
		public static Exception Finalizer(Exception __exception)
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Exit(Lock);
			return __exception;
		}
		private static object Lock { get; } = new();
	}

	public class GetStatValue_Patch : FishPatch
	{
		protected GetStatValue_Patch() => FishSettings.ThreadingPatches.Add(this);
		public override bool DefaultState => false;
		public override string Description => "Lock for threading. Only needed with threading enabled and off by default";
		public override Delegate TargetMethodGroup => StatExtension.GetStatValue;
		public static void Prefix()
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Enter(Lock);
		}
		public static Exception Finalizer(Exception __exception)
		{
			if (FishSettings.ThreadingEnabled)
				Monitor.Exit(Lock);
			return __exception;
		}
		private static object Lock { get; } = new();
	}
#endif
}