// https://stackoverflow.com/a/18363848

using System.Diagnostics.CodeAnalysis;

namespace PerformanceFish.Utility;

public static class Is
{
	public static bool Null<T>(T? value) where T : class => value == null;

	public static bool NotNull<T>([NotNullWhen(true)] T? value) where T : class => value != null;

	public static bool Null<T>(T? nullableValue) where T : struct => !nullableValue.HasValue;

	public static bool NotNull<T>([NotNullWhen(true)] T? nullableValue) where T : struct => nullableValue.HasValue;

	public static bool HasValue<T>([NotNullWhen(true)] T? nullableValue) where T : struct => nullableValue.HasValue;

	public static bool HasNoValue<T>(T? nullableValue) where T : struct => !nullableValue.HasValue;
}