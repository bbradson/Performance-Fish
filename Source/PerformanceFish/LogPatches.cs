// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using PerformanceFish.Prepatching;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace PerformanceFish;

public sealed class LogPatches : ClassWithFishPrepatches
{
	public const string PERFORMANCE_FISH_WELCOME_MESSAGE = "Performance Fish!!!";
	public const string PERFORMANCE_FISH_WELCOME_STACKTRACE = "UwU";
	
	// This breaks my FishStash somehow // TODO: FIX
	// public sealed class LogVersionNumberPatch : FishPrepatch
	// {
	// 	public override MethodBase MethodBase { get; } = methodof(Root.CheckGlobalInit);
	//
	// 	public static void Prefix() => Log.Message("Performance Fish???");
	// }
	
	public sealed class ClassPatch : FishClassPrepatch
	{
		public override string? Description { get; }
			= "Important logging patch";

		public override bool Enabled => true;

		public override bool ShowSettings => false;

		public override Type Type => typeof(Verse.Log);

		public override void FreePatch(TypeDefinition typeDefinition)
		{
			var module = typeDefinition.Module;

			var fishStashHandleField = new FieldDefinition(nameof(FishStashHandle),
				FieldAttributes.Private | FieldAttributes.Static,
				typeDefinition.Module.ImportReference(typeof(FishStashHandle)));
			
			typeDefinition.Fields.Add(fishStashHandleField);
			
			var staticConstructorBody = typeDefinition.GetStaticConstructor().Body;
			
			staticConstructorBody.GetILProcessor().InsertRange(staticConstructorBody.Instructions.Count - 2,
				(OpCodes.Ldc_I8, FishStash.StaticHandle),
				(OpCodes.Newobj,
					module.ImportReference(typeof(FishStashHandle).GetConstructor([typeof(long)]))),
				(OpCodes.Stsfld, fishStashHandleField),
				(OpCodes.Call, module.ImportReference(InsertEarlyMessages)));
		}

		public static void InsertEarlyMessages()
		{
			var unhandledMessages = Log.UnhandledMessages;
			if (unhandledMessages is null)
				Debug.LogError("Message queue is null");
			
			Log.Ready = true;
			
			Verse.Log.messageQueue.Enqueue(new(LogMessageType.Message, PERFORMANCE_FISH_WELCOME_MESSAGE,
				PERFORMANCE_FISH_WELCOME_STACKTRACE));

			while (unhandledMessages!.TryDequeue(out var message))
			{
				try
				{
					if (Marshal.PtrToStringUni(message.Text) == PERFORMANCE_FISH_WELCOME_MESSAGE)
						continue;

					Verse.Log.messageQueue.Enqueue(message.ToLogMessage());
				}
				finally
				{
					message.Dispose();
				}
			}
		}
	}
}