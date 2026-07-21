#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Single catalogue writer shared by every idempotent restoration authoring pass.</summary>
    public static class RestorationCatalogueImporter
    {
        private const string DataRoot = "Assets/_Hollowfen/Data/Restoration";
        private const string DatabasePath =
            "Assets/_Hollowfen/Resources/RestorationProjectDatabase.asset";

        private static readonly Dictionary<string, int> DisplayOrder =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "cottages", 0 },
                { "wend_bridge", 1 },
                { "jorens_forge", 2 },
                { "chapel_garden", 3 },
                { "crooked_pintle", 4 },
                { "witch_cottage", 5 },
                { "tobin_workshop", 6 },
            };

        public static RestorationProjectDatabase UpsertDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<RestorationProjectDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<RestorationProjectDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var projects = AssetDatabase.FindAssets("t:RestorationProjectData", new[] { DataRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<RestorationProjectData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(project => project != null && !string.IsNullOrWhiteSpace(project.Id))
                .OrderBy(project => DisplayOrder.TryGetValue(project.Id, out int order)
                    ? order : int.MaxValue)
                .ThenBy(project => project.Id, StringComparer.Ordinal)
                .ToArray();

            var field = typeof(RestorationProjectDatabase).GetField("_projects",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(nameof(RestorationProjectDatabase), "_projects");
            field.SetValue(database, projects);
            EditorUtility.SetDirty(database);
            return database;
        }
    }
}
#endif
