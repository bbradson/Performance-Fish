// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using JetBrains.Annotations;
#if V1_5
using LudeonTK;
#endif
using UnityEngine.Rendering;

namespace PerformanceFish.Utility;

public static class DebugActions
{
	[UsedImplicitly]
	[DebugAction("Misc", "Pipette tool", actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	public static void PipetteTool()
	{
		var things = UI.MouseCell().GetThingList(Find.CurrentMap).ToList();
		things.Sort(static (x, y) => ((int)x.def.altitudeLayer).CompareTo((int)y.def.altitudeLayer));
		
		for (var i = things.Count; i-- > 0; )
		{
			var thing = things[i];
			var material = thing.Graphic?.MatAt(thing.Rotation, thing);
			if (material == null)
				continue;

			var texture = material.mainTexture;
			if (texture == null)
				continue;

			var destinationTexture = RenderTexture.GetTemporary(texture.width, texture.height);
			Graphics.Blit(texture, destinationTexture);
			
			AsyncGPUReadback.Request(destinationTexture, callback: result
				=>
			{
				try
				{
					using var pixelDataNative = result.GetData<Color32>();
					
					var pixelDataFiltered = pixelDataNative
						.Where(static color => (color.r > 0 || color.g > 0 || color.b > 0) && color.a > 0)
						.ToArray();

					var averageColor = pixelDataFiltered.Length < 1
						? (Color32)Color.black
						: GetAverage(pixelDataFiltered);

					Log.Message($"Texture of thing '{thing}' has average color of {averageColor} {
						"(■)".Colorize(averageColor)}.");

					Find.LetterStack.AddLetterSilently(LetterMaker.MakeLetter("Pipette result",
						$"Texture of thing '{thing}' has average color of {averageColor} {
							"(■)".Colorize(averageColor)}",
						LetterDefOf.NeutralEvent), averageColor);
				}
				catch (Exception e)
				{
					Log.Error($"Exception in AsyncGPUReadback:\n{e}");
				}
				finally
				{
					RenderTexture.ReleaseTemporary(destinationTexture);
				}
			});
			break;
		}
	}

	private static Color32 GetAverage(Color32[] colors)
		=> new(GetAverage(colors, static color => color.r),
			GetAverage(colors, static color => color.g),
			GetAverage(colors, static color => color.b),
			GetAverage(colors, static color => color.a));

	private static byte GetAverage(IEnumerable<Color32> colors, Func<Color32, int> selector)
		=> (byte)Mathf.RoundToInt((float)colors.Average(selector));

#if testing
	[UsedImplicitly]
	[DebugAction("Misc", "Test ListerThings", actionType = DebugActionType.ToolMap,
		allowedGameStates = AllowedGameStates.PlayingOnMap)]
	public static void TestListerThings() => Find.CurrentMap.listerThings.Remove(ThingMaker.MakeThing(ThingDefOf.Beer));
#endif
}