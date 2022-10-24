// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;

using ConstructorInfoCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, PerformanceFish.System.ReflectionCaching.ByValueComparableArray<System.Type>, System.Reflection.ConstructorInfo>;
using FieldGetters = PerformanceFish.Cache.ByReference<System.RuntimeFieldHandle, PerformanceFish.System.ReflectionCaching.FieldInfoPatches.FieldInfoGetterCache>;
using FieldInfoCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string, System.Reflection.FieldInfo>;
using FieldSetters = PerformanceFish.Cache.ByReference<System.RuntimeFieldHandle, PerformanceFish.System.ReflectionCaching.FieldInfoPatches.FieldInfoSetterCache>;
using MethodInfoCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string, System.Reflection.MethodInfo>;
using MethodInfoWithTypesAndFlagsCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string, PerformanceFish.System.ReflectionCaching.ByValueComparableArray<System.Type>, System.Reflection.MethodInfo>;
using MethodInfoWithTypesCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, string, PerformanceFish.System.ReflectionCaching.ByValueComparableArray<System.Type>, System.Reflection.MethodInfo>;
using MethodInvokers = PerformanceFish.Cache.ByReference<System.RuntimeMethodHandle, PerformanceFish.System.ReflectionCaching.MethodBasePatches.MethodInvokerCache>;
using PropertyInfoCache = PerformanceFish.Cache.ByReference<System.RuntimeTypeHandle, System.Reflection.BindingFlags, string, System.Reflection.PropertyInfo>;

namespace PerformanceFish.System;
public class ReflectionCaching : ClassWithFishPatches
{
	public static void Initialize() // necessary to prevent recursion between patches and function pointer creation
	{
		try
		{
			ConstructorInfoCache.Initialize();
			FieldGetters.Initialize();
			FieldInfoCache.Initialize();
			FieldSetters.Initialize();
			MethodInfoCache.Initialize();
			MethodInfoWithTypesAndFlagsCache.Initialize();
			MethodInfoWithTypesCache.Initialize();
			MethodInvokers.Initialize();
			PropertyInfoCache.Initialize();
		}
		catch (Exception ex)
		{
			Log.Error($"Performance Fish encountered an exception while trying to initialize its ReflectionCaching:\n{ex}");
		}
	}

	public struct StateAndFlags
	{
		public bool State;
		public BindingFlags Flags;
	}

	public class TypePatches
	{
		public class GetField_Patch : FishPatch
		{
			public override string Description => "Caches GetField lookups";
			public override MethodBase TargetMethodInfo
				=> Reflection.MethodInfo("mscorlib", "System.RuntimeType", nameof(Type.GetField), new[] { typeof(string), typeof(BindingFlags) });

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, ref FieldInfo? __result, out StateAndFlags __state)
			{
				var key = new FieldInfoCache(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = FieldInfoCache.Get.TryGetValue(ref key);

				if (__result is null)
				{
					__state = new() { State = true, Flags = bindingAttr };
					return true;
				}
				else
				{
					__state = new() { State = false, Flags = bindingAttr };
					return false;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, FieldInfo __result, StateAndFlags __state)
			{
				if (!__state.State || __result is null)
					return;

				FieldInfoCache.Get[new(__instance.TypeHandle, __state.Flags, ToLowerIfNeededForBindingFlags(name, __state.Flags))] = __result;
			}
		}

		public class GetMethodWithFlags_Patch : FishPatch
		{
			public override string Description => "Caches GetMethod lookups";
			public override Expression<Action> TargetMethod => () => default(Type)!.GetMethod(null, default(BindingFlags));

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, ref MethodInfo? __result, out bool __state)
			{
				var key = new MethodInfoCache(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = MethodInfoCache.Get.TryGetValue(ref key);

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, BindingFlags bindingAttr, MethodInfo __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				MethodInfoCache.Get[new(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr))] = __result;
			}
		}

		public class GetMethodWithTypes_Patch : FishPatch
		{
			public override string Description => "Caches GetMethod lookups";
			public override Expression<Action> TargetMethod => () => default(Type)!.GetMethod(null, null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, Type[] types, ref MethodInfo? __result, out bool __state)
			{
				var key = new MethodInfoWithTypesCache(__instance.TypeHandle, name, new(types));

				__result = MethodInfoWithTypesCache.Get.TryGetValue(ref key);

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, Type[] types, MethodInfo __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				MethodInfoWithTypesCache.Get[new(__instance.TypeHandle, name, new(types))] = __result;
			}
		}

		public class GetMethodWithFlagsAndTypes_Patch : FishPatch
		{
			public override string Description => "Caches GetMethod lookups";
			public override Expression<Action> TargetMethod => () => default(Type)!.GetMethod(null, default, null, null, null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers, ref MethodInfo? __result, out bool __state)
			{
				if (binder != null || modifiers is { Length: > 0 })
				{
					__state = false;
					return true;
				}

				var key = new MethodInfoWithTypesAndFlagsCache(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr), new(types));

				__result = MethodInfoWithTypesAndFlagsCache.Get.TryGetValue(ref key);

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, Type[] types, BindingFlags bindingAttr, MethodInfo __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				MethodInfoWithTypesAndFlagsCache.Get[new(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr), new(types))] = __result;
			}
		}

		public class GetConstructor_Patch : FishPatch
		{
			public override string Description => "Caches GetConstructor lookups";
			public override Expression<Action> TargetMethod => () => default(Type)!.GetConstructor(default, null, null, null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, BindingFlags bindingAttr, Binder binder, Type[] types, ParameterModifier[] modifiers, ref ConstructorInfo? __result, out bool __state)
			{
				if (binder != null || modifiers is { Length: > 0 })
				{
					__state = false;
					return true;
				}

				var key = new ConstructorInfoCache(__instance.TypeHandle, bindingAttr, new(types));

				__result = ConstructorInfoCache.Get.TryGetValue(ref key);

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, BindingFlags bindingAttr, Type[] types, ConstructorInfo __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				ConstructorInfoCache.Get[new(__instance.TypeHandle, bindingAttr, new(types))] = __result;
			}
		}

		public class GetProperty_Patch : FishPatch
		{
			public override string Description => "Caches GetProperty lookups";
			public override Expression<Action> TargetMethod => () => default(Type)!.GetProperty(null, default(BindingFlags));

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(Type __instance, string name, BindingFlags bindingAttr, ref PropertyInfo? __result, out bool __state)
			{
				var key = new PropertyInfoCache(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr));

				__result = PropertyInfoCache.Get.TryGetValue(ref key);

				return __state = __result is null;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void Postfix(Type __instance, string name, BindingFlags bindingAttr, PropertyInfo __result, bool __state)
			{
				if (!__state || __result is null)
					return;

				PropertyInfoCache.Get[new(__instance.TypeHandle, bindingAttr, ToLowerIfNeededForBindingFlags(name, bindingAttr))] = __result;
			}
		}

		// probably never actually needed
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static string ToLowerIfNeededForBindingFlags(string name, BindingFlags bindingAttr) => (bindingAttr & BindingFlags.IgnoreCase) != 0 ? name.ToLowerInvariant() : name;
	}

	public struct ByValueComparableArray<T> : IEquatable<ByValueComparableArray<T>>
		where T : class
	{
		public T[] Array;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ByValueComparableArray(T[] array)
		{
			Guard.IsNotNull(array);
			Array = array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ByValueComparableArray<T> other)
		{
			var length = Array.Length;
			if (length != other.Array.Length)
				return false;

			for (var i = 0; i < length; i++)
			{
				if (!Array[i].Equals(other.Array[i]))
					return false;
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj) => obj is ByValueComparableArray<T> array && Equals(array);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => HashCode.Combine(Array.Length, Array.Length > 0 && Array[0] is { } item ? item.GetHashCode() : 0);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(ByValueComparableArray<T> left, ByValueComparableArray<T> right) => left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(ByValueComparableArray<T> left, ByValueComparableArray<T> right) => !(left == right);
	}

#if eee == false
	public class FieldInfoPatches
	{
		public class GetValue_Patch : FishPatch
		{
			public override string Description => "Optimizes the GetValue method by invoking it through specialized cached delegates";

			//public GetValue_Patch()
			//{
			//	var types = AllSubclassesNonAbstract(typeof(FieldInfo))
			//		.SelectMany(t
			//			=> DeclaredMethodOrNothing(t, nameof(FieldInfo.GetValue), new[] { typeof(object) }))
			//		.Select(m => m.DeclaringType);
			//	Log.Message(types.ToStringSafeEnumerable());
			//}

			public override MethodBase TargetMethodInfo => Reflection.MethodInfo("mscorlib", "System.Reflection.MonoField", nameof(FieldInfo.GetValue));

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(FieldInfo __instance, object obj, ref object __result)
			{
				ref var cache = ref FieldGetters.Get.TryGetReferenceUnsafe(__instance.FieldHandle);

				if (!Unsafe.IsNullRef(ref cache))
				{
					if (cache.func != null || UpdateGetterCache(ref cache, __instance))
					{
						__result = cache.func!(obj);
						return false;
					}
					return true;
				}
				else
				{
					return AddCache(__instance);
				}
			}

			private static bool AddCache(FieldInfo __instance)
			{
				FieldGetters.Get.Add(__instance.FieldHandle, new());
				return true;
			}
		}

		public class SetValue_Patch : FishPatch
		{
			public override string Description => "Optimizes the SetValue method by invoking it through specialized cached delegates";

			public override Expression<Action> TargetMethod => () => default(FieldInfo)!.SetValue(null, null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(FieldInfo __instance, object obj, object value)
			{
				ref var cache = ref FieldSetters.Get.TryGetReferenceUnsafe(__instance.FieldHandle);

				if (!Unsafe.IsNullRef(ref cache))
				{
					if (cache.action != null || UpdateSetterCache(ref cache, __instance))
					{
						cache.action!(obj, value);
						return false;
					}
					return true;
				}
				else
				{
					return AddCache(__instance);
				}
			}

			private static bool AddCache(FieldInfo __instance)
			{
				FieldSetters.Get.Add(__instance.FieldHandle, new());
				return true;
			}
		}

		public static bool UpdateGetterCache(ref FieldInfoGetterCache cache, FieldInfo instance)
		{
			cache.callCount++;

			if (cache.callCount == 300)
				cache.func = MakeGetterDelegate(instance);
			else if (cache.callCount > 300)
				return false;

			FieldGetters.Get[instance.FieldHandle] = cache;

			return cache.func != null;
		}

		public static bool UpdateSetterCache(ref FieldInfoSetterCache cache, FieldInfo instance)
		{
			cache.callCount++;

			if (cache.callCount == 300)
				cache.action = MakeSetterDelegate(instance);
			else if (cache.callCount > 300)
				return false;

			FieldSetters.Get[instance.FieldHandle] = cache;

			return cache.action != null;
		}

		private static FieldGetter? MakeGetterDelegate(FieldInfo info)
		{
			try
			{
				var dm = new DynamicMethod($"FieldGetter_{info.Name}", typeof(object), new[] { typeof(object) }, info.DeclaringType, true);
				var il = dm.GetILGenerator();

				if (!info.IsStatic)
				{
					il.Emit(FishTranspiler.This);

					il.Emit(info.DeclaringType.IsValueType
						? FishTranspiler.Unbox(info.DeclaringType)
						: FishTranspiler.Cast(info.DeclaringType));
				}

				il.Emit(
					/*info.DeclaringType.IsEnum
					&& info.DeclaringType.GetEnumUnderlyingType() is var enumType
					? enumType == typeof(long) || enumType == typeof(ulong)
						? FishTranspiler.Constant((long)info.GetValue(info.DeclaringType))
						: FishTranspiler.Constant((int)info.GetValue(info.DeclaringType))
					:*/ info.IsLiteral && !info.IsInitOnly ? FishTranspiler.Constant(info.GetRawConstantValue())
					: FishTranspiler.Field(info));

				if (info.FieldType.IsValueType)
					il.Emit(FishTranspiler.Box(info.FieldType));

				il.Emit(FishTranspiler.Return);

				return (FieldGetter)dm.CreateDelegate(typeof(FieldGetter));
			}
			catch (Exception ex)
			{
				Log.Error($"PerformanceFish failed to generate an optimized delegate for {GetFieldDescription(info)}. Reverting to default behaviour instead.\n{ex}");
				return null;
			}
		}

		private static FieldSetter? MakeSetterDelegate(FieldInfo info)
		{
			try
			{
				var dm = new DynamicMethod($"FieldSetter_{info.Name}", typeof(void), new[] { typeof(object), typeof(object) }, info.DeclaringType, true);
				var il = dm.GetILGenerator();

				if (!info.IsStatic)
				{
					il.Emit(FishTranspiler.This);

					il.Emit(info.DeclaringType.IsValueType
						? FishTranspiler.Unbox(info.DeclaringType)
						: FishTranspiler.Cast(info.DeclaringType));
				}

				il.Emit(FishTranspiler.Argument(1));

				if (info.FieldType.IsValueType)
				{
					//il.Emit(FishTranspiler.UnboxAny(info.FieldType));
					il.Emit(FishTranspiler.Call(typeof(ReflectionCaching), info.FieldType.IsNullable() ? nameof(UnboxNullableSafely) : nameof(UnboxSafely),
						new[] { typeof(object) },
						new[] { info.FieldType.IsNullable() ? Nullable.GetUnderlyingType(info.FieldType) : info.FieldType }));
				}
				else
				{
					il.Emit(FishTranspiler.Cast(info.FieldType));
				}

				il.Emit(FishTranspiler.StoreField(info));

				il.Emit(FishTranspiler.Return);

				return (FieldSetter)dm.CreateDelegate(typeof(FieldSetter));
			}
			catch (Exception ex)
			{
				Log.Error($"PerformanceFish failed to generate an optimized delegate for {GetFieldDescription(info)}. Reverting to default behaviour instead.\n{ex}");
				return null;
			}
		}

		public struct FieldInfoGetterCache
		{
			public FieldGetter? func;
			public int callCount;
		}

		public struct FieldInfoSetterCache
		{
			public FieldSetter? action;
			public int callCount;
		}

		public delegate object FieldGetter(object obj);

		public delegate void FieldSetter(object obj, object value);
	}

	private static string GetFieldDescription(FieldInfo info) => $"{(info.IsPublic ? "public" : "private")} {(info.IsLiteral && !info.IsInitOnly ? "const " : info.IsStatic ? "static " : "")} {info.FieldType.FullDescription()} {info.DeclaringType.FullDescription()}:{info.Name}";

	public class MethodBasePatches
	{
		public class Invoke_Patch : FishPatch
		{
			public override string Description => "Optimizes the Invoke method by invoking it through specialized cached delegates";

			public override Expression<Action> TargetMethod => () => default(MethodBase)!.Invoke(null, null);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool Prefix(MethodBase __instance, object obj, object[] parameters, ref object __result)
			{
				if (__instance is not MethodInfo method)
					return true;

				ref var cache = ref MethodInvokers.Get.TryGetReferenceUnsafe(method.MethodHandle);

				if (!Unsafe.IsNullRef(ref cache))
				{
					if (cache.func != null || UpdateCache(ref cache, method))
					{
						__result = cache.func!(obj, parameters);
						return false;
					}
					return true;
				}
				else
				{
					return AddCache(method);
				}
			}

			private static bool AddCache(MethodInfo method)
			{
				MethodInvokers.Get.Add(method.MethodHandle, new());
				return true;
			}
		}

		public static bool UpdateCache(ref MethodInvokerCache cache, MethodInfo instance)
		{
			cache.callCount++;
			if (cache.callCount == 300)
				cache.func = MakeInvokeDelegate(instance);
			else if (cache.callCount > 300)
				return false;

			MethodInvokers.Get[instance.MethodHandle] = cache;

			return cache.func != null;
		}

		// HarmonyLib.MethodInvoker can't handle arrays with null values when used with ByRef value types. That aside this is near identical.
		public static MethodInvoker? MakeInvokeDelegate(MethodInfo info)
		{
			try
			{
				//DebugLog.Message($"Making delegate for {(info.IsStatic ? "static" : "")} {info.DeclaringType} {info} with arguments {info.GetParameters().ToStringSafeEnumerable()}");

				var dm = new DynamicMethod($"MethodInvoker_{info.Name}", typeof(object), new[] { typeof(object), typeof(object[]) }, info.DeclaringType, true);
				var il = dm.GetILGenerator();

				var parameters = info.GetParameters();

				if (!info.IsStatic)
				{
					il.Emit(FishTranspiler.This);
					if (info.DeclaringType.IsValueType)
						il.Emit(FishTranspiler.UnboxAny(info.DeclaringType));
				}

				for (var i = 0; i < parameters.Length; i++)
				{
					il.Emit(FishTranspiler.Argument(1));
					il.Emit(FishTranspiler.Constant(i));

					var parameterType = parameters[i].ParameterType;

					if (parameterType.IsByRef)
					{
						parameterType = parameterType.GetElementType();

						il.Emit(FishTranspiler.LoadElementAddress<object>());

						il.Emit(parameterType.IsValueType
							? FishTranspiler.Call(typeof(ReflectionCaching), nameof(UnboxRefSafely),
								new[] { typeof(object).MakeByRefType() },
								new[] { parameterType.IsNullable() ? Nullable.GetUnderlyingType(parameterType) : parameterType })
							: FishTranspiler.Call(typeof(ReflectionCaching), nameof(CastRefSafely),
								new[] { typeof(object).MakeByRefType() },
								new[] { typeof(object), parameterType }));
					}
					else
					{
						il.Emit(FishTranspiler.LoadElement<object>());

						il.Emit(parameterType.IsValueType
							? FishTranspiler.Call(typeof(ReflectionCaching), parameterType.IsNullable() ? nameof(UnboxNullableSafely) : nameof(UnboxSafely),
								new[] { typeof(object) },
								new[] { parameterType.IsNullable() ? Nullable.GetUnderlyingType(parameterType) : parameterType })
							: FishTranspiler.As(parameterType));
					}
				}

				il.Emit(FishTranspiler.Call(info));

				if (info.ReturnType == typeof(void))
					il.Emit(FishTranspiler.Null);
				else if (info.ReturnType.IsValueType)
					il.Emit(FishTranspiler.Box(info.ReturnType));

				il.Emit(FishTranspiler.Return);

				return (MethodInvoker)dm.CreateDelegate(typeof(MethodInvoker));
			}
			catch (Exception ex)
			{
				Log.Error($"PerformanceFish failed to generate an optimized delegate for {info.FullDescription()}. Reverting to default behaviour instead.\n{ex}");
				return null;
			}
		}

		public struct MethodInvokerCache
		{
			public MethodInvoker? func;
			public int callCount;
		}

		public delegate object MethodInvoker(object obj, object[] parameters);
	}
#endif

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TTo CastRefSafely<TFrom, TTo>(ref TFrom from)
			=> ref from is TTo or null
			? ref Unsafe.As<TFrom, TTo>(ref from)
			: ref ThrowInvalidCastException<TTo>();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ref TTo UnboxRefSafely<TTo>(ref object from) where TTo : struct
		=> ref from is TTo
		? ref Unsafe.Unbox<TTo>(from)
		: ref Unsafe.Unbox<TTo>(from
			= from is null
			? default
			: FisheryLib.Convert.To<IConvertible, TTo>((IConvertible)from));
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TTo UnboxSafely<TTo>(object from) where TTo : struct
		=> from is TTo to
		? to
		// : from is IConvertible
		: FisheryLib.Convert.To<IConvertible, TTo>((IConvertible)from);
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TTo? UnboxNullableSafely<TTo>(object from) where TTo : struct
		=> from is TTo to
		? to
		: from is null
		? null
		// : from is IConvertible
		: FisheryLib.Convert.To<IConvertible, TTo>((IConvertible)from);

	[DoesNotReturn]
	private static ref T ThrowInvalidCastException<T>() => throw new InvalidCastException();
}