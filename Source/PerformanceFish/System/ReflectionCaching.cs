// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Security;
using System.Threading.Tasks;
using PerformanceFish.Cache;
using PerformanceFish.ModCompatibility;
using ConstructorInfoCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags,
		PerformanceFish.System.ReflectionCaching.RecordArray<System.Type>,
		PerformanceFish.System.ReflectionCaching.TypePatches.ConstructorInfoCacheValue>;
using FieldGetters
	= PerformanceFish.Cache.ByReferenceUnclearable<System.RuntimeFieldHandle,
		PerformanceFish.System.ReflectionCaching.FieldInfoPatches.FieldInfoGetterCache>;
using FieldInfoCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string,
		PerformanceFish.System.ReflectionCaching.TypePatches.FieldInfoCacheValue>;
using FieldInfosCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags,
		PerformanceFish.System.ReflectionCaching.TypePatches.FieldInfosCacheValue>;
using FieldSetters
	= PerformanceFish.Cache.ByReferenceUnclearable<System.RuntimeFieldHandle,
		PerformanceFish.System.ReflectionCaching.FieldInfoPatches.FieldInfoSetterCache>;
using MethodInfoCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string,
		PerformanceFish.System.ReflectionCaching.TypePatches.MethodInfoCacheValue>;
using MethodInfoWithTypesAndFlagsCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string,
		PerformanceFish.System.ReflectionCaching.RecordArray<System.Type>,
		PerformanceFish.System.ReflectionCaching.TypePatches.MethodInfoCacheValue>;
using MethodInfoWithTypesCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, string,
		PerformanceFish.System.ReflectionCaching.RecordArray<System.Type>,
		PerformanceFish.System.ReflectionCaching.TypePatches.MethodInfoCacheValue>;
using MethodInvokers
	= PerformanceFish.Cache.ByReferenceUnclearable<System.RuntimeMethodHandle,
		PerformanceFish.System.ReflectionCaching.MethodBasePatches.MethodInvokerCache>;
using PropertyInfoCache
	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string,
		PerformanceFish.System.ReflectionCaching.TypePatches.PropertyInfoCacheValue>;
using TypeFullNameCache
	= PerformanceFish.Cache.ByReferenceUnclearable<System.RuntimeTypeHandle,
		PerformanceFish.System.ReflectionCaching.TypePatches.TypeFullNameCacheValue>;
using ActivatorCache
	= PerformanceFish.Cache.ByReferenceUnclearable<System.RuntimeTypeHandle,
		PerformanceFish.System.ReflectionCaching.ActivatorPatches.ActivatorCacheValue>;
// using ActivatorWithArgumentsCache
// 	= PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle,
// 		PerformanceFish.System.ReflectionCaching.ByValueComparableArray<System.Type>,
// 		PerformanceFish.System.ReflectionCaching.ActivatorPatches.ActivatorCacheValue>;
using CustomAttributeCache
	= PerformanceFish.Cache.ByReferenceClassic<System.Reflection.ICustomAttributeProvider, System.RuntimeTypeHandle, bool,
		PerformanceFish.System.ReflectionCaching.MonoCustomAttrs.CustomAttributeCacheValue>;

namespace PerformanceFish.System;

public sealed class ReflectionCaching : ClassWithFishPatches
{
	static ReflectionCaching() // necessary to prevent recursion between patches and function pointer creation
	{
		try
		{
			ConstructorInfoCache.Initialize();
			FieldGetters.Initialize();
			FieldInfoCache.Initialize();
			FieldInfosCache.Initialize();
			FieldSetters.Initialize();
			MethodInfoCache.Initialize();
			MethodInfoWithTypesAndFlagsCache.Initialize();
			MethodInfoWithTypesCache.Initialize();
			MethodInvokers.Initialize();
			PropertyInfoCache.Initialize();
			TypeFullNameCache.Initialize();
			ActivatorCache.Initialize();
			// ActivatorWithArgumentsCache.Initialize();
			CustomAttributeCache.Initialize();
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize its ReflectionCaching:\n{
				ex}");
		}
	}

	public record struct StateAndFlags
	{
		public bool State;
		public BindingFlags Flags;
	}

	public sealed class TypePatches
	{
		public sealed class GetField_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetField lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(RuntimeType), nameof(RuntimeType.GetField),
					[typeof(string), typeof(BindingFlags)]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, out FieldInfo? __result,
				out StateAndFlags __state)
			{
				var key = new FieldInfoCache(__instance.TypeHandle, bindingAttr,
					ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = FieldInfoCache.Get.GetOrAdd(ref key).Info;
				__state.Flags = bindingAttr;

				if (__result != null)
				{
					__state.State = false;
					return false;
				}
				else
				{
					__state.State = true;
					return true;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, FieldInfo? __result, StateAndFlags __state)
			{
				if (!__state.State || __result is null)
					return;

				UpdateCache(__instance, name, __result, __state);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, string name, FieldInfo __result, StateAndFlags __state)
				=> FieldInfoCache.GetExistingReference(__instance.TypeHandle, __state.Flags,
						ToLowerIfNeededForBindingFlags(name, __state.Flags)).Info
					= __result;
		}
		
		public sealed class GetFieldsPatch : FishPatch
		{
			public override string Description { get; } = "Caches GetFields lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(RuntimeType), nameof(RuntimeType.GetFields),
					[typeof(BindingFlags)]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, BindingFlags bindingAttr, out FieldInfo[]? __result,
				out bool __state)
			{
				var key = new FieldInfosCache(__instance.TypeHandle, bindingAttr);

				__result = FieldInfosCache.Get.GetOrAdd(ref key).Info;

				if (__result != null)
				{
					__state = false;
					return false;
				}
				else
				{
					__state = true;
					return true;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, FieldInfo[] __result, BindingFlags bindingAttr, bool __state)
			{
				if (!__state)
					return;

				UpdateCache(__instance, __result, bindingAttr);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, FieldInfo[] __result, BindingFlags bindingAttr)
				=> FieldInfosCache.GetExistingReference(__instance.TypeHandle, bindingAttr).Info = __result;
		}

		public sealed class GetMethodWithFlags_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetMethod lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetMethod),
					[typeof(string), typeof(BindingFlags)]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, out MethodInfo? __result,
				out bool __state)
			{
				var key = new MethodInfoCache(__instance.TypeHandle, bindingAttr,
					ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = MethodInfoCache.Get.GetOrAdd(ref key).Info;

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, BindingFlags bindingAttr, MethodInfo? __result,
				bool __state)
			{
				if (!__state || __result is null)
					return;

				UpdateCache(__instance, name, bindingAttr, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, string name, BindingFlags bindingAttr, MethodInfo __result)
				=> MethodInfoCache.GetExistingReference(__instance.TypeHandle, bindingAttr,
						ToLowerIfNeededForBindingFlags(name, bindingAttr)).Info
					= __result;
		}

		public sealed class GetMethodWithTypes_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetMethod lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetMethod),
					[typeof(string), typeof(Type[])]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, Type[] types, out MethodInfo? __result,
				out bool __state)
			{
				var key = new MethodInfoWithTypesCache(__instance.TypeHandle, name, new(types));

				__result = MethodInfoWithTypesCache.Get.GetOrAdd(ref key).Info;

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, Type[] types, MethodInfo? __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				UpdateCache(__instance, name, types, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, string name, Type[] types, MethodInfo __result)
				=> MethodInfoWithTypesCache.GetExistingReference(__instance.TypeHandle, name, new(types)).Info
					= __result;
		}

		public sealed class GetMethodWithFlagsAndTypes_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetMethod lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetMethod),
				[
					typeof(string), typeof(BindingFlags), typeof(Binder), typeof(Type[]), typeof(ParameterModifier[])
				]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, Binder? binder,
				Type[] types, ParameterModifier[] modifiers, ref MethodInfo? __result, out bool __state)
			{
				if (binder != null || modifiers is { Length: > 0 })
				{
					__state = false;
					return true;
				}

				var key = new MethodInfoWithTypesAndFlagsCache(__instance.TypeHandle, bindingAttr,
					ToLowerIfNeededForBindingFlags(name, bindingAttr), new(types));

				__result = MethodInfoWithTypesAndFlagsCache.Get.GetOrAdd(ref key).Info;

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, Type[] types, BindingFlags bindingAttr,
				MethodInfo? __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				UpdateCache(__instance, name, types, bindingAttr, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, string name, Type[] types, BindingFlags bindingAttr,
				MethodInfo __result)
				=> MethodInfoWithTypesAndFlagsCache.GetExistingReference(__instance.TypeHandle, bindingAttr,
					ToLowerIfNeededForBindingFlags(name, bindingAttr),
					new(types)).Info = __result;
		}

		public sealed class GetConstructor_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetConstructor lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetConstructor),
					[typeof(BindingFlags), typeof(Binder), typeof(Type[]), typeof(ParameterModifier[])]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, BindingFlags bindingAttr, Binder? binder, Type[] types,
				ParameterModifier[] modifiers, ref ConstructorInfo? __result, out bool __state)
			{
				if (binder != null || modifiers is { Length: > 0 })
				{
					__state = false;
					return true;
				}

				var key = new ConstructorInfoCache(__instance.TypeHandle, bindingAttr, new(types));

				__result = ConstructorInfoCache.Get.GetOrAdd(ref key).Info;

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, BindingFlags bindingAttr, Type[] types,
				ConstructorInfo? __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				UpdateCache(__instance, bindingAttr, types, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, BindingFlags bindingAttr, Type[] types,
				ConstructorInfo __result)
				=> ConstructorInfoCache.GetExistingReference(__instance.TypeHandle, bindingAttr, new(types)).Info
					= __result;
		}

		public sealed class GetProperty_Patch : FishPatch
		{
			public override string Description { get; } = "Caches GetProperty lookups";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(Type), nameof(Type.GetProperty),
					[typeof(string), typeof(BindingFlags)]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr,
				out PropertyInfo? __result, out bool __state)
			{
				var key = new PropertyInfoCache(__instance.TypeHandle, bindingAttr,
					ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = PropertyInfoCache.Get.GetOrAdd(ref key).Info;

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, BindingFlags bindingAttr, PropertyInfo? __result,
				bool __state)
			{
				if (!__state || __result is null)
					return;

				UpdateCache(__instance, name, bindingAttr, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(Type __instance, string name, BindingFlags bindingAttr,
				PropertyInfo __result)
				=> PropertyInfoCache.GetExistingReference(__instance.TypeHandle, bindingAttr,
						ToLowerIfNeededForBindingFlags(name, bindingAttr)).Info
					= __result;
		}

		public sealed class GetFullName : FishPatch
		{
			public override string? Description { get; }
				= "Normally averages at over 1000ns per call. Caching makes this about 30 times faster, including "
				+ "Harmony overhead. Type.Name is similarly slow too, but unfortunately can't be patched as it's an "
				+ "internal call. This has a relatively large load time impact.";
				// https://github.com/Unity-Technologies/mono/blob/unity-2019.4-mbe/mono/metadata/icall.c#L2737-L2754

			public override MethodBase TargetMethodInfo { get; }
				= AccessTools.DeclaredPropertyGetter(typeof(RuntimeType), nameof(RuntimeType.FullName));

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(RuntimeType __instance, ref string? __result, out bool __state)
			{
				ref var cache = ref TypeFullNameCache.GetOrAddReference(__instance.TypeHandle);

				if (!cache.Cached && !TryGetFromCentralCache(__instance, ref cache))
					return __state = true;

				__result = cache.Name;
				return __state = false;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static bool TryGetFromCentralCache(RuntimeType instance,
				ref TypeFullNameCacheValue cache)
			{
				lock (_lock)
				{
					ref var centralCache
						= ref TypeFullNameCache.GetDirectly.GetOrAddReference(instance.TypeHandle);
					cache.Name = centralCache.Name;
				}

				return cache.Cached;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(RuntimeType __instance, string? __result, bool __state)
			{
				if (!__state)
					return;

				UpdateCache(__instance, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(RuntimeType __instance, string? __result)
			{
				ref var cache = ref TypeFullNameCache.GetExistingReference(__instance.TypeHandle);
				cache.Name = __result;
				
				lock (_lock)
				{
					ref var centralCache
						= ref TypeFullNameCache.GetDirectly.GetReference(__instance.TypeHandle);
					centralCache.Name = __result;
				}
			}

			private static object _lock = new();
		}

		// probably never actually needed
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SuppressMessage("Globalization", "CA1308")]
		private static string ToLowerIfNeededForBindingFlags(string name, BindingFlags bindingAttr)
			=> (bindingAttr & BindingFlags.IgnoreCase) != 0 ? name.ToLowerInvariant() : name;

		public record struct FieldInfoCacheValue
		{
			public FieldInfo? Info;
		}

		public record struct FieldInfosCacheValue
		{
			public FieldInfo[]? Info;
		}

		public record struct MethodInfoCacheValue
		{
			public MethodInfo? Info;
		}

		public record struct ConstructorInfoCacheValue
		{
			public ConstructorInfo? Info;
		}

		public record struct PropertyInfoCacheValue
		{
			public PropertyInfo? Info;
		}

		public record struct TypeFullNameCacheValue()
		{
			private object? _value = typeof(void);

			public string? Name
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => Unsafe.As<string?>(_value);
				
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				set => _value = value;
			}

			public bool Cached
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => _value is not MemberInfo;
			}
		}
	}

	public sealed class MonoCustomAttrs
	{
		public sealed class GetCustomAttributes : FishPatch
		{
			public override string? Description { get; }
				= "Caches attributes for reflection lookups. Decent load time improvement.";

			public override Delegate? TargetMethodGroup { get; }
				= (Func<ICustomAttributeProvider, Type, bool, object[]>)global::System.MonoCustomAttrs
					.GetCustomAttributes;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(ICustomAttributeProvider obj, Type attributeType, bool inherit,
				out object[]? __result, out CustomAttributeState __state)
			{
				var key = new CustomAttributeCache(obj, (__state.AttributeType = attributeType).TypeHandle,
					__state.Inherit = inherit);

				ref var cache = ref CustomAttributeCache.GetOrAddReference(in key);
				if (cache.Attributes is null)
					TryGetFromCentralCache(ref key, ref cache);

				return __state.State = (__result = cache.Attributes) is null;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void TryGetFromCentralCache(ref CustomAttributeCache key,
				ref CustomAttributeCacheValue cache)
			{
				lock (_lock)
					cache.Attributes = CustomAttributeCache.GetDirectly.GetOrAdd(ref key).Attributes;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(ICustomAttributeProvider obj, object[]? __result,
				in CustomAttributeState __state)
			{
				if (!__state.State || __result is null)
					return;

				UpdateCache(obj, __state, __result);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static void UpdateCache(ICustomAttributeProvider obj, in CustomAttributeState __state,
				object[]? __result)
			{
				var key = new CustomAttributeCache(obj, __state.AttributeType.TypeHandle, __state.Inherit);
				
				CustomAttributeCache.GetExistingReference(in key).Attributes = __result;

				lock (_lock)
					CustomAttributeCache.GetDirectly.GetReference(ref key).Attributes = __result;
			}

			private static object _lock = new();
		}

		public record struct CustomAttributeCacheValue
		{
			public object[]? Attributes;
		}

		public record struct CustomAttributeState
		{
			public bool State, Inherit;
			public Type AttributeType;
		}
	}

	public record struct RecordArray<T>
		where T : class
	{
		private int _hashCode;
		public T[]? Array;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public RecordArray(T[] array)
		{
			Guard.IsNotNull(array);
			Guard.IsLessThanOrEqualTo(array.Length, byte.MaxValue);
			Array = array;
			var length = array.Length;
			_hashCode = HashCode.Combine(length, length > 0 && Array[0] is { } item ? item.GetHashCode() : 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(RecordArray<T> other)
		{
			if (Array is null)
				return other.Array is null;

			if (other.Array is null || Array.Length != other.Array.Length)
				return false;

			var length = Array.Length;
			for (var i = 0; i < length; i++)
			{
				if (!Array[i].Equals(other.Array![i]))
					return false;
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => _hashCode;
	}

	public sealed class ActivatorPatches
	{
		public sealed class CreateInstance_Type : FishPatch
		{
			public override List<string> IncompatibleModIDs { get; } = [PackageIDs.MULTIPLAYER];

			public override string? Description { get; }
				= "Optimizes the parameterless Activator.CreateInstance method by invoking through specialized cached "
				+ "delegates";

			public override Delegate? TargetMethodGroup { get; } = (Func<Type, object>)Activator.CreateInstance;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type? type, ref object __result)
			{
				if (type is null)
					return true;
				
				ref var cache = ref ActivatorCache.GetOrAddReference(type.TypeHandle);
				
				if (cache.Dirty
					&& !ActivatorCache.UpdateAsyncCache<ActivatorCacheValue, Type, Func<object>>(ref cache,
						type.TypeHandle, type))
				{
					return true;
				}

				__result = cache.Result!();
				return false;
			}
		}

		// public sealed class CreateInstance_Type_Args : FishPatch
		// {
		// 	public override Delegate? TargetMethodGroup { get; }
		// 		= (Func<Type, object[], object>)Activator.CreateInstance;
		// }

		public record struct ActivatorCacheValue : IAsyncCacheable<RuntimeTypeHandle, Type, Func<object>>
		{
			public Func<object>? Result { get; set; }
			public Task<Func<object>>? Task { get; set; }
			public bool Dirty => Result == null;

			public ValueTask<Func<object>?> MakeResultAsync(RuntimeTypeHandle key, Type type)
				=> new(Reflection.CreateConstructorDelegate<Func<object>>(type));
		}

		// public record struct ActivatorWithArgumentsCacheValue : IAsyncCacheable<ActivatorWithArgumentsCache, Type,
		// 	MethodBasePatches.MethodInvoker[]>
		// {
		// 	public MethodBasePatches.MethodInvoker[]? Result { get; set; }
		// 	public Task<MethodBasePatches.MethodInvoker[]>? Task { get; set; }
		// 	public bool Dirty => Result == null;
		//
		// 	public async ValueTask<MethodBasePatches.MethodInvoker[]?> MakeResultAsync(
		// 		ActivatorWithArgumentsCache key, Type type)
		// 	{
		// 		var constructors = type.GetConstructors();
		// 		var methodInvokerTasks = new List<Task<MethodBasePatches.MethodInvoker>>();
		//
		// 		foreach (var constructor in constructors)
		// 		{
		// 			var parameters = constructor.GetParameters();
		// 			
		// 			if (parameters.Length != key.Second.Length)
		// 				continue;
		//
		// 			var parametersAreAssignable = true;
		// 			for (var i = 0; i < parameters.Length; i++)
		// 			{
		// 				if (parameters[i].ParameterType.IsAssignableFrom(key.Second.Array[i]))
		// 					parametersAreAssignable = false;
		// 			}
		//
		// 			if (!parametersAreAssignable) // TODO: find the best constructor
		// 				continue;
		// 			
		// 			methodInvokerTasks.Add(MethodInvokers
		// 				.RequestFromCacheAsync<MethodBasePatches.MethodInvokerCache, MethodBase,
		// 					MethodBasePatches.MethodInvoker>(constructor.MethodHandle, constructor));
		// 		}
		//
		// 		var methodInvokers = new MethodBasePatches.MethodInvoker[methodInvokerTasks.Count];
		// 		for (var i = 0; i < methodInvokerTasks.Count; i++)
		// 			methodInvokers[i] = await methodInvokerTasks[i].ConfigureAwait(false);
		//
		// 		return methodInvokers;
		// 	}
		// }
	}

	// public static class Test // TODO: Toggle this when actually testing
	// {
	// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	// 	public static bool GetValue(FieldInfo __instance, object? obj, ref object? __result)
	// 	{
	// 		ref var cache = ref FieldGetters.GetOrAddReference(__instance.FieldHandle);
	//
	// 		if (cache.Dirty)
	// 			cache.Result = FieldInfoPatches.MakeGetterDelegate(__instance);
	//
	// 		__result = cache.Result!(obj);
	// 		return false;
	// 	}
	// 	
	// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	// 	public static bool SetValue(FieldInfo __instance, object? obj, object? value)
	// 	{
	// 		ref var cache = ref FieldSetters.GetOrAddReference(__instance.FieldHandle);
	//
	// 		if (cache.Dirty)
	// 			cache.Result = FieldInfoPatches.MakeSetterDelegate(__instance);
	//
	// 		cache.Result!(obj, value);
	// 		return false;
	// 	}
	// 	
	// 	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	// 	public static bool Invoke(MethodBase __instance, object? obj, object[]? parameters, ref object? __result)
	// 	{
	// 		ref var cache = ref MethodInvokers.GetOrAddReference(__instance.MethodHandle);
	//
	// 		if (cache.Dirty)
	// 			cache.Result = MethodBasePatches.MakeInvokeDelegate(__instance);
	//
	// 		__result = cache.Result!(obj, parameters);
	// 		return false;
	// 	}
	// }

	public sealed class FieldInfoPatches
	{
		public sealed class GetValue_Patch : FishPatch
		{
			public override string Description { get; }
				= "Optimizes the GetValue method by invoking it through specialized cached delegates";

			//public GetValue_Patch()
			//{
			//	var types = AllSubclassesNonAbstract(typeof(FieldInfo))
			//		.SelectMany(t
			//			=> DeclaredMethodOrNothing(t, nameof(FieldInfo.GetValue), new[] { typeof(object) }))
			//		.Select(m => m.DeclaringType);
			//	Log.Message(types.ToStringSafeEnumerable());
			//}

			public override MethodBase TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(MonoField), nameof(MonoField.GetValue))!;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(FieldInfo __instance, object? obj, ref object? __result)
			{
				ref var cache = ref FieldGetters.GetOrAddReference(__instance.FieldHandle);

				if (cache.Dirty
					&& !FieldGetters.UpdateAsyncCache<FieldInfoGetterCache, FieldInfo, FieldGetter>(ref cache,
						__instance.FieldHandle, __instance))
				{
					return true;
				}

				__result = cache.Result!(obj);
				return false;
			}
		}

		public sealed class SetValue_Patch : FishPatch
		{
			public override string Description { get; }
				= "Optimizes the SetValue method by invoking it through specialized cached delegates";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(FieldInfo), nameof(FieldInfo.SetValue),
					[typeof(object), typeof(object)]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(FieldInfo __instance, object? obj, object? value)
			{
				ref var cache = ref FieldSetters.GetOrAddReference(__instance.FieldHandle);

				if (cache.Dirty && !TryUpdate(__instance, ref cache))
					return true;

				cache.Result!(obj, value);
				return false;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static bool TryUpdate(FieldInfo __instance, ref FieldInfoSetterCache cache)
				=> cache.Task != _fieldSetterFailureTask && __instance.IsConst()
					? ErrorForInvalidCallAttempt(__instance, ref cache)
					: FieldSetters.UpdateAsyncCache<FieldInfoSetterCache, FieldInfo, FieldSetter>(ref cache,
						__instance.FieldHandle, __instance);

			[MethodImpl(MethodImplOptions.NoInlining)]
			private static bool ErrorForInvalidCallAttempt(FieldInfo info, ref FieldInfoSetterCache cache)
			{
				cache.Task = _fieldSetterFailureTask;
				Log.Error($"FieldInfo.SetValue has been called on '{
					info.FullDescription()}'. This is a const that gets copied at compile time. An attempt at "
					+ $"setting a value will always fail.\n{new StackTrace(2, true)}");
				
				return false;
			}
		}

		private static Task<FieldSetter> _fieldSetterFailureTask = new(static () => null!);

		public static FieldGetter? MakeGetterDelegate(FieldInfo info)
		{
			try
			{
				var dm = new DynamicMethod($"FieldGetter_{info.Name}", typeof(object), [typeof(object)],
					GetOwnerType(info), true);
				var il = dm.GetILGenerator();

				TryEmitFieldInstanceArgument(il, info);

				il.Emit(info.IsConst()
						? FishTranspiler.Constant(info.GetRawConstantValue())
						: FishTranspiler.Field(info));

				if (info.FieldType.IsValueType)
					il.Emit(FishTranspiler.Box(info.FieldType));

				il.Emit(FishTranspiler.Return);

				return (FieldGetter)dm.CreateDelegate(typeof(FieldGetter));
			}
			catch (Exception ex)
			{
				Log.Error($"PerformanceFish failed to generate an optimized delegate for {
					info.FullDescription()}. Reverting to default behaviour instead.\n{ex}\n{
						new StackTrace(true)}");
				return null;
			}
		}

		public static FieldSetter? MakeSetterDelegate(FieldInfo info)
		{
			try
			{
				var dm = new DynamicMethod($"FieldSetter_{info.Name}", typeof(void),
					[typeof(object), typeof(object)], GetOwnerType(info), true);
				var il = dm.GetILGenerator();

				TryEmitFieldInstanceArgument(il, info);

				il.Emit(FishTranspiler.Argument(1));

				if (info.FieldType.IsValueType)
				{
					il.Emit(FishTranspiler.Call(typeof(ReflectionCaching),
						info.FieldType.IsNullable() ? nameof(UnboxNullableSafely) : nameof(UnboxSafely),
						[typeof(object)],
						[
							info.FieldType.IsNullable() // TODO: verify that this is actually correct
								? Nullable.GetUnderlyingType(info.FieldType)
								: info.FieldType
						]));
				}
				else
				{
					il.Emit(FishTranspiler.Call(typeof(ReflectionCaching), nameof(CastOrConvert),
						generics: [info.FieldType]));
				}

				il.Emit(FishTranspiler.StoreField(info));

				il.Emit(FishTranspiler.Return);

				return (FieldSetter)dm.CreateDelegate(typeof(FieldSetter));
			}
			catch (Exception ex)
			{
				Log.Error($"PerformanceFish failed to generate an optimized delegate for {
					info.FullDescription()}. Reverting to default behaviour instead.\n{ex}\n{new StackTrace(true)}");
				return null;
			}
		}

		private static void TryEmitFieldInstanceArgument(ILGenerator il, FieldInfo info)
		{
			if (!info.IsStatic)
				EmitInstanceArgument(il, info);
		}

		public record struct FieldInfoGetterCache : IAsyncCacheable<RuntimeFieldHandle, FieldInfo, FieldGetter>
		{
			public FieldGetter? Result { get; set; }
			public Task<FieldGetter>? Task { get; set; }

			public bool Dirty => Result == null;
			
			public ValueTask<FieldGetter?> MakeResultAsync(RuntimeFieldHandle key, FieldInfo second)
				=> new(MakeGetterDelegate(second));
		}

		public record struct FieldInfoSetterCache : IAsyncCacheable<RuntimeFieldHandle, FieldInfo, FieldSetter>
		{
			public FieldSetter? Result { get; set; }
			public Task<FieldSetter>? Task { get; set; }

			public bool Dirty => Result == null;
			
			public ValueTask<FieldSetter?> MakeResultAsync(RuntimeFieldHandle key, FieldInfo second)
				=> new(MakeSetterDelegate(second));

		}

		public delegate object? FieldGetter(object? obj);

		public delegate void FieldSetter(object? obj, object? value);
	}

	public sealed class MethodBasePatches
	{
		public sealed class Invoke_Patch : FishPatch
		{
			public override bool DefaultState => false; // TODO: fix

			public override string Description { get; }
				= "Optimizes the Invoke method by compiling specialized cached delegates. Disabled by default due to "
				+ "issues with various mods, like RIMMSqol and Performance Optimizer";

			public override MethodBase? TargetMethodInfo { get; }
				= AccessTools.DeclaredMethod(typeof(MethodBase), nameof(MethodBase.Invoke),
					[typeof(object), typeof(object[])]);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(MethodBase __instance, object? obj, object[]? parameters, ref object? __result)
			{
				ref var cache = ref MethodInvokers.GetOrAddReference(__instance.MethodHandle);

				// if (cache.Dirty) // TODO: remove
				// {
				// 	if (OnStartup.State.Initialized)
				// 	{
				// 		Log.Message($"Called Invoke for {__instance.FullDescription()} with {obj.ToStringSafe()}, {
				// 			parameters?.Length.ToStringSafe() ?? "null"} parameters");
				// 	}
				//
				// 	if (!MethodInvokers.UpdateAsyncCache<MethodInvokerCache, MethodBase, MethodInvoker>(ref cache,
				// 		__instance.MethodHandle, __instance))
				// 	{
				// 		return true;
				// 	}
				// }

				if (cache.Dirty
					&& !MethodInvokers.UpdateAsyncCache<MethodInvokerCache, MethodBase, MethodInvoker>(ref cache,
						__instance.MethodHandle, __instance))
				{
					return true;
				}

				__result = cache.Result!(obj, parameters);
				return false;
			}
		}

		// Similar to HarmonyLib.MethodInvoker, but correctly handles a couple of edge cases Harmony would throw on.
		[SuppressMessage("Globalization", "CA1310")]
		public static MethodInvoker? MakeInvokeDelegate(MethodBase methodBase)
		{
			if (methodBase.DeclaringType is null && methodBase.Name.StartsWith("PadMethod"))
				return null; // padding methods generated by harmony for linux
			
			try
			{
				var methodInfo = methodBase as MethodInfo;
				if (methodInfo is { ReturnType.IsByRef: true })
					return null; // how to box a ref return?
				
				var constructorInfo = methodBase as ConstructorInfo;

				if (methodInfo is null && constructorInfo is null)
					return null;
				
				var dm = new DynamicMethod($"MethodInvoker_{methodBase.Name}", typeof(object),
					[typeof(object), typeof(object[])],
					GetOwnerType(methodBase), true);
				var il = dm.GetILGenerator();

				var parameters = methodBase.GetParameters();

				if (methodInfo != null && !methodBase.IsStatic)
					EmitInstanceArgument(il, methodBase);

				for (var i = 0; i < parameters.Length; i++)
				{
					il.Emit(FishTranspiler.Argument(1));
					il.Emit(FishTranspiler.Constant(i));

					var parameterType = parameters[i].ParameterType;

					if (parameterType.IsByRef)
						EmitInvokeByRefParameterLoad(il, parameterType);
					else
						EmitInvokeParameterLoad(il, parameterType);
				}

				if (methodInfo != null)
					EmitInvokeMethodCall(methodBase, il, methodInfo);
				else if (constructorInfo != null)
					il.Emit(FishTranspiler.New(constructorInfo));

				il.Emit(FishTranspiler.Return);

				return (MethodInvoker)dm.CreateDelegate(typeof(MethodInvoker));
			}
			catch (Exception ex)
			{
				Log.Warning($"PerformanceFish failed to generate an optimized delegate for {
					Reflection.FullDescription(methodBase)}. Reverting to default behaviour instead.\n{ex}\n{
						new StackTrace(true)}");
				return null;
			}
		}

		private static void EmitInvokeMethodCall(MethodBase methodBase, ILGenerator il, MethodInfo methodInfo)
		{
			il.Emit(FishTranspiler.Call(methodBase));

			if (!methodInfo.ReturnType.IsValueType)
				return;

			il.Emit(methodInfo.ReturnType == typeof(void)
				? FishTranspiler.Null
				: FishTranspiler.Box(methodInfo.ReturnType));
		}

		private static void EmitInvokeParameterLoad(ILGenerator il, Type parameterType)
		{
			il.Emit(FishTranspiler.LoadElement<object>());

			il.Emit(parameterType.IsValueType
				? FishTranspiler.Call(typeof(ReflectionCaching),
					parameterType.IsNullable() ? nameof(UnboxNullableSafely) : nameof(UnboxSafely),
					[typeof(object)],
					[
						parameterType.IsNullable()
							? Nullable.GetUnderlyingType(parameterType)
							: parameterType
					])
				: FishTranspiler.Call(typeof(ReflectionCaching), nameof(CastOrConvert),
					generics: [parameterType]));
		}

		private static void EmitInvokeByRefParameterLoad(ILGenerator il, Type parameterType)
		{
			parameterType = parameterType.GetElementType()!;

			il.Emit(FishTranspiler.LoadElementAddress<object>());

			if (parameterType.IsNullable())
			{
				il.Emit(FishTranspiler.Call(typeof(ReflectionCaching), nameof(VerifyNullableBoxObject),
					generics: [Nullable.GetUnderlyingType(parameterType)]));
				
				il.Emit(FishTranspiler.UnboxAddress(parameterType));
			}
			else
			{
				il.Emit(parameterType.IsValueType
					? FishTranspiler.Call(typeof(ReflectionCaching), nameof(UnboxRefSafely),
						generics:
						[
							/*parameterType.IsNullable()
							? Nullable.GetUnderlyingType(parameterType)
							:*/ parameterType
						])
					: FishTranspiler.Call(typeof(ReflectionCaching), nameof(CastRefSafely), generics: [parameterType]));
			}
		}

		public record struct MethodInvokerCache : IAsyncCacheable<RuntimeMethodHandle, MethodBase, MethodInvoker>
		{
			public MethodInvoker? Result { get; set; }
			public Task<MethodInvoker>? Task { get; set; }
			public bool Dirty => Result == null;
			
			public ValueTask<MethodInvoker?> MakeResultAsync(RuntimeMethodHandle key, MethodBase method)
				=> new(MakeInvokeDelegate(method));
		}

		public delegate object? MethodInvoker(object? obj, object[]? parameters);
	}

	private static void EmitInstanceArgument(ILGenerator il, MemberInfo info)
	{
		var declaringType = info.DeclaringType!;

		if (declaringType.IsValueType)
		{
			il.Emit(FishTranspiler.ArgumentAddress(0));
			il.Emit(FishTranspiler.Call(typeof(ReflectionCaching), nameof(UnboxRefSafely), generics: [declaringType]));
		}
		else
		{
			il.Emit(FishTranspiler.This);
			il.Emit(FishTranspiler.Call(typeof(ReflectionCaching), nameof(CastOrConvert), generics: [declaringType]));
		}
	}

	private static Type GetOwnerType(MemberInfo info)
		=> (info.DeclaringType ?? info.ReflectedType ?? typeof(void)) is var type
			// ReSharper disable once MergeIntoPattern
			&& type.IsInterface ? typeof(InterfaceCache)
			: type.IsArray ? typeof(ArrayCache)
			: type;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SecuritySafeCritical]
	public static ref TTo CastRefSafely<TTo>(ref object? from)
		=> ref from is TTo or null
			? ref Unsafe.As<object?, TTo>(ref from)
			: ref ThrowInvalidCastException<TTo>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SecuritySafeCritical]
	public static ref TTo UnboxRefSafely<TTo>(ref object? from) where TTo : struct
		=> ref from is TTo
			? ref Unsafe.Unbox<TTo>(from)
			: ref Unsafe.Unbox<TTo>(from
				= from is null
					? default
					: FisheryLib.Convert.Type<object, TTo>(from));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static object? VerifyNullableBoxObject<TTo>(ref object? from) where TTo : struct
		=> from is TTo
			? from
			: from = from is null
				? default
				: FisheryLib.Convert.Type<object, TTo?>(from);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	[SecuritySafeCritical]
	public static TTo? CastOrConvert<TTo>(object? from)
		=> from is TTo or null
			? Unsafe.As<object?, TTo?>(ref from)
			: FisheryLib.Convert.Type<object, TTo>(from);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TTo UnboxSafely<TTo>(object? from) where TTo : struct
		=> from switch
		{
			TTo to => to,
			null => default,
			_ => FisheryLib.Convert.Type<object, TTo>(from)
		};

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TTo? UnboxNullableSafely<TTo>(object? from) where TTo : struct
		=> from switch
		{
			TTo to => to,
			null => null,
			_ => FisheryLib.Convert.Type<object, TTo>(from)
		};

	[DoesNotReturn]
	private static ref T ThrowInvalidCastException<T>() => throw new InvalidCastException();

	public static class InterfaceCache
	{
		// populated with dynamic methods by MethodBasePatches.MakeInvokeDelegate, MakeGetterDelegate and
		// MakeSetterDelegate
	}

	public static class ArrayCache
	{
		// populated with dynamic methods by MethodBasePatches.MakeInvokeDelegate, MakeGetterDelegate and
		// MakeSetterDelegate
	}
}