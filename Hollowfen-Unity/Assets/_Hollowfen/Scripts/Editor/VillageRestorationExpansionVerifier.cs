#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Restoration;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Isolated production coverage for the five-project village expansion.</summary>
    public static class VillageRestorationExpansionVerifier
    {
        private static readonly string[] ExpansionIds =
        {
            "jorens_forge", "chapel_garden", "crooked_pintle", "witch_cottage", "tobin_workshop",
        };

        private static readonly string[] UnlockQuests =
        {
            "forgeKnife", "caldenReconcile", "festivalHosted", "wendlightFound", "almyTeach",
        };

        [MenuItem("Hollowfen/Verify/Full Village Restoration Expansion")]
        private static void RunFromMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            int originalCopper = CoinPurse.TotalCopper;
            var originalLedger = CoinPurse.ToLedgerSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            var originalRestoration = RestorationProjects.ToSnapshot();
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            var clock = TimeManager.Instance;
            int originalDay = clock != null ? clock.Day : 1;
            float originalHour = clock != null ? clock.Hour : 8f;
            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-village-restoration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;

            try
            {
                SaveCoordinator.StartNewGame(3);
                var database = Resources.Load<RestorationProjectDatabase>("RestorationProjectDatabase");
                Require(database != null && database.Projects.Count(project => project != null) == 7,
                    "the runtime catalogue must contain exactly seven projects");
                var projects = ExpansionIds.Select(id => database.Projects.FirstOrDefault(project =>
                    project != null && project.Id == id)).ToArray();
                Require(projects.All(project => project != null), "one or more expansion projects are missing");

                VerifyAuthoredWorld(projects);
                VerifyCrewRoutes();
                VerifyAtomicProgressionAndBenefits(projects, clock);
                VerifyFirstUseRollback(projects[0]);
                return "VILLAGE RESTORATION EXPANSION — PASS: 7-project catalogue, five staged worksites, " +
                       "story-gated crews, ten atomic supply lines, two dawn beats, five permanent benefits, " +
                       "Witch's Path flags, score rewards, first-use rollback, and bounded world cost";
            }
            finally
            {
                foreach (var reveal in UnityEngine.Object.FindObjectsByType<RestorationRevealDirector>(
                             FindObjectsInactive.Include)) reveal.CancelPending();
                if (SaveManager.IsAtomicTransactionActive)
                    SaveManager.EditorCancelAtomicTransactionForVerification();
                SaveManager.EditorRejectNextAtomicCommit = false;
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                CoinPurse.HydrateFrom(originalCopper, originalLedger);
                GameScores.HydrateFrom(originalScores);
                RestorationProjects.HydrateFrom(originalRestoration);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                if (clock != null) clock.SetTime(originalDay, originalHour);
                foreach (var schedule in UnityEngine.Object.FindObjectsByType<NPCSchedule>(
                             FindObjectsInactive.Include)) schedule.RefreshImmediate();
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyAuthoredWorld(RestorationProjectData[] projects)
        {
            int rendererCount = 0;
            int particleCount = 0;
            int lightCount = 0;
            int growBedCount = 0;
            int maximumSimultaneousStageRenderers = 0;
            var sharedMaterials = new System.Collections.Generic.HashSet<Material>();
            foreach (var project in projects)
            {
                var root = GameObject.Find("_LivingRestoration_" + project.Id);
                Require(root != null, "missing scene root for " + project.Id);
                var site = root.GetComponent<RestorationSite>();
                var use = root.GetComponentInChildren<RestorationUseTrigger>(true);
                var scheduler = root.GetComponent<DayFlagScheduler>();
                var reveal = root.GetComponent<RestorationRevealDirector>();
                Require(site != null && site.Project == project && use != null && use.Project == project &&
                        scheduler != null && reveal != null && reveal.Project == project && reveal.FocusTarget != null,
                    project.Id + " is missing its site, first use, dawn scheduler, or reveal");
                Require(root.transform.Find("Surveyed") != null && root.transform.Find("SuppliesCommitted") != null &&
                        root.transform.Find("WorkUnderway") != null && root.transform.Find("Restored") != null &&
                        root.transform.Find("Occupied") != null,
                    project.Id + " is missing one or more stage presentation roots");
                Require(project.Contributions.Length == 2 && project.Milestones.Length == 5 &&
                        !string.IsNullOrWhiteSpace(project.BenefitId),
                    project.Id + " does not fit the production ledger contract");

                // Tobin's complete apothecary owns and verifies its own draw/material/triangle,
                // particle, and light budgets. Keep the restoration envelope stable instead of
                // quietly relaxing it for the purchased building.
                var renderers = root.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.GetComponentInParent<
                        Hollowfen.Apothecary.ApothecaryStation>(true) == null).ToArray();
                rendererCount += renderers.Length;
                foreach (var renderer in renderers)
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null) sharedMaterials.Add(material);
                int surveyed = RestorationRendererCount(root.transform.Find("Surveyed"));
                int supplied = RestorationRendererCount(root.transform.Find("SuppliesCommitted"));
                int working = RestorationRendererCount(root.transform.Find("WorkUnderway"));
                int restored = RestorationRendererCount(root.transform.Find("Restored"));
                int occupied = RestorationRendererCount(root.transform.Find("Occupied"));
                maximumSimultaneousStageRenderers += Mathf.Max(Mathf.Max(surveyed, supplied),
                    Mathf.Max(working, restored + occupied));
                particleCount += root.GetComponentsInChildren<ParticleSystem>(true)
                    .Count(system => system.GetComponentInParent<
                        Hollowfen.Apothecary.ApothecaryStation>(true) == null);
                lightCount += root.GetComponentsInChildren<Light>(true)
                    .Count(light => light.GetComponentInParent<
                        Hollowfen.Apothecary.ApothecaryStation>(true) == null);
                growBedCount += root.GetComponentsInChildren<Hollowfen.Cultivation.GrowBed>(true).Length;
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled) continue;
                    bool expected = collider.GetComponent<RestorationSite>() != null ||
                                    collider.GetComponent<RestorationUseTrigger>() != null ||
                                    collider.GetComponent<Hollowfen.Cultivation.GrowBed>() != null ||
                                    collider.GetComponentInParent<
                                        Hollowfen.Apothecary.ApothecaryStation>(true) != null;
                    Require(expected, project.Id + " contains an unexpected blocking dressing collider on " +
                                      collider.name);
                }
            }

            // Vendor furniture retains its source LOD renderer components, but shares the existing
            // meshes/materials and only one LOD draws. Bound both serialized components and the
            // worst stage combination so later authoring cannot quietly compound either cost.
            Require(rendererCount <= 430, "expansion dressing exceeds the 430-renderer authored budget");
            Require(maximumSimultaneousStageRenderers <= 240,
                "expansion dressing exceeds the 240-renderer maximum simultaneous-stage budget");
            Require(sharedMaterials.Count <= 18,
                "expansion dressing exceeds the 18-shared-material budget");
            Require(particleCount <= 7, "expansion dressing exceeds the seven-system particle budget");
            Require(lightCount <= 4, "expansion dressing exceeds the four-light budget");
            Require(growBedCount == 3, "chapel and Witch restorations should author exactly three grow beds");
        }

        private static int RestorationRendererCount(Transform root)
        {
            return root == null ? 0 : root.GetComponentsInChildren<Renderer>(true)
                .Count(renderer => renderer.GetComponentInParent<
                    Hollowfen.Apothecary.ApothecaryStation>(true) == null);
        }

        private static void VerifyCrewRoutes()
        {
            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            Require(schedules.Length == 9,
                "restoration expansion must coexist with all nine derived village schedules");
            Require(HasSlot(schedules, "Joren", "Rebuilding the forge hearth") &&
                    HasSlot(schedules, "Pell", "Keeping the forge repair ledger") &&
                    HasSlot(schedules, "Almy", "Restoring the chapel beds") &&
                    HasSlot(schedules, "Pell", "Overseeing the chapel beds") &&
                    HasSlot(schedules, "Bram", "Refitting the Crooked Pintle") &&
                    HasSlot(schedules, "Joren", "Refitting the Crooked Pintle") &&
                    HasSlot(schedules, "Almy", "Preserving Sable's cottage") &&
                    HasSlot(schedules, "Joren", "Repairing Tobin's apothecary") &&
                    HasSlot(schedules, "Bram", "Bringing a meal to the apothecary crew") &&
                    HasSlot(schedules, "Pell", "Keeping Tobin's apothecary ledger"),
                "one or more authored expansion crew routes are missing");

            var joren = schedules.First(schedule => schedule.Actor != null && schedule.Actor.name == "NPC_Joren");
            int forgeSlot = joren.FindSlot("Working at the forge");
            Transform forge = joren.GetSlotDestination(forgeSlot);
            Require(forge != null && Vector3.Distance(forge.position,
                        new Vector3(198.4f, 32.65f, 195.7f)) < .05f,
                "Joren's ordinary routine still points to the obsolete forge location");
        }

        private static void VerifyAtomicProgressionAndBenefits(RestorationProjectData[] projects,
            TimeManager clock)
        {
            Require(clock != null, "TimeManager is missing");
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(UnlockQuests, Array.Empty<string>());
            RestorationProjects.HydrateFrom(null);
            clock.SetTime(1, 8f);
            CoinPurse.Add(500, "verify.restoration.expansion.seed");
            int expectedCopper = 500;
            long revision = SaveManager.InspectSlot(3).Revision;

            foreach (var project in projects)
            {
                Require(RestorationProjects.GetStage(project) == RestorationStage.Surveyed,
                    project.Id + " did not unlock from its canonical completed quest");
                foreach (var contribution in project.Contributions)
                {
                    expectedCopper -= contribution.CostCopper;
                    Require(RestorationProjects.Contribute(project, contribution) ==
                            RestorationProjects.ContributionResult.Funded &&
                            CoinPurse.TotalCopper == expectedCopper &&
                            SaveManager.InspectSlot(3).Revision == ++revision,
                        project.Id + " contribution did not commit exactly one revision and exact copper");
                }
                Require(RestorationProjects.GetStage(project) == RestorationStage.SuppliesCommitted,
                    project.Id + " did not enter SuppliesCommitted after both rows");
            }

            clock.AdvanceTo(2, 8f, true);
            foreach (var project in projects)
                Require(RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                    project.Id + " skipped or missed the first work dawn");
            clock.AdvanceTo(3, 8f, true);
            foreach (var project in projects)
                Require(RestorationProjects.GetStage(project) == RestorationStage.Restored,
                    project.Id + " skipped or missed the second restoration dawn");

            int expectedHope = 0;
            int expectedKnowledge = 0;
            foreach (var project in projects)
            {
                expectedHope += project.VillageHopeReward;
                expectedKnowledge += project.KnowledgeReward;
                string primary = Prefix(project.Id) + "_in_use";
                string[] consequences = project.Id == "witch_cottage"
                    ? new[] { "witch_cottage_restored", "old_knowledge_restored" }
                    : Array.Empty<string>();
                Require(RestorationProjects.CompleteFirstUse(project, primary, consequences,
                            RestorationStage.Restored, RestorationStage.Occupied) &&
                        RestorationProjects.GetStage(project) == RestorationStage.Occupied,
                    project.Id + " first use did not atomically enter Occupied");
                Require(!RestorationProjects.CompleteFirstUse(project, primary, consequences,
                        RestorationStage.Restored, RestorationStage.Occupied),
                    project.Id + " first-use reward was not idempotent");
            }

            Require(GameScores.VillageHope == expectedHope && GameScores.Knowledge == expectedKnowledge,
                "project score rewards were missing or applied more than once");
            Require(GameScores.HasFlag("witch_cottage_restored") &&
                    GameScores.HasFlag("old_knowledge_restored"),
                "Witch's Cottage did not close both existing Witch's Path prerequisites");
            Require(RestorationBenefits.CuttingStrokes == 5 &&
                    Mathf.Approximately(RestorationBenefits.CultivationHoursMultiplier, .75f) &&
                    RestorationBenefits.DailyRequestBonusCopper == 2 &&
                    RestorationBenefits.WildRespawnDays(3) == 2 &&
                    RestorationBenefits.CultivationYieldBonus == 1,
                "one or more occupied-project gameplay benefits are not active");
        }

        private static void VerifyFirstUseRollback(RestorationProjectData project)
        {
            SaveCoordinator.StartNewGame(3);
            RestorationProjects.Advance(project, RestorationStage.Restored);
            var beforeScores = new SaveSlotMeta();
            GameScores.WriteTo(beforeScores);
            long revision = SaveManager.InspectSlot(3).Revision;
            SaveManager.EditorRejectNextAtomicCommit = true;
            bool result = RestorationProjects.CompleteFirstUse(project, "forge_in_use",
                new[] { "forge_service_unlocked" }, RestorationStage.Restored, RestorationStage.Occupied);
            Require(!result && !GameScores.HasFlag("forge_in_use") &&
                    !GameScores.HasFlag("forge_service_unlocked") &&
                    GameScores.VillageHope == beforeScores.VillageHope &&
                    RestorationProjects.GetStage(project) == RestorationStage.Restored &&
                    SaveManager.InspectSlot(3).Revision == revision,
                "a rejected first-use commit leaked flags, score, stage, or revision");
        }

        private static string Prefix(string projectId)
        {
            switch (projectId)
            {
                case "jorens_forge": return "forge";
                case "chapel_garden": return "chapel_garden";
                case "crooked_pintle": return "crooked_pintle";
                case "witch_cottage": return "witch_cottage";
                case "tobin_workshop": return "tobin_workshop";
                default: return projectId;
            }
        }

        private static bool HasSlot(NPCSchedule[] schedules, string actorName, string label)
        {
            var schedule = schedules.FirstOrDefault(item => item.Actor != null &&
                item.Actor.name.IndexOf(actorName, StringComparison.OrdinalIgnoreCase) >= 0);
            return schedule != null && schedule.FindSlot(label) >= 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(
                "[VillageRestorationExpansionVerifier] " + message);
        }
    }
}
#endif
