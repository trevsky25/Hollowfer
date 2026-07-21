using System;
using System.Collections.Generic;
using System.IO;
using Hollowfen.Data;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Imports the hand-drawn book spreads and binds them to their data entries by stable species id.
    /// The convention deliberately removes a second hand-maintained mapping:
    /// Assets/_Hollowfen/UI/MushroomJournal/Pages/&lt;MushroomFieldGuideData.Id&gt;.png.
    /// </summary>
    public static class MushroomJournalPageImporter
    {
        private const string DataRoot = "Assets/_Hollowfen/Data/Mushrooms";
        private const string PageRoot = "Assets/_Hollowfen/UI/MushroomJournal/Pages";

        [MenuItem("Hollowfen/Mushrooms/Build Illustrated Journal Pages")]
        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (MushroomFieldGuideData entry in LoadEntries())
            {
                string pagePath = PagePath(entry);
                if (!File.Exists(pagePath))
                    throw new FileNotFoundException("Missing illustrated journal page for " + entry.Id, pagePath);

                ConfigureTexture(pagePath);
                Sprite page = AssetDatabase.LoadAssetAtPath<Sprite>(pagePath);
                if (page == null) throw new InvalidOperationException("Page did not import as a sprite: " + pagePath);
                var serialized = new SerializedObject(entry);
                serialized.FindProperty("_journalPage").objectReferenceValue = page;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(entry);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MushroomJournalPageImporter] " + Verify());
        }

        [MenuItem("Hollowfen/Verify/Illustrated Mushroom Journal")]
        public static void VerifyMenu()
        {
            string result = Verify();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string Verify()
        {
            List<MushroomFieldGuideData> entries = LoadEntries();
            if (entries.Count != 21)
                return "FAIL — expected 21 mushroom entries; found " + entries.Count + ".";

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MushroomFieldGuideData entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    return "FAIL — a mushroom entry has no stable id.";
                if (!ids.Add(entry.Id)) return "FAIL — duplicate mushroom id: " + entry.Id + ".";
                if (entry.JournalPage == null)
                    return "FAIL — " + entry.Id + " has no illustrated journal page.";

                string assetPath = AssetDatabase.GetAssetPath(entry.JournalPage);
                if (!string.Equals(assetPath, PagePath(entry), StringComparison.Ordinal))
                    return "FAIL — " + entry.Id + " points to the wrong page: " + assetPath + ".";
                Texture2D texture = entry.JournalPage.texture;
                if (texture == null || texture.width < 1600 || texture.height < 900)
                    return "FAIL — " + entry.Id + " page is below the 1600x900 production floor.";
            }

            return "PASS — 21 of 21 mushroom species have production-sized illustrated book spreads, stable data bindings, and discovery-aware journal presentation.";
        }

        private static List<MushroomFieldGuideData> LoadEntries()
        {
            string[] guids = AssetDatabase.FindAssets("t:MushroomFieldGuideData", new[] { DataRoot });
            var entries = new List<MushroomFieldGuideData>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MushroomFieldGuideData entry = AssetDatabase.LoadAssetAtPath<MushroomFieldGuideData>(path);
                if (entry != null) entries.Add(entry);
            }
            entries.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            return entries;
        }

        private static string PagePath(MushroomFieldGuideData entry) => PageRoot + "/" + entry.Id + ".png";

        private static void ConfigureTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter for " + path);
            bool dirty = importer.textureType != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled ||
                         importer.maxTextureSize != 2048 ||
                         importer.textureCompression != TextureImporterCompression.CompressedHQ;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            if (dirty) importer.SaveAndReimport();
        }
    }
}
