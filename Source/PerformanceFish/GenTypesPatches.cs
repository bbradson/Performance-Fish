// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using PerformanceFish.Prepatching;

namespace PerformanceFish;

public sealed class GenTypesPatches : ClassWithFishPrepatches
{
	public sealed class AllTypes : FishPrepatch
	{
		public override string? Description { get; }
			= "Fix for thread safety. The vanilla method was rarely breaking and preventing the game from loading";

		public static readonly object Lock = new();
		
		public override MethodBase TargetMethodBase { get; }
			= AccessTools.PropertyGetter(typeof(GenTypes), nameof(GenTypes.AllTypes));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Prefix() => Monitor.Enter(Lock);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Postfix() => Monitor.Exit(Lock); // TODO: replace with finalizer
	}

	public sealed class AllTypesWithAttribute : FishPrepatch
	{
		public override string? Description { get; }
			= "Fix for thread safety. The vanilla method was rarely breaking and preventing the game from loading";

		public static readonly object Lock = new();

		public override MethodBase TargetMethodBase { get; } = methodof(GenTypes.AllTypesWithAttribute<Attribute>);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody<Attribute>);

		public static List<Type> ReplacementBody<TAttr>() where TAttr : Attribute
		{
			lock (Lock)
			{
				var typesWithAttribute = GenTypes.cachedTypesWithAttribute.TryGetValue(typeof(TAttr));

				if (typesWithAttribute is null)
				{
					GenTypes.cachedTypesWithAttribute.Add(typeof(TAttr),
						typesWithAttribute = GenTypes.AllTypes.AsParallel()
							.Where(Predicate<TAttr>()).ToList());
				}
			
				return typesWithAttribute;
			}
		}

		public static Func<Type, bool> Predicate<TAttr>() where TAttr : Attribute
			=> static type => type.HasAttribute<TAttr>();
	}

	public sealed class AllSubclasses : FishPrepatch
	{
		public override string? Description { get; }
			= "Fix for thread safety. The vanilla method was rarely breaking and preventing the game from loading";

		public static readonly object Lock = new();

		public override MethodBase TargetMethodBase { get; } = methodof(GenTypes.AllSubclasses);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static List<Type> ReplacementBody(Type baseType)
		{
			lock (Lock)
			{
				var subclasses = GenTypes.cachedSubclasses.TryGetValue(baseType);

				if (subclasses is null)
				{
					((PredicateClass)Predicate.Target).BaseType = baseType;
					GenTypes.cachedSubclasses.Add(baseType,
						subclasses = GenTypes.AllTypes.AsParallel()
							.Where(Predicate).ToList());
				}
			
				return subclasses;
			}
		}

		public static Func<Type, bool> Predicate = new PredicateClass().Invoke;

		public sealed class PredicateClass
		{
			public Type? BaseType;
			public bool Invoke(Type type) => type.IsSubclassOf(BaseType!);
		}
	}

	public sealed class AllSubclassesNonAbstract : FishPrepatch
	{
		public override string? Description { get; }
			= "Fix for thread safety, paired with a small optimization for faster loading. The vanilla method was "
			+ "rarely breaking and preventing the game from loading";

		public static readonly object Lock = new();

		public override MethodBase TargetMethodBase { get; } = methodof(GenTypes.AllSubclassesNonAbstract);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(ReplacementBody);

		public static List<Type> ReplacementBody(Type baseType)
		{
			lock (Lock)
			{
				var subclassesNonAbstract = GenTypes.cachedSubclassesNonAbstract.TryGetValue(baseType);

				if (subclassesNonAbstract is null)
				{
					GenTypes.cachedSubclassesNonAbstract.Add(baseType,
						subclassesNonAbstract = baseType.AllSubclasses().AsParallel()
							.Where(Predicate).ToList());
				}
			
				return subclassesNonAbstract;
			}
		}

		public static Func<Type, bool> Predicate = static subClass => !subClass.IsAbstract;
	}
}