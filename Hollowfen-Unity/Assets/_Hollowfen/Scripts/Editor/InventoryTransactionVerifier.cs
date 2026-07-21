#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Items;
using Hollowfen.Map;
using Hollowfen.Quests;
using Hollowfen.Requests;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Destructive transaction tests isolated under a unique temporary save directory. Runtime
    /// stores are snapshotted and restored so the verifier can run without touching real journals.
    /// </summary>
    public static class InventoryTransactionVerifier
    {
        public static string RunAll()
        {
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            var originalInventory = InventoryRuntime.ToSnapshot();
            int originalCopper = CoinPurse.TotalCopper;
            var originalLedger = CoinPurse.ToLedgerSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            QuestManager.IsCompleted("__inventory_transaction_snapshot__");
            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            var originalRequests = VillageRequests.ToSnapshot();
            LocationMarker originalWaypoint = LocationRegistry.ActiveWaypoint;

            string testDirectory = Path.Combine(Path.GetTempPath(),
                "hollowfen-inventory-transaction-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;
            SaveManager.SetActiveSlot(0);

            try
            {
                VerifyStrictBatchCommit();
                VerifyVillageDeliveryAtomicCommit();
                VerifyVillageDeliveryFailureIsolation();
                VerifyFinalVillageCommitFailureIsolation();
                return "INVENTORY TRANSACTIONS — PASS: strict batch validation, duplicate aggregation, " +
                       "single-revision village commit, damaged-journal isolation, and final-commit " +
                       "rollback with zero quest/card/UI/achievement publication";
            }
            finally
            {
                if (SaveManager.IsAtomicTransactionActive)
                    SaveManager.EditorCancelAtomicTransactionForVerification();
                SaveManager.EditorRejectNextAtomicCommit = false;
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                InventoryRuntime.HydrateFrom(originalInventory);
                CoinPurse.HydrateFrom(originalCopper, originalLedger);
                GameScores.HydrateFrom(originalScores);
                VillageRequests.HydrateFrom(originalRequests);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
                if (originalWaypoint != null) LocationRegistry.SetWaypoint(originalWaypoint);
                else LocationRegistry.ClearWaypoint();
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyStrictBatchCommit()
        {
            var database = Resources.Load<MushroomFieldGuideDatabase>("MushroomFieldGuideDatabase");
            var species = database?.Entries?.FirstOrDefault(entry => entry != null);
            Require(species != null, "field-guide database has no species for the transaction fixture");

            PrepareState(1, new InventorySnapshot
            {
                Ids = new[] { species.Id },
                Counts = new[] { 3 },
            });

            int events = 0;
            Action<string, int> changed = (_, __) => events++;
            InventoryRuntime.OnChanged += changed;
            try
            {
                long initialRevision = SaveManager.InspectSlot(0).Revision;
                Require(!InventoryRuntime.TryRemoveBatch(new[] { species }, new[] { 1, 1 }, out var mismatch) &&
                        mismatch == InventoryRuntime.BatchRemovalFailure.InvalidBatch,
                    "mismatched batch arrays were accepted");
                Require(!InventoryRuntime.TryRemoveBatch(new[] { species }, new[] { 0 }, out var invalidAmount) &&
                        invalidAmount == InventoryRuntime.BatchRemovalFailure.InvalidBatch,
                    "non-positive batch amount was coerced instead of rejected");
                Require(!InventoryRuntime.TryRemoveBatch(new[] { species, species }, new[] { 2, 2 },
                            out var insufficient) &&
                        insufficient == InventoryRuntime.BatchRemovalFailure.InsufficientStock,
                    "duplicate requirements were not aggregated before stock validation");
                Require(InventoryRuntime.GetCount(species) == 3 &&
                        SaveManager.InspectSlot(0).Revision == initialRevision && events == 0,
                    "a rejected batch changed inventory, disk revision, or events");

                Require(InventoryRuntime.TryRemoveBatch(new[] { species, species }, new[] { 1, 1 },
                            out var success) && success == InventoryRuntime.BatchRemovalFailure.None,
                    "valid duplicate batch did not commit");
                var committed = SaveManager.InspectSlot(0);
                Require(InventoryRuntime.GetCount(species) == 1 && committed.CanLoad &&
                        committed.Revision == initialRevision + 1 &&
                        SnapshotCount(committed.Meta.Inventory, species.Id) == 1 && events == 1,
                    "successful batch was not one durable revision plus one live-state event");
            }
            finally
            {
                InventoryRuntime.OnChanged -= changed;
            }
        }

        private static void VerifyVillageDeliveryFailureIsolation()
        {
            int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            PrepareState(day, null);
            var request = VillageRequests.CurrentForNpc("marra");
            Require(request != null && !request.OneShot && request.RewardCopper > 0,
                "no eligible paid Marra request exists for the failure fixture");

            InventoryRuntime.HydrateFrom(Requirements(request));
            WriteCurrentState(day);
            WriteCurrentState(day); // ensure both primary and previous-revision backup exist

            string primaryPath = SaveManager.SlotPath(0);
            string backupPath = SaveManager.BackupPathForSlot(0);
            File.WriteAllText(primaryPath, "{}");
            File.WriteAllText(backupPath, "{ deliberately damaged backup");
            string primaryBefore = File.ReadAllText(primaryPath);
            string backupBefore = File.ReadAllText(backupPath);
            var inventoryBefore = InventoryRuntime.ToSnapshot();
            string requestsBefore = JsonUtility.ToJson(VillageRequests.ToSnapshot());
            string[] questsBefore = QuestManager.CompletedQuestIds.ToArray();
            string[] cardsBefore = QuestManager.UnlockedStoryCardIds.ToArray();

            int inventoryEvents = 0;
            int requestEvents = 0;
            Action<string, int> inventoryChanged = (_, __) => inventoryEvents++;
            Action requestChanged = () => requestEvents++;
            InventoryRuntime.OnChanged += inventoryChanged;
            VillageRequests.OnChanged += requestChanged;
            VillageRequests.CompletionResult result;
            try
            {
                Require(VillageRequests.CanDeliver(request), "failure fixture basket is not deliverable");
                result = VillageRequests.Complete(request);
            }
            finally
            {
                InventoryRuntime.OnChanged -= inventoryChanged;
                VillageRequests.OnChanged -= requestChanged;
            }

            Require(!result.Success && result.Copper == 0 && !result.FirstCompletion &&
                    result.Failure != null && result.Failure.Contains("could not be saved"),
                "delivery did not report the durable-save failure");
            Require(SnapshotsEqual(InventoryRuntime.ToSnapshot(), inventoryBefore) &&
                    CoinPurse.TotalCopper == 0 && inventoryEvents == 0 && requestEvents == 0,
                "failed delivery consumed ingredients, granted copper, or published events");
            Require(!GameScores.HasFlag("village_request_first_" + request.Id) &&
                    GameScores.GetRelationship(request.NpcId) == 0 && GameScores.Knowledge == 0,
                "failed delivery granted its first-completion flag or score rewards");
            if (request.CompletionFlagIds != null)
                foreach (string flag in request.CompletionFlagIds)
                    Require(string.IsNullOrWhiteSpace(flag) || !GameScores.HasFlag(flag),
                        "failed delivery granted completion flag " + flag);
            Require(JsonUtility.ToJson(VillageRequests.ToSnapshot()) == requestsBefore &&
                    QuestManager.CompletedQuestIds.OrderBy(id => id).SequenceEqual(questsBefore.OrderBy(id => id)) &&
                    QuestManager.UnlockedStoryCardIds.OrderBy(id => id).SequenceEqual(cardsBefore.OrderBy(id => id)),
                "failed delivery changed request, quest, or story-card state");
            Require(File.ReadAllText(primaryPath) == primaryBefore &&
                    File.ReadAllText(backupPath) == backupBefore &&
                    !File.Exists(SaveManager.TempPathForSlot(0)) &&
                    Directory.GetFiles(SaveManager.SaveDirectory, "slot0.json.corrupt-*").Length == 0,
                "failed delivery wrote or quarantined any save artifact");
        }

        private static void VerifyVillageDeliveryAtomicCommit()
        {
            int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            PrepareState(day, null);
            var request = VillageRequests.CurrentForNpc("marra");
            Require(request != null && !request.OneShot && request.RewardCopper > 0,
                "no eligible paid Marra request exists for the success fixture");

            InventoryRuntime.HydrateFrom(Requirements(request));
            WriteCurrentState(day);
            var before = SaveManager.InspectSlot(0);
            Require(before.CanLoad, "success fixture has no loadable base revision");

            var result = VillageRequests.Complete(request);
            var after = SaveManager.InspectSlot(0);
            Require(result.Success && result.Copper == request.RewardCopper && result.FirstCompletion,
                "valid village delivery did not report success");
            Require(after.CanLoad && after.Revision == before.Revision + 1,
                "village delivery did not commit exactly one durable revision");
            Require(InventoryRuntime.TotalCount == 0 &&
                    (after.Meta.Inventory?.Counts == null || after.Meta.Inventory.Counts.All(count => count == 0)),
                "successful village delivery did not consume the staged basket on disk and in memory");
            Require(CoinPurse.TotalCopper == request.RewardCopper &&
                    after.Meta.CoinsCopper == request.RewardCopper,
                "successful village delivery did not commit its copper reward");
            Require(GameScores.HasFlag("village_request_first_" + request.Id) &&
                    after.Meta.GameFlagIds != null &&
                    after.Meta.GameFlagIds.Contains("village_request_first_" + request.Id),
                "successful village delivery did not commit its first-completion flag");
            Require(JsonUtility.ToJson(after.Meta.VillageRequests) ==
                    JsonUtility.ToJson(VillageRequests.ToSnapshot()),
                "successful village delivery disk request state differs from runtime state");
        }

        private static void VerifyFinalVillageCommitFailureIsolation()
        {
            int day = TimeManager.Instance != null ? TimeManager.Instance.Day : 1;
            var request = PrepareFestivalState(day);
            Require(request != null && request.OneShot && request.CompleteQuest != null &&
                    request.CompleteQuest.UnlockStoryCardOnComplete != null,
                "festival request is not a quest/card transaction fixture");
            Require(VillageRequests.CanDeliver(request), "festival transaction fixture is not deliverable");

            var diskBefore = SaveManager.InspectSlot(0);
            byte[] primaryBefore = ReadArtifact(SaveManager.SlotPath(0));
            byte[] backupBefore = ReadArtifact(SaveManager.BackupPathForSlot(0));
            byte[] tempBefore = ReadArtifact(SaveManager.TempPathForSlot(0));
            var inventoryBefore = InventoryRuntime.ToSnapshot();
            int copperBefore = CoinPurse.TotalCopper;
            string ledgerBefore = JsonUtility.ToJson(CoinPurse.ToLedgerSnapshot());
            var scoresBefore = new SaveSlotMeta();
            GameScores.WriteTo(scoresBefore);
            string scoreJsonBefore = ScoreJson(scoresBefore);
            string requestsBefore = JsonUtility.ToJson(VillageRequests.ToSnapshot());
            string[] questsBefore = QuestManager.CompletedQuestIds.ToArray();
            string[] cardsBefore = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData activeBefore = QuestManager.ActiveQuest;
            LocationMarker waypointBefore = LocationRegistry.ActiveWaypoint;

            int inventoryEvents = 0;
            int requestEvents = 0;
            int scoreEvents = 0;
            int coinEvents = 0;
            int questCompletedEvents = 0;
            int questStartedEvents = 0;
            int storyCardEvents = 0;
            int waypointEvents = 0;
            int achievementEvents = 0;
            Action<string, int> inventoryChanged = (_, __) => inventoryEvents++;
            Action requestChanged = () => requestEvents++;
            Action scoresChanged = () => scoreEvents++;
            Action<int> coinsChanged = _ => coinEvents++;
            Action<QuestData> questCompleted = _ => questCompletedEvents++;
            Action<QuestData> questStarted = _ => questStartedEvents++;
            Action<string> storyCard = _ => storyCardEvents++;
            Action<LocationMarker> waypoint = _ => waypointEvents++;
            Action<string> achievement = _ => achievementEvents++;

            VillageRequests.CompletionResult result;
            InventoryRuntime.OnChanged += inventoryChanged;
            VillageRequests.OnChanged += requestChanged;
            GameScores.OnChanged += scoresChanged;
            CoinPurse.OnChanged += coinsChanged;
            QuestManager.QuestCompleted += questCompleted;
            QuestManager.QuestStarted += questStarted;
            QuestManager.StoryCardUnlocked += storyCard;
            LocationRegistry.WaypointChanged += waypoint;
            GameEvents.OnAchievementTrigger += achievement;
            try
            {
                SaveManager.EditorRejectNextAtomicCommit = true;
                result = VillageRequests.Complete(request);
            }
            finally
            {
                SaveManager.EditorRejectNextAtomicCommit = false;
                InventoryRuntime.OnChanged -= inventoryChanged;
                VillageRequests.OnChanged -= requestChanged;
                GameScores.OnChanged -= scoresChanged;
                CoinPurse.OnChanged -= coinsChanged;
                QuestManager.QuestCompleted -= questCompleted;
                QuestManager.QuestStarted -= questStarted;
                QuestManager.StoryCardUnlocked -= storyCard;
                LocationRegistry.WaypointChanged -= waypoint;
                GameEvents.OnAchievementTrigger -= achievement;
            }

            Require(!result.Success && result.Copper == 0 && !result.FirstCompletion &&
                    result.Failure != null && result.Failure.Contains("could not be saved"),
                "final full-snapshot fault did not reject the delivery");
            Require(!SaveManager.IsAtomicTransactionActive,
                "failed final commit leaked an active save transaction");
            Require(SnapshotsEqual(InventoryRuntime.ToSnapshot(), inventoryBefore) &&
                    CoinPurse.TotalCopper == copperBefore &&
                    JsonUtility.ToJson(CoinPurse.ToLedgerSnapshot()) == ledgerBefore,
                "failed final commit did not restore inventory or coin state");
            var scoresAfter = new SaveSlotMeta();
            GameScores.WriteTo(scoresAfter);
            Require(ScoreJson(scoresAfter) == scoreJsonBefore &&
                    JsonUtility.ToJson(VillageRequests.ToSnapshot()) == requestsBefore,
                "failed final commit did not restore scores/flags or request state");
            Require(QuestManager.CompletedQuestIds.OrderBy(id => id).SequenceEqual(questsBefore.OrderBy(id => id)) &&
                    QuestManager.UnlockedStoryCardIds.OrderBy(id => id).SequenceEqual(cardsBefore.OrderBy(id => id)) &&
                    QuestManager.ActiveQuest == activeBefore && LocationRegistry.ActiveWaypoint == waypointBefore,
                "failed final commit did not restore quest/card/waypoint state");
            Require(inventoryEvents == 0 && requestEvents == 0 && scoreEvents == 0 && coinEvents == 0 &&
                    questCompletedEvents == 0 && questStartedEvents == 0 && storyCardEvents == 0 &&
                    waypointEvents == 0 && achievementEvents == 0,
                "failed final commit published a UI, quest, story-card, waypoint, or achievement event");
            var diskAfter = SaveManager.InspectSlot(0);
            Require(diskAfter.CanLoad && diskAfter.Revision == diskBefore.Revision &&
                    ArtifactsEqual(primaryBefore, ReadArtifact(SaveManager.SlotPath(0))) &&
                    ArtifactsEqual(backupBefore, ReadArtifact(SaveManager.BackupPathForSlot(0))) &&
                    ArtifactsEqual(tempBefore, ReadArtifact(SaveManager.TempPathForSlot(0))),
                "failed final commit changed a save artifact or disk revision");
        }

        private static void PrepareState(int day, InventorySnapshot inventory)
        {
            SaveManager.DeleteSlot(0);
            SaveManager.SetActiveSlot(0);
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(new[] { "firstSale" }, Array.Empty<string>());
            GameScores.HydrateFrom(new SaveSlotMeta());
            CoinPurse.HydrateFrom(0);
            VillageRequests.HydrateFrom(null);
            InventoryRuntime.HydrateFrom(inventory);
            WriteCurrentState(day);
        }

        private static VillageRequestData PrepareFestivalState(int day)
        {
            SaveManager.DeleteSlot(0);
            SaveManager.SetActiveSlot(0);
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameFlagIds = new[] { "festival_gathering_active" },
            });
            CoinPurse.HydrateFrom(0);
            VillageRequests.HydrateFrom(null);

            var request = VillageRequests.Resolve("festival_four_dishes");
            Require(request != null && request.CompleteQuest != null,
                "festival request/quest data is missing");
            QuestManager.StartQuest(request.CompleteQuest);
            InventoryRuntime.HydrateFrom(Requirements(request));
            WriteCurrentState(day);
            WriteCurrentState(day); // preserve a previous-revision backup for byte-level checks
            Require(VillageRequests.CurrentForNpc(request.NpcId) == request,
                "festival request is not the active authored story request");
            return request;
        }

        private static void WriteCurrentState(int day)
        {
            var active = QuestManager.ActiveQuest;
            var meta = new SaveSlotMeta
            {
                CurrentQuestId = active != null ? active.Id : "inventory-transaction-verifier",
                CurrentAct = active != null ? active.Act : 2,
                GameDay = Math.Max(1, day),
                GameHour = 9f,
                Inventory = InventoryRuntime.ToSnapshot(),
                CompletedQuestIds = QuestManager.CompletedQuestIds.ToArray(),
                UnlockedStoryCardIds = QuestManager.UnlockedStoryCardIds.ToArray(),
                CoinsCopper = CoinPurse.TotalCopper,
                CoinLedger = CoinPurse.ToLedgerSnapshot(),
                VillageRequests = VillageRequests.ToSnapshot(),
            };
            GameScores.WriteTo(meta);
            SaveManager.WriteSlot(0, meta);
        }

        private static string ScoreJson(SaveSlotMeta meta)
        {
            int count = Math.Min(meta?.RelationshipNpcIds?.Length ?? 0,
                meta?.RelationshipValues?.Length ?? 0);
            var relationships = new string[count];
            for (int i = 0; i < count; i++)
                relationships[i] = meta.RelationshipNpcIds[i] + "=" + meta.RelationshipValues[i];
            Array.Sort(relationships, StringComparer.Ordinal);
            string[] flags = (meta?.GameFlagIds ?? Array.Empty<string>())
                .OrderBy(flag => flag, StringComparer.Ordinal).ToArray();
            return (meta?.VillageHope ?? 0) + "|" + (meta?.Knowledge ?? 0) + "|" +
                   string.Join(",", relationships) + "|" + string.Join(",", flags);
        }

        private static byte[] ReadArtifact(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        private static bool ArtifactsEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            return left != null && right != null && left.SequenceEqual(right);
        }

        private static InventorySnapshot Requirements(VillageRequestData request)
        {
            var totals = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < request.RequirementCount; i++)
            {
                string id = request.RequiredSpecies[i].Id;
                totals.TryGetValue(id, out int current);
                totals[id] = current + request.RequiredCountAt(i);
            }
            return new InventorySnapshot
            {
                Ids = totals.Keys.OrderBy(id => id).ToArray(),
                Counts = totals.OrderBy(row => row.Key).Select(row => row.Value).ToArray(),
            };
        }

        private static int SnapshotCount(InventorySnapshot snapshot, string id)
        {
            if (snapshot?.Ids == null || snapshot.Counts == null) return 0;
            int length = Math.Min(snapshot.Ids.Length, snapshot.Counts.Length);
            for (int i = 0; i < length; i++)
                if (snapshot.Ids[i] == id) return snapshot.Counts[i];
            return 0;
        }

        private static bool SnapshotsEqual(InventorySnapshot left, InventorySnapshot right)
        {
            if (left?.Ids == null || left.Counts == null || right?.Ids == null || right.Counts == null)
                return left?.Ids == null && left?.Counts == null && right?.Ids == null && right?.Counts == null;
            if (left.Ids.Length != left.Counts.Length || right.Ids.Length != right.Counts.Length) return false;
            var a = left.Ids.Select((id, index) => new { id, count = left.Counts[index] })
                .OrderBy(row => row.id).ToArray();
            var b = right.Ids.Select((id, index) => new { id, count = right.Counts[index] })
                .OrderBy(row => row.id).ToArray();
            return a.Length == b.Length && a.Zip(b, (x, y) => x.id == y.id && x.count == y.count).All(equal => equal);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[InventoryTransactionVerifier] " + message);
        }
    }
}
#endif
