using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hollowfen.Save
{
    // Versioned, semantically validated save storage. Each slot may have a primary, flushed
    // temporary candidate, and backup; inspection selects the highest valid revision without
    // ever treating parseable garbage as a new game.
    public static class SaveManager
    {
        public const int TotalSlots = 4; // 3 manual + 1 autosave
        public const int AutosaveSlot = 0;

        private static readonly HashSet<string> RecoveryNotices = new HashSet<string>();

        private sealed class AtomicTransaction
        {
            internal int Slot;
            internal long BaseRevision;
            internal SaveSlotMeta StagedMeta;
            internal readonly List<Action> Publications = new List<Action>();
        }

        private static AtomicTransaction _atomicTransaction;
        private static bool _committingAtomicTransaction;

#if UNITY_EDITOR
        // Verification harnesses use an isolated directory instead of mutating real journals.
        public static string EditorSaveDirectoryOverride { get; set; }

        // Entering Play Mode runs SubsystemRegistration before scene Awake methods. Automation
        // must therefore arm its journal override while still in Edit Mode; applying it after
        // manage_editor.play races TimeManager and other boot-time readers against the real save.
        private const string EditorPendingSaveOverrideKey =
            "Hollowfen.SaveManager.PendingSaveDirectoryOverride";
        private const string EditorActiveSaveOverrideKey =
            "Hollowfen.SaveManager.ActiveSaveDirectoryOverride";

        public static void EditorArmSaveDirectoryOverrideForNextPlay(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("An isolated save directory is required.", nameof(directory));
            UnityEditor.SessionState.SetString(EditorPendingSaveOverrideKey, directory);
            // Keep the same isolated journal through any script/domain reload that occurs
            // during Play Mode. The harness explicitly clears both keys after stopping.
            UnityEditor.SessionState.SetString(EditorActiveSaveOverrideKey, directory);
        }

        public static void EditorClearSaveDirectoryOverride()
        {
            EditorSaveDirectoryOverride = null;
            UnityEditor.SessionState.EraseString(EditorPendingSaveOverrideKey);
            UnityEditor.SessionState.EraseString(EditorActiveSaveOverrideKey);
        }

        // One-shot, pre-write fault injection for the isolated transaction verifier. This fails
        // the final full-snapshot commit, after every runtime system has staged its new state.
        public static bool EditorRejectNextAtomicCommit { get; set; }

        public static void EditorCancelAtomicTransactionForVerification()
        {
            CancelAtomicTransaction();
        }
#endif

        public static int ActiveSlot { get; private set; } = AutosaveSlot;
        public static bool IsAtomicTransactionActive => _atomicTransaction != null;

        public static void SetActiveSlot(int slot)
        {
            ActiveSlot = Mathf.Clamp(slot, 0, TotalSlots - 1);
        }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            ActiveSlot = AutosaveSlot;
            RecoveryNotices.Clear();
            _atomicTransaction = null;
            _committingAtomicTransaction = false;
#if UNITY_EDITOR
            string pendingOverride = UnityEditor.SessionState.GetString(
                EditorPendingSaveOverrideKey, string.Empty);
            string activeOverride = UnityEditor.SessionState.GetString(
                EditorActiveSaveOverrideKey, string.Empty);
            EditorSaveDirectoryOverride = !string.IsNullOrWhiteSpace(pendingOverride)
                ? pendingOverride
                : activeOverride;
            if (!string.IsNullOrWhiteSpace(EditorSaveDirectoryOverride))
                UnityEditor.SessionState.SetString(EditorActiveSaveOverrideKey,
                    EditorSaveDirectoryOverride);
            UnityEditor.SessionState.EraseString(EditorPendingSaveOverrideKey);
            if (string.IsNullOrWhiteSpace(EditorSaveDirectoryOverride))
                EditorSaveDirectoryOverride = null;
            EditorRejectNextAtomicCommit = false;
#endif
        }
#endif

        public static string SaveDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorSaveDirectoryOverride)) return EditorSaveDirectoryOverride;
                // Assembly reloads during Play Mode reset static properties without invoking a
                // second SubsystemRegistration pass. SessionState remains the durable safety
                // authority, so never fall through to a tester's real journals while armed.
                string activeOverride = UnityEditor.SessionState.GetString(
                    EditorActiveSaveOverrideKey, string.Empty);
                if (!string.IsNullOrEmpty(activeOverride)) return activeOverride;
#endif
                return Path.Combine(Application.persistentDataPath, "saves");
            }
        }

        public static string SlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"slot{Mathf.Clamp(slot, 0, TotalSlots - 1)}.json");

        public static string BackupPathForSlot(int slot) => SlotPath(slot) + ".bak";
        public static string TempPathForSlot(int slot) => SlotPath(slot) + ".tmp";

        public static bool SlotHasData(int slot) => InspectSlot(slot).HasArtifacts;

        public static SaveSlotInspection InspectSlot(int slot)
        {
            slot = Mathf.Clamp(slot, 0, TotalSlots - 1);
            var candidates = ReadCandidates(slot);
            bool any = false;
            SaveFileFormat.Candidate winner = null;
            var errors = new List<string>();

            foreach (var candidate in candidates)
            {
                if (!candidate.Exists) continue;
                any = true;
                if (!candidate.IsValid)
                {
                    errors.Add(Path.GetFileName(candidate.Path) + ": " + candidate.Error);
                    continue;
                }
                if (winner == null || candidate.Revision > winner.Revision ||
                    (candidate.Revision == winner.Revision && candidate.Priority < winner.Priority))
                    winner = candidate;
            }

            if (!any)
                return new SaveSlotInspection(SaveInspectionStatus.Empty, null, null, 0, "No journal data exists.");
            if (winner == null)
                return new SaveSlotInspection(SaveInspectionStatus.Corrupt, null, null, 0,
                    errors.Count > 0 ? string.Join("; ", errors) : "No valid journal snapshot was found.");
            if (winner.IsIncompatible)
                return new SaveSlotInspection(SaveInspectionStatus.IncompatibleNewerVersion, null,
                    winner.Path, winner.Revision, winner.Error);

            bool primary = winner.Priority == 0;
            string detail = primary
                ? (winner.IsLegacy ? "Historical journal is ready and will upgrade on its next save." : "Journal is ready.")
                : $"Recovered revision {winner.Revision} from {Path.GetFileName(winner.Path)}.";
            return new SaveSlotInspection(primary ? SaveInspectionStatus.Ready : SaveInspectionStatus.Recovered,
                winner.Meta, winner.Path, winner.Revision, detail);
        }

        public static SaveSlotMeta GetSlotMeta(int slot)
        {
            var inspection = InspectSlot(slot);
            if (!inspection.CanLoad) return null;
            if (inspection.Status == SaveInspectionStatus.Recovered)
            {
                string key = slot + ":" + inspection.Revision + ":" + inspection.SourcePath;
                if (RecoveryNotices.Add(key))
                    Debug.LogWarning($"[SaveManager] Slot {slot} {inspection.Detail}");
            }
            return inspection.Meta;
        }

        public static void DeleteSlot(int slot)
        {
            string path = SlotPath(slot);
            DeleteIfPresent(path);
            DeleteIfPresent(BackupPathForSlot(slot));
            DeleteIfPresent(TempPathForSlot(slot));
            if (Directory.Exists(SaveDirectory))
            {
                foreach (string quarantine in Directory.GetFiles(SaveDirectory,
                             Path.GetFileName(path) + ".corrupt-*"))
                    DeleteIfPresent(quarantine);
            }
        }

        public static void WritePlaceholderToSlot(int slot)
        {
            EnsureDirectory();
            WriteJsonAtomically(slot, new SaveSlotMeta
            {
                SlotNumber = slot,
                CurrentQuest = Localization.Get("quest.arrive.name"),
                CurrentQuestId = "arrive",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f,
            });
        }

        public static void WriteSlot(int slot, SaveSlotMeta meta)
        {
            if (meta == null) return;
            slot = Mathf.Clamp(slot, 0, TotalSlots - 1);
            if (_atomicTransaction != null && !_committingAtomicTransaction)
            {
                if (slot != _atomicTransaction.Slot)
                    throw new InvalidOperationException("Cannot stage a different save slot inside an atomic transaction.");
                _atomicTransaction.StagedMeta = CloneMeta(meta);
                return;
            }
            EnsureDirectory();
            WriteJsonAtomically(slot, meta);
        }

        // Begins a synchronous, main-thread save transaction. Targeted AutoSave* calls become
        // no-ops, WriteSlot stages the latest full snapshot, and outward publications are held
        // until that snapshot is verified as the new recovery winner.
        internal static bool TryBeginAtomicTransaction(out string failure)
        {
            failure = null;
            if (_atomicTransaction != null)
            {
                failure = "Another save transaction is already active.";
                return false;
            }

            var inspection = InspectSlot(ActiveSlot);
            if (!inspection.CanLoad)
            {
                failure = $"The active journal is not writable ({inspection.Status}).";
                return false;
            }

            _atomicTransaction = new AtomicTransaction
            {
                Slot = ActiveSlot,
                BaseRevision = inspection.Revision,
            };
            return true;
        }

        // Returns false with the transaction still active, allowing the caller to restore all
        // runtime stores while notifications and targeted autosaves remain suppressed. A write
        // exception after a force-flushed temp still succeeds when inspection proves that exact
        // staged payload is the higher-revision recovery winner.
        internal static bool TryCommitAtomicTransaction(out SaveSlotInspection inspection)
        {
            inspection = null;
            var transaction = _atomicTransaction;
            if (transaction == null || transaction.StagedMeta == null) return false;

#if UNITY_EDITOR
            if (EditorRejectNextAtomicCommit)
            {
                EditorRejectNextAtomicCommit = false;
                return false;
            }
#endif

            Exception writeFailure = null;
            _committingAtomicTransaction = true;
            try
            {
                WriteJsonAtomically(transaction.Slot, transaction.StagedMeta);
            }
            catch (Exception exception)
            {
                writeFailure = exception;
            }
            finally
            {
                _committingAtomicTransaction = false;
            }

            try { inspection = InspectSlot(transaction.Slot); }
            catch (Exception exception)
            {
                if (writeFailure == null) writeFailure = exception;
            }

            bool committed = inspection != null && inspection.CanLoad &&
                             inspection.Revision > transaction.BaseRevision &&
                             string.Equals(
                                 SaveFileFormat.CanonicalPayloadJson(inspection.Meta, transaction.Slot),
                                 SaveFileFormat.CanonicalPayloadJson(transaction.StagedMeta, transaction.Slot),
                                 StringComparison.Ordinal);
            if (!committed)
            {
                if (writeFailure != null)
                    Debug.LogWarning("[SaveManager] Atomic save commit failed: " + writeFailure.Message);
                return false;
            }

            if (writeFailure != null)
                Debug.LogWarning("[SaveManager] Atomic save recovered from a late write error: " + writeFailure.Message);

            var publications = transaction.Publications.ToArray();
            _atomicTransaction = null;
            foreach (var publication in publications) InvokePublicationSafely(publication);
            return true;
        }

        internal static void CancelAtomicTransaction()
        {
            _atomicTransaction = null;
            _committingAtomicTransaction = false;
#if UNITY_EDITOR
            EditorRejectNextAtomicCommit = false;
#endif
        }

        // Stores use this for UI/event/achievement publication. State mutations still happen
        // synchronously, but a failed transaction discards every externally visible callback.
        internal static void PublishAfterAtomicCommit(Action publication)
        {
            if (publication == null) return;
            if (_atomicTransaction != null && !_committingAtomicTransaction)
            {
                _atomicTransaction.Publications.Add(publication);
                return;
            }
            InvokePublicationSafely(publication);
        }

        public static void AutoSaveInventory(InventorySnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.Inventory = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveIntroSeen()
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.HomecomingIntroSeen = true;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveCoins(int totalCopper, CoinLedgerSnapshot ledger = null)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.CoinsCopper = totalCopper;
            meta.CoinLedger = ledger;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveQuestState(string[] completedQuestIds, string[] unlockedStoryCardIds)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.CompletedQuestIds = completedQuestIds;
            meta.UnlockedStoryCardIds = unlockedStoryCardIds;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveKeyItems(string[] ids)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.KeyItemIds = ids;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveScores()
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            Hollowfen.Quests.GameScores.WriteTo(meta);
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveDiscoveredLocations(string[] ids)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.DiscoveredLocationIds = ids;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveGrowBeds(GrowBedSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.GrowBeds = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveDiscovery(string[] ids)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.DiscoveredMushroomIds = ids;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveForageNodes(ForageNodeSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.ForageNodes = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveVillageRequests(VillageRequestSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.VillageRequests = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveRestorationProjects(RestorationSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.RestorationProjects = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveApothecaryCases(ApothecaryCaseSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.ApothecaryCases = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveVillagerRelationships(VillagerRelationshipSnapshot snapshot)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.VillagerRelationships = snapshot;
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveClockAndPlaytime(int gameDay, float gameHour, float totalPlayTimeSeconds)
        {
            SaveSlotMeta meta;
            if (!TryGetWritableMeta(out meta)) return;
            meta.GameDay = Mathf.Max(1, gameDay);
            meta.GameHour = Mathf.Repeat(gameHour, 24f);
            meta.TotalPlayTimeSeconds = Mathf.Max(0f, totalPlayTimeSeconds);
            WriteJsonAtomically(ActiveSlot, meta);
        }

        private static bool TryGetWritableMeta(out SaveSlotMeta meta)
        {
            var inspection = InspectSlot(ActiveSlot);
            if (inspection.CanLoad)
            {
                meta = inspection.Meta;
                return true;
            }

            meta = null;
            Debug.LogError($"[SaveManager] Refused autosave to slot {ActiveSlot}: {inspection.Status}. " +
                           "Only Start New Game may create an empty journal, and damaged journals are never overwritten.");
            return false;
        }

        private static SaveFileFormat.Candidate[] ReadCandidates(int slot)
        {
            return new[]
            {
                SaveFileFormat.DecodeFile(SlotPath(slot), slot, 0),
                SaveFileFormat.DecodeFile(TempPathForSlot(slot), slot, 1),
                SaveFileFormat.DecodeFile(BackupPathForSlot(slot), slot, 2),
            };
        }

        private static void WriteJsonAtomically(int slot, SaveSlotMeta meta)
        {
            slot = Mathf.Clamp(slot, 0, TotalSlots - 1);
            if (_atomicTransaction != null && !_committingAtomicTransaction)
            {
                if (slot != _atomicTransaction.Slot)
                    throw new InvalidOperationException("Cannot write a different save slot inside an atomic transaction.");
                return; // targeted AutoSave* calls are represented by the final staged WriteSlot
            }
            EnsureDirectory();
            string path = SlotPath(slot);
            string temp = TempPathForSlot(slot);
            string backup = BackupPathForSlot(slot);
            var before = ReadCandidates(slot);
            long highestRevision = 0;
            foreach (var candidate in before)
                if (candidate.IsValid) highestRevision = Math.Max(highestRevision, candidate.Revision);

            long nowMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            meta.SlotNumber = slot;
            meta.TimestampUnix = nowMilliseconds / 1000L;
            string json = SaveFileFormat.Encode(meta, highestRevision + 1L, nowMilliseconds);
            bool committed = false;

            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                SaveFileFormat.Candidate primary = before[0];
                if (File.Exists(path) && primary.IsValid && !primary.IsIncompatible)
                {
                    try
                    {
                        File.Replace(temp, path, backup);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithPortableFallback(temp, path, backup);
                    }
                }
                else if (File.Exists(path))
                {
                    // Preserve the damaged/future primary for support rather than rotating it
                    // over a good backup. The recovered snapshot becomes the new primary.
                    File.Move(path, NextQuarantinePath(path));
                    File.Move(temp, path);
                }
                else
                {
                    File.Move(temp, path);
                }
                committed = true;
            }
            finally
            {
                // A flushed temp is a legitimate recovery candidate after a failed replace.
                if (committed && File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void ReplaceWithPortableFallback(string temp, string path, string backup)
        {
            File.Copy(path, backup, true);
            File.Delete(path);
            File.Move(temp, path);
        }

        private static string NextQuarantinePath(string path)
        {
            string prefix = path + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
            string candidate = prefix;
            int suffix = 1;
            while (File.Exists(candidate)) candidate = prefix + "-" + suffix++;
            return candidate;
        }

        private static void DeleteIfPresent(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static SaveSlotMeta CloneMeta(SaveSlotMeta meta)
        {
            return JsonUtility.FromJson<SaveSlotMeta>(JsonUtility.ToJson(meta, false));
        }

        private static void InvokePublicationSafely(Action publication)
        {
            try { publication?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
        }
    }
}
