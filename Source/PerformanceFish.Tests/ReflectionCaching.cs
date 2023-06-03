global using System;
global using System.Collections.Generic;
global using Xunit;
global using Xunit.Abstractions;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using FisheryLib;
using HarmonyLib;
using JetBrains.Annotations;
using PerformanceFish.Collections;
using Verse;
// ReSharper disable InconsistentNaming

//[assembly: AllowPartiallyTrustedCallers]
//[assembly: SecurityTransparent]
[assembly: SecurityRules(SecurityRuleSet.Level2, SkipVerificationInFullTrust = true)]
[assembly: Debuggable(false, false)]

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: SecurityCritical(SecurityCriticalScope.Everything)]

namespace PerformanceFish.Tests;

[UsedImplicitly]
public class ReflectionCaching
{
	internal static void InitializeLog(ITestOutputHelper output)
	{
		Log.Config.Message = output.WriteLine; // message => output.WriteLine("\u001b[33m" + message);
		Log.Config.Warning = output.WriteLine; // message => output.WriteLine("\u001b[33m" + message);
		Log.Config.Error = static message => throw new(message);
	}

	// [Collection("Fish")]
	// public class FishTableTest
	// {
	// 	public FishTableTest(ITestOutputHelper output) => InitializeLog(output);
	//
	// 	[Fact]
	// 	public void Test()
	// 	{
	// 		const int COUNT = 538762;
	// 		
	// 		var randomBuffer = new byte[4 * COUNT];
	// 		new Random(42).NextBytes(randomBuffer);
	// 		var randomNumbers = new int[COUNT];
	// 		Unsafe.CopyBlock(ref randomBuffer[0], ref Unsafe.As<int, byte>(ref randomNumbers[0]), 4 * COUNT);
	// 		
	// 		var fishTable = new FishTable<int, int>();
	// 		foreach (var i in randomNumbers)
	// 		{
	// 			fishTable.GetOrAddReference(i) = -i;
	// 		}
	//
	// 		foreach (var i in randomNumbers)
	// 		{
	// 			Assert.Equal(-i, fishTable.GetOrAddReference(i));
	// 		}
	// 	}
	// }

#if false
	[Collection("Fish")]
	public class FishTableTest2
	{
		public FishTableTest2(ITestOutputHelper output) => InitializeLog(output);

		[Fact]
		public void Test()
		{
			var allTypes = GenTypes.AllTypes.ToArray();
			var allTypeHandles = Array.ConvertAll(allTypes, static type => type.TypeHandle);
			var hashCodes = Array.ConvertAll(allTypes, static type => type.TypeHandle.GetHashCode());

			var hashCodeCounts = new Dictionary<int, uint>();
			foreach (var code in hashCodes)
				hashCodeCounts.GetOrAddReference(code)++;

			var count = allTypes.Length;
			
			Log.Message($"allTypes: {count}, hashCodes: {hashCodeCounts.Count}, max: {
				hashCodeCounts.Max(static pair => pair.Value)}{Environment.NewLine}");

			var random = new Random(42);
			var randomNumbers = new int[count];
			for (var i = 0; i < count; i++)
			{
				var randomNumber = random.Next();
				
				while (randomNumber == 0)
					randomNumber = random.Next();
				
				randomNumbers[i] = randomNumber;
			}

			var success = true;
			
			var fishTable = new FishTable<IntPtr, int>();
			var addedEntries = 0;

			fishTable.EntryAdded += _ => addedEntries++;
			for (var i = 0; i < count; i++)
			{
				Exception caughtException = null;
				try
				{
					try
					{
						fishTable.GetOrAddReference(allTypes[i].TypeHandle.Value) = randomNumbers[i];

						try
						{
							VerifyCount(fishTable, i + 1);
						}
						catch (Exception e)
						{
							caughtException = e;
							
							VerifyNumbers(randomNumbers, allTypeHandles, fishTable, i);
						
							throw;
						}
						VerifyNumbers(randomNumbers, allTypeHandles, fishTable, i);
					}
					catch (Exception e)
					{
						throw new($"Inserted number: {randomNumbers[i]}, Retrieved number: {
							fishTable.GetOrAddReference(allTypes[i].TypeHandle.Value)} {e}\n\n{caughtException}");
					}
				}
				catch (Exception ex)
				{
					Log.Warning($"{ex}");
					Log.Message(Environment.NewLine);
					success = false;
					
					// throw;
				}
			}

			var failureCount = 0;

			for (var i = 0; i < count; i++)
			{
				try
				{
					Assert.Equal(randomNumbers[i], fishTable.GetOrAddReference(allTypes[i].TypeHandle.Value));
				}
				catch (Exception ex)
				{
					failureCount++;

					Log.Warning($"{ex}");
					Log.Message(Environment.NewLine);
					success = false;
				}
			}
			
			Log.Message($"FailureCount: {failureCount}");

			for (var i = 0; i < count; i++)
			{
				Assert.True(hashCodes[i].Equals<int>(allTypes[i].TypeHandle.GetHashCode()));
			}
			
			for (var i = 0; i < count; i++)
			{
				Assert.True(allTypeHandles[i].Equals<RuntimeTypeHandle>(allTypes[i].TypeHandle));
			}

			Assert.True(success);
			
			VerifyCount(fishTable, allTypeHandles.Length);
			
			Assert.Equal(count, addedEntries);
		}

		private static void VerifyNumbers(int[] expectedNumbers, RuntimeTypeHandle[] types,
			FishTable<IntPtr, int> fishTable, int count)
		{
			for (var i = 0; i < count; i++)
			{
				var number = expectedNumbers[i];
				var type = types[i];
				
				try
				{
					Assert.True(number.Equals<int>(fishTable[type.Value]));
				}
				catch (Exception e)
				{
					throw new($"number: {number}, hashCode: {type.GetHashCode()}, i: {i}, size: {
						fishTable.Count} {e}");
				}
			}
		}
		
		private static void VerifyCount(FishTable<IntPtr, int> fishTable, int expectedCount)
		{
			var realCount = 0;
			var returnedCount = fishTable.Count;
			
			foreach (var _ in fishTable)
			{
				realCount++;
			}

			if (realCount != returnedCount)
			{
				throw new($"Invalid count! real: {realCount}, returned: {returnedCount}, expected: {
					expectedCount}");
			}
		}
	}
#endif

	
	[Collection("Fish")]
	public class GetValue
	{
		public GetValue(ITestOutputHelper output) => InitializeLog(output);

		private const int COUNT = 50000;

		public static IEnumerable<object[]> Data
			=> new[]
			{
				new object[] { nameof(_enum) },
				new object[] { nameof(_nullableWithoutValue) },
				new object[] { nameof(_nullableWithValue) },
				new object[] { nameof(_primitive) },
				new object[] { nameof(_reference) },
				new object[] { nameof(CONST) }
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
				System.ReflectionCaching.Test.GetValue(field, null, ref result);
			
			Assert.Equal(field.GetValue(null), result);
		}

		private static TypeCode _enum = TypeCode.Int32;
		private static int? _nullableWithoutValue = null;
		private static int? _nullableWithValue = 1;
		private static int _primitive = 0;
		private static object _reference = new();
		private const string CONST = "a";
		private static Type _type = typeof(GetValue);
	}

	[Collection("Fish")]
	public class SetValue
	{
		public SetValue(ITestOutputHelper output) => InitializeLog(output);

		private const int COUNT = 500;

		public static IEnumerable<object[]> Data
			=> new[]
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
				System.ReflectionCaching.Test.SetValue(field, null, input);

			Assert.Equal(input, field.GetValue(null));
		}

		[Fact]
		public void Test2()
		{
			SimpleThingDefOf.Initialize();
			var def = SimpleThingDefOf.Instance;
			
			Assert.NotNull(def.Description);
			a(def, "descriptionDetailedCached", null);
			Assert.Null(def.Description);
			
			Assert.NotEqual(default, def.Label);
			a(def, "cachedLabelCap", null);
			Assert.Equal(default, def.Label);
		}

		[Fact]
		public void Test3()
		{
			for (var i = 0; i < COUNT; i++)
			{
				SimpleThingDefOf.Initialize();
				var def = SimpleThingDefOf.Instance;
			
				Assert.NotNull(def.Description);
				a_Optimized(def, "descriptionDetailedCached", null);
				Assert.Null(def.Description);
			
				Assert.NotEqual(default, def.Label);
				a_Optimized(def, "cachedLabelCap", null);
				Assert.Equal(default, def.Label);
			}
		}

		[Fact]
		public void Test4()
		{
			for (var i = 0; i < 2; i++)
			{
				_primitive = 0;
				var field = GetType().GetField(nameof(_primitive), AccessTools.all);

				IntPtr value = (nint)(-476);

				var failed = false;
				try
				{
					if (i == 0)
						field.SetValue(null, value);
					else
						System.ReflectionCaching.Test.SetValue(field, null, value);
				}
				catch (Exception e)
				{
					failed = true;
					Log.Message(e + "\n");
				}
				
				Assert.True(failed);
			
				// Assert.Equal(-476, field.GetValue(null));
			}
		}

		private static void a(object A_0, string A_1, object A_2)
			=> (A_0?.GetType().GetField(A_1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				?.SetValue(A_0, A_2);
		
		private static void a_Optimized(object A_0, string A_1, object A_2)
		{
			if (A_0?.GetType().GetField(A_1, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				is { } field)
			{
				System.ReflectionCaching.Test.SetValue(field, A_0, A_2);
			}
		}

		private static TypeCode _enum = TypeCode.Int32;
		private static int? _nullableWithoutValue = null;
		private static int? _nullableWithValue = 1;
		private static int _primitive = 0;
		private static object _reference = new();
		private static Type _type = typeof(SetValue);
	}

	public class SimpleThingDef
	{
		private string descriptionDetailedCached;
		private TaggedString cachedLabelCap;

		public string Description => descriptionDetailedCached;

		public TaggedString Label
		{
			get => cachedLabelCap;
			set => cachedLabelCap = value;
		}

		public SimpleThingDef(string description, string label)
		{
			descriptionDetailedCached = description;
			cachedLabelCap = label;
		}
	}

	public static class SimpleThingDefOf
	{
		public static SimpleThingDef Instance;
		
		public static void Initialize() => Instance = new("InitialDescription", "InitialLabel");
	}

	[Collection("Fish")]
	public class Invoke
	{
		public Invoke(ITestOutputHelper output) => InitializeLog(output);
	
		private const int COUNT = 500;
	
		public static IEnumerable<object[]> Data
			=> new[]
			{
				new object[]
				{
					AccessTools.Method(typeof(Scribe_Values), nameof(Scribe_Values.Look),
						generics: new[] { typeof(string) }),
					new object[] { "", "", "", false }
				}
			};
	
		// [Theory]
		// [MemberData(nameof(Data))]
		// public void Test(MethodBase method, object[] input)
		// {
		// 	Assert.NotNull(method);
		//
		// 	object result = null;
		//
		// 	for (var i = 0; i < COUNT; i++)
		// 		System.ReflectionCaching.Test.Invoke(method, null, input, ref result);
		// }

		[Fact]
		public void TestString()
		{
			var method = AccessTools.Method(GetType(), nameof(Look), generics: new[] { typeof(string) });

			var value = "Input";
			var label = "Label";
			var defaultValue = "DefaultValue";
			var forceSave = true;

			var parameters = new object[] { value, label, defaultValue, forceSave };

			var input = MakeString(value, label, defaultValue, forceSave);

			object output = null;

			System.ReflectionCaching.Test.Invoke(method, null, parameters, ref output);
			
			Assert.Equal(input, output);
			Assert.Equal("Test", parameters[0]);
		}

		[Fact]
		public void TestIntOptimized()
		{
			var method = AccessTools.Method(GetType(), nameof(Look), generics: new[] { typeof(int) });
		
			var value = 5;
			var label = "Label";
			var defaultValue = 0;
			var forceSave = false;
		
			var parameters = new object[] { value, label, defaultValue, forceSave };
		
			var input = MakeString(value, label, defaultValue, forceSave);
		
			object output = null;
		
			System.ReflectionCaching.Test.Invoke(method, null, parameters, ref output);
			
			Assert.Equal(input, output);
			Assert.Equal(17, parameters[0]);
		}

		[Fact]
		public void TestIntExpected()
		{
			var value = 5;
			var label = "Label";
			var defaultValue = 0;
			var forceSave = false;

			var input = MakeString(value, label, defaultValue, forceSave);

			var output = Look(ref value, label, defaultValue, forceSave);
			
			Assert.Equal(input, output);
			Assert.Equal(17, value);
		}

		private static string Look<T>(ref T value, string label, T defaultValue = default, params object[] ctorArgs)
		{
			var input = MakeString(value, label, defaultValue, ctorArgs);

			if (typeof(string).IsAssignableFrom(typeof(T)))
				value = (T)(object)"Test";
			else if (typeof(int).IsAssignableFrom(typeof(T)))
				value = (T)(object)17;
			
			return input;
		}

		private static string MakeString<T>(T value, string label, T defaultValue, params object[] ctorArgs)
			=> $"value: {value}, label: {label}, defaultValue: {defaultValue}, ctorArgs: {
				ctorArgs.ToStringSafeEnumerable()}";
	}
}