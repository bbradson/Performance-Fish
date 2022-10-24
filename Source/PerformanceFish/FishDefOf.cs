// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

/*using System.Linq;

namespace PerformanceFish;
[StaticConstructorOnStartup]
public static class FishDefOfOrNull
{
	public static RecipeDef VFEA_MendWeapon { get; }
	public static RecipeDef VFEA_MendArmor { get; }
	public static RecipeDef VFEA_MendApparel { get; }

	static FishDefOfOrNull()
	{
		foreach (var property in typeof(FishDefOfOrNull).GetProperties())
		{
			typeof(FishDefOfOrNull).GetFields(BindingFlags.Static | BindingFlags.NonPublic).First(f => f.Name.Contains(property.Name)).SetValue(null,
				typeof(FishDefOfOrNull).GetMethod(nameof(DefOf), BindingFlags.Static | BindingFlags.NonPublic)
				.MakeGenericMethod(property.GetMethod.ReturnType).Invoke(null, new[] { property.Name }));
		}
	}
	private static T DefOf<T>(string name) where T : Def => DefDatabase<T>.GetNamedSilentFail(name);
}*/