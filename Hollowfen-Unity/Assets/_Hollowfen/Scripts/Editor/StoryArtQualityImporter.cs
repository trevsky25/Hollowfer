using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Keeps full-screen story paintings out of Unity's lossy texture pipeline. These images are
    /// intentionally animated with a subtle push-in, so even modest block compression becomes
    /// visible on faces, paper, hair, and foliage.
    /// </summary>
    public sealed class StoryArtQualityImporter : AssetPostprocessor
    {
        private const int MaximumTextureSize = 4096;
        private const int MinimumHdWidth = 1600;
        private const int MinimumHdHeight = 900;

        private static readonly string[] StoryRoots =
        {
            "Assets/_Hollowfen/UI/StoryCards",
            "Assets/_Hollowfen/UI/StoryMoments",
        };

        private static readonly string[] RuntimePlatforms =
        {
            "Standalone",
            "WebGL",
        };

        private void OnPreprocessTexture()
        {
            if (!IsStoryArt(assetPath)) return;
            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("Hollowfen/Story/Apply HD Story Art Quality")]
        public static void ApplyAll()
        {
            string[] paths = FindStoryArtPaths();
            int changed = 0;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar("Hollowfen HD story art",
                        path, paths.Length == 0 ? 1f : i / (float)paths.Length);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || !Configure(importer)) continue;
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[StoryArtQualityImporter] HD quality applied to {changed}/{paths.Length} " +
                      "story paintings. " + Verify());
        }

        [MenuItem("Hollowfen/Verify/HD Story Art")]
        public static void VerifyMenu()
        {
            string result = Verify();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string Verify()
        {
            string[] paths = FindStoryArtPaths();
            var failures = new List<string>();

            foreach (string path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || texture == null)
                {
                    failures.Add(path + " (missing texture importer)");
                    continue;
                }

                if (texture.width < MinimumHdWidth || texture.height < MinimumHdHeight)
                    failures.Add($"{path} ({texture.width}x{texture.height})");
                else if (!HasProductionSettings(importer))
                    failures.Add(path + " (lossy or downscaled import settings)");
            }

            return failures.Count == 0
                ? $"PASS — {paths.Length} story paintings are HD, uncompressed, and mip-free."
                : "FAIL — Story art quality issues:\n" + string.Join("\n", failures.Take(20));
        }

        internal static bool Configure(TextureImporter importer)
        {
            bool changed = !HasProductionSettings(importer);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = MaximumTextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.compressionQuality = 100;
            importer.crunchedCompression = false;

            foreach (string platform in RuntimePlatforms)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                settings.name = platform;
                settings.overridden = true;
                settings.maxTextureSize = MaximumTextureSize;
                settings.format = TextureImporterFormat.Automatic;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                settings.compressionQuality = 100;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
            }

            return changed;
        }

        private static bool HasProductionSettings(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled || importer.streamingMipmaps || importer.isReadable ||
                !importer.sRGBTexture || importer.alphaSource != TextureImporterAlphaSource.None ||
                importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != MaximumTextureSize ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.compressionQuality != 100 || importer.crunchedCompression)
                return false;

            foreach (string platform in RuntimePlatforms)
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                if (!settings.overridden || settings.maxTextureSize != MaximumTextureSize ||
                    settings.textureCompression != TextureImporterCompression.Uncompressed ||
                    settings.compressionQuality != 100 || settings.crunchedCompression)
                    return false;
            }

            return true;
        }

        private static bool IsStoryArt(string path) =>
            !string.IsNullOrEmpty(path) &&
            StoryRoots.Any(root => path.StartsWith(root + "/", StringComparison.Ordinal)) &&
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        private static string[] FindStoryArtPaths() =>
            AssetDatabase.FindAssets("t:Texture2D", StoryRoots)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsStoryArt)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
    }
}
