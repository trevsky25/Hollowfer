#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.GameTime;
using Hollowfen.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent scene authoring for the time-of-day rig and village practical lights.</summary>
    public static class DayNightLightingImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string LightRootName = "_NightLighting";

        private readonly struct Practical
        {
            public readonly string Name;
            public readonly string LocationId;
            public readonly Vector3 Offset;
            public readonly float Range;
            public readonly float Intensity;

            public Practical(string name, string locationId, Vector3 offset, float range, float intensity)
            {
                Name = name;
                LocationId = locationId;
                Offset = offset;
                Range = range;
                Intensity = intensity;
            }
        }

        // These are deliberately broad, shadowless window/hearth pools rather than visible props.
        // A later world-dressing pass can move or replace the fixtures without changing the cycle.
        private static readonly Practical[] Practicals =
        {
            new Practical("CrookedPintle", "crooked_pintle", new Vector3(0f, 1.6f, 0f), 8.5f, 3.0f),
            new Practical("JorensForge", "jorens_forge", new Vector3(0f, 1.45f, 0f), 7.0f, 2.8f),
            new Practical("VillageWell", "village_well", new Vector3(0f, 1.25f, 0f), 5.5f, 2.2f),
            new Practical("EddasCottage", "eddas_cottage", new Vector3(0f, 1.5f, 0f), 6.5f, 2.5f),
            new Practical("Chapel", "chapel", new Vector3(0f, 1.7f, 0f), 7.0f, 2.4f),
            new Practical("FathersMill", "fathers_mill", new Vector3(0f, 1.5f, 0f), 7.5f, 2.6f),
        };

        [MenuItem("Hollowfen/Time of Day/Apply Lighting Rig")]
        public static void ApplyAll()
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

            WireTimeManager();
            AuthorPracticals();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[DayNightLighting] Authored the clock lighting rig and {Practicals.Length} warm village practicals.");
        }

        private static void WireTimeManager()
        {
            var clockObject = GameObject.Find("_TimeManager");
            if (clockObject == null) throw new InvalidOperationException("_TimeManager is missing from gameplay scene.");
            var clock = clockObject.GetComponent<TimeManager>();
            if (clock == null) throw new InvalidOperationException("_TimeManager has no TimeManager component.");
            var lighting = clockObject.GetComponent<DayNightLighting>();
            if (lighting == null) lighting = clockObject.AddComponent<DayNightLighting>();

            Light sun = RenderSettings.sun;
            var lightingSo = new SerializedObject(lighting);
            lightingSo.FindProperty("_keyLight").objectReferenceValue = sun;
            lightingSo.ApplyModifiedPropertiesWithoutUndo();

            var clockSo = new SerializedObject(clock);
            clockSo.FindProperty("_sun").objectReferenceValue = sun;
            clockSo.FindProperty("_lighting").objectReferenceValue = lighting;
            clockSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clockObject);
        }

        private static void AuthorPracticals()
        {
            var markers = UnityEngine.Object.FindObjectsByType<LocationMarker>(FindObjectsInactive.Include)
                .Where(marker => marker.gameObject.scene.path == GameplayScene)
                .ToDictionary(marker => marker.Id, StringComparer.Ordinal);

            var root = GameObject.Find(LightRootName);
            if (root == null) root = new GameObject(LightRootName);

            foreach (var practical in Practicals)
            {
                if (!markers.TryGetValue(practical.LocationId, out var marker))
                    throw new InvalidOperationException("Missing LocationMarker '" + practical.LocationId + "'.");

                string objectName = "_NightLight_" + practical.Name;
                var child = root.transform.Find(objectName);
                GameObject go;
                if (child != null)
                {
                    go = child.gameObject;
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component is Transform) continue;
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }
                else
                {
                    go = new GameObject(objectName);
                    go.transform.SetParent(root.transform, false);
                }

                go.transform.position = marker.WorldPosition + practical.Offset;
                go.transform.rotation = Quaternion.identity;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, .48f, .20f, 1f);
                light.range = practical.Range;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                light.enabled = false;

                var nightLight = go.AddComponent<NightLight>();
                var so = new SerializedObject(nightLight);
                so.FindProperty("_light").objectReferenceValue = light;
                so.FindProperty("_nightIntensity").floatValue = practical.Intensity;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(go);
            }
        }
    }
}
#endif
