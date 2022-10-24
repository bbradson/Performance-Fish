// Copyright (c) 2022 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;

namespace PerformanceFish.FunctionPointers;

public static class IndexGetter<T> where T : notnull
{
	public static readonly unsafe delegate*<T, int> Default = IndexGetter.GetDefaultIndexFunctionPointer<T>();
}

public static class IndexGetter
{
	public static class Methods
	{
		public static int HediffSet(HediffSet set) => set.pawn.thingIDNumber;
		public static int HediffDef(HediffDef def) => def.index;
		public static int Bill(Bill bill) => bill.loadID;
		public static int Pawn(Pawn pawn) => pawn.thingIDNumber;
		public static int Thing(Thing thing) => thing.thingIDNumber;
		public static int CompAssignableToPawn_Bed(CompAssignableToPawn_Bed comp) => comp.parent.thingIDNumber;
		public static int Map(Map map) => map.uniqueID;
		public static int DesignationManager(DesignationManager manager) => manager.map.uniqueID;
		public static int DesignationDef(DesignationDef def) => def.index;
		public static int Room(Room room) => room.ID;
		public static int Ideo(Ideo ideo) => ideo.id;
		public static int RecipeDef(RecipeDef def) => def.index;
	}

	internal static Cache.IndexGetter<T> GetDefaultIndexGetter<T>() where T : notnull
		=> _indexFuncs.TryGetValue(typeof(T), out var value)
		? (Cache.IndexGetter<T>)value
		: Fallback<T>();

	internal static unsafe delegate*<T, int> GetDefaultIndexFunctionPointer<T>() where T : notnull
		=> (delegate*<T, int>)(GetGetterMethods().FirstOrDefault(m => m.GetParameters()[0].ParameterType == typeof(T))
		?? Fallback<T>().Method).MethodHandle.GetFunctionPointer();

	private static IEnumerable<MethodInfo> GetGetterMethods()
		=> typeof(Methods)
		.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
		.Where(m
			=> m.ReturnType == typeof(int)
			&& m.GetParameters().Length == 1);

	private static Cache.IndexGetter<T> Fallback<T>() where T : notnull
	{
		var isDef = typeof(Def).IsAssignableFrom(typeof(T));
		var isThing = false; //typeof(Thing).IsAssignableFrom(typeof(T)) && !typeof(Mote).IsAssignableFrom(typeof(T)); // generally always safe in vanilla, but logging mods won't hurt either

		var message = $"Performance Fish failed to find type {typeof(T).FullDescription()} in indexFuncs dictionary. Using fallback instead";

		if (isDef)
			Log.Error($"{message} to prevent further issues, but this likely won't work correctly.");
		else if (!isThing)
			Log.Warning($"{message}.");

		var type = typeof(T).BaseType;
		Delegate func;
		while (!_indexFuncs.TryGetValue(type, out func))
			type = type.BaseType ?? throw new Exception($"Performance Fish failed to find a fallback method to handle type {typeof(T).FullDescription()} from mod {typeof(T).Assembly.GetName().Name}");

		if (!isThing)
			Log.Message($"Found fallback of type {type.FullDescription()}.{(!isDef ? " This likely means we can safely continue from here." : "")}");

		return (Cache.IndexGetter<T>)func;
	}

	private static Dictionary<Type, Delegate> _indexFuncs
		= GetGetterMethods()
		.ToDictionary(
			m => m.GetParameters()[0].ParameterType,
			m => m.CreateDelegate(typeof(Cache.IndexGetter<>).MakeGenericType(m.GetParameters()[0].ParameterType)));
}