// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if notWorking

using System.Linq;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace PerformanceFish;

[UsedImplicitly]
public sealed class AllocationProfiling : ClassWithFishPatches
{
	public override bool RequiresLoadedGameForPatching => false;

	public static bool Active
	{
		get => _active;
		set
		{
			return;
			_active = value;
			FishPatch.Get<ObjectPatch>().Enabled = value;
		}
	}

	public static FishTable<Type, TypeData> Data = new();
	private static bool _active;

	/// <summary>
	/// The idea here was to patch object..ctor, fetch the type, count calls and get the allocation count for every
	/// type by doing so, while ignoring structs which are expected to not construct as full object with type.
	/// This however doesn't work. The IL method bodies of class constructors do contain calls to object..ctor, but
	/// they appear to only go through for static constructor invocations and literal object construction. Everything
	/// else somehow skips the call.
	/// </summary>
	public sealed class ObjectPatch : FishPatch
	{
		public override bool DefaultState => true;

		public override MethodBase? TargetMethodInfo { get; }
			= MainTabWindow._objectConstructor;
			// = AccessTools.Constructor(typeof(NotAClass));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void P0stf1x(object __instance)
		{
			CallCount++;
			
			ref var typeData = ref Data.GetOrAddReference(__instance.GetType());
			typeData.Count++;
			
			// if ((typeData.Count & 1023) != 0)
			// 	return;
			
			typeData.StackTraces.GetOrAddReference(StackTraceUtility.ExtractStackTrace())++;
		}

		public static CodeInstructions Transpiler(CodeInstructions instructions)
		{
			var codes = instructions.AsOrToList();
			Log.Message(string.Join("\n", codes));
			var retIndex = codes.FindIndex(static code => code.opcode == OpCodes.Ret);
			if (retIndex == -1)
				retIndex = codes.Count - 1;
			
			codes.Insert(retIndex, new(OpCodes.Ldarg_0));
			codes.Insert(retIndex + 1, new(OpCodes.Call, methodof(P0stf1x)));

			return codes;
		}

		// ReSharper disable once VirtualMemberCallInConstructor
		//private ObjectPatch() => TryPatch();

		public static class NotAClass
		{
			
		}
	}

	public static ulong CallCount;

	public record struct TypeData
	{
		public int Count;
		public FishTable<string, int> StackTraces = new();

		[UsedImplicitly]
		public TypeData()
		{
		}
	}

	[UsedImplicitly]
	public sealed class MainTabWindow : RimWorld.MainTabWindow
	{
		public MainTabWindow()
		{
			draggable = true;
			resizeable = true;
			absorbInputAroundWindow = false;
			closeOnClickedOutside = false;
		}
		
		private GUIScope.ScrollViewStatus _scrollViewStatus = new();
		private Listing_Standard _listing = new();

		public override void PreOpen()
		{
			Active = true;
			base.PreOpen();
		}

		public override void PostClose()
		{
			base.PostClose();
			Active = false;
		}

		internal static MethodBase _objectConstructor
			= (MethodBase)MethodBodyReader.GetInstructions(null, AccessTools.DeclaredConstructor(typeof(PreceptComp)))
					.First(static code => code.opcode == OpCodes.Call
						&& code.operand is MethodBase method
						&& method.Name.Contains("ctor"))
				.operand;
			// = typeof(object).GetMembers(AccessTools.all).OfType<MethodBase>()
			// .First(static member => member.Name.Contains("ctor"));
			// = AccessTools.Constructor(typeof(ObjectPatch.NotAClass));

		public override void DoWindowContents(Rect inRect)
		{
			absorbInputAroundWindow = false;
			closeOnClickedOutside = false;
			
			if (GUIHelper.CanSkip)
				return;
			
			Widgets.Label(inRect with { height = Text.LineHeight }, $"Data: {Data}, count: {
				Data.Count.ToStringCached()}, callCount: {CallCount.ToString()}");
			inRect.yMin += Text.LineHeight;

			var objectPatches = Harmony.GetPatchInfo(_objectConstructor);

			var patchWidgetLabel = $"Patches on {_objectConstructor.FullDescription()}: pre {
				objectPatches?.Prefixes?.Select(static patch => patch.PatchMethod.FullDescription()).ToStringSafeEnumerable()}, post: {
					objectPatches?.Postfixes?.Select(static patch => patch.PatchMethod.FullDescription()).ToStringSafeEnumerable()}, trans: {
						objectPatches?.Transpilers?.Select(static patch => patch.PatchMethod.FullDescription()).ToStringSafeEnumerable()}, final: {
							objectPatches?.Finalizers?.Select(static patch => patch.PatchMethod.FullDescription()).ToStringSafeEnumerable()}, Active? {
								Active.ToString()}, Enabled? {FishPatch.Get<ObjectPatch>().Enabled.ToString()}";

			var patchWidgetHeight = Text.CalcHeight(patchWidgetLabel, inRect.width);
			Widgets.Label(inRect with { height = patchWidgetHeight }, patchWidgetLabel);
			inRect.yMin += patchWidgetHeight;

			var objectMembers = typeof(object).GetMembers(AccessTools.all).OfType<MethodBase>()
				.Where(static member => member.Name.Contains("ctor"));
			var objectMembersLabel = $"object members:\n{string.Join("\n  ---  ", objectMembers.Select(static member
				=> member.FullDescription()))}";
			var objectMembersHeight = Text.CalcHeight(objectMembersLabel, inRect.width);
			Widgets.Label(inRect with { height = objectMembersHeight }, objectMembersLabel);
			inRect.yMin += objectMembersHeight;
			
			using var scrollView = new GUIScope.ScrollableListingStandard(inRect, _scrollViewStatus, _listing);
			var listing = _listing;

			using var keyValuePairsPooled = new PooledArray<KeyValuePair<Type, TypeData>>(Data);
			keyValuePairsPooled.Sort(_typeDataComparer);
			
			var keyValuePairs = keyValuePairsPooled.Array;
			var typeCount = keyValuePairsPooled.Length;

			for (var i = 0; i < typeCount; i++)
			{
				if (scrollView.TryCull(Text.LineHeight))
					continue;

				var kvp = keyValuePairs[i];
				var labelRect = listing.GetRect(Text.LineHeight);
				
				Widgets.Label(labelRect,
					string.Concat(kvp.Key, " ", kvp.Value.Count.ToStringCached()));
				
				if (Mouse.IsOver(labelRect))
				{
					var traces = kvp.Value.StackTraces;
					using var traceArray = new PooledArray<KeyValuePair<string, int>>(traces);
					traceArray.Sort(_traceComparer);
					// listing.Label(traceArray.Array[0].Key);
					listing.Label(traceArray.FirstOrDefault(static kvp => !kvp.Key.Contains("cctor")).Key ?? "NULL");
					// TooltipHandler.TipRegion(labelRect,
					// 	string.Join("\n", traceArray.Select(static kvp => kvp.Key)));
				}
			}
		}

		private static IComparer<KeyValuePair<Type, TypeData>> _typeDataComparer
			= Comparer<KeyValuePair<Type, TypeData>>.Create(static (x, y) => x.Value.Count.CompareTo(y.Value.Count));

		private static IComparer<KeyValuePair<string, int>> _traceComparer
			= Comparer<KeyValuePair<string, int>>.Create(static (x, y) => x.Value.CompareTo(y.Value));
	}
}
#endif