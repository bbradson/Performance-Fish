// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

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

	public static IEnumerable<Type> SubclassesWithNoMethodOverride(this Type type, Type[] allowedDeclaringTypes,
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
				if (allowedDeclaringTypes.Contains(AccessTools.Method(allSubclasses[i], names[j])!.DeclaringType!))
					continue;

				hasOverrides = true;
				break;
			}

			if (!hasOverrides)
				yield return allSubclasses[i];
		}
	}
	
	public static IEnumerable<Type> SubclassesWithNoMethodOverrideAndSelf(this Type type, Type[] allowedDeclaringTypes,
		params string[] names)
	{
		yield return type;

		foreach (var subclass in type.SubclassesWithNoMethodOverride(allowedDeclaringTypes, names))
			yield return subclass;
	}
}