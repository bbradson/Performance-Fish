// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using RimWorld.Planet;

namespace PerformanceFish.Planet;

public sealed class WorldObjectsOptimization : ClassWithFishPrepatches
{
	public sealed class WorldObjectsHolderTickPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "The world objects holder is responsible for ticking every world object. This includes settlements, "
			+ "caravans, outposts and any other object placed in the world, instead of maps. Normally it ticks "
			+ "everything equally, including many static objects that cannot possibly affect anything from a tick. "
			+ "This patch improves the world objects holder to determine objects that need ticking in advance, cache "
			+ "the list of them, and only tick those, skipping all the others.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.WorldObjectsHolderTick));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(WorldObjectsHolderTick);

		public static void WorldObjectsHolderTick(WorldObjectsHolder instance)
		{
			if (CacheDirty(instance))
				UpdateCache(instance);

			var worldObjects = WorldObjectsHolder.tmpWorldObjects;
			for (var i = worldObjects.Count; i-- > 0;)
				worldObjects[i].Tick();
		}

		public static void UpdateCache(WorldObjectsHolder instance)
		{
			var staticWorldObjects = WorldObjectsHolder.tmpWorldObjects;
			staticWorldObjects.Clear();

			var instanceWorldObjects = instance.worldObjects;

			for (var i = instanceWorldObjects.Count; i-- > 0;)
			{
				var worldObject = instanceWorldObjects[i];

				if (worldObject is not MapParent { HasMap: true }
					&& SkippableWorldObjects.Contains(worldObject.GetType())
					&& (worldObject is not Settlement settlement || settlement.trader?.stock is null) 
					&& CanSkipCompTick(worldObject))
				{
					continue;
				}
				
				staticWorldObjects.Add(worldObject);
			}

			CachedWorldObjectsVersion = instanceWorldObjects._version;
			CachedMapsVersion = Current.gameInt.maps._version;
		}

		private static bool CanSkipCompTick(WorldObject worldObject)
		{
			var comps = worldObject.comps;

			for (var i = comps.Count; i-- > 0;)
			{
				var comp = comps[i];
				if (comp is EnterCooldownComp { Active: true })
					return false;

				if (!SkippableComps.Contains(comp.GetType()))
					return false;
				
				comp.CompTick();
			}

			return true;
		}

		public static int
			CachedWorldObjectsVersion = -2,
			CachedMapsVersion = -2;

		private static Type?[]
			_whitelistedTickingCompTypes =
			[
				typeof(WorldObjectComp),
				typeof(FormCaravanComp),
				typeof(TimedDetectionRaids),
				typeof(EnterCooldownComp)
			],
			_whitelistedWorldObjectTypes =
			[
				typeof(WorldObject),
				typeof(MapParent),
				typeof(Settlement),
				ModCompatibility.Types.RealRuins.POIWorldObject
			];

		public static void AddCompToWhiteList(Type compType)
		{
			_whitelistedTickingCompTypes = _whitelistedTickingCompTypes.Add(compType);
			SkippableComps = InitializeSkippableComps();
		}

		public static void AddWorldObjectToWhiteList(Type worldObjectType)
		{
			_whitelistedWorldObjectTypes = _whitelistedWorldObjectTypes.Add(worldObjectType);
			SkippableWorldObjects = InitializeSkippableWorldObjects();
		}

		public static HashSet<Type>
			SkippableComps = InitializeSkippableComps(),
			SkippableWorldObjects = InitializeSkippableWorldObjects();

		private static HashSet<Type> InitializeSkippableComps()
			=> MakeSubclassHashSet(typeof(WorldObjectComp), nameof(WorldObjectComp.CompTick),
				_whitelistedTickingCompTypes);

		private static HashSet<Type> InitializeSkippableWorldObjects()
			=> MakeSubclassHashSet(typeof(WorldObject), nameof(WorldObject.Tick), _whitelistedWorldObjectTypes);

		private static HashSet<Type> MakeSubclassHashSet(Type type, string name, Type?[] allowedDeclaringTypes)
			=> type.SubclassesWithNoMethodOverrideAndSelf(allowedDeclaringTypes, name).ToHashSet();

		public static bool CacheDirty(WorldObjectsHolder instance)
			=> CachedWorldObjectsVersion != instance.worldObjects._version
			|| CachedMapsVersion != Current.gameInt.maps._version;

		public static void SetDirty() => CachedWorldObjectsVersion = CachedMapsVersion = -2;

		static WorldObjectsHolderTickPatch() => Cache.Utility.Cleared += SetDirty;
	}

	public sealed class ExpandingIconCaching : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches icons that get displayed for world objects like settlements on the planet view and adds various "
			+ "safety checks to better catch and log errors in case of missing icons";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(WorldObject), nameof(WorldObject.ExpandingIcon));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static Texture2D ReplacementBody(WorldObject __instance)
			=> __instance.ExpandingIconCache() ?? InitializeExpandingIcon(__instance);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Texture2D InitializeExpandingIcon(WorldObject? worldObject)
		{
			if (worldObject is null)
				return BaseContent.BadTex;
			
			var def = worldObject.def;
			ref var cache = ref worldObject.ExpandingIconCache();
			
			try
			{
				cache = def?.ExpandingIconTexture;

				if (cache != null)
					return cache;

				var material = worldObject.Material;
				if (material != null)
					cache = material.mainTexture as Texture2D;

				if (cache == null)
				{
					cache = BaseContent.BadTex;
					Log.Error($"No expanding icon found for '{worldObject.ToStringSafe()}' of def '{
						def?.ToStringSafe()}' from mod '{def?.modContentPack?.Name}'. Assigning default.");
				}
				
				return cache;
			}
			catch (Exception ex)
			{
				Log.Error($"Exception while initializing icon for '{worldObject.ToStringSafe()}' of def '{
					def?.ToStringSafe()}' from mod '{def?.modContentPack?.Name}':\n{ex}");
				
				return cache = BaseContent.BadTex;
			}
		}
	}
	
	public sealed class ExpandingIconColorCaching : FishPrepatch
	{
		public override string? Description { get; }
			= "Caches colors of icons that get displayed for world objects like settlements on the planet view and "
			+ "adds various safety checks to better catch and log errors in case of missing data";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredPropertyGetter(typeof(WorldObject), nameof(WorldObject.ExpandingIconColor));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static Color ReplacementBody(WorldObject __instance)
			=> __instance.ExpandingIconColorCache() ?? InitializeExpandingIconColor(__instance);

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Color InitializeExpandingIconColor(WorldObject? worldObject)
		{
			if (worldObject is null)
				return Color.white;
			
			var def = worldObject.def;
			ref var cache = ref worldObject.ExpandingIconColorCache();
			
			try
			{
				cache = def?.expandingIconColor;

				if (cache != null)
					return cache.GetValueOrDefault();

				var material = worldObject.Material;
				if (material != null)
					cache = material.color;

				if (cache == null)
				{
					cache = Color.white;
					Log.Error($"No expanding icon color found for '{worldObject.ToStringSafe()}' of def '{
						def?.ToStringSafe()}' from mod '{def?.modContentPack?.Name}'. Assigning default.");
				}
				
				return cache.GetValueOrDefault();
			}
			catch (Exception ex)
			{
				Log.Error($"Exception while initializing icon color for '{worldObject.ToStringSafe()}' of def '{
					def?.ToStringSafe()}' from mod '{def?.modContentPack?.Name}':\n{ex}");
				
				return (cache = Color.white).GetValueOrDefault();
			}
		}
	}
}