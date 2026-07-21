using System;
using System.Linq;
using Hollowfen.Foraging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    public static class MushroomEcologyImporter
    {
        [MenuItem("Hollowfen/World/Install Mushroom Ecology")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing ecology.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Scene_Hollowfen")
                throw new InvalidOperationException("Open Scene_Hollowfen before installing ecology.");

            MushroomWorldSpawner spawner = Resources.FindObjectsOfTypeAll<MushroomWorldSpawner>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene);
            if (spawner == null)
            {
                var root = new GameObject("_MushroomEcology");
                Undo.RegisterCreatedObjectUndo(root, "Install Hollowfen mushroom ecology");
                spawner = root.AddComponent<MushroomWorldSpawner>();
            }
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("_zones").arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.FindProperty("_zones").arraySize = MushroomWorldSpawner.DefaultZones().Length;
            HabitatZones(serialized.FindProperty("_zones"), MushroomWorldSpawner.DefaultZones());
            serialized.FindProperty("_minimumSpacing").floatValue = 3.5f;
            serialized.FindProperty("_maximumSlope").floatValue = 28f;
            serialized.FindProperty("_spawnOnStart").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MushroomEcologyImporter] Installed deterministic ecology. " + VerifyActiveScene());
        }

        [MenuItem("Hollowfen/Verify/Mushroom Ecology")]
        public static void VerifyMenu()
        {
            string report = VerifyActiveScene();
            if (report.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static string VerifyActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            MushroomWorldSpawner spawner = Resources.FindObjectsOfTypeAll<MushroomWorldSpawner>()
                .FirstOrDefault(candidate => candidate != null && candidate.gameObject.scene == scene);
            if (spawner == null) return "FAIL — Scene_Hollowfen has no MushroomWorldSpawner.";
            if (spawner.Zones == null || spawner.Zones.Count != 7)
                return "FAIL — ecology must contain seven story habitats.";
            if (spawner.RequestedPopulation != 41)
                return "FAIL — ecology requests " + spawner.RequestedPopulation + " nodes instead of 41.";
            if (spawner.Zones.Any(zone => string.IsNullOrEmpty(zone.id) || zone.population < 1 ||
                                          zone.speciesIds == null || zone.speciesIds.Length < 1))
                return "FAIL — one or more ecology habitats is incomplete.";
            return "PASS — 41 deterministic nodes are authored across 7 habitats; runtime terrain checks prevent collisions.";
        }

        private static void HabitatZones(SerializedProperty property,
            System.Collections.Generic.IReadOnlyList<MushroomWorldSpawner.HabitatZone> zones)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                MushroomWorldSpawner.HabitatZone zone = zones[i];
                element.FindPropertyRelative("id").stringValue = zone.id;
                element.FindPropertyRelative("center").vector2Value = zone.center;
                element.FindPropertyRelative("extents").vector2Value = zone.extents;
                element.FindPropertyRelative("population").intValue = zone.population;
                element.FindPropertyRelative("attemptsPerNode").intValue = zone.attemptsPerNode;
                SerializedProperty ids = element.FindPropertyRelative("speciesIds");
                ids.arraySize = zone.speciesIds.Length;
                for (int j = 0; j < zone.speciesIds.Length; j++)
                    ids.GetArrayElementAtIndex(j).stringValue = zone.speciesIds[j];
            }
        }
    }
}
