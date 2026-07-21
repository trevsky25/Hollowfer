#if UNITY_EDITOR
using System;
using System.Linq;
using Hollowfen.Audio;
using Hollowfen.Map;
using Hollowfen.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring for gameplay ambience, adaptive music, region titles, and coverage.</summary>
    public static class WorldFeedbackImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string MixerPath = "Assets/_Hollowfen/Audio/MainMixer.mixer";

        [MenuItem("Hollowfen/World Feedback/Apply Regional Ambience")]
        public static void ApplyAll()
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

            var root = GetOrCreateRoot("_WorldFeedback");
            ConfigureAmbience(root);
            ConfigureArrivalToast(root);
            ConfigureMusic();

            var regions = GetOrCreateChild(root.transform, "RegionCoverage");
            ConfigureExistingRegion("RegionTrigger_Village", "village", 10);
            ConfigureExistingRegion("RegionTrigger_Wend", "wend", 12);
            ConfigureExistingRegion("RegionTrigger_OldWood", "old_wood", 15);
            ConfigureRegion(regions.transform, "RegionTrigger_VillageSouth", "village", 10,
                new Vector3(283f, 35f, 130f), new Vector3(95f, 24f, 125f));
            ConfigureRegion(regions.transform, "RegionTrigger_ClearCut", "wend", 12,
                new Vector3(145f, 34f, 365f), new Vector3(105f, 24f, 105f));
            ConfigureRegion(regions.transform, "RegionTrigger_Manor", "manor", 20,
                new Vector3(365f, 34f, 190f), new Vector3(70f, 24f, 70f));

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[WorldFeedback] Authored adaptive ambience/music, region arrival title, and six-region trigger coverage.");
        }

        private static void ConfigureAmbience(GameObject root)
        {
            var ambience = root.GetComponent<AmbienceManager>();
            if (ambience == null) ambience = root.AddComponent<AmbienceManager>();
            var serialized = new SerializedObject(ambience);
            serialized.FindProperty("_output").objectReferenceValue = MixerGroup("Master");
            serialized.FindProperty("_fadeInSeconds").floatValue = 3.5f;
            serialized.FindProperty("_regionCrossfadeSeconds").floatValue = 4f;
            serialized.FindProperty("_ceiling").floatValue = .38f;
            serialized.FindProperty("_defaultGameplayRegion").stringValue = "village";
            serialized.FindProperty("_menuRegion").stringValue = "old_wood";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ambience);
        }

        private static void ConfigureArrivalToast(GameObject root)
        {
            var toast = root.GetComponent<RegionArrivalToast>();
            if (toast == null) toast = root.AddComponent<RegionArrivalToast>();
            var serialized = new SerializedObject(toast);
            serialized.FindProperty("_initialDelay").floatValue = .75f;
            serialized.FindProperty("_fadeSeconds").floatValue = .45f;
            serialized.FindProperty("_holdSeconds").floatValue = 2.8f;
            serialized.FindProperty("_sortingOrder").intValue = 14;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(toast);
        }

        private static void ConfigureMusic()
        {
            var music = UnityEngine.Object.FindAnyObjectByType<MusicManager>(FindObjectsInactive.Include);
            if (music == null) throw new InvalidOperationException("Gameplay _Music/MusicManager is missing.");
            var serialized = new SerializedObject(music);
            serialized.FindProperty("_stateCrossfadeSeconds").floatValue = 5f;
            serialized.FindProperty("_nightVolumeScale").floatValue = .82f;

            var states = serialized.FindProperty("_regionStates");
            states.arraySize = 4;
            SetMusicState(states.GetArrayElementAtIndex(0), "village", 1f, 22000f, 14500f);
            SetMusicState(states.GetArrayElementAtIndex(1), "wend", .86f, 19000f, 11000f);
            SetMusicState(states.GetArrayElementAtIndex(2), "old_wood", .72f, 15500f, 8500f);
            SetMusicState(states.GetArrayElementAtIndex(3), "manor", .78f, 17000f, 9500f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(music);
        }

        private static void SetMusicState(SerializedProperty state, string id, float volume,
            float dayCutoff, float nightCutoff)
        {
            state.FindPropertyRelative("regionId").stringValue = id;
            state.FindPropertyRelative("track").objectReferenceValue = null;
            state.FindPropertyRelative("volumeScale").floatValue = volume;
            state.FindPropertyRelative("dayLowPassHz").floatValue = dayCutoff;
            state.FindPropertyRelative("nightLowPassHz").floatValue = nightCutoff;
        }

        private static void ConfigureExistingRegion(string objectName, string id, int priority)
        {
            var trigger = UnityEngine.Object.FindObjectsByType<RegionTrigger>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.name == objectName);
            if (trigger == null) throw new InvalidOperationException(objectName + " is missing.");
            ConfigureTrigger(trigger, id, priority);
        }

        private static void ConfigureRegion(Transform parent, string name, string id, int priority,
            Vector3 position, Vector3 size)
        {
            var regionObject = GetOrCreateChild(parent, name);
            regionObject.transform.SetPositionAndRotation(position, Quaternion.identity);
            var box = regionObject.GetComponent<BoxCollider>();
            if (box == null) box = regionObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = size;
            var trigger = regionObject.GetComponent<RegionTrigger>();
            if (trigger == null) trigger = regionObject.AddComponent<RegionTrigger>();
            ConfigureTrigger(trigger, id, priority);
            EditorUtility.SetDirty(regionObject);
            EditorUtility.SetDirty(box);
        }

        private static void ConfigureTrigger(RegionTrigger trigger, string id, int priority)
        {
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("_regionId").stringValue = id;
            serialized.FindProperty("_priority").intValue = priority;
            serialized.FindProperty("_playerTag").stringValue = "Player";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var box = trigger.GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
            EditorUtility.SetDirty(trigger);
        }

        private static AudioMixerGroup MixerGroup(string name)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) throw new InvalidOperationException("Main mixer is missing.");
            var group = mixer.FindMatchingGroups(name).FirstOrDefault();
            if (group == null) throw new InvalidOperationException("Mixer group '" + name + "' is missing.");
            return group;
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            var existing = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.parent == null && candidate.name == name);
            return existing != null ? existing.gameObject : new GameObject(name);
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var created = new GameObject(name);
            created.transform.SetParent(parent, false);
            return created;
        }
    }
}
#endif
