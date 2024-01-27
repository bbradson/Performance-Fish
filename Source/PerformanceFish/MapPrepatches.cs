// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class MapPrepatches : ClassWithFishPrepatches
{
	public sealed class FinalizeLoadingPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "An attempt at logging and fixing possible causes for the common FMOD error spam. None of "
			+ "Performance Fish's other patches interact with this in any way.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.DeclaredMethod(typeof(Map), nameof(Map.FinalizeLoading));

		public static void Postfix(Map __instance)
		{
			var listerThings = __instance.listerThings;
			
			var musicalInstruments = listerThings.ThingsInGroup(ThingRequestGroup.MusicalInstrument);
			for (var i = musicalInstruments.Count; i-- > 0;)
			{
				var thing = musicalInstruments[i];
				if (thing is Building_MusicalInstrument)
					continue;
				
				Log.Error($"Map '{__instance}' contains thing that is not of type 'Building_MusicalInstrument' "
					+ $"in ThingsInGroup for ThingRequestGroup.MusicalInstrument: '{
						thing}' at cell '{thing.Position}'. Removing now to prevent further issues.");
				
				if (!RefundOrRemoveIfUnassignable(__instance, thing))
					listerThings.RemoveFromGroup(thing, ThingRequestGroup.MusicalInstrument);
			}

			var musicSources = listerThings.ThingsInGroup(ThingRequestGroup.MusicSource);
			for (var i = musicSources.Count; i-- > 0;)
			{
				var thing = musicSources[i];
				if (thing.TryGetComp<CompPlaysMusic>() != null)
					continue;
				
				Log.Error($"Map '{__instance}' contains thing without comp of type 'CompPlaysMusic' in "
					+ $"ThingsInGroup for ThingRequestGroup.MusicSource: '{
						thing}' at cell '{thing.Position}'. Removing now to prevent further issues.");

				if (!RefundOrRemoveIfUnassignable(__instance, thing))
					listerThings.RemoveFromGroup(thing, ThingRequestGroup.MusicSource);
			}

			var allThings = listerThings.AllThings;
			for (var i = allThings.Count; i-- > 0;)
			{
				var thing = allThings[i];
				var thingDef = thing.def;

				if (thingDef is null)
				{
					Log.Error($"Map '{__instance}' contains thing '{thing}' with null def at cell '{
						thing.Position}'. This will likely cause issues. Removing now.");
					
					listerThings.Remove(thing);
					// continue;
				}
				// else if (thing.GetType().IsAssignableTo(thingDef.thingClass))
				// {
				// 	Log.Error($"Map '{__instance}' contains thing '{thing}' of incorrect type '{
				// 		thing.GetType()}' at cell '{thing.Position}'. This should be '{
				// 			thingDef.thingClass}' instead and will likely cause issues. Destroying and refunding now.");
				// }
				// else
				// {
				// 	continue;
				// }
				//
				// if (!TryRefundOrRemove(__instance, thing))
				// 	listerThings.Remove(thing);
			}
		}

		private static bool RefundOrRemoveIfUnassignable(Map map, Thing thing)
		{
			if (thing.def != null && thing.GetType().IsAssignableTo(thing.def.thingClass))
				return false;

			return TryRefundOrRemove(map, thing);
		}

		private static bool TryRefundOrRemove(Map map, Thing thing)
		{
			var previousAllowDestroy = Thing.allowDestroyNonDestroyable;
			Thing.allowDestroyNonDestroyable = true;

			try
			{
				GenSpawn.Refund(thing, map, CellRect.Empty);
			}
			catch (Exception refundException)
			{
				Log.Error($"Failed to refund thing '{thing}'. Destroying.\n{refundException}");
				try
				{
					thing.Destroy();
				}
				catch (Exception destroyException)
				{
					Log.Error($"Failed to destroy thing '{thing}'.\n{destroyException}");
					return false;
				}
			}
			finally
			{
				Thing.allowDestroyNonDestroyable = previousAllowDestroy;
			}

			return true;
		}
	}
}