// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using FisheryLib.FunctionPointers;
using JetBrains.Annotations;
using PerformanceFish.Events;

namespace PerformanceFish.Cache;

public static class Utility
{
	public static event Action? Cleared;

	private static volatile int _destroyedThingCounter, _lastGCTick, _gcRate = 1000;
	private static int _currentGCProgress;
	private static volatile bool _gcInProgress;

	private static ICollection[]? _cachesToProcessForGC;

	private static object _gcLock = new();
	
	public static ConcurrentBag<ICollection> All { get; } = [];

	public static void Clear()
	{
		foreach (var cache in All)
			cache.Clear();
		
		Cleared?.Invoke();
	}

	internal static void Initialize() => ThingEvents.Destroyed += TryContinueGC;

	public static void TryContinueGC(Thing thing)
	{
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
			if (collection[i].Count > 0)
				count++;
		}

		return count;
	}

	public static void LogCurrentCacheUtilization()
	{
		var allCaches = All.Where(static c => c.Count > 0).ToArray();
		var allCacheTypes = allCaches.Select(static cache => cache.GetType()).Distinct();
		var cacheData = allCacheTypes.Select(type
			=>
		{
			var cachesOfType = allCaches.Where(cache => cache.GetType() == type).ToList();
			return (type, cachesOfType.Select(static cache => cache.Count).Sum(),
				TryCalculateSize(type, cachesOfType));
		}).ToArray();
		
		Array.Sort(cacheData, static (x, y)
			=> y.Item3 is { } size ? size.CompareTo(x.Item3 ?? 0L)
				: y.Item2 != x.Item2 ? y.Item2.CompareTo(x.Item2)
			: string.Compare(x.Item1.ToString(), y.Item1.ToString(), StringComparison.Ordinal));

		Log.Message($"Total utilized caches: {allCaches.Length} ({
			ToByteString(cacheData.Select(static tuple => tuple.Item3 ?? 0L).Sum())})\n\n"
			+ $"Individual counts:\n{string.Join("\n", cacheData.Select(static tuple
				=> $"{tuple.Item1.FullDescription()} :: {tuple.Item2.ToStringCached()}{
					(tuple.Item3 is { } size ? $" ({ToByteString(size)})"
						: "")}"))}\n=============================================================");
	}

	private static long? TryCalculateSize(Type type, IEnumerable<ICollection> cachesOfType)
	{
		var sizeGetter = type.GetProperty(nameof(FishTable<object, object>.SizeEstimate))?.GetMethod
			?? (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
				? AccessTools.Method(typeof(FisheryLib.CollectionExtensions),
					nameof(FisheryLib.CollectionExtensions.GetSizeEstimate), generics: type.GetGenericArguments())
				: null);

		return sizeGetter is null
			? null
			: cachesOfType.Select(cache
				=> (long)(uint)sizeGetter.Invoke(sizeGetter.IsStatic ? null : cache,
					sizeGetter.IsStatic ? [cache] : null)).Sum();
	}

	private static string ToByteString(long sizeInBytes)
		=> sizeInBytes > 1024L * 1024L
			? $"{Math.Round(sizeInBytes / (1024d * 1024d), 2).ToString(CultureInfo.CurrentCulture)} MiB"
			: sizeInBytes > 1024L
				? $"{Math.Round(sizeInBytes / 1024d, 2).ToString(CultureInfo.CurrentCulture)} KiB"
				: $"{((int)sizeInBytes).ToStringCached()} B";

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
	public static FishTable<TKey, TValue> AddNew<TKey, TValue>(Func<TKey, TValue>? valueInitializer = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryAdded = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryRemoved = null)
	{
		var newCollection = new FishTable<TKey, TValue>();

		if (valueInitializer != null)
			newCollection.ValueInitializer = valueInitializer;
		
		if (onEntryAdded != null)
			newCollection.EntryAdded += onEntryAdded;

		if (typeof(TKey).IsAssignableTo(typeof(Thing)))
		{
			newCollection.EntryAdded
				+= entry =>
				{
					var data = new OnDestroyActionData<TKey, TValue>(entry.Key, newCollection);
					data.Action = data.Invoke;
					data.Thing.Events().Destroyed += data.Action;
				};
		}

		if (onEntryRemoved != null)
			newCollection.EntryRemoved += onEntryRemoved;
		
		All.Add(newCollection);
		return newCollection;
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static FishTable<TKey, TValue> AddNew<TKey, TValue>(List<IDictionary> cacheList,
		Func<TKey, TValue>? valueInitializer = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryAdded = null,
		Action<KeyValuePair<TKey, TValue>>? onEntryRemoved = null)
	{
		var newCache = AddNew(valueInitializer, onEntryAdded, onEntryRemoved);
		
		lock (cacheList)
			cacheList.Add(newCache);
		
		return newCache;
	}

	private sealed record OnDestroyActionData<TKey, TValue>(TKey Key, FishTable<TKey, TValue> Collection)
	{
		public Action<Thing>? Action;
		public Thing Thing => Unsafe.As<Thing>(Key);

		public void Invoke(Thing thing)
		{
			Collection.Remove(Key);
			Thing.Events().Destroyed -= Action;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	public static FishTable<TKey, TValue> AddNew<TKey, TValue>(Func<FishTable<TKey, TValue>> collectionInitializer)
	{
		var newCollection = collectionInitializer();
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

	public static class Internal
	{
		public static void RemoveIn<T>(List<IDictionary> caches, T key)
		{
			lock (caches)
			{
				for (var i = caches.Count; i-- > 0;)
					caches[i].Remove(key!);
			}
		}
		
		public static void ClearIn(List<IDictionary> caches)
		{
			lock (caches)
			{
				for (var i = caches.Count; i-- > 0;)
					caches[i].Clear();
			}
		}

		public static unsafe void Initialize<T>()
		{
			try
			{
				_ = Equals<T>.Default;
				_ = EqualsByRef<T>.Default;
				_ = GetHashCode<T>.Default;
				_ = GetHashCodeByRef<T>.Default;
				_ = FisheryLib.Collections.Internal.KeyUtility<T>.Default;
			}
			catch (Exception ex)
			{
				string name;

				try
				{
					name = typeof(T).FullDescription();
				}
				catch
				{
					try
					{
						name = typeof(T).FullName ?? typeof(T).Name;
					}
					catch
					{
						try
						{
							name = typeof(T).Name;
						}
						catch (Exception inner)
						{
							name = inner.ToString();
						}
					}
				}
				
				Log.Error($"Performance Fish encountered an exception while trying to initialize '{
					name}'\n==================================================\n{ex}");
			}
		}
	}
}

public delegate int IndexGetter<in T>(T item);

public interface IClearable
{
	public void Clear();
}

[PublicAPI]
public sealed class CollectionRef<T>(AccessTools.FieldRef<T> fieldRef) : IClearable, ICollection
	where T : ICollection
{
	private static readonly ClearInvoker? _clearInvoker = CreateClearDelegate();

	public ref T Get => ref fieldRef();

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
			[typeof(T).MakeByRefType()], typeof(CollectionRef<T>), true);
		var il = dm.GetILGenerator();
		
		il.Emit(FishTranspiler.Argument(0));
		il.Emit(FishTranspiler.Call(clearMethod));
		il.Emit(FishTranspiler.Return);

		return (ClearInvoker)dm.CreateDelegate(typeof(ClearInvoker));
	}

	private delegate void ClearInvoker(ref T collectionRef);
}