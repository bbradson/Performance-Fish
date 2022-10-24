// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Reflection.Emit;

namespace PerformanceFish;

public class GenLocalDateCaching : ClassWithFishPatches
{
	public class DayTickByThing_Patch : FishPatch
	{
		public override string? Description => "Caches results of GenLocalDate.DayTick for the first map. This is similar to Rim73's mind state optimization, but yields accurate results instead of a placeholder value to avoid issues.";
		public override Delegate? TargetMethodGroup => (Func<Thing, int>)GenLocalDate.DayTick;
		public override int TranspilerMethodPriority => Priority.First;

		public static CodeInstructions Transpiler(CodeInstructions codes, MethodBase method, ILGenerator generator)
		//=> Reflection.GetCodeInstructions(Replacement);
		{
			var labelToStartOfMethod = generator.DefineLabel();
			codes.First().labels.Add(labelToStartOfMethod);

			yield return FishTranspiler.Argument(method, "thing");
			yield return FishTranspiler.Field(typeof(Thing), nameof(Thing.mapIndexOrState));
			yield return FishTranspiler.Constant(0);
			yield return FishTranspiler.IfLessThan(labelToStartOfMethod);

			yield return FishTranspiler.Field(typeof(Current), nameof(Current.gameInt));
			yield return FishTranspiler.Field(typeof(Game), nameof(Game.maps));
			yield return FishTranspiler.Argument(method, "thing");
			yield return FishTranspiler.Field(typeof(Thing), nameof(Thing.mapIndexOrState));
			yield return FishTranspiler.Call(typeof(List<Map>), "get_Item");
			yield return FishTranspiler.Call<Func<Map, int>>(GenLocalDate.DayTick);
			yield return FishTranspiler.Return;

			foreach (var code in codes)
				yield return code;

		}

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public static int Replacement(Thing thing)
		//	=> thing.mapIndexOrState < 0 ? GenDate.DayTick(GenTicks.TicksAbs, GenLocalDate.LongitudeForDate(thing))
		//	: GenLocalDate.DayTick(Current.gameInt.maps[thing.mapIndexOrState]);

		//[MethodImpl(MethodImplOptions.AggressiveInlining)]
		//public static bool Prefix(Thing thing, ref int __result)
		//{
		//	if (thing.mapIndexOrState < 0)
		//		return true;

		//	__result = GenLocalDate.DayTick(Current.gameInt.maps[thing.mapIndexOrState]);
		//	return false;
		//}
	}

	public class DayTickByMap_Patch : FishPatch
	{
		public override string? Description => "Caches results of GenLocalDate.DayTick for the first map. This is similar to Rim73's mind state optimization, but yields accurate results instead of a placeholder value to avoid issues.";
		public override Delegate? TargetMethodGroup => (Func<Map, int>)GenLocalDate.DayTick;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Prefix(Map map, ref int __result, out bool __state)
		{
			if (map != SavedMap)
			{
				if (SavedMap is null && Find.Maps?.Count > 0 && Find.Maps[0] == map)
					SavedMap = map;

				return __state = true;
			}

			if (SavedTick != GenTicks.TicksGame)
				return __state = true;

			__result = DayTick;
			return __state = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix(Map map, int __result, bool __state)
		{
			if (!__state || SavedMap != map)
				return;

			SavedTick = GenTicks.TicksGame;
			DayTick = __result;
		}
	}

	public static Map? SavedMap;
	public static int SavedTick;
	public static int DayTick;
}