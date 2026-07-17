#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring for Hollowfen's first daily NPC routines.</summary>
    public static class NPCScheduleImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string RootName = "_NPCSchedules";

        private sealed class SlotSpec
        {
            public string Label;
            public Transform Destination;
            public bool AllDay;
            public float Start;
            public float End;
            public QuestData ActiveQuest;
            public QuestData CompletedQuest;
            public string RequiredFlag;
            public string BlockedFlag;
        }

        [MenuItem("Hollowfen/NPC Schedules/Apply Village Rhythm")]
        public static void ApplyAll()
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

            var root = GetOrCreateRoot(RootName);
            var actors = GetOrCreateChild(root.transform, "Actors");

            var theo = FindActor("NPC_Theo");
            var joren = FindActor("NPC_Joren");
            var bram = FindActor("NPC_Bram");
            var pell = FindActor("NPC_Pell");
            ReparentActor(theo, actors.transform, false);
            ReparentActor(joren, actors.transform, true);
            ReparentActor(bram, actors.transform, true);
            ReparentActor(pell, actors.transform, true);

            QuestData capital = Quest("theoCapitalOffer");
            QuestData eddaApprentice = Quest("eddaApprentice");
            QuestData forgeKnife = Quest("forgeKnife");
            QuestData meetAlmy = Quest("meetAlmy");
            QuestData cottagesReopen = Quest("cottagesReopen");

            Transform theoWagon = Anchor(root.transform, "Theo_Wagon", new Vector3(291f, 37.026184f, 148f), 0f);
            Transform theoInn = Anchor(root.transform, "Theo_Pintle", new Vector3(285f, 35f, 92f), 205f);
            Transform jorenForge = Anchor(root.transform, "Joren_Forge", new Vector3(273.5f, 34.99f, 111.5f), 325f);
            Transform jorenInn = Anchor(root.transform, "Joren_Pintle", new Vector3(283f, 35f, 90f), 145f);
            Transform bramWell = Anchor(root.transform, "Bram_Well", new Vector3(284.8f, 37.026184f, 158.4f), 180f);
            Transform bramInn = Anchor(root.transform, "Bram_Pintle", new Vector3(286f, 35f, 95f), 155f);
            Transform pellWell = Anchor(root.transform, "Pell_Well", new Vector3(288f, 37.026184f, 161f), 0f);
            Transform pellInn = Anchor(root.transform, "Pell_Pintle", new Vector3(282f, 35f, 96f), 40f);

            Configure(root.transform, "Theo", theo, true, new[]
            {
                Slot("Capital offer at the Pintle", theoInn, active: capital,
                    requiredFlag: "theo_wagon_arrived"),
                Slot("Evening at the Pintle", theoInn, 18.5f, 7f, completed: eddaApprentice,
                    requiredFlag: "theo_wagon_arrived"),
                Slot("Trading at the wagon", theoWagon, requiredFlag: "theo_wagon_arrived"),
            });
            Configure(root.transform, "Joren", joren, false, new[]
            {
                Slot("Evening at the Pintle", jorenInn, 18.5f, 7f, completed: forgeKnife),
                Slot("Working at the forge", jorenForge),
            });
            Configure(root.transform, "Bram", bram, false, new[]
            {
                Slot("Evening at the Pintle", bramInn, 18.5f, 7f, completed: meetAlmy),
                Slot("At the village well", bramWell),
            });
            Configure(root.transform, "Pell", pell, false, new[]
            {
                Slot("Evening at the Pintle", pellInn, 18.5f, 7f, completed: cottagesReopen),
                Slot("At the village well", pellWell),
            });

            RepointCapitalQuest(capital);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[NPCSchedules] Authored Theo, Joren, Bram, and Pell routines; Theo's Capital waypoint now targets the Crooked Pintle.");
        }

        private static SlotSpec Slot(string label, Transform destination, QuestData active = null,
            QuestData completed = null, string requiredFlag = null, string blockedFlag = null)
        {
            return new SlotSpec
            {
                Label = label,
                Destination = destination,
                AllDay = true,
                ActiveQuest = active,
                CompletedQuest = completed,
                RequiredFlag = requiredFlag,
                BlockedFlag = blockedFlag,
            };
        }

        private static SlotSpec Slot(string label, Transform destination, float start, float end,
            QuestData active = null, QuestData completed = null, string requiredFlag = null,
            string blockedFlag = null)
        {
            return new SlotSpec
            {
                Label = label,
                Destination = destination,
                Start = start,
                End = end,
                ActiveQuest = active,
                CompletedQuest = completed,
                RequiredFlag = requiredFlag,
                BlockedFlag = blockedFlag,
            };
        }

        private static void Configure(Transform root, string id, GameObject actor, bool hideWithoutMatch,
            IReadOnlyList<SlotSpec> slots)
        {
            var host = GetOrCreateChild(root, "_Schedule_" + id);
            var schedule = host.GetComponent<NPCSchedule>();
            if (schedule == null) schedule = host.AddComponent<NPCSchedule>();
            var so = new SerializedObject(schedule);
            so.FindProperty("_actor").objectReferenceValue = actor;
            so.FindProperty("_hideWhenNoSlotMatches").boolValue = hideWithoutMatch;
            so.FindProperty("_safeRelocationDistance").floatValue = 12f;
            so.FindProperty("_pollInterval").floatValue = .35f;

            var array = so.FindProperty("_slots");
            array.arraySize = slots.Count;
            for (int i = 0; i < slots.Count; i++)
            {
                var source = slots[i];
                var entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("label").stringValue = source.Label;
                entry.FindPropertyRelative("destination").objectReferenceValue = source.Destination;
                entry.FindPropertyRelative("allDay").boolValue = source.AllDay;
                entry.FindPropertyRelative("startHour").floatValue = source.Start;
                entry.FindPropertyRelative("endHour").floatValue = source.End;
                entry.FindPropertyRelative("activeQuest").objectReferenceValue = source.ActiveQuest;
                entry.FindPropertyRelative("requiresQuestCompleted").objectReferenceValue = source.CompletedQuest;
                entry.FindPropertyRelative("requiresFlagId").stringValue = source.RequiredFlag ?? "";
                entry.FindPropertyRelative("blockedByFlagId").stringValue = source.BlockedFlag ?? "";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(schedule);
        }

        private static void RepointCapitalQuest(QuestData quest)
        {
            string guid = AssetDatabase.FindAssets("LocationData_CrookedPintle t:LocationData",
                new[] { "Assets/_Hollowfen/Data/Locations" }).FirstOrDefault();
            var location = string.IsNullOrEmpty(guid) ? null :
                AssetDatabase.LoadAssetAtPath<LocationData>(AssetDatabase.GUIDToAssetPath(guid));
            if (location == null) throw new InvalidOperationException("Crooked Pintle location data is missing.");
            var so = new SerializedObject(quest);
            so.FindProperty("_waypointLocation").objectReferenceValue = location;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
        }

        private static QuestData Quest(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:QuestData", new[] { "Assets/_Hollowfen/Data/Quests" }))
            {
                var quest = AssetDatabase.LoadAssetAtPath<QuestData>(AssetDatabase.GUIDToAssetPath(guid));
                if (quest != null && string.Equals(quest.Id, id, StringComparison.Ordinal)) return quest;
            }
            throw new InvalidOperationException("Quest '" + id + "' is missing.");
        }

        private static GameObject FindActor(string objectName)
        {
            var actor = UnityEngine.Object.FindObjectsByType<NPCInteractable>(FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.gameObject.scene.path == GameplayScene &&
                                             candidate.gameObject.name == objectName);
            if (actor == null) throw new InvalidOperationException(objectName + " is missing from gameplay scene.");
            return actor.gameObject;
        }

        private static void ReparentActor(GameObject actor, Transform parent, bool active)
        {
            actor.transform.SetParent(parent, true);
            actor.SetActive(active);
            EditorUtility.SetDirty(actor);
        }

        private static Transform Anchor(Transform root, string name, Vector3 position, float yaw)
        {
            var anchors = GetOrCreateChild(root, "Anchors");
            var anchor = GetOrCreateChild(anchors.transform, name);
            anchor.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return anchor.transform;
        }

        private static GameObject GetOrCreateRoot(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go : new GameObject(name);
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
#endif
