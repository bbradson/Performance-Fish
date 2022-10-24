// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using System.Xml;

namespace PerformanceFish;

public static class AnalyzerFixes
{
	public static void Patch()
	{
		var modBase = AccessTools.Constructor(Reflection.Type("PerformanceAnalyzer", "Analyzer.Modbase"), new[] { typeof(ModContentPack) });
		if (modBase is null)
			return /*false*/;

		var success = PerformanceFishMod.Harmony.Patch(modBase, postfix: new(methodof(XmlParser.CollectXmlData))) != null;

		var methodTransplanting = Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.MethodTransplanting", "UpdateMethod",
			 new[] { typeof(Type), typeof(MethodInfo) });

		if (methodTransplanting is null
			|| PerformanceFishMod.Harmony.Patch(methodTransplanting, transpiler: new(methodof(UpdateMethod_Transpiler))) is null)
		{
			success = false;
		}

		var panelLogs = Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Panel_Logs", "RightClickDropDown");

		if (panelLogs is null
			|| PerformanceFishMod.Harmony.Patch(panelLogs, postfix: new(methodof(PanelLogs_Postfix))) is null)
		{
			success = false;
		}

		return /*success*/;
	}

	/// <summary>
	/// yes this is literally a prefix
	/// </summary>
	public static CodeInstructions UpdateMethod_Transpiler(CodeInstructions codes, MethodBase method)
	{
		yield return FishTranspiler.ArgumentAddress(method, "meth");
		yield return FishTranspiler.Call(UpdateMethod_Prefix);
		foreach (var code in codes)
			yield return code;
	}

	public static void UpdateMethod_Prefix(ref MethodInfo meth)
	{
		if (AccessTools.EnumeratorMoveNext(meth) is { } moveNext)
			meth = moveNext;
	}

	public static IEnumerable<FloatMenuOption> PanelLogs_Postfix(IEnumerable<FloatMenuOption> values, object log)
	{
		var method = ProfileLog_meth(log) as MethodInfo;
		var returned = false;
		foreach (var value in values)
		{
			yield return value;
			if (IsValidMethodForOverriding(method) && !returned && value.Label == "Profile the internal methods of")
			{
				returned = true;
				yield return new("Profile overriding methods of",
					() => PatchOverridesForMethod(method, (Category)GUIController_CurrentCategory()));
			}
		}
	}

	private static bool IsValidMethodForOverriding([NotNullWhen(true)] MethodInfo? info)
		=> info is not null
		&& (info.IsVirtual || info.IsAbstract || info.GetBaseDefinition().DeclaringType != info.DeclaringType);

	public enum Category
	{
		Settings,
		Tick,
		Update,
		GUI,
		Modder
	}

	public static void PatchOverridesForMethod(MethodInfo method, Category category)
	{
		if (OverridingMethodUtility.PatchedOverrides.Contains(method))
		{
			var signature = Utility_GetSignature(method, true);
			Messages.Message("Have already patched overrides for the method " + signature, MessageTypeDefOf.CautionInput, false);
			Utility_Warn("Trying to repeat patching already profiled overrides of method - " + signature);
		}
		else
		{
			PatchOverridesForMethodFull(method, category);
		}
	}

	private static void PatchOverridesForMethodFull(MethodInfo method, Category category)
	{
		try
		{
			OverridingMethodUtility.Generate(method, category);
			/*Task.Factory.StartNew(delegate
			{
				try
				{
					Modbase_Harmony().Patch(method, null, null, OverridingMethodUtility.OverrideProfiler);
				}
				catch (Exception e2)
				{
					Utility_ReportException(e2, "Failed to patch overriding methods for " + Utility_GetSignature(method, false));
				}
			});*/
		}
		catch (Exception e)
		{
			Utility_ReportException(e, "Failed to set up state to patch overriding methods");
			OverridingMethodUtility.PatchedOverrides.Remove(method);
		}
	}

	public static class OverridingMethodUtility
	{
		public static HashSet<MethodInfo> PatchedOverrides = new();

		public static void ClearCaches() => PatchedOverrides.Clear();

		public static void Generate(MethodInfo method, Category category)
		{
			PatchedOverrides.Add(method);

			var name = $"{method.DeclaringType}:{method.Name}-overrides";

			var methods = new HashSet<MethodInfo>();
			var subClasses = method.DeclaringType.AllSubclasses();

			foreach (var subClass in subClasses)
			{
				var overridingMethod = Array.Find(subClass.GetMethods(AccessTools.allDeclared), m => m.GetBaseDefinition() == method.GetBaseDefinition());
				if (overridingMethod is null || overridingMethod.IsAbstract)
					continue;

				methods.Add(overridingMethod);

				//Log.Message($"Added method {Utility_GetSignature(overridingMethod, true)} for profiling");
			}

			var type = DynamicTypeBuilder_CreateType(name, methods);
			var entry = Entry_Create(name, (int)category, type, false, true);
			GUIController_AddSpecificEntry((int)category, entry, type);
			GUIController_SwapToEntry(name);
		}
	}

	public static AccessTools.FieldRef<object, MethodBase> ProfileLog_meth
		=> _profileLog_meth
		??= AccessTools.FieldRefAccess<object, MethodBase>(Reflection.FieldInfo("PerformanceAnalyzer", "Analyzer.Profiling.ProfileLog", "meth"));
	private static AccessTools.FieldRef<object, MethodBase>? _profileLog_meth;

	public static Func<Harmony> Modbase_Harmony
		=> _modbase_Harmony
		??= (Func<Harmony>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Modbase", "get_Harmony")
		.CreateDelegate(typeof(Func<Harmony>));
	private static Func<Harmony>? _modbase_Harmony;

	public static Action<string, int> GUIController_AddEntry
		=> _guiController_AddEntry
		??= (Action<string, int>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.GUIController", "AddEntry")
		.CreateDelegate(typeof(Action<string, int>));
	private static Action<string, int>? _guiController_AddEntry;

	public static Action<string> GUIController_SwapToEntry
		=> _guiController_SwapToEntry
		??= (Action<string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.GUIController", "SwapToEntry")
		.CreateDelegate(typeof(Action<string>));
	private static Action<string>? _guiController_SwapToEntry;

	public static Func<MethodBase, bool, string> Utility_GetSignature
		=> _utility_GetSignature
		??= (Func<MethodBase, bool, string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Utility", "GetSignature")
		.CreateDelegate(typeof(Func<MethodBase, bool, string>));
	private static Func<MethodBase, bool, string>? _utility_GetSignature;

	public static Func<int> GUIController_CurrentCategory
		=> _guiController_CurrentCategory
		??= (Func<int>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.GUIController", "get_CurrentCategory")
		.CreateDelegate(typeof(Func<int>));
	private static Func<int>? _guiController_CurrentCategory;

	public static Action<string> Utility_Warn
		=> _utility_Warn
		??= (Action<string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Utility", "Warn")
		.CreateDelegate(typeof(Action<string>));
	private static Action<string>? _utility_Warn;

	public static Action<Exception, string> Utility_ReportException
		=> _utility_ReportException
		??= (Action<Exception, string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Utility", "ReportException")
		.CreateDelegate(typeof(Action<Exception, string>));
	private static Action<Exception, string>? _utility_ReportException;

	public static Action<string> Utility_Error
		=> _utility_Error
		??= (Action<string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Utility", "Error")
		.CreateDelegate(typeof(Action<string>));
	private static Action<string>? _utility_Error;

	public static CodeInstructions Parse_Transpiler(CodeInstructions CodeInstructions)
	{
		var codes = CodeInstructions.ToList();
		var parseMethod = FishTranspiler.Call("PerformanceAnalyzer", "Analyzer.Profiling.XmlParser", "ParseMethod");
		var errorMethod = FishTranspiler.Call("PerformanceAnalyzer", "Analyzer.Profiling.ThreadSafeLogger", "Error");

		for (var i = 0; i < codes.Count; i++)
		{
			if (codes[i] == parseMethod)
			{
				yield return new(codes[i - 2]); //Ldloc xmlNode
				yield return FishTranspiler.Call(ParseMethod_Fixed);
				yield return FishTranspiler.Call<Action<HashSet<MethodInfo>, IEnumerable<MethodInfo>>>(GenCollection.AddRange);
				i += 2; //removing OpCodes.Pop
			}
			else if (codes[i] == errorMethod)
			{
				yield return new(codes[i - 4]); //Ldloc xmlNode
				yield return FishTranspiler.Call(ErrorFix);
			}
			else
			{
				yield return codes[i];
			}
		}
	}

	public static IEnumerable<MethodInfo> ParseMethod_Fixed(string info, XmlNode node)
		=> node.NodeType == XmlNodeType.Comment
		? Enumerable.Empty<MethodInfo>()
		: ParseMethod(info);

	private static IEnumerable<MethodInfo> ParseMethod(string info)
	{
		var array = info.Split(':');
		if (array.Length != 2)
			throw new ArgumentException($"Method must be specified as 'Namespace.Type1.Type2:MethodName. Got {info} instead.", nameof(info));

		var methods = Reflection.MethodsOfName(AccessTools.TypeByName(array[0]), array[1]);
		foreach (var method in methods)
		{
			yield return method.IsGenericMethodDefinition
				? method.MakeGenericMethod(method.GetGenericArguments().Select(t => t.GetGenericParameterConstraints() is var constraints2 && constraints2.Length != 0 ? constraints2[0] : typeof(object)).ToArray())
				: method;
		}
	}

	public static void ErrorFix(string message, XmlNode node)
	{
		if (node.NodeType == XmlNodeType.Comment)
			return;

		ThreadSafeLogger_Error(message);
	}

	public static Action<string> ThreadSafeLogger_Error
		=> _threadSafeLogger_Error
		??= (Action<string>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.ThreadSafeLogger", "Error")
		.CreateDelegate(typeof(Action<string>));
	private static Action<string>? _threadSafeLogger_Error;

	public static Func<string, HashSet<MethodInfo>, Type> DynamicTypeBuilder_CreateType
		=> _dynamicTypeBuilder_CreateType
		??= (Func<string, HashSet<MethodInfo>, Type>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.DynamicTypeBuilder", "CreateType")
		.CreateDelegate(typeof(Func<string, HashSet<MethodInfo>, Type>));
	private static Func<string, HashSet<MethodInfo>, Type>? _dynamicTypeBuilder_CreateType;

	public static Func<Type, bool, IEnumerable<MethodInfo>> Utility_GetTypeMethods
		=> _utility_GetTypeMethods
		??= (Func<Type, bool, IEnumerable<MethodInfo>>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Utility", "GetTypeMethods")
		.CreateDelegate(typeof(Func<Type, bool, IEnumerable<MethodInfo>>));
	private static Func<Type, bool, IEnumerable<MethodInfo>>? _utility_GetTypeMethods;

	public static AccessTools.FieldRef<object, string> Entry_tip
		=> _entry_tip
		??= AccessTools.FieldRefAccess<string>(Type.GetType("Analyzer.Profiling.Entry, PerformanceAnalyzer"), "tip");
	private static AccessTools.FieldRef<object, string>? _entry_tip;

	public static Action<int, object, Type> GUIController_AddSpecificEntry => _guiController_AddSpecificEntry ??= MakeGUIController_AddEntryMethod();
	private static Action<int, object, Type>? _guiController_AddSpecificEntry;
	private static Action<int, object, Type> MakeGUIController_AddEntryMethod()
	{
		var analyzer_Profiling_EntryType = Reflection.Type("PerformanceAnalyzer", "Analyzer.Profiling.Entry");
		if (analyzer_Profiling_EntryType == null)
			Log.Error("analyzer_Profiling_EntryType is null");

		var gUIController_Tab = Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.GUIController", "Tab");
		if (gUIController_Tab == null)
			Log.Error("gUIController_Tab is null");

		var tab_entries = Reflection.FieldInfo("PerformanceAnalyzer", "Analyzer.Profiling.Tab", "entries");
		if (tab_entries == null)
			Log.Error("tab_entries is null");

		/*var memberInfo_Name = AccessTools.PropertyGetter(typeof(MemberInfo), nameof(MemberInfo.Name));
		if (memberInfo_Name == null)
			Log.Error("memberInfo_Name is null");

		var entry_Create = AccessTools.Method(analyzer_Profiling_EntryType, "Create");
		if (entry_Create == null)
			Log.Error("entry_Create is null");*/

		var dictionary_Add = AccessTools.Method(typeof(Dictionary<,>).MakeGenericType(new[] { analyzer_Profiling_EntryType, typeof(Type) }),
			  "Add", new[] { analyzer_Profiling_EntryType, typeof(Type) });
		if (dictionary_Add == null)
			Log.Error("dictionary_Add is null");

		var dynamicMethod = new DynamicMethod("GUIController_AddEntryMethod", typeof(void), new[] { typeof(int), typeof(object), typeof(Type) });
		var il = dynamicMethod.GetILGenerator();

		il.Emit(FishTranspiler.Argument(0)); //Category
		il.Emit(FishTranspiler.Call(gUIController_Tab!)); //Tab(Category)
		il.Emit(FishTranspiler.Field(tab_entries!)); //tab.entries
		/*il.Emit(FishTranspiler.Argument(2)); //type
		il.Emit(FishTranspiler.Call(memberInfo_Name)); //type.Name
		il.Emit(FishTranspiler.LoadConstant(4)); //Category.Modder
		il.Emit(FishTranspiler.Argument(2)); //type
		il.Emit(FishTranspiler.LoadConstant(0)); //closeable = false
		il.Emit(FishTranspiler.LoadConstant(1)); //dynGen = true
		il.Emit(FishTranspiler.Call(entry_Create)); //Create(string, Category, Type, bool, bool)*/
		il.Emit(FishTranspiler.Argument(1)); //entry
		il.Emit(FishTranspiler.Argument(2)); //type
		il.Emit(FishTranspiler.Call(dictionary_Add!)); //entries.Add(Entry, Type)
		il.Emit(FishTranspiler.Return);

		return (Action<int, object, Type>)dynamicMethod.CreateDelegate(typeof(Action<int, object, Type>));
	}

	public static Func<string, int, Type, bool, bool, object> Entry_Create => _entry_Create
		??= (Func<string, int, Type, bool, bool, object>)Reflection.MethodInfo("PerformanceAnalyzer", "Analyzer.Profiling.Entry", "Create").CreateDelegate(typeof(Func<string, int, Type, bool, bool, object>));
	private static Func<string, int, Type, bool, bool, object>? _entry_Create;

	//Copied from Wiri's analyzer fork, with above fixes integrated. The steam version lacks this functionality
	public static class XmlParser
	{
		public static void CollectXmlData()
		{
			foreach (var dir in ModLister.AllActiveModDirs)
			{
				var xmlFiles = dir.GetFiles("DubsAnalyzer.xml");
				if (xmlFiles.Length != 0)
				{
					foreach (var file in xmlFiles)
					{
						var doc = new XmlDocument();
						doc.Load(file.OpenRead());

						Parse(doc);
					}
				}
			}
		}

		// Iterates through each child element in the document and attempts to extract method(s) from the strings inside the children
		private static void Parse(XmlDocument doc)
			=> ForEach(doc.DocumentElement.ChildNodes, node =>
			{
				var methods = new HashSet<MethodInfo>();
				var tip = string.Empty;
				var category = (int)Category.Modder;
				ForEach(node.ChildNodes, child =>
				{
					switch (child.Name.ToLowerInvariant())
					{
						case "category":
							category = (int)Enum.Parse(typeof(Category), child.InnerText, true); //child.InnerText;
							break;
						case "tooltip":
						case "tip":
						case "description":
							tip += child.InnerText;
							break;
						case "methods":
						case "method":
							AddMethodsFromChildNodes(child, methods, ParseMethod);
							break;
						case "derivedmethods":
							AddMethodsFromChildNodes(child, methods, ParseDerivedMethods);
							break;
						case "types":
						case "type":
							AddMethodsFromChildNodes(child, methods, ParseTypeMethods);
							break;
						case "derivedtypes":
							AddMethodsFromChildNodes(child, methods, ParseDerivedTypeMethods);
							break;
						case "nestedtype":
						case "nestedtypes":
							AddMethodsFromChildNodes(child, methods, ParseNestedTypeMethods);
							break;
						default:
							ThreadSafeLogger_Error($"[Analyzer] Attempting to read unknown value from a DubsAnalyzer.xml, the given input was {child.Name}, it should have been either 'methods', 'types', 'derivedMethods', 'derivedTypes', 'nestedTypes', 'category' or 'tooltip'");
							break;
					}
				});

				var type = DynamicTypeBuilder_CreateType(node.Name, methods);
				var entry = Entry_Create(node.Name, category, type, false, true);
				if (tip != string.Empty)
					Entry_tip(entry) = tip;
				GUIController_AddSpecificEntry(category, entry, type);
			});

		private static void AddMethodsFromChildNodes(XmlNode node, HashSet<MethodInfo> methods, Func<string, IEnumerable<MethodInfo>> func)
			=> ForEach(node.ChildNodes, childNode
				=> methods.AddRange(func(childNode.InnerText)));

		private static void ForEach(XmlNodeList nodes, Action<XmlNode> action)
		{
			foreach (XmlNode node in nodes)
			{
				if (node.NodeType != XmlNodeType.Comment)
					action(node);
			}
		}

		private static IEnumerable<MethodInfo> ParseDerivedMethods(string str)
		{
			foreach (var method in ParseMethod(str))
			{
				var subClasses = method.DeclaringType.AllSubclasses();

				foreach (var subClass in subClasses)
				{
					var overridingMethod = Array.Find(subClass.GetMethods(AccessTools.allDeclared), m => m.GetBaseDefinition() == method.GetBaseDefinition());
					if (overridingMethod is null || overridingMethod.IsAbstract)
						continue;

					yield return overridingMethod;
				}
			}
		}

		private static IEnumerable<MethodInfo> ParseTypeMethods(string str) => Utility_GetTypeMethods(AccessTools.TypeByName(str), false);

		private static IEnumerable<MethodInfo> ParseNestedTypeMethods(string str)
		{
			var type = AccessTools.TypeByName(str);
			foreach (var subType in type.GetNestedTypes())
			{
				foreach (var method in Utility_GetTypeMethods(subType, false))
					yield return method;
			}
		}

		private static IEnumerable<MethodInfo> ParseDerivedTypeMethods(string str)
		{
			var type = AccessTools.TypeByName(str);
			foreach (var subType in type.AllSubclasses())
			{
				foreach (var method in Utility_GetTypeMethods(subType, false))
					yield return method;
			}
		}
	}
}