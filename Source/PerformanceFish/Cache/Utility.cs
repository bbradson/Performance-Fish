// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace PerformanceFish.Cache;

public static class Utility
{
	public static event Action<Thing>? ThingDestroyed; // TODO

	public static event Action? Cleared;

	private static volatile int _destroyedThingCounter, _lastGCTick, _gcRate = 1000;
	private static int _currentGCProgress;
	private static volatile bool _gcInProgress;

	private static ICollection[]? _cachesToProcessForGC;

	private static object _gcLock = new();

	public static void NotifyThingDestroyed(Thing thing)
	{
		ThingDestroyed?.Invoke(thing);

		if (Interlocked.Increment(ref _destroyedThingCounter) < 100
			|| Current.programStateInt != ProgramState.Playing
			|| TickHelper.TicksGame - _lastGCTick < _gcRate)
		{
			return;
		}

		ContinueGC();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ContinueGC()
	{
		lock (_gcLock)
		{
			if (_gcInProgress)
				return;
			
			_gcInProgress = true;
			
			if (_cachesToProcessForGC is null || _currentGCProgress >= _cachesToProcessForGC.Length)
			{
				_cachesToProcessForGC = All.ToArray();
				_gcRate = GenDate.TicksPerTwelfth / _cachesToProcessForGC.CountNonEmpty();
				_currentGCProgress = 0;
				_destroyedThingCounter = 0;
			}

			while (_cachesToProcessForGC[_currentGCProgress].Count == 0
				&& _cachesToProcessForGC.Length - _currentGCProgress > 1)
			{
				_currentGCProgress++;
			}

			if (_currentGCProgress < _cachesToProcessForGC.Length)
				_cachesToProcessForGC[_currentGCProgress++].Clear();
			
			_lastGCTick = TickHelper.TicksGame;

			_gcInProgress = false;
		}
	}

	private static int CountNonEmpty(this ICollection[] collection)
	{
		var count = 0;
		for (var i = collection.Length; i-- > 0;)
		{
			if (collection.Length > 0)
				count++;
		}

		return count;
	}

	public static ConcurrentBag<ICollection> All { get; } = new();

	public static void Clear()
	{
		foreach (var cache in All)
			cache.Clear();
		
		Cleared?.Invoke();
	}

	public static void LogCurrentCacheUtilization()
	{
		var allCaches = All.ToArray();
		Log.Message($"Total utilized caches: {allCaches.CountNonEmpty()}\n\n"
			+ $"Individual counts:\n{string.Join("\n",
				allCaches.Select(static cache => cache.GetType()).Distinct().Select(type
					=> $"{type.FullDescription()} :: {allCaches.Where(cache => cache.GetType() == type)
						.Select(static cache => cache.Count).Sum()}{TryCalculateSize(
						type, allCaches)}"))}\n=============================================================");
	}

	private static string TryCalculateSize(Type type, ICollection[] allCaches)
		=> (type.GetProperty(nameof(FishTable<object, object>.SizeEstimate))?.GetMethod
			?? (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
				? AccessTools.Method(typeof(FisheryLib.CollectionExtensions),
					nameof(FisheryLib.CollectionExtensions.GetSizeEstimate), generics: type.GetGenericArguments())
				: null)) is { } sizeGetter
			? $" ({(allCaches.Where(cache => cache.GetType() == type)
						.Select(cache => (long)(uint)sizeGetter.Invoke(sizeGetter.IsStatic ? null : cache,
							sizeGetter.IsStatic ? new object[] { cache } : null)).Sum()
					is var sizeInBytes
				&& sizeInBytes > 1024L * 1024L ? $"{Math.Round(sizeInBytes / (1024d * 1024d), 2)} MiB"
				: sizeInBytes > 1024L ? $"{Math.Round(sizeInBytes / 1024d, 2)} KiB"
				: $"{sizeInBytes} B")})"
			: "";

	private static void Clear(this ICollection cache)
	{
		switch (cache)
		{
			case IClearable clearable:
				clearable.Clear();
				break;
			case IDictionary dictionary:
				dictionary.Clear();
				break;
			case IList list:
				list.Clear();
				break;
			default:
				ref var clearMethod = ref ClearMethods.GetOrAddReference(cache.GetType().TypeHandle.Value);
				if (clearMethod.Action is null)
					AssignClearMethod(cache, out clearMethod.Action);
				clearMethod.Action(cache);
				break;
		}
	}

	private static void AssignClearMethod(ICollection cache, out Action<ICollection> clearMethod)
		=> clearMethod = Unsafe.As<Action<ICollection>>(Array.Find(cache.GetType().GetMethods(),
				static m => m.Name == nameof(IList.Clear) && m.GetParameters().Length == 0)
			.CreateDelegate(typeof(Action<>).MakeGenericType(cache.GetType())));

	private static FishTable<IntPtr, ClearAction> ClearMethods => _clearMethods ??= new();

	[ThreadStatic]
	private static FishTable<IntPtr, ClearAction>? _clearMethods;

	private record struct ClearAction
	{
		public Action<ICollection>? Action;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static T AddNew<T>() where T : class, ICollection, new()
	{
		var newCollection = Reflection.New<T>();
		
		All.Add(newCollection);
		return newCollection;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static FishTable<TKey, TValue> AddNew<TKey, TValue>(Action<KeyValuePair<TKey, TValue>>? onEntryAdded = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryRemoved = null)
	{
		var newCollection = new FishTable<TKey, TValue>();
		
		if (onEntryAdded != null)
			newCollection.EntryAdded += onEntryAdded;

		if (onEntryRemoved != null)
			newCollection.EntryRemoved += onEntryRemoved;
		
		All.Add(newCollection);
		return newCollection;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static T AddNewUnclearable<T>() where T : class, ICollection, new()
	{
		var newCollection = Reflection.New<T>();
		return newCollection;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static FishTable<TKey, TValue> AddNewUnclearable<TKey, TValue>(
		Action<KeyValuePair<TKey, TValue>>? onEntryAdded = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryRemoved = null)
	{
		var newCollection = new FishTable<TKey, TValue>();
		
		if (onEntryAdded != null)
			newCollection.EntryAdded += onEntryAdded;

		if (onEntryRemoved != null)
			newCollection.EntryRemoved += onEntryRemoved;
		
		return newCollection;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static T AddNewWithRef<T>(AccessTools.FieldRef<T?> fieldRef) where T : ICollection, new()
	{
		var collectionRef = Reflection.New<CollectionRef<T>, AccessTools.FieldRef<T?>>(fieldRef);
		All.Add(collectionRef);
		return Reflection.New<T>();
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static T AddNew<T>(IEqualityComparer comparer) where T : ICollection, new()
	{
		var newCollection = (T)Activator.CreateInstance(typeof(T), comparer);	// different comparer subclasses can
		All.Add(newCollection);													// require different constructors.
		return newCollection;													// Reflection.New<T> can't use arguments
	}																			// for resolving method calls (yet)
}

public delegate int IndexGetter<in T>(T item);

public interface IClearable
{
	public void Clear();
}

public interface IFishBool
{
	public bool Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get;
	}
}

public struct FishBool : IFishBool
{
	private byte _value;

	public bool Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _value == 1;
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => _value = value ? (byte)1 : (byte)0;
	}
	
	public struct True : IFishBool
	{
		public bool Value => true;
	}

	public struct False : IFishBool
	{
		public bool Value => false;
	}
}

[PublicAPI]
public class CollectionRef<T> : IClearable, ICollection where T : ICollection
{
	private readonly AccessTools.FieldRef<T> _fieldRef;
	private static readonly ClearInvoker? _clearInvoker = CreateClearDelegate();

	public CollectionRef(AccessTools.FieldRef<T> fieldRef) => _fieldRef = fieldRef;

	public ref T Get => ref _fieldRef();

	public void Create() => Get = Reflection.New<T>();

	public void Clear()
	{
		if (_clearInvoker is null)
			ThrowForMissingClearMethod();

		_clearInvoker(ref Get);
	}

	[DoesNotReturn]
	private static void ThrowForMissingClearMethod()
		=> throw new InvalidOperationException(
		$"Tried to call Clear() on invalid collection of type {typeof(T).FullName}");

	public IEnumerator GetEnumerator() => Get.GetEnumerator();

	public void CopyTo(Array array, int index) => Get.CopyTo(array, index);

	public int Count => Get.Count;

	public object SyncRoot => Get.SyncRoot;

	public bool IsSynchronized => Get.IsSynchronized;

	private static ClearInvoker? CreateClearDelegate()
	{
		var clearMethod = AccessTools.Method(typeof(T), nameof(IList.Clear), Type.EmptyTypes);
		if (clearMethod is null)
			return null;
		
		var dm = new DynamicMethod($"ClearInvoker_{typeof(T).FullName}", null,
			new[] { typeof(T).MakeByRefType() }, typeof(CollectionRef<T>), true);
		var il = dm.GetILGenerator();
		
		il.Emit(FishTranspiler.Argument(0));
		il.Emit(FishTranspiler.Call(clearMethod));
		il.Emit(FishTranspiler.Return);

		return (ClearInvoker)dm.CreateDelegate(typeof(ClearInvoker));
	}

	private delegate void ClearInvoker(ref T collectionRef);
}