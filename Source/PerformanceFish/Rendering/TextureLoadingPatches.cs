// Copyright (c) 2023 bradson
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using FisheryLib.Pools;
using PerformanceFish.Prepatching;
using RimWorld.IO;

namespace PerformanceFish.Rendering;

public sealed class TextureLoadingPatches : ClassWithFishPrepatches
{
	public const string DDS_EXTENSION = ".dds";
	
	public sealed class ModContentLoader_StaticConstructor : FishPrepatch
	{
		public override string? Description { get; }
			= "Adds .dds to the accepted extensions for texture loading. Those files load faster than png, render at "
			+ "better quality and help prevent memory issues";

		public override MethodBase TargetMethodBase { get; }
			= AccessTools.Constructor(typeof(ModContentLoader<>), searchForStatic: true);

		public static void Postfix<T>() where T : class
			=> ModContentLoader<T>.AcceptableExtensionsTexture
				= ModContentLoader<T>.AcceptableExtensionsTexture.Add(DDS_EXTENSION);
	}
	
	public sealed class ModContentLoader_LoadItem : FishPrepatch
	{
		public override string? Description { get; }
			= "Adds .dds to the accepted extensions for texture loading. Those files load faster than png, render at "
			+ "better quality and help prevent memory issues";

		public override MethodBase TargetMethodBase { get; } = methodof(ModContentLoader<Texture2D>.LoadItem);

		public static bool Prefix<T>(VirtualFile file, ref LoadedContentItem<T>? __result) where T : class
		{
			if (typeof(T) != typeof(Texture2D)
				|| (GetFileInfo(file)?.Extension.Equals(DDS_EXTENSION, StringComparison.OrdinalIgnoreCase) ?? true)
				|| !File.Exists(Path.ChangeExtension(file.FullPath, DDS_EXTENSION)))
			{
				return true;
			}

			__result = null;
			return false;
		}
	}
	
	public sealed class ModContentLoader_LoadTexture : FishPrepatch
	{
		public override string? Description { get; }
			= "Adds .dds to the accepted extensions for texture loading. Those files load faster than png, render at "
			+ "better quality and help prevent memory issues";

		public override MethodBase TargetMethodBase { get; } = methodof(ModContentLoader<Texture2D>.LoadTexture);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
			=> ilProcessor.ReplaceBodyWith(LoadTexture);

		public static Texture2D? LoadTexture(VirtualFile file)
		{
			if (!file.Exists)
				return null;

			Texture2D? texture2D;

			var fileInfo = GetFileInfo(file);
			var isDds = fileInfo?.Extension.Equals(DDS_EXTENSION, StringComparison.OrdinalIgnoreCase) ?? false;
			var hasStoredMipMaps = false;
			
			if (isDds)
			{
				texture2D = DDSLoader.LoadDDS(fileInfo!.OpenRead(), out hasStoredMipMaps);
				
				if (!DDSLoader.error.NullOrEmpty())
					LogDDSLoadingError(file);

				if (texture2D is null)
				{
					isDds = false;
					if ((texture2D = LoadTextureUsingFallbackFormat(file)) is null)
						return null;
				}
			}
			else
			{
				var data = file.ReadAllBytes();
				texture2D = new(2, 2, TextureFormat.Alpha8, true);
				texture2D.LoadImage(data);
				FixMipMapsIfNeeded(texture2D, data, file);
			}
			
			if (!isDds && Prefs.TextureCompression)
				texture2D.Compress(true);
			
			texture2D.name = Path.GetFileNameWithoutExtension(file.Name);
			texture2D.filterMode = FilterMode.Trilinear;
			texture2D.anisoLevel = 0;
			
			if (texture2D.mipmapCount > 1)
				texture2D.mipMapBias = -0.7f;
			
			texture2D.Apply(!hasStoredMipMaps, true);
			return texture2D;
		}

		public static Texture2D? LoadTextureUsingFallbackFormat(VirtualFile file)
		{
			if (ReadBytesForFallbackFormat(file) is not { } data)
			{
				Log.Error($"Could not load texture at {file.FullPath}.");
				return null;
			}

			var texture2D = new Texture2D(2, 2, TextureFormat.Alpha8, true);
			texture2D.LoadImage(data);
			FixMipMapsIfNeeded(texture2D, data, file);
			return texture2D;
		}

		public static void FixMipMapsIfNeeded(Texture2D texture2D, byte[] data, VirtualFile file)
		{
			if (((texture2D.width & 3) == 0) & ((texture2D.height & 3) == 0))
				return;

			if (Prefs.LogVerbose)
			{
				Log.Warning($"Texture does not support mipmapping, needs to be divisible by 4 ({
					texture2D.width}x{texture2D.height}) for '{file.Name}'");
			}

			texture2D = new(2, 2, TextureFormat.Alpha8, mipChain: false);
			texture2D.LoadImage(data);
		}

		private static byte[]? ReadBytesForFallbackFormat(VirtualFile file)
		{
			byte[]? data = null;
			foreach (var acceptableExtension in ModContentLoader<Texture2D>.AcceptableExtensionsTexture)
			{
				if (acceptableExtension == DDS_EXTENSION)
					continue;

				var fallbackPath = Path.ChangeExtension(file.FullPath, acceptableExtension);
				if (!File.Exists(fallbackPath))
					continue;

				Log.Warning($"Could not load .dds at {file.FullPath}. Loading as {acceptableExtension} instead.");
				data = File.ReadAllBytes(fallbackPath);
				break;
			}

			return data;
		}

		public static void LogDDSLoadingError(VirtualFile file)
			=> Log.Warning($"DDS error at '{file.FullPath}': {DDSLoader.error}");
	}

	public static FileInfo? GetFileInfo(VirtualFile file)
		=> file is FilesystemFile systemFile ? systemFile.fileInfo : null;

	public sealed class GetAllFilesForModPatch : FishPrepatch
	{
		public override string? Description { get; }
			= "Adds .dds to the accepted extensions for texture loading. Those files load faster than png, render at "
			+ "better quality and help prevent memory issues";

		public override MethodBase TargetMethodBase { get; } = methodof(ModContentPack.GetAllFilesForMod);

		public override void Transpiler(ILProcessor ilProcessor, ModuleDefinition module)
		{
			var index = ilProcessor.instructions.FirstIndexOf(static i
				=> i.OpCode == OpCodes.Newobj
				&& i.Operand is MethodReference method
				&& method.DeclaringType.Name == "Dictionary`2");

			ilProcessor.instructions[index].Operand = module.ImportReference(AccessTools.Constructor(
				typeof(Dictionary<string, FileInfo>), [typeof(IEqualityComparer<string>)]));
			ilProcessor.InsertAt(index, OpCodes.Call, AccessTools.PropertyGetter(typeof(StringComparer),
				nameof(StringComparer.OrdinalIgnoreCase)));
		}

		public static void Postfix(Dictionary<string, FileInfo> __result)
		{
			using var keysToRemove = new PooledIList<List<string>>();
			using var sb = new PooledStringBuilder();

			foreach (var pair in __result)
			{
				if (pair.Key.EndsWith(DDS_EXTENSION, StringComparison.OrdinalIgnoreCase))
					continue;

				sb.Append(pair.Key);
				var extensionLength = pair.Value.Extension.Length;
				sb.Remove(pair.Key.Length - extensionLength, extensionLength);
				sb.Append(DDS_EXTENSION);

				if (__result.ContainsKey(sb.ToString()))
					keysToRemove.List.Add(pair.Key);

				sb.Clear();
			}

			foreach (var key in keysToRemove.List)
				__result.Remove(key);
		}
	}
}