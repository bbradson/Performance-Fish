// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using System.Runtime.InteropServices;

namespace PerformanceFish.Prepatching;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public unsafe struct FishStash : IEquatable<FishStash>
{
	private const string ANCHOR_STRING_VALUE = "F1shSt4sh6912345";
	private const int ANCHOR_STRING_LENGTH = 16;
	
	private fixed char _anchorString[ANCHOR_STRING_LENGTH];
	
	internal GCHandle MessageQueueGCHandle;

	private int _activePrepatchCount;
	private IntPtr _activePrepatches;

	public UnhandledMessageQueue? UnhandledMessages
		=> MessageQueueGCHandle != default && MessageQueueGCHandle.IsAllocated
			? Unsafe.As<UnhandledMessageQueue>(MessageQueueGCHandle.Target)
			: null;
	
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
		fixed (char* anchor = stash._anchorString)
		{
			var anchorString = new string(anchor, 0, ANCHOR_STRING_LENGTH);
			if (anchorString != ANCHOR_STRING_VALUE)
				ThrowHelper.ThrowInvalidDataException($"FishStash started with invalid anchor: {anchorString}");
		}

		return ref stash;
	}

	internal static FishStashHandle? InternalFishStashHandle { get; }
		= typeof(Verse.Log).GetField(nameof(FishStashHandle), BindingFlags.NonPublic | BindingFlags.Static)
			?.GetValue(null) as FishStashHandle;
	
	internal static nint StaticHandle { get; private set; }
	
	internal static ref FishStash FromHandle(IntPtr handle) => ref Unsafe.AsRef<FishStash>(handle.ToPointer());
	
	private static void Create()
	{
		Guard.IsEqualTo(ANCHOR_STRING_VALUE.Length, ANCHOR_STRING_LENGTH);
		
		if (StaticHandle != default)
			ThrowHelper.ThrowInvalidOperationException("Tried creating duplicate FishStash.");

		var handle = (FishStash*)Marshal.AllocHGlobal(sizeof(FishStash));
		Unsafe.InitBlockUnaligned(handle, 0, (uint)sizeof(FishStash));
		
		for (var i = 0; i < ANCHOR_STRING_VALUE.Length; i++)
			handle->_anchorString[i] = ANCHOR_STRING_VALUE[i];

		StaticHandle = (nint)handle;
	}

	internal void InitializeActivePrepatchIDs()
	{
		var activePrepatches = PerformanceFishMod.AllPrepatchClasses!
			.SelectMany(static patchClass => patchClass.Patches
				.Where(static patch => patch.Enabled)
				.Select(static patch => patch.IDNumber))
			.ToArray();

		var activePrepatchCount = activePrepatches.Length;
		_activePrepatchCount = activePrepatchCount;
		_activePrepatches = Marshal.AllocHGlobal(sizeof(int) * activePrepatchCount);
		Marshal.Copy(activePrepatches, 0, _activePrepatches, activePrepatchCount);
	}

	internal void Release() => Marshal.FreeHGlobal(_activePrepatches);

	internal bool IsPatchActive(FishPrepatchBase patch)
	{
		if (_activePrepatchCount <= 0)
			return false;

		var patchID = patch.IDNumber;

		var activePrepatches = (int*)_activePrepatches;
		for (var i = _activePrepatchCount; i-- > 0;)
		{
			if (activePrepatches[i] == patchID)
				return true;
		}

		return false;
	}

	static FishStash()
	{
		if (!Log.Ready && InternalFishStashHandle is null)
			Create();
	}

	public override bool Equals(object? obj) => obj is FishStash stash && Equals(stash);

	public override int GetHashCode()
		=> HashCode.Combine(MessageQueueGCHandle.GetHashCode(), _activePrepatchCount, _activePrepatches.GetHashCode());

	public static bool operator ==(FishStash left, FishStash right) => left.Equals(right);

	public static bool operator !=(FishStash left, FishStash right) => !(left == right);

	public bool Equals(FishStash other)
	{
		fixed (char* anchor = _anchorString)
		{
			if (anchor != other._anchorString)
				return false;
		}
		
		return MessageQueueGCHandle == other.MessageQueueGCHandle
			&& _activePrepatchCount == other._activePrepatchCount
			&& _activePrepatches == other._activePrepatches;
	}
}