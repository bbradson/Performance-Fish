// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics;
using System.Reflection.Emit;
using JetBrains.Annotations;

namespace PerformanceFish.Utility;

[PublicAPI]
public static class ReflectionExtensions
{
	/// <summary>
	/// Returns the first method that matches either by name OR by predicate
	/// </summary>
	public static MethodInfo? TryGetMethod(this Type type, string nameCaseInsensitive,
		Predicate<MethodInfo>? predicate = null)
	{
		predicate ??= static _ => true;
		var methods = type.GetMethods(AccessTools.all);

		for (var i = 0; i < methods.Length; i++)
		{
			if (methods[i].NameEqualsCaseInsensitive(nameCaseInsensitive) || predicate(methods[i]))
				return methods[i];
		}

		return null;
	}

	public static bool IsConst(this FieldInfo info) => info is { IsLiteral: true, IsInitOnly: false };
	
	public static IEnumerable<Type> SubclassesWithNoMethodOverride(this Type type, params string[] names)
	{
		Guard.IsNotNull(type);
		Guard.IsNotNull(names);
		Guard.IsNotEmpty(names);
		
		var allSubclasses = type.AllSubclassesNonAbstract();
		for (var i = 0; i < allSubclasses.Count; i++)
		{
			var hasOverrides = false;
			for (var j = names.Length; j-- > 0;)
			{
				if (AccessTools.DeclaredMethod(allSubclasses[i], names[j]) is null)
					continue;

				hasOverrides = true;
				break;
			}

			if (!hasOverrides)
				yield return allSubclasses[i];
		}
	}
	
	public static IEnumerable<Type> SubclassesWithNoMethodOverrideAndSelf(this Type type, params string[] names)
	{
		yield return type;

		foreach (var subclass in type.SubclassesWithNoMethodOverride(names))
			yield return subclass;
	}

	public static IEnumerable<Type> SubclassesWithNoMethodOverride(this Type type, Type?[] allowedDeclaringTypes,
		params string[] names)
	{
		Guard.IsNotNull(type);
		Guard.IsNotNull(allowedDeclaringTypes);
		Guard.IsNotEmpty(allowedDeclaringTypes);
		Guard.IsNotNull(names);
		Guard.IsNotEmpty(names);
		
		var allSubclasses = type.AllSubclassesNonAbstract();
		for (var i = 0; i < allSubclasses.Count; i++)
		{
			var hasOverrides = false;
			for (var j = names.Length; j-- > 0;)
			{
				if (AccessTools.Method(allSubclasses[i], names[j])!.DeclaringType is { } declaringType
					&& allowedDeclaringTypes.Contains(declaringType))
				{
					continue;
				}

				hasOverrides = true;
				break;
			}

			if (!hasOverrides)
				yield return allSubclasses[i];
		}
	}
	
	public static IEnumerable<Type> SubclassesWithNoMethodOverrideAndSelf(this Type type, Type?[] allowedDeclaringTypes,
		params string[] names)
	{
		yield return type;

		foreach (var subclass in type.SubclassesWithNoMethodOverride(allowedDeclaringTypes, names))
			yield return subclass;
	}

	public static Version GetReferencedAssemblyVersion(this Assembly assembly, Assembly referencedAssembly)
		=> assembly.GetReferencedAssemblyVersion(referencedAssembly.GetName().Name);

	public static Version GetReferencedAssemblyVersion(this Assembly assembly, string referencedAssemblyName)
		=> assembly.GetReferencedAssemblies().First(reference
			=> reference.FullName.StartsWith($"{referencedAssemblyName}, Version", StringComparison.Ordinal)).Version;

	public static Version GetLoadedVersion(this Assembly assembly) => assembly.GetName().Version;

	public static T MemberwiseClone<T>(this T obj) => MemberwiseCloneCache<T>.InstanceFunc(obj);

	private static class MemberwiseCloneCache
	{
		[ThreadStatic]
		private static FishTable<Type, Delegate>? _staticFuncs;

		public static FishTable<Type, Delegate> StaticFuncs => _staticFuncs ??= Initialize();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static FishTable<Type, Delegate> Initialize()
			=> new()
			{
				ValueInitializer = static type
					=> (Delegate)AccessTools.DeclaredField(typeof(MemberwiseCloneCache<>).MakeGenericType(type),
						nameof(MemberwiseCloneCache<object>.StaticFunc)).GetValue(null)
			};
	}
	
	private static class MemberwiseCloneCache<T>
	{
		internal static readonly Func<T, T> StaticFunc, InstanceFunc;

		static MemberwiseCloneCache()
		{
			var type = typeof(T);
			try
			{
				var dm = new DynamicMethod($"FisheryMemberwiseCloneFunc_{type.Name}",
					type, [type], type, true);
				
				var il = dm.GetILGenerator();

				if (type.IsValueType)
				{
					il.Emit(FishTranspiler.Argument(0));
				}
				else
				{
					var copyVariable = FishTranspiler.NewLocalVariable(type, il);
	
					il.Emit(FishTranspiler.New(type, Type.EmptyTypes));
					il.Emit(copyVariable.Store());

					foreach (var field in typeof(T).GetFields(BindingFlags.Instance
						| BindingFlags.Public
						| BindingFlags.NonPublic))
					{
						var fieldInstruction = FishTranspiler.Field(field);
					
						il.Emit(copyVariable.Load());
						il.Emit(FishTranspiler.Argument(0));
						il.Emit(fieldInstruction.Load());
						il.Emit(fieldInstruction.Store());
					}
	
					il.Emit(copyVariable.Load());
				}
				
				il.Emit(FishTranspiler.Return);
				
				StaticFunc = (Func<T, T>)dm.CreateDelegate(typeof(Func<T, T>));

				InstanceFunc = static obj => obj!.GetType() == typeof(T)
					? StaticFunc(obj)
					: ((Func<T, T>)MemberwiseCloneCache.StaticFuncs.GetOrAdd(obj.GetType()))(obj);
			}
			catch (Exception e)
			{
				Log.Error($"Failed creating a MemberwiseClone delegate for {type.FullDescription()}\n{
					e}\n{new StackTrace()}");

				var defaultCloneFunc = (Func<object, object>)AccessTools
					.DeclaredMethod(typeof(object), nameof(MemberwiseClone))
					.CreateDelegate(typeof(Func<object, object>));
				
				InstanceFunc = StaticFunc = obj => (T)defaultCloneFunc(obj!);
			}
		}
	}
}