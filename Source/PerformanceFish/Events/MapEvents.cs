// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish.Events;

public sealed class MapEvents : ClassWithFishPrepatches
{
	public sealed class Instanced
	{
		public event Action<Map>?
			ComponentsConstructed,
			ComponentsLoaded;

		internal void OnComponentsConstructed(Map map) => ComponentsConstructed?.Invoke(map);

		internal void OnComponentsLoaded(Map map) => ComponentsLoaded?.Invoke(map);
	}
	
	public sealed class MapConstructComponentsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Hook to raise an event when map components finish constructing. Does nothing by itself, but certain "
			+ "functions require this.";
		
		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Map), nameof(Map.ConstructComponents));

		public static void Postfix(Map __instance) => __instance.Events().OnComponentsConstructed(__instance);
	}
	
	public sealed class MapExposeComponentsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Maps apparently construct all components twice when loading a save. Once in ConstructComponents and "
			+ "afterwards another time here. This is the companion patch to MapConstructCompanions, raising the "
			+ "ComponentsExposed event.";
		
		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Map), nameof(Map.ExposeComponents));

		public static void Postfix(Map __instance)
		{
			if (Scribe.mode == LoadSaveMode.LoadingVars)
				__instance.Events().OnComponentsLoaded(__instance);
		}
	}
}