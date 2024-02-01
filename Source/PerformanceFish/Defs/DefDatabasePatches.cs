// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Defs;

public sealed class DefDatabasePatches : ClassWithFishPrepatches
{
	public sealed class GetNamedPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes the method with fewer instructions and faster dictionary lookup function";

		public override MethodBase TargetMethodBase { get; } = methodof(DefDatabase<Def>.GetNamed);
	
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(Replacement<Def>.GetNamed);
	}
	
	public sealed class GetByShortHashPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Optimizes the method with fewer instructions and faster dictionary lookup function";
		
		public override MethodBase TargetMethodBase { get; } = methodof(DefDatabase<Def>.GetByShortHash);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(Replacement<Def>.GetByShortHash);
	}
	
	public static class Replacement<T> where T : Def
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? GetNamed(string defName, bool errorOnFail = true)
		{
			var value = DefDatabase<T>.defsByName.TryGetValue(defName);
	
			if (errorOnFail && value is null)
				LogMissingDef(defName);
	
			return value;
		}
		
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void LogMissingDef(string defName)
			=> Log.Error($"Failed to find {typeof(T)} named '{defName}'. DefDatabase<{
				typeof(T)}> contains a total of {DefDatabase<T>.defsList.Count} loaded defs.");
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? GetByShortHash(ushort shortHash)
			=> DefDatabase<T>.defsByShortHash.TryGetValue(shortHash);
	}
}