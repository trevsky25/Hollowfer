#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Apothecary;
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
            public string ApothecaryCaseId;
            public ApothecaryCaseStage ApothecaryCaseStage;
            public bool FollowUpDue;
            public bool RequiresWetWeather;
            public bool ForcePlacement;
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
            var almy = FindActor("NPC_Almy");
            var marra = FindActor("NPC_Marra");
            var edda = FindActor("NPC_Edda");
            var voss = FindActor("NPC_Voss");
            var calden = FindActor("NPC_Calden");
            ReparentActor(theo, actors.transform, false);
            ReparentActor(joren, actors.transform, true);
            ReparentActor(bram, actors.transform, true);
            ReparentActor(pell, actors.transform, true);
            ReparentActor(almy, actors.transform, true);
            ReparentActor(marra, actors.transform, true);
            ReparentActor(edda, actors.transform, true);
            ReparentActor(voss, actors.transform, false);
            ReparentActor(calden, actors.transform, false);

            QuestData capital = Quest("theoCapitalOffer");
            QuestData eddaApprentice = Quest("eddaApprentice");
            QuestData forgeKnife = Quest("forgeKnife");
            QuestData firstSale = Quest("firstSale");
            QuestData meetAlmy = Quest("meetAlmy");
            QuestData cottagesReopen = Quest("cottagesReopen");
            QuestData firstTax = Quest("firstTax");
            QuestData theoTrade = Quest("theoTrade");
            QuestData caldenWarning = Quest("caldenWarning");
            QuestData caldenReconcile = Quest("caldenReconcile");
            QuestData aldricLetter = Quest("aldricLetter");

            Transform theoWagon = Anchor(root.transform, "Theo_Wagon", new Vector3(327f, 36.45f, 213f), 270f);
            Transform theoInn = Anchor(root.transform, "Theo_Pintle", new Vector3(285f, 35f, 92f), 205f);
            // The exploration-layout forge moved west; keep Joren at the actual authored smithy,
            // not the pre-relocation placeholder near the Pintle road.
            Transform jorenForge = Anchor(root.transform, "Joren_Forge", new Vector3(198.4f, 32.65f, 195.7f), 10f);
            Transform jorenInn = Anchor(root.transform, "Joren_Pintle", new Vector3(283f, 35f, 90f), 145f);
            Transform bramWell = Anchor(root.transform, "Bram_Well", new Vector3(284.8f, 37.026184f, 158.4f), 180f);
            Transform bramInn = Anchor(root.transform, "Bram_Pintle", new Vector3(286f, 35f, 95f), 155f);
            Transform pellWell = Anchor(root.transform, "Pell_Well", new Vector3(288f, 37.026184f, 161f), 0f);
            Transform pellInn = Anchor(root.transform, "Pell_Pintle", new Vector3(282f, 35f, 96f), 40f);
            Transform jorenCottage = Anchor(root.transform, "Joren_NorthLaneRepair",
                new Vector3(222.1f, 35.18f, 303.45f), 55f);
            Transform pellCottage = Anchor(root.transform, "Pell_NorthLaneOversight",
                new Vector3(224.9f, 35.18f, 302.7f), 325f);
            Transform bramCottage = Anchor(root.transform, "Bram_NorthLaneMeal",
                new Vector3(226.4f, 35.18f, 302.95f), 320f);
            Transform jorenBridge = Anchor(root.transform, "Joren_WendBridgeBracing",
                new Vector3(214.9f, 32.70f, 218.1f), 32f);
            Transform theoBridge = Anchor(root.transform, "Theo_WendBridgeTimber",
                new Vector3(222.4f, 32.70f, 214.0f), 205f);
            Transform pellBridge = Anchor(root.transform, "Pell_WendBridgeLedger",
                new Vector3(222.2f, 32.70f, 235.9f), 145f);
            Transform pellForge = Anchor(root.transform, "Pell_ForgeLedger",
                new Vector3(201.0f, 32.65f, 195.4f), 285f);
            Transform jorenPintleWork = Anchor(root.transform, "Joren_PintleRepairs",
                new Vector3(279.7f, 35f, 88.2f), 270f);
            Transform bramPintleWork = Anchor(root.transform, "Bram_PintleRepairs",
                new Vector3(280.2f, 35f, 91.2f), 215f);
            Transform pellPintleWork = Anchor(root.transform, "Pell_PintleLedger",
                new Vector3(281.2f, 35f, 93.1f), 210f);
            Transform almyGarden = Anchor(root.transform, "Almy_ChapelGarden",
                new Vector3(211.8f, 38f, 317.2f), 55f);
            Transform pellGarden = Anchor(root.transform, "Pell_ChapelGarden",
                new Vector3(216.4f, 38f, 316.2f), 300f);
            Transform almyWitch = Anchor(root.transform, "Almy_WitchCottage",
                new Vector3(259.4f, 35f, 451.5f), 35f);
            Transform jorenMill = Anchor(root.transform, "Joren_TobinWorkshop",
                new Vector3(221.3f, 35.75f, 331.2f), 310f);
            Transform pellMill = Anchor(root.transform, "Pell_TobinWorkshop",
                new Vector3(223.2f, 35.75f, 332.4f), 285f);
            Transform bramMill = Anchor(root.transform, "Bram_TobinWorkshopMeal",
                new Vector3(224.2f, 35.75f, 330.6f), 300f);
            // Restore Almy's original mill-threshold blocking only for A Knock at the Door.
            // Her everyday location remains the isolated western house from the exploration pass.
            Transform almyMill = Anchor(root.transform, "Almy_MillDoor",
                new Vector3(232.2f, 32.626373f, 312.5f), 20f);
            Transform almyHome = Anchor(root.transform, "Almy_WesternDoorway",
                new Vector3(153f, 31.45f, 271f), 270f);
            Transform marraKitchen = Anchor(root.transform, "Marra_Kitchen",
                new Vector3(283f, 35f, 93f), 180f);
            Transform marraApothecary = Anchor(root.transform, "Marra_ApothecaryLunch",
                new Vector3(222.7f, 35.75f, 329.7f), 315f);
            Transform eddaCottage = Anchor(root.transform, "Edda_Cottage",
                new Vector3(205f, 37.95f, 279f), 315f);
            Transform eddaApothecary = Anchor(root.transform, "Edda_ApothecaryShelf",
                new Vector3(220.8f, 35.75f, 329.6f), 25f);
            Transform eddaGarden = Anchor(root.transform, "Edda_ChapelGarden",
                new Vector3(214.1f, 38f, 316.8f), 330f);
            Transform eddaMill = Anchor(root.transform, "Edda_MillDoor",
                new Vector3(234.1f, 32.81f, 311.6f), 205f);
            Transform vossMarket = Anchor(root.transform, "Voss_EastMarket",
                new Vector3(321f, 36.45f, 216f), 135f);
            Transform vossMill = Anchor(root.transform, "Voss_MillDoor",
                new Vector3(235.1f, 32.81f, 310.7f), 205f);
            Transform caldenMill = Anchor(root.transform, "Calden_MillDoor",
                new Vector3(234.5f, 32.80f, 309f), 20f);
            Transform caldenChapel = Anchor(root.transform, "Calden_ChapelGate",
                new Vector3(212f, 38.05f, 314f), 270f);
            // The purchased apothecary's original alchemy chair and ledger table define this
            // staging. Patients occupy the chair-side mark; Edda stands opposite the open book.
            Transform apothecaryPatient = Anchor(root.transform, "Apothecary_PatientChair",
                new Vector3(211.47f, 35.72f, 342.77f), 321f);
            Transform apothecaryMentor = Anchor(root.transform, "Apothecary_EddaCasework",
                new Vector3(214.33f, 35.72f, 342.42f), 141f);

            Configure(root.transform, "Theo", theo, true, new[]
            {
                CaseSlot("Road-cold appointment", apothecaryPatient, "theo_road_cold",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Road-cold follow-up", apothecaryPatient, "theo_road_cold",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                Slot("Capital offer at the Pintle", theoInn, active: capital,
                    requiredFlag: "theo_wagon_arrived"),
                Slot("Delivering timber to the Wend bridge", theoBridge, 8f, 17f,
                    requiredFlag: "wend_bridge_work_started", blockedFlag: "wend_bridge_restored"),
                Slot("Evening at the Pintle", theoInn, 18.5f, 7f, completed: eddaApprentice,
                    requiredFlag: "theo_wagon_arrived"),
                Slot("Trading at the wagon", theoWagon, requiredFlag: "theo_wagon_arrived"),
            });
            Configure(root.transform, "Joren", joren, false, new[]
            {
                CaseSlot("Hammer-echo appointment", apothecaryPatient, "joren_hammer_echo",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Hammer-echo follow-up", apothecaryPatient, "joren_hammer_echo",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                Slot("Bracing the Wend bridge", jorenBridge, 7f, 18f,
                    requiredFlag: "wend_bridge_work_started", blockedFlag: "wend_bridge_restored"),
                Slot("Repairing Tobin's apothecary", jorenMill, 7f, 18f,
                    requiredFlag: "tobin_workshop_work_started", blockedFlag: "tobin_workshop_restored"),
                Slot("Refitting the Crooked Pintle", jorenPintleWork, 7f, 18f,
                    requiredFlag: "crooked_pintle_work_started", blockedFlag: "crooked_pintle_restored"),
                Slot("Rebuilding the forge hearth", jorenForge, 7f, 18f,
                    requiredFlag: "forge_work_started", blockedFlag: "forge_restored"),
                Slot("Repairing the north-lane cottage", jorenCottage, 7f, 18.5f,
                    active: cottagesReopen, requiredFlag: "cottages_reopened_1",
                    blockedFlag: "cottages_reopened_2"),
                EncounterSlot("Settling the forge ledger", jorenForge, 10f, 12f,
                    "forge_in_use", "encounter.forge_ledger_seen"),
                Slot("Evening at the Pintle", jorenInn, 18.5f, 7f, completed: forgeKnife),
                Slot("Tightening the apothecary shelves", jorenMill, 11f, 14f,
                    requiredFlag: "tobin_workshop_in_use"),
                Slot("Keeping the restored forge fire", jorenForge, 7f, 18.5f,
                    requiredFlag: "forge_in_use"),
                Slot("Working at the forge", jorenForge),
            });
            Configure(root.transform, "Bram", bram, false, new[]
            {
                CaseSlot("Rain-shiver appointment", apothecaryPatient, "bram_rain_shiver",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Rain-shiver follow-up", apothecaryPatient, "bram_rain_shiver",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                Slot("First sale at the Pintle", bramInn, active: firstSale),
                Slot("Refitting the Crooked Pintle", bramPintleWork, 8f, 18f,
                    requiredFlag: "crooked_pintle_work_started", blockedFlag: "crooked_pintle_restored"),
                Slot("Bringing a meal to the apothecary crew", bramMill, 11f, 13.5f,
                    requiredFlag: "tobin_workshop_work_started", blockedFlag: "tobin_workshop_restored"),
                Slot("Bringing a meal to the cottage crew", bramCottage, 11f, 13.5f,
                    active: cottagesReopen, requiredFlag: "cottages_reopened_1",
                    blockedFlag: "cottages_reopened_2"),
                EncounterSlot("Rain ledger at Tobin's apothecary", apothecaryPatient, 14f, 17f,
                    "apothecary_story_complete", "encounter.apothecary_rain_seen", true),
                EncounterSlot("Setting the Pintle's last table", bramInn, 19f, 22f,
                    "crooked_pintle_in_use", "encounter.pintle_last_table_seen"),
                Slot("Keeping the restored Pintle", bramInn,
                    requiredFlag: "crooked_pintle_in_use"),
                Slot("Evening at the Pintle", bramInn, 18.5f, 7f, completed: meetAlmy),
                Slot("At the village well", bramWell),
            });
            Configure(root.transform, "Pell", pell, false, new[]
            {
                CaseSlot("Fading-ledger appointment", apothecaryPatient, "pell_fading_ledger",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Fading-ledger follow-up", apothecaryPatient, "pell_fading_ledger",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                Slot("Keeping the Wend bridge ledger", pellBridge, 9f, 16.5f,
                    requiredFlag: "wend_bridge_work_started", blockedFlag: "wend_bridge_restored"),
                Slot("Keeping Tobin's apothecary ledger", pellMill, 9f, 16.5f,
                    requiredFlag: "tobin_workshop_work_started", blockedFlag: "tobin_workshop_restored"),
                Slot("Keeping the Pintle repair ledger", pellPintleWork, 9f, 16.5f,
                    requiredFlag: "crooked_pintle_work_started", blockedFlag: "crooked_pintle_restored"),
                Slot("Overseeing the chapel beds", pellGarden, 9f, 16.5f,
                    requiredFlag: "chapel_garden_work_started", blockedFlag: "chapel_garden_restored"),
                Slot("Keeping the forge repair ledger", pellForge, 9f, 16.5f,
                    requiredFlag: "forge_work_started", blockedFlag: "forge_restored"),
                Slot("Overseeing the cottage repairs", pellCottage, 7f, 18.5f,
                    active: cottagesReopen, requiredFlag: "cottages_reopened_1",
                    blockedFlag: "cottages_reopened_2"),
                EncounterSlot("Settling the forge ledger", pellForge, 10f, 12f,
                    "forge_in_use", "encounter.forge_ledger_seen"),
                Slot("Reviewing the restored apothecary", pellMill, 9f, 12f,
                    requiredFlag: "apothecary_story_complete"),
                Slot("Visiting the occupied north-lane cottages", pellCottage, 13f, 16.5f,
                    requiredFlag: "cottages_reopened_2"),
                Slot("Evening at the Pintle", pellInn, 18.5f, 7f, completed: cottagesReopen),
                Slot("At the village well", pellWell),
            });
            Configure(root.transform, "Almy", almy, false, new[]
            {
                CaseSlot("Bright-sleep appointment", apothecaryPatient, "almy_brightspore_sleep",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Bright-sleep follow-up", apothecaryPatient, "almy_brightspore_sleep",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                Slot("Waiting at the mill door", almyMill, active: meetAlmy, forcePlacement: true),
                Slot("Preserving Sable's cottage", almyWitch, 8f, 16f,
                    requiredFlag: "witch_cottage_work_started", blockedFlag: "witch_cottage_restored"),
                Slot("Restoring the chapel beds", almyGarden, 7f, 17f,
                    requiredFlag: "chapel_garden_work_started", blockedFlag: "chapel_garden_restored"),
                EncounterSlot("Trading seed at the chapel garden", almyGarden, 8f, 10f,
                    "chapel_garden_in_use", "encounter.garden_seed_seen"),
                Slot("Tending the restored chapel beds", almyGarden, 7f, 11.5f,
                    requiredFlag: "chapel_garden_in_use"),
                Slot("Checking the apothecary labels", jorenMill, 13f, 16f,
                    requiredFlag: "apothecary_story_complete"),
                Slot("At her western doorway", almyHome),
            });
            Configure(root.transform, "Marra", marra, false, new[]
            {
                CaseSlot("Cellar-bloom appointment", apothecaryPatient, "marra_cellar_bloom",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Cellar-bloom follow-up", apothecaryPatient, "marra_cellar_bloom",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                EncounterSlot("Trading seed at the chapel garden", eddaGarden, 8f, 10f,
                    "chapel_garden_in_use", "encounter.garden_seed_seen"),
                Slot("Bringing lunch to the apothecary", marraApothecary, 11.5f, 13.5f,
                    requiredFlag: "apothecary_story_complete"),
                EncounterSlot("Setting the Pintle's last table", marraKitchen, 19f, 22f,
                    "crooked_pintle_in_use", "encounter.pintle_last_table_seen"),
                Slot("Keeping the restored Pintle kitchen", marraKitchen,
                    requiredFlag: "crooked_pintle_in_use"),
                Slot("At her kitchen", marraKitchen),
            });
            Configure(root.transform, "Edda", edda, true, new[]
            {
                CaseSlot("Mentoring Bram's appointment", apothecaryMentor, "bram_rain_shiver",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Bram's follow-up", apothecaryMentor, "bram_rain_shiver",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                CaseSlot("Mentoring Pell's appointment", apothecaryMentor, "pell_fading_ledger",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Pell's follow-up", apothecaryMentor, "pell_fading_ledger",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                CaseSlot("Mentoring Joren's appointment", apothecaryMentor, "joren_hammer_echo",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Joren's follow-up", apothecaryMentor, "joren_hammer_echo",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                CaseSlot("Mentoring Marra's appointment", apothecaryMentor, "marra_cellar_bloom",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Marra's follow-up", apothecaryMentor, "marra_cellar_bloom",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                CaseSlot("Mentoring Almy's appointment", apothecaryMentor, "almy_brightspore_sleep",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Almy's follow-up", apothecaryMentor, "almy_brightspore_sleep",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                CaseSlot("Mentoring Theo's appointment", apothecaryMentor, "theo_road_cold",
                    ApothecaryCaseStage.Investigating),
                CaseSlot("Receiving Theo's follow-up", apothecaryMentor, "theo_road_cold",
                    ApothecaryCaseStage.AwaitingFollowUp, true),
                EncounterSlot("Rain ledger at Tobin's apothecary", apothecaryMentor, 14f, 17f,
                    "apothecary_story_complete", "encounter.apothecary_rain_seen", true),
                Slot("Asking at the mill door", eddaMill, active: eddaApprentice,
                    forcePlacement: true),
                Slot("Keeping the apothecary care shelf", eddaApothecary, 8.5f, 13f,
                    requiredFlag: "apothecary_story_complete"),
                Slot("Making rounds through the chapel beds", eddaGarden, 13f, 17f,
                    requiredFlag: "chapel_garden_in_use"),
                Slot("At her cottage", eddaCottage, completed: theoTrade),
            });
            Configure(root.transform, "Voss", voss, true, new[]
            {
                Slot("Delivering Aldric's letter at the mill", vossMill, active: aldricLetter,
                    forcePlacement: true),
                Slot("Collecting the Wenmar tax at the east market", vossMarket, active: firstTax,
                    forcePlacement: true),
                Slot("Keeping accounts at the east market", vossMarket, completed: firstTax,
                    blockedFlag: "aldric_letter_received"),
            });
            Configure(root.transform, "Calden", calden, true, new[]
            {
                Slot("Warning Wren at the mill", caldenMill, active: caldenWarning,
                    blockedFlag: "calden_warning_received", forcePlacement: true),
                Slot("Waiting at the locked chapel garden", caldenChapel, active: caldenWarning,
                    requiredFlag: "calden_warning_received"),
                Slot("Opening the chapel garden", caldenChapel, active: caldenReconcile,
                    forcePlacement: true),
                Slot("At the chapel", caldenChapel, completed: caldenWarning),
            });

            RepointCapitalQuest(capital);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[NPCSchedules] Authored nine village routines with quest-priority staging, restored whole-map anchors, story arrivals, restoration crews, and persistent post-restoration roles.");
        }

        private static SlotSpec Slot(string label, Transform destination, QuestData active = null,
            QuestData completed = null, string requiredFlag = null, string blockedFlag = null,
            bool forcePlacement = false)
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
                ForcePlacement = forcePlacement,
            };
        }

        private static SlotSpec CaseSlot(string label, Transform destination, string caseId,
            ApothecaryCaseStage stage, bool followUpDue = false)
        {
            return new SlotSpec
            {
                Label = label,
                Destination = destination,
                AllDay = true,
                ApothecaryCaseId = caseId,
                ApothecaryCaseStage = stage,
                FollowUpDue = followUpDue,
                ForcePlacement = true,
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

        private static SlotSpec EncounterSlot(string label, Transform destination, float start,
            float end, string requiredFlag, string completedFlag, bool wetWeather = false)
        {
            return new SlotSpec
            {
                Label = label,
                Destination = destination,
                Start = start,
                End = end,
                RequiredFlag = requiredFlag,
                BlockedFlag = completedFlag,
                RequiresWetWeather = wetWeather,
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
                entry.FindPropertyRelative("requiresApothecaryCaseId").stringValue =
                    source.ApothecaryCaseId ?? "";
                entry.FindPropertyRelative("requiresApothecaryCaseStage").enumValueIndex =
                    (int)source.ApothecaryCaseStage;
                entry.FindPropertyRelative("requiresApothecaryFollowUpDue").boolValue =
                    source.FollowUpDue;
                entry.FindPropertyRelative("requiresWetWeather").boolValue =
                    source.RequiresWetWeather;
                entry.FindPropertyRelative("forcePlacement").boolValue = source.ForcePlacement;
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
