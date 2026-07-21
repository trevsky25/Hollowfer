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
    /// <summary>Isolated Play Mode coverage for the Wend bridge vertical slice.</summary>
    public static class BridgeRestorationVerifier
    {
        [MenuItem("Hollowfen/Verify/Wend Bridge Restoration")]
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
                "hollowfen-wend-bridge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;

            try
            {
                SaveCoordinator.StartNewGame(3);
                var database = Resources.Load<RestorationProjectDatabase>("RestorationProjectDatabase");
                Require(database != null && database.Projects.Count(project => project != null) >= 2,
                    "the restoration catalogue does not contain both authored projects");
                var project = database.Projects.FirstOrDefault(item => item != null && item.Id == "wend_bridge");
                Require(project != null, "Wend bridge project is missing from the runtime database");
                VerifyAuthoredWorld(project);
                VerifyAtomicContributions(project);
                VerifyOvernightWorkAndFirstUse(project, clock);
                VerifyFailureRollback(project);
                return "WEND BRIDGE RESTORATION — PASS: 2-line atomic funding, protected foot lane, " +
                       "cart-width reopened collider, three-person crew, two distinct dawn beats, reveal, " +
                       "first-crossing completion, save-failure rollback, and shared catalogue registration";
            }
            finally
            {
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
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyAuthoredWorld(RestorationProjectData project)
        {
            var root = GameObject.Find("_LivingRestoration_WendBridge");
            Require(root != null && Mathf.Abs(root.transform.position.y - 32.63f) < .02f,
                "the bridge presentation is not aligned to the existing y=32.67 deck");
            var site = root.GetComponent<RestorationSite>();
            var use = root.GetComponentInChildren<RestorationUseTrigger>(true);
            var reveal = root.GetComponents<RestorationRevealDirector>()
                .FirstOrDefault(candidate => candidate.Project == project);
            Require(site != null && site.Project == project && use != null &&
                    use.Project == project && use.CompletionFlagId == "wend_bridge_in_use",
                "the bridge site or first-use trigger is not authored against the project");
            Require(reveal != null && reveal.FocusTarget != null,
                "the bridge dawn reveal is missing its focus target");
            Require(root.GetComponents<DayFlagScheduler>().Length == 1,
                "the bridge needs one dedicated overnight scheduler");

            var restricted = root.transform.Find("RestrictedFootway");
            var left = restricted?.Find("CondemnedWestDeck")?.GetComponent<BoxCollider>();
            var right = restricted?.Find("CondemnedEastDeck")?.GetComponent<BoxCollider>();
            Require(left != null && right != null && right.bounds.min.x - left.bounds.max.x >= 2.8f,
                "the pre-restoration bridge does not preserve a controller-safe central lane");
            var reopened = root.transform.Find("RestoredSpan/ReopenedCartDeckCollider")?
                .GetComponent<BoxCollider>();
            Require(reopened != null && reopened.size.x >= 6f && reopened.size.z >= 21f,
                "the restored stage does not provide a full cart-width crossing collider");

            var schedules = UnityEngine.Object.FindObjectsByType<NPCSchedule>(FindObjectsInactive.Include);
            Require(HasSlot(schedules, "joren", "Bracing the Wend bridge") &&
                    HasSlot(schedules, "theo", "Delivering timber to the Wend bridge") &&
                    HasSlot(schedules, "pell", "Keeping the Wend bridge ledger"),
                "Joren, Theo, and Pell do not all have authored bridge-work routines");
        }

        private static void VerifyAtomicContributions(RestorationProjectData project)
        {
            RestorationProjects.HydrateFrom(null);
            Require(RestorationProjects.Advance(project, RestorationStage.Surveyed),
                "bridge did not enter Surveyed for the funding fixture");
            CoinPurse.Add(40, "verify.bridge.seed");
            var contributions = project.Contributions;
            Require(contributions != null && contributions.Length == 2,
                "bridge needs exactly two independently legible contribution lines");

            long beforeTimber = SaveManager.InspectSlot(3).Revision;
            Require(RestorationProjects.Contribute(project, contributions[0]) ==
                    RestorationProjects.ContributionResult.Funded,
                "timber contribution failed");
            Require(CoinPurse.TotalCopper == 16 &&
                    GameScores.HasFlag("wend_bridge_timber_funded") &&
                    RestorationProjects.GetStage(project) == RestorationStage.Surveyed &&
                    SaveManager.InspectSlot(3).Revision == beforeTimber + 1,
                "timber was not one durable 24-copper transaction that leaves iron outstanding");
            Require(RestorationProjects.Contribute(project, contributions[0]) ==
                    RestorationProjects.ContributionResult.AlreadyFunded && CoinPurse.TotalCopper == 16,
                "duplicate timber funding spent coin twice");

            long beforeIron = SaveManager.InspectSlot(3).Revision;
            Require(RestorationProjects.Contribute(project, contributions[1]) ==
                    RestorationProjects.ContributionResult.Funded,
                "iron contribution failed");
            Require(CoinPurse.TotalCopper == 4 && GameScores.HasFlag("wend_bridge_iron_funded") &&
                    GameScores.HasFlag("wend_bridge_supplies_ready") &&
                    RestorationProjects.GetStage(project) == RestorationStage.SuppliesCommitted &&
                    SaveManager.InspectSlot(3).Revision == beforeIron + 1,
                "both supply lines did not atomically promote the bridge to SuppliesCommitted");
        }

        private static void VerifyOvernightWorkAndFirstUse(RestorationProjectData project, TimeManager clock)
        {
            Require(clock != null, "TimeManager is missing from the bridge fixture");
            int day = clock.Day;
            clock.SetTime(day, 19f);
            clock.AdvanceTo(day + 1, 7f, true);
            Require(GameScores.HasFlag("wend_bridge_work_started") &&
                    !GameScores.HasFlag("wend_bridge_restored") &&
                    RestorationProjects.GetStage(project) == RestorationStage.WorkUnderway,
                "the first rollover cascaded past the distinct crew-work day");

            var root = GameObject.Find("_LivingRestoration_WendBridge");
            Require(root.transform.Find("RestrictedFootway").gameObject.activeSelf &&
                    root.transform.Find("BridgeCrewWorksite").gameObject.activeSelf,
                "WorkUnderway did not keep the safe lane while revealing the crew site");
            var reveal = root.GetComponents<RestorationRevealDirector>()
                .First(candidate => candidate.Project == project);
            int queuedBefore = reveal.QueuedCount;
            clock.AdvanceTo(day + 2, 7f, true);
            Require(GameScores.HasFlag("wend_bridge_restored") &&
                    RestorationProjects.GetStage(project) == RestorationStage.Restored &&
                    reveal.QueuedCount == queuedBefore + 1,
                "the second rollover did not reopen and reveal the bridge exactly once");
            reveal.CancelPending();

            var use = root.GetComponentInChildren<RestorationUseTrigger>(true);
            var player = GameObject.FindGameObjectWithTag("Player");
            var playerCollider = player != null ? player.GetComponentInChildren<Collider>() : null;
            Require(use != null && playerCollider != null, "first-crossing fixture lacks its trigger or player collider");
            use.gameObject.SendMessage("OnTriggerEnter", playerCollider, SendMessageOptions.DontRequireReceiver);
            Require(GameScores.HasFlag("wend_bridge_in_use") &&
                    RestorationProjects.GetStage(project) == RestorationStage.Occupied &&
                    root.transform.Find("CrossingInUse").gameObject.activeSelf,
                "Wren's first restored crossing did not ink the final In Use stage");
        }

        private static void VerifyFailureRollback(RestorationProjectData project)
        {
            SaveCoordinator.StartNewGame(3);
            RestorationProjects.Advance(project, RestorationStage.Surveyed);
            CoinPurse.Add(30, "verify.bridge.rollback.seed");
            long revision = SaveManager.InspectSlot(3).Revision;
            int scoreEvents = 0;
            Action changed = () => scoreEvents++;
            GameScores.OnChanged += changed;
            RestorationProjects.ContributionResult result;
            try
            {
                SaveManager.EditorRejectNextAtomicCommit = true;
                result = RestorationProjects.Contribute(project, project.Contributions[0]);
            }
            finally
            {
                GameScores.OnChanged -= changed;
            }
            Require(result == RestorationProjects.ContributionResult.SaveUnavailable &&
                    CoinPurse.TotalCopper == 30 && !GameScores.HasFlag("wend_bridge_timber_funded") &&
                    RestorationProjects.GetStage(project) == RestorationStage.Surveyed &&
                    SaveManager.InspectSlot(3).Revision == revision && scoreEvents == 0,
                "a rejected final save leaked coin, flags, stages, revisions, or events");
        }

        private static bool HasSlot(NPCSchedule[] schedules, string npcId, string label)
        {
            var schedule = schedules.FirstOrDefault(candidate => candidate.Actor != null &&
                candidate.Actor.name.IndexOf(npcId, StringComparison.OrdinalIgnoreCase) >= 0);
            return schedule != null && schedule.FindSlot(label) >= 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BridgeRestorationVerifier] " + message);
        }
    }
}
#endif
