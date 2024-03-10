// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Events;

public sealed class ThingEvents : ClassWithFishPrepatches
{
	public static event Action<Thing>?
		Initialized,
		Destroyed,
		QualityChanging;
	
	public sealed class Instanced
	{
		public event ThingGridEvents.EventHandler?
			RegisteredAtThingGrid,
			DeregisteredAtThingGrid;
		
		public event Action<Thing, Map>? Spawned;

		public event Action<Thing>?
			DeSpawning,
			DeSpawned,
			Destroying,
			Destroyed;

		internal void OnSpawned(Thing thing, Map map) => Spawned?.Invoke(thing, map);

		internal void OnDeSpawning(Thing thing) => DeSpawning?.Invoke(thing);

		internal void OnDeSpawned(Thing thing) => DeSpawned?.Invoke(thing);

		internal void OnDestroying(Thing thing) => Destroying?.Invoke(thing);

		internal void OnDestroyed(Thing thing) => Destroyed?.Invoke(thing);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void OnRegisteredAtThingGrid(Thing thing, Map map, in IntVec3 cell)
			=> RegisteredAtThingGrid?.Invoke(thing, map, cell);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void OnDeregisteredAtThingGrid(Thing thing, Map map, in IntVec3 cell)
			=> DeregisteredAtThingGrid?.Invoke(thing, map, cell);
	}

	public sealed class SpawnSetupPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things spawn. Does nothing by itself, but certain functions require "
			+ "this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.SpawnSetup));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Thing __instance, Map map) => __instance.Events().OnSpawned(__instance, map);
	}

	public sealed class DeSpawnPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things despawn. Does nothing by itself, but certain functions require "
			+ "this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.DeSpawn));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(Thing __instance) => __instance.Events().OnDeSpawning(__instance);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Thing __instance) => __instance.Events().OnDeSpawned(__instance);
	}

	public sealed class DestroyPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event whenever things get destroyed. Does nothing by itself, but certain functions "
			+ "require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.Destroy));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(Thing __instance) => __instance.Events().OnDestroying(__instance);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Thing __instance)
		{
			__instance.Events().OnDestroyed(__instance);
			Destroyed?.Invoke(__instance);
		}
	}
	
	public sealed class ExposeDataInitializePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event when things finish loading from a save file. Does nothing by itself, but "
			+ "certain functions require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.ExposeData));

		public static void Postfix(Thing __instance)
		{
			if (Scribe.mode == LoadSaveMode.LoadingVars && __instance is not ThingWithComps)
				OnThingInitialized(__instance);
		}
	}

	public sealed class PostMakePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event when things get created. Does nothing by itself, but certain functions require "
			+ "this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Thing), nameof(Thing.PostMake));

		public static void Postfix(Thing __instance)
		{
			if (__instance is not ThingWithComps)
				OnThingInitialized(__instance);
		}
	}

	public sealed class InitializeCompsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to invoke an event when things with comps get initialized. Does nothing by itself, but certain "
			+ "functions require this.";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(ThingWithComps), nameof(ThingWithComps.InitializeComps));

		public static void Postfix(ThingWithComps __instance) => OnThingInitialized(__instance);
	}

	public sealed class SetQualityPatch : FishPrepatch
	{
		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(CompQuality), nameof(CompQuality.SetQuality));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix(CompQuality __instance, QualityCategory q)
		{
			if (q != __instance.qualityInt)
				QualityChanging?.Invoke(__instance.parent);
		}
	}

	private static void OnThingInitialized(Thing thing) => Initialized?.Invoke(thing);
}