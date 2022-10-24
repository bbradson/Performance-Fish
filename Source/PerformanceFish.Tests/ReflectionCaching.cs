global using System;
global using System.Collections.Generic;
global using Xunit;
global using Xunit.Abstractions;
using HarmonyLib;

namespace PerformanceFish.Tests;

public class ReflectionCaching
{
	internal static void InitializeLog(ITestOutputHelper output)
	{
		Log.Config.Message = output.WriteLine;
		Log.Config.Warning = output.WriteLine;
		Log.Config.Error = message => throw new(message);
	}

	public class GetValue
	{
		public GetValue(ITestOutputHelper output) => InitializeLog(output);

		private const int COUNT = 500;

		public static IEnumerable<object[]> Data
			=> new object[][]
			{
				new[] { nameof(_enum) },
				new[] { nameof(_nullableWithoutValue) },
				new[] { nameof(_nullableWithValue) },
				new[] { nameof(_primitive) },
				new[] { nameof(_reference) },
				new[] { nameof(CONST) }
			};

		[Theory]
		[MemberData(nameof(Data))]
		public void Test(string name)
		{
			if (name == nameof(CONST))
			{
				Log.Message("sPeCiFiEd MeThOd Is NoT sUpPoRtEd!\nworks fine on mono however");
				return;
			}

			var field = _type.GetField(name, AccessTools.all);
			Assert.NotNull(field);

			object result = null;

			for (var i = 0; i < COUNT; i++)
				System.ReflectionCaching.FieldInfoPatches.GetValue_Patch.Prefix(field, null, ref result);

			Assert.Equal(result, field.GetValue(null));
		}

		private static TypeCode _enum = TypeCode.Int32;
		private static int? _nullableWithoutValue = null;
		private static int? _nullableWithValue = 1;
		private static int _primitive = 0;
		private static object _reference = new();
		private const string CONST = "a";
		private static Type _type = typeof(GetValue);
	}

	public class SetValue
	{
		public SetValue(ITestOutputHelper output) => InitializeLog(output);

		private const int COUNT = 500;

		public static IEnumerable<object[]> Data
			=> new object[][]
			{
				new object[] { nameof(_enum), TypeCode.Byte },
				new object[] { nameof(_nullableWithoutValue), 1 },
				new object[] { nameof(_nullableWithValue), null },
				new object[] { nameof(_primitive), 1 },
				new object[] { nameof(_reference), new() }
			};

		[Theory]
		[MemberData(nameof(Data))]
		public void Test(string name, object input)
		{
			var field = _type.GetField(name, AccessTools.all);
			Assert.NotNull(field);

			for (var i = 0; i < COUNT; i++)
				System.ReflectionCaching.FieldInfoPatches.SetValue_Patch.Prefix(field, null, input);

			Assert.Equal(input, field.GetValue(null));
		}

		private static TypeCode _enum = TypeCode.Int32;
		private static int? _nullableWithoutValue = null;
		private static int? _nullableWithValue = 1;
		private static int _primitive = 0;
		private static object _reference = new();
		private static Type _type = typeof(SetValue);
	}
}