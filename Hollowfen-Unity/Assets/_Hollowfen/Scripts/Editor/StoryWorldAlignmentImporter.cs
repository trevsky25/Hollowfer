#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Map;
using Hollowfen.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Keeps the canonical quest route, current objective copy, and physical NPC staging in
    /// agreement. Reapplying this pass is safe after other world or schedule importers.
    /// </summary>
    public static class StoryWorldAlignmentImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string RouterName = "_QuestWaypointRouting";

        private sealed class RouteSpec
        {
            public QuestData Quest;
            public LocationData Location;
            public string ObjectiveTextId;
            public string[] RequiredFlags = Array.Empty<string>();
            public string[] BlockedFlags = Array.Empty<string>();
            public MushroomFieldGuideData RequiredForage;
            public string RequiredItemId;
        }

        [MenuItem("Hollowfen/Story/Apply Story-World Alignment")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying story-world alignment.");
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

            NPCScheduleImporter.ApplyAll();
            ApplyQuestWaypoints();
            ConfigureRouter();

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[StoryWorldAlignment] Applied staged objectives, canonical waypoints, and nine whole-map NPC routines.");
        }

        private static void ApplyQuestWaypoints()
        {
            SetWaypoint("speakBram", "village_well");
            SetWaypoint("firstTax", "theos_wagon");
            SetWaypoint("caldenWarning", "fathers_mill");
            SetWaypoint("aldricLetter", "fathers_mill");
            SetWaypoint("theoCapitalOffer", "crooked_pintle");
            SetWaypoint("eddaApprentice", "fathers_mill");
        }

        private static void ConfigureRouter()
        {
            GameObject root = GameObject.Find(RouterName);
            if (root == null) root = new GameObject(RouterName);
            QuestWaypointRouter router = root.GetComponent<QuestWaypointRouter>();
            if (router == null) router = root.AddComponent<QuestWaypointRouter>();

            QuestData almyTeach = Quest("almyTeach");
            QuestData edsGrandfather = Quest("edsGrandfather");
            QuestData caldenWarning = Quest("caldenWarning");
            MushroomFieldGuideData brightspore = Mushroom("brightspore");

            var routes = new List<RouteSpec>
            {
                Route(almyTeach, "fathers_mill", "quest.almyTeach.stage.plant",
                    requiredFlags: new[] { "act2_started" }),
                Route(almyTeach, "almy_doorway", "quest.almyTeach.stage.lesson"),

                Route(edsGrandfather, "eddas_cottage", "quest.edsGrandfather.stage.return",
                    requiredFlags: new[] { "edda_check_due" }),
                Route(edsGrandfather, "eddas_cottage", "quest.edsGrandfather.stage.wait",
                    requiredFlags: new[] { "tonic_delivered" }),
                Route(edsGrandfather, "eddas_cottage", "quest.edsGrandfather.stage.deliver",
                    requiredItemId: "item.brightspore_tonic"),
                Route(edsGrandfather, "marra_kitchen", "quest.edsGrandfather.stage.brew",
                    requiredForage: brightspore),
                Route(edsGrandfather, "old_wood_edge", "quest.edsGrandfather.stage.forage",
                    requiredFlags: new[] { "almy_brightspore_told" }),
                Route(edsGrandfather, "almy_doorway", "quest.edsGrandfather.stage.almy",
                    requiredFlags: new[] { "edda_ask_heard" }),
                Route(edsGrandfather, "eddas_cottage", "quest.edsGrandfather.stage.ask"),

                Route(caldenWarning, "chapel", "quest.caldenWarning.stage.chapel",
                    requiredFlags: new[] { "calden_warning_received" }),
                Route(caldenWarning, "fathers_mill", "quest.caldenWarning.stage.mill"),
            };

            var serialized = new SerializedObject(router);
            SerializedProperty array = serialized.FindProperty("_routes");
            array.arraySize = routes.Count;
            for (int i = 0; i < routes.Count; i++)
            {
                RouteSpec source = routes[i];
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("quest").objectReferenceValue = source.Quest;
                element.FindPropertyRelative("location").objectReferenceValue = source.Location;
                element.FindPropertyRelative("objectiveTextId").stringValue = source.ObjectiveTextId;
                WriteStrings(element.FindPropertyRelative("requiresFlagIds"), source.RequiredFlags);
                WriteStrings(element.FindPropertyRelative("blockedByFlagIds"), source.BlockedFlags);
                element.FindPropertyRelative("requiresForage").objectReferenceValue = source.RequiredForage;
                element.FindPropertyRelative("requiresItemId").stringValue = source.RequiredItemId ?? "";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(router);
        }

        private static RouteSpec Route(QuestData quest, string locationId, string objectiveTextId,
            string[] requiredFlags = null, string[] blockedFlags = null,
            MushroomFieldGuideData requiredForage = null, string requiredItemId = null)
        {
            return new RouteSpec
            {
                Quest = quest,
                Location = Location(locationId),
                ObjectiveTextId = objectiveTextId,
                RequiredFlags = requiredFlags ?? Array.Empty<string>(),
                BlockedFlags = blockedFlags ?? Array.Empty<string>(),
                RequiredForage = requiredForage,
                RequiredItemId = requiredItemId,
            };
        }

        private static void WriteStrings(SerializedProperty array, IReadOnlyList<string> values)
        {
            array.arraySize = values != null ? values.Count : 0;
            for (int i = 0; i < array.arraySize; i++)
                array.GetArrayElementAtIndex(i).stringValue = values[i] ?? "";
        }

        private static void SetWaypoint(string questId, string locationId)
        {
            QuestData quest = Quest(questId);
            var serialized = new SerializedObject(quest);
            serialized.FindProperty("_waypointLocation").objectReferenceValue = Location(locationId);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(quest);
        }

        private static QuestData Quest(string id) => FindById<QuestData>(
            "Assets/_Hollowfen/Data/Quests", asset => asset.Id, id);

        private static LocationData Location(string id) => FindById<LocationData>(
            "Assets/_Hollowfen/Data/Locations", asset => asset.Id, id);

        private static MushroomFieldGuideData Mushroom(string id) =>
            FindById<MushroomFieldGuideData>("Assets/_Hollowfen/Data/Mushrooms", asset => asset.Id, id);

        private static T FindById<T>(string root, Func<T, string> id, string expected) where T : UnityEngine.Object
        {
            T result = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null && string.Equals(id(asset), expected,
                    StringComparison.Ordinal));
            if (result == null) throw new InvalidOperationException(
                typeof(T).Name + " '" + expected + "' is missing beneath " + root + ".");
            return result;
        }
    }
}
#endif
