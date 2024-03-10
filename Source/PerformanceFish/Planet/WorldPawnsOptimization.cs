// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;
using RimWorld.Planet;

namespace PerformanceFish.Planet;

public sealed class WorldPawnsOptimization : ClassWithFishPrepatches
{
	public sealed class AllPawnsAlivePatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Minor optimization to reduce the amount of copying happening in the WorldPawns.AllPawnsAlive method";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.PropertyGetter(typeof(WorldPawns), nameof(WorldPawns.AllPawnsAlive));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static List<Pawn> ReplacementBody(WorldPawns instance)
		{
			ref var cache = ref instance.Cache();

			if (CacheDirty(ref cache, instance))
			{
				instance.allPawnsAliveResult.Clear();
				instance.allPawnsAliveResult.AddRange(instance.pawnsAlive);
				instance.allPawnsAliveResult.AddRange(instance.pawnsMothballed);
				
				UpdateCache(ref cache, instance);
			}

			return instance.allPawnsAliveResult;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void UpdateCache(ref Cache cache, WorldPawns instance)
		{
			cache.PawnsAliveVersion = instance.pawnsAlive._version;
			cache.PawnsMothballedVersion = instance.pawnsMothballed._version;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CacheDirty(ref Cache cache, WorldPawns instance)
			=> instance.pawnsAlive._version != cache.PawnsAliveVersion
				|| instance.pawnsMothballed._version != cache.PawnsMothballedVersion;
	}
	
	public sealed class AllPawnsAliveOrDeadPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Minor optimization to reduce the amount of copying happening in the WorldPawns.AllPawnsAliveOrDead "
			+ "method";
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.PropertyGetter(typeof(WorldPawns), nameof(WorldPawns.AllPawnsAliveOrDead));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static List<Pawn> ReplacementBody(WorldPawns instance)
		{
			ref var cache = ref instance.Cache();

			var allPawnsAlive = instance.AllPawnsAlive;
			var allPawnsDead = instance.AllPawnsDead;
			
			if (CacheDirty(ref cache, allPawnsAlive, allPawnsDead))
			{
				instance.allPawnsAliveOrDeadResult.Clear();
				instance.allPawnsAliveOrDeadResult.AddRange(allPawnsAlive);
				instance.allPawnsAliveOrDeadResult.AddRange(allPawnsDead);
				
				UpdateCache(ref cache, allPawnsAlive, allPawnsDead);
			}

			return instance.allPawnsAliveOrDeadResult;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void UpdateCache(ref Cache cache, List<Pawn> allPawnsAlive, HashSet<Pawn> allPawnsDead)
		{
			cache.AllPawnsAliveVersion = allPawnsAlive._version;
			cache.AllPawnsDeadVersion = allPawnsDead._version;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool CacheDirty(ref Cache cache, List<Pawn> allPawnsAlive, HashSet<Pawn> allPawnsDead)
			=> allPawnsAlive._version != cache.AllPawnsAliveVersion
				|| allPawnsDead._version != cache.AllPawnsDeadVersion;
	}

	public record struct Cache()
	{
		public int
			PawnsAliveVersion = -2,
			PawnsMothballedVersion = -2,
			AllPawnsAliveVersion = -2,
			AllPawnsDeadVersion = -2;
	}
}