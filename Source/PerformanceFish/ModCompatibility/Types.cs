// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace PerformanceFish.ModCompatibility;

public static class Types
{
	public static class RealRuins
	{
		public static Type? POIWorldObject = Reflection.Type("RealRuins", "RealRuins.RealRuinsPOIWorldObject");
	}

	public static class VFEAncients
	{
		public static Type? RecipeExtensionMend = Reflection.Type("VFEAncients", "VFEAncients.RecipeExtension_Mend");
	}

	public static class PerformanceAnalyzer
	{
		public static Type?
			ModBase = Reflection.Type("PerformanceAnalyzer", "Analyzer.Modbase"),
			ProfilingEntry = Reflection.Type("PerformanceAnalyzer", "Analyzer.Profiling.Entry"),
			Window_Analyzer = Reflection.Type("PerformanceAnalyzer", "Analyzer.Window_Analyzer");
	}

	public static class LWM
	{
		public static Type? CompProperties = Reflection.Type("LWM.DeepStorage.Properties");
	}

	public static class Rimfactory
	{
		public static Type? ModExtension = Reflection.Type("ProjectRimFactory.Storage.Editables.DefModExtension_Crate");
	}
}