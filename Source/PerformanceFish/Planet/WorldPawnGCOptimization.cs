// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.ModCompatibility;
using PerformanceFish.Prepatching;
using RimWorld.Planet;

namespace PerformanceFish.Planet;

public sealed class WorldPawnGCOptimization : ClassWithFishPrepatches
{
	public sealed class AddRelationshipsPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "The world pawn GC is responsible for deleting no longer relevant pawns. AddAllRelationships here tries "
			+ "to find every pawn another has any kind of relation to, no matter how distant and through how many "
			+ "other relatives, to block these pawns from getting deleted. This normally runs for every pawn the game "
			+ "considers critical, which includes colonists, faction leaders, spawned pawns and many other less "
			+ "important pawns, like someone who happened to have come up in someone's battle log, in a tale for art, "
			+ "in a quest etc. This patch prevents this relationship calculation for many of the less important pawns.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldPawnGC), nameof(WorldPawnGC.AddAllRelationships));

		public static bool Prefix(WorldPawnGC __instance, Pawn pawn, Dictionary<Pawn, string> keptPawns)
			=> AcceptedCriticalPawnReasons.Contains(keptPawns[pawn]);
	}

	public sealed class WorldPawnGCTickPatch : FishPrepatch
	{
		public override List<string> IncompatibleModIDs { get; } = [PackageIDs.MULTIPLAYER];

		public override string? Description { get; }
			= "Normally the world pawn GC processes 1 pawn per tick, and speeds up if it fails to complete its "
			+ "processing before the game had to add or remove world pawns through other means. This patch changes the "
			+ "baseline to one pawn every 4 ticks to make the slowdown less noticeable.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldPawnGC), nameof(WorldPawnGC.WorldPawnGCTick));

		public static bool Prefix() => TickHelper.MatchesModulo(4);
	}

	public sealed class CancelGCPassPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "When the game determines that it has to speed up its GC process because of world pawn additions or "
			+ "removals it normally aborts the process and entirely starts over. This patch makes it not do that, and "
			+ "instead just lets it continue at the faster rate.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldPawnGC), nameof(WorldPawnGC.CancelGCPass));

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static void ReplacementBody(WorldPawnGC instance)
		{
			if (instance.activeGCProcess == null)
				return;

			// instance.activeGCProcess = null;
			instance.currentGCRate = Mathf.Min(instance.currentGCRate * 2, 16777216);
			if (DebugViewSettings.logWorldPawnGC)
				Log.Message("World pawn GC cancelled");
		}
	}

	public sealed class GetCriticalPawnReasonPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Critical pawns are pawns the game's world pawn GC does not delete. Relationships other than parents of "
			+ "colonists are normally not included here, and instead get calculated within the AddRelationships "
			+ "method. The AddRelationships patch Performance Fish includes prevents many of the relationship "
			+ "calculations in that method however, so this here adds every direct relation to player-visible pawns "
			+ "as critical pawn reason, preventing these relatives from getting deleted.";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Method(typeof(WorldPawnGC), nameof(WorldPawnGC.GetCriticalPawnReason));

		public static void Postfix(ref string? __result, Pawn pawn)
		{
			if (__result != null || pawn.relations is not { } relations)
				return;

			foreach (var pawnWithDirectRelation in relations.pawnsWithDirectRelationsWithMe)
			{
				if (pawnWithDirectRelation is (not { RaceProps.Humanlike: true }) or
				{
					Faction: not { IsPlayer: true }, Spawned: false,
					HostFaction: not { IsPlayer: true }
				})
				{
					continue;
				}

				__result = "Relationship";
				return;
			}
		}
	}

	public static HashSet<string> AcceptedCriticalPawnReasons
		= [..new[] { "Colonist", "Generating", "Kidnapped", "CaravanMember", "TransportPod", "Spawned" }];
}