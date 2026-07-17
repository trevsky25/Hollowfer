#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hollowfen.Data;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    public static class DataImporter
    {
        [Serializable]
        private class StoryCardJson
        {
            public string id;
            public string act;
            public string scene;
            public string title;
            public string subtitle;
            public string body;
            public string wrenNote;
            public string image;
            public string questId;
            public int unlockAt;
            public string[] beats;
        }

        [Serializable]
        private class StoryCardList
        {
            public StoryCardJson[] items;
        }

        [Serializable]
        private class MushroomJson
        {
            public string id;
            public string commonName;
            public string latinName;
            public string edibility;
            public string edibilityLabel;
            public string description;
            public string[] idFeatures;
            public string habitat;
            public string season;
            public string lookalikes;
            public string notes;
            public string photo;
            public string photoCredit;
        }

        [Serializable]
        private class MushroomList
        {
            public MushroomJson[] items;
        }

        public static string ImportStoryCards(string jsonPath, string outFolder, string spriteFolder)
        {
            var raw = File.ReadAllText(jsonPath);
            var list = JsonUtility.FromJson<StoryCardList>(raw);
            if (list == null || list.items == null) return "ERROR: failed to parse json";
            if (!AssetDatabase.IsValidFolder(outFolder)) Directory.CreateDirectory(outFolder);
            int i = 0;
            int total = list.items.Length;
            int created = 0;
            foreach (var c in list.items)
            {
                i++;
                var so = ScriptableObject.CreateInstance<StoryCardData>();
                SetField(so, "_id", c.id);
                SetField(so, "_act", c.act ?? "");
                SetField(so, "_scene", c.scene ?? "");
                SetField(so, "_title", c.title ?? "");
                SetField(so, "_subtitle", c.subtitle ?? "");
                SetField(so, "_body", c.body ?? "");
                SetField(so, "_wrenNote", c.wrenNote ?? "");
                SetField(so, "_beats", c.beats ?? Array.Empty<string>());
                SetField(so, "_unlockAt", c.unlockAt);
                SetField(so, "_questId", c.questId ?? "");
                SetField(so, "_displayNameId", $"story.{c.id}.title");
                SetField(so, "_descriptionId", $"story.{c.id}.body");

                if (!string.IsNullOrEmpty(c.image))
                {
                    var spritePath = $"{spriteFolder}/{c.image}.png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) Debug.LogWarning($"[DataImporter] Sprite not found at {spritePath} for card {c.id}");
                    SetField(so, "_image", sprite);
                }

                var assetName = $"StoryCard_{NumberPrefix(i, total)}_{ToPascal(c.id)}.asset";
                var assetPath = $"{outFolder}/{assetName}";
                AssetDatabase.CreateAsset(so, assetPath);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"created {created}/{total} StoryCard SOs in {outFolder}";
        }

        public static string ImportMushrooms(string jsonPath, string outFolder, string spriteFolder)
        {
            var raw = File.ReadAllText(jsonPath);
            var list = JsonUtility.FromJson<MushroomList>(raw);
            if (list == null || list.items == null) return "ERROR: failed to parse json";
            if (!AssetDatabase.IsValidFolder(outFolder)) Directory.CreateDirectory(outFolder);
            int i = 0;
            int total = list.items.Length;
            int created = 0;
            foreach (var m in list.items)
            {
                i++;
                var so = ScriptableObject.CreateInstance<MushroomFieldGuideData>();
                SetField(so, "_id", m.id);
                SetField(so, "_commonName", m.commonName ?? "");
                SetField(so, "_latinName", m.latinName ?? "");
                SetField(so, "_edibility", ParseEdibility(m.edibility));
                SetField(so, "_edibilityLabel", m.edibilityLabel ?? "");
                SetField(so, "_description", m.description ?? "");
                SetField(so, "_idFeatures", m.idFeatures ?? Array.Empty<string>());
                SetField(so, "_habitat", m.habitat ?? "");
                SetField(so, "_season", m.season ?? "");
                SetField(so, "_lookalikes", m.lookalikes ?? "");
                SetField(so, "_notes", m.notes ?? "");
                SetField(so, "_photoCredit", m.photoCredit ?? "");
                SetField(so, "_displayNameId", $"mushroom.{m.id}.name");
                SetField(so, "_descriptionId", $"mushroom.{m.id}.description");

                if (!string.IsNullOrEmpty(m.photo))
                {
                    var spritePath = $"{spriteFolder}/{m.photo}.png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                    if (sprite == null) Debug.LogWarning($"[DataImporter] Sprite not found at {spritePath} for mushroom {m.id}");
                    SetField(so, "_photo", sprite);
                }

                string worldModelPath = MushroomModelImporter.WorldPrefabPathForSpecies(m.id);
                if (!string.IsNullOrEmpty(worldModelPath))
                {
                    var worldModel = AssetDatabase.LoadAssetAtPath<GameObject>(worldModelPath);
                    if (worldModel == null) Debug.LogWarning($"[DataImporter] World model not found at {worldModelPath} for mushroom {m.id}");
                    SetField(so, "_worldPrefab", worldModel);
                }

                string journalModelPath = MushroomModelImporter.JournalPrefabPathForSpecies(m.id);
                if (!string.IsNullOrEmpty(journalModelPath))
                {
                    var journalModel = AssetDatabase.LoadAssetAtPath<GameObject>(journalModelPath);
                    if (journalModel == null) Debug.LogWarning($"[DataImporter] Journal model not found at {journalModelPath} for mushroom {m.id}");
                    SetField(so, "_journalPreviewPrefab", journalModel);
                }
                SetField(so, "_journalExposure", MushroomModelImporter.JournalExposureForSpecies(m.id));

                var assetName = $"Mushroom_{NumberPrefix(i, total)}_{ToPascal(m.id)}.asset";
                var assetPath = $"{outFolder}/{assetName}";
                AssetDatabase.CreateAsset(so, assetPath);
                created++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"created {created}/{total} Mushroom SOs in {outFolder}";
        }

        private static Edibility ParseEdibility(string s)
        {
            if (string.IsNullOrEmpty(s)) return Edibility.Unknown;
            switch (s.ToLowerInvariant())
            {
                case "edible": return Edibility.Edible;
                case "deadly": return Edibility.Deadly;
                case "magic": return Edibility.Psychoactive;
                case "psychoactive": return Edibility.Psychoactive;
                case "medicinal": return Edibility.Medicinal;
                default: return Edibility.Unknown;
            }
        }

        private static string NumberPrefix(int i, int total)
        {
            int width = total < 100 ? 2 : 3;
            return i.ToString().PadLeft(width, '0');
        }

        private static string ToPascal(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Untitled";
            var parts = id.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new System.Text.StringBuilder();
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
            return sb.ToString();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var t = target.GetType();
            var f = t.GetField(fieldName, System.Reflection.BindingFlags.Instance |
                                          System.Reflection.BindingFlags.NonPublic |
                                          System.Reflection.BindingFlags.Public);
            if (f == null) { Debug.LogWarning($"[DataImporter] Field '{fieldName}' not found on {t.Name}"); return; }
            f.SetValue(target, value);
        }
    }
}
#endif
