// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.InteropServices;

namespace PerformanceFish.Prepatching;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct FishStash
{
	private const string ANCHOR_STRING_VALUE = "F1shSt4sh69";
	
	[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
	private string? _anchorString;
	public UnhandledMessageQueue? UnhandledMessages;
	public GCHandle MessageQueueGCHandle;
	
	public static ref FishStash Get
		=> ref StaticHandle != default
			? ref FromHandle(StaticHandle)
			: ref GetFishStashFromInternalHandle();

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ref FishStash GetFishStashFromInternalHandle()
	{
		StaticHandle = InternalFishStashHandle?.Handle
			?? ThrowHelper.ThrowInvalidOperationException<nint>("Failed to fetch internal FishStashHandle.");

		ref var stash = ref InternalFishStashHandle.Stash;
		if (stash._anchorString != ANCHOR_STRING_VALUE)
			ThrowHelper.ThrowInvalidDataException($"FishStash started with invalid anchor: {stash._anchorString}");

		return ref stash;
	}

	internal static FishStashHandle? InternalFishStashHandle { get; }
		= typeof(Verse.Log).GetField(nameof(FishStashHandle), BindingFlags.NonPublic | BindingFlags.Static)
			?.GetValue(null) as FishStashHandle;
	
	internal static nint StaticHandle { get; private set; }
	
	internal static unsafe ref FishStash FromHandle(IntPtr handle) => ref Unsafe.AsRef<FishStash>(handle.ToPointer());
	
	private static unsafe void Create()
	{
		if (StaticHandle != default)
			ThrowHelper.ThrowInvalidOperationException("Tried creating duplicate FishStash.");
		
		var handle = Marshal.AllocHGlobal(sizeof(FishStash));
		Unsafe.InitBlockUnaligned(handle.ToPointer(), 0, (uint)sizeof(FishStash));
		((FishStash*)handle)->_anchorString = ANCHOR_STRING_VALUE;
		
		StaticHandle = handle;
	}

	static FishStash()
	{
		if (!Log.Ready && InternalFishStashHandle is null)
			Create();
	}
}