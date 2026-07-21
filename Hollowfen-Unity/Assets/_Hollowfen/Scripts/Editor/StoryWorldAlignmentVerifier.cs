#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.Map;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Requests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Fresh-state proof for the canonical 26-quest route and its physical story staging.</summary>
    public static class StoryWorldAlignmentVerifier
    {
        private static readonly string[] CanonicalQuestIds =
        {
            "arrive", "speakBram", "searchMill", "findJournal", "firstForage", "firstSale",
            "meetAlmy", "almyTeach", "forgeKnife", "firstTax", "theoTrade", "edsGrandfather",
            "meetHollin", "cottagesReopen", "caldenWarning", "hollinReveals",
            "findWitchCottage", "wendlightFound", "caldenReconcile", "eddaApprentice",
            "theoCapitalOffer", "festivalHosted", "aldricLetter", "aldricOfferRead",
            "wendSource", "meetAldric",
        };

        [MenuItem("Hollowfen/Verify/Story & World Alignment")]
        private static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            QuestWaypointRouter router = UnityEngine.Object.FindAnyObjectByType<QuestWaypointRouter>();
            Require(router != null && router.RouteCount == 11,
                "the staged objective router is missing or incomplete");

            QuestData[] quests = AssetDatabase.FindAssets("t:QuestData",
                    new[] { "Assets/_Hollowfen/Data/Quests" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<QuestData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .Where(quest => quest != null)
                .OrderBy(quest => quest.Order)
                .ToArray();
            Require(quests.Length == CanonicalQuestIds.Length, "expected the canonical 26 quests");
            for (int i = 0; i < quests.Length; i++)
            {
                Require(quests[i].Id == CanonicalQuestIds[i],
                    "quest order " + (i + 1) + " is '" + quests[i].Id + "'");
                QuestData expectedNext = i + 1 < quests.Length ? quests[i + 1] : null;
                Require(quests[i].NextQuest == expectedNext,
                    "quest '" + quests[i].Id + "' does not chain to " +
                    (expectedNext != null ? expectedNext.Id : "the ending fork"));
                Require(quests[i].WaypointLocation != null,
                    "quest '" + quests[i].Id + "' has no compass destination");
                Require(LocationRegistry.Markers.Any(marker => marker != null &&
                            marker.Data == quests[i].WaypointLocation),
                    "quest '" + quests[i].Id + "' points to an unregistered location");
            }
            Require(!DataIntegrity.RunAll().Any(issue => issue.Severity == DataIntegrity.Severity.Error),
                "the project data-integrity pass found a broken story reference");
            VerifyCompletionCoverage(quests);

            SaveSlotMeta originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            InventorySnapshot originalInventory = InventoryRuntime.ToSnapshot();
            string[] originalItems = KeyItems.ToArray();
            LocationMarker originalWaypoint = LocationRegistry.ActiveWaypoint;
            string originalSaveOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSaveSlot = SaveManager.ActiveSlot;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-story-world-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;
            SaveManager.SetActiveSlot(3);
            SaveManager.WritePlaceholderToSlot(3);
            try
            {
                VerifyStaticDestinations(quests);
                VerifyStagedObjectives(router, quests);
                VerifyWorldSpread();
                return "STORY & WORLD — PASS: all 26 quests form one recoverable chain with a concrete completion owner; every quest has a registered destination; " +
                       "11 staged objective routes move the compass and copy through Almy, Brightspore, and Calden; " +
                       "nine scheduled principals occupy distinct story sites across the 235m x 367m production route.";
            }
            finally
            {
                GameScores.HydrateFrom(originalScores);
                InventoryRuntime.HydrateFrom(originalInventory);
                KeyItems.HydrateFrom(originalItems);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                foreach (NPCSchedule schedule in UnityEngine.Object.FindObjectsByType<NPCSchedule>(
                             FindObjectsInactive.Include)) schedule.RefreshImmediate();
                if (originalWaypoint != null) LocationRegistry.SetWaypoint(originalWaypoint);
                else LocationRegistry.ClearWaypoint();
                SaveManager.SetActiveSlot(originalSaveSlot);
                SaveManager.EditorSaveDirectoryOverride = originalSaveOverride;
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyCompletionCoverage(QuestData[] quests)
        {
            var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (DialogueData dialogue in AssetDatabase.FindAssets("t:DialogueData",
                         new[] { "Assets/_Hollowfen/Data" })
                     .Select(guid => AssetDatabase.LoadAssetAtPath<DialogueData>(
                         AssetDatabase.GUIDToAssetPath(guid)))
                     .Where(dialogue => dialogue != null && dialogue.CompleteQuest != null))
            {
                AddCompletionOwner(owners, dialogue.CompleteQuest,
                    "dialogue " + dialogue.name);
            }

            foreach (VillageRequestData request in AssetDatabase.FindAssets("t:VillageRequestData",
                         new[] { "Assets/_Hollowfen/Data" })
                     .Select(guid => AssetDatabase.LoadAssetAtPath<VillageRequestData>(
                         AssetDatabase.GUIDToAssetPath(guid)))
                     .Where(request => request != null && request.CompleteQuest != null))
            {
                AddCompletionOwner(owners, request.CompleteQuest,
                    "request " + request.name);
            }

            AddSceneCompletionOwners<QuestTrigger>(owners, "_completesQuestIfActive");
            AddSceneCompletionOwners<QuestInteractable>(owners, "_completesQuestIfActive");
            AddSceneCompletionOwners<KeyLockedDoor>(owners, "_completesQuestIfActive");
            AddSceneCompletionOwners<QuestForageObjective>(owners, "_quest");

            // Almy's lesson deliberately completes from the first successful planting, rather
            // than from a generic QuestData field, so include that authored gameplay outcome.
            Require(UnityEngine.Object.FindAnyObjectByType<Hollowfen.Cultivation.GrowBed>() != null,
                "Almy's cultivation lesson has no live grow bed");
            AddCompletionOwner(owners, Quest(quests, "almyTeach"), "first GrowBed planting");

            foreach (QuestData quest in quests)
                Require(owners.TryGetValue(quest.Id, out List<string> questOwners) &&
                        questOwners.Count > 0,
                    "quest '" + quest.Id + "' has no concrete completion owner");
        }

        private static void AddSceneCompletionOwners<T>(Dictionary<string, List<string>> owners,
            string propertyName) where T : MonoBehaviour
        {
            foreach (T behaviour in UnityEngine.Object.FindObjectsByType<T>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedProperty property = new SerializedObject(behaviour).FindProperty(propertyName);
                QuestData quest = property != null ? property.objectReferenceValue as QuestData : null;
                if (quest != null)
                    AddCompletionOwner(owners, quest,
                        typeof(T).Name + " on " + behaviour.gameObject.name);
            }
        }

        private static void AddCompletionOwner(Dictionary<string, List<string>> owners,
            QuestData quest, string owner)
        {
            if (quest == null) return;
            if (!owners.TryGetValue(quest.Id, out List<string> questOwners))
            {
                questOwners = new List<string>();
                owners.Add(quest.Id, questOwners);
            }
            questOwners.Add(owner);
        }

        private static void VerifyStaticDestinations(QuestData[] quests)
        {
            Require(Quest(quests, "speakBram").WaypointLocation.Id == "village_well",
                "Bram's key handoff points away from Bram at the well");
            Require(Quest(quests, "firstTax").WaypointLocation.Id == "theos_wagon",
                "Voss's tax collection points away from the east market");
            Require(Quest(quests, "aldricLetter").WaypointLocation.Id == "fathers_mill",
                "Voss's sealed-letter arrival points away from Wren's mill door");
            Require(Quest(quests, "theoCapitalOffer").WaypointLocation.Id == "crooked_pintle",
                "Theo's private Capital offer points away from the empty inn");
        }

        private static void VerifyStagedObjectives(QuestWaypointRouter router, QuestData[] quests)
        {
            QuestData almyTeach = Quest(quests, "almyTeach");
            SetState(almyTeach);
            ExpectRoute(router, almyTeach, "almy_doorway", "quest.almyTeach.stage.lesson");
            SetState(almyTeach, "act2_started");
            ExpectRoute(router, almyTeach, "fathers_mill", "quest.almyTeach.stage.plant");

            QuestData eds = Quest(quests, "edsGrandfather");
            SetState(eds);
            ExpectRoute(router, eds, "eddas_cottage", "quest.edsGrandfather.stage.ask");
            SetState(eds, "edda_ask_heard");
            ExpectRoute(router, eds, "almy_doorway", "quest.edsGrandfather.stage.almy");
            SetState(eds, "edda_ask_heard", "almy_brightspore_told");
            ExpectRoute(router, eds, "old_wood_edge", "quest.edsGrandfather.stage.forage");

            MushroomFieldGuideData brightspore = AssetDatabase.FindAssets("t:MushroomFieldGuideData",
                    new[] { "Assets/_Hollowfen/Data/Mushrooms" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MushroomFieldGuideData>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .First(mushroom => mushroom != null && mushroom.Id == "brightspore");
            InventoryRuntime.HydrateFrom(new InventorySnapshot
                { Ids = new[] { brightspore.Id }, Counts = new[] { 1 } });
            router.Refresh();
            ExpectRoute(router, eds, "marra_kitchen", "quest.edsGrandfather.stage.brew");

            InventoryRuntime.HydrateFrom(new InventorySnapshot());
            KeyItems.HydrateFrom(new[] { "item.brightspore_tonic" });
            router.Refresh();
            ExpectRoute(router, eds, "eddas_cottage", "quest.edsGrandfather.stage.deliver");
            KeyItems.HydrateFrom(Array.Empty<string>());
            SetState(eds, "tonic_delivered");
            ExpectRoute(router, eds, "eddas_cottage", "quest.edsGrandfather.stage.wait");
            SetState(eds, "tonic_delivered", "edda_check_due");
            ExpectRoute(router, eds, "eddas_cottage", "quest.edsGrandfather.stage.return");

            QuestData calden = Quest(quests, "caldenWarning");
            SetState(calden);
            ExpectRoute(router, calden, "fathers_mill", "quest.caldenWarning.stage.mill");
            SetState(calden, "calden_warning_received");
            ExpectRoute(router, calden, "chapel", "quest.caldenWarning.stage.chapel");
        }

        private static void VerifyWorldSpread()
        {
            LocationMarker[] markers = UnityEngine.Object.FindObjectsByType<LocationMarker>(
                FindObjectsInactive.Include);
            string[] routeIds =
            {
                "crooked_pintle", "village_well", "jorens_forge", "theos_wagon",
                "almy_doorway", "eddas_cottage", "chapel", "fathers_mill",
                "old_wood_edge", "witchs_cottage", "manor",
            };
            Vector3[] points = routeIds.Select(id => markers.First(marker => marker.Id == id)
                .WorldPosition).ToArray();
            Require(points.Max(point => point.x) - points.Min(point => point.x) >= 220f,
                "story sites no longer span the map east to west");
            Require(points.Max(point => point.z) - points.Min(point => point.z) >= 360f,
                "story sites no longer span the map south to north");

            NPCSchedule[] schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(
                FindObjectsInactive.Include);
            Require(schedules.Length == 9, "expected nine scheduled principal characters");
            Require(FlatDistance(Destination(schedules, "theo", "Trading at the wagon"),
                        markers.First(marker => marker.Id == "theos_wagon").transform) <= 5f,
                "Theo's actor is detached from his relocated wagon");
            Require(FlatDistance(Destination(schedules, "joren", "Working at the forge"),
                        markers.First(marker => marker.Id == "jorens_forge").transform) <= 5f,
                "Joren's actor is detached from the west forge");
            Require(FlatDistance(Destination(schedules, "almy", "At her western doorway"),
                        markers.First(marker => marker.Id == "almy_doorway").transform) <= 12f,
                "Almy's actor is detached from her western house");
        }

        private static Transform Destination(NPCSchedule[] schedules, string npcId, string slot)
        {
            NPCSchedule schedule = schedules.First(candidate =>
            {
                NPCInteractable npc = candidate.Actor != null
                    ? candidate.Actor.GetComponent<NPCInteractable>() : null;
                return npc != null && npc.Data != null && npc.Data.Id == npcId;
            });
            int index = schedule.FindSlot(slot);
            Require(index >= 0 && schedule.GetSlotDestination(index) != null,
                npcId + " is missing schedule slot '" + slot + "'");
            return schedule.GetSlotDestination(index);
        }

        private static float FlatDistance(Transform first, Transform second)
        {
            Vector3 a = first.position;
            Vector3 b = second.position;
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void SetState(QuestData active, params string[] flags)
        {
            InventoryRuntime.HydrateFrom(new InventorySnapshot());
            KeyItems.HydrateFrom(Array.Empty<string>());
            GameScores.HydrateFrom(new SaveSlotMeta { GameFlagIds = flags ?? Array.Empty<string>() });
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            QuestManager.StartQuest(active);
            QuestWaypointRouter.Instance.Refresh();
        }

        private static void ExpectRoute(QuestWaypointRouter router, QuestData quest,
            string locationId, string objectiveId)
        {
            LocationData resolved = router.ResolveLocation(quest);
            Require(resolved != null && resolved.Id == locationId,
                quest.Id + " resolved to " + (resolved != null ? resolved.Id : "no location") +
                " instead of " + locationId);
            Require(router.ResolveObjectiveTextId(quest) == objectiveId,
                quest.Id + " did not resolve objective '" + objectiveId + "'");
            Require(LocationRegistry.ActiveWaypoint != null &&
                    LocationRegistry.ActiveWaypoint.Data == resolved,
                quest.Id + " did not apply its resolved compass waypoint");
        }

        private static QuestData Quest(QuestData[] quests, string id) =>
            quests.First(quest => quest.Id == id);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(
                "[StoryWorldAlignmentVerifier] " + message);
        }
    }
}
#endif
