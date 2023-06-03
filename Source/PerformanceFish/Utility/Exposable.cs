// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.Utility;

/// <summary>
/// Scribe_Deep normally creates a new instance when loading data. Scribe on this class prevents that. Also includes a
/// try-catch block
/// </summary>
public class Exposable : IExposable
{
	public readonly IExposable Parent;
	public Exposable(IExposable @this) => Parent = @this;

	public void ExposeData()
	{
		try
		{
			Parent.ExposeData();
		}
		catch (Exception ex)
		{
			Log.Error($"{ex}");
		}
	}

	public override string ToString() => TrimName(Parent.ToString());
	public override bool Equals(object obj) => obj.Equals(Parent);
	public override int GetHashCode() => Parent.GetHashCode();

	public static void Scribe(IExposable iExposable, string label, bool trimLabel = true)
	{
		var wrappedExposable = new Exposable(iExposable);
		Scribe_Deep.Look(ref wrappedExposable, trimLabel ? TrimName(label) : label, iExposable);
	}

	internal static string TrimName(string name)
		=> name.Replace('+', '.') is var fullName
			&& $"{typeof(PerformanceFishMod).Namespace}." is var @namespace
			&& fullName.StartsWith(@namespace)
				? fullName.Remove(0, @namespace.Length)
				: fullName;
}