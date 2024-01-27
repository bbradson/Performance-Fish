// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

// crashes to desktop somehow
/*using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish.Defs;

public sealed class DefDatabasePatches : ClassWithFishPrepatches
{
	public sealed class GetNamedPatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; } = methodof(DefDatabase<Def>.GetNamed);
	
		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(GetNamed<Def>);
	
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? GetNamed<T>(string defName, bool errorOnFail = true) where T : Def
		{
			var value = DefDatabase<T>.defsByName.TryGetValue(defName);
	
			if (errorOnFail && value is null)
				LogMissingDef<T>(defName);
	
			return value;
		}
	
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void LogMissingDef<T>(string defName) where T : Def
			=> Log.Error($"Failed to find {typeof(T)} named {defName}. There are {
				DefDatabase<T>.defsList.Count} defs of this type loaded.");
	}
	
	public sealed class GetByShortHashPatch : FishPrepatch
	{
		public override MethodBase TargetMethodBase { get; } = methodof(DefDatabase<Def>.GetByShortHash);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(GetByShortHash<Def>);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? GetByShortHash<T>(ushort shortHash) where T : Def
			=> DefDatabase<T>.defsByShortHash.TryGetValue(shortHash);
	}
}*/