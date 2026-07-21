#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Destructive save-format tests isolated under a unique temporary directory.</summary>
    public static class SaveIntegrityVerifier
    {
        [Serializable]
        private sealed class TestEnvelope
        {
            public string FormatId;
            public int SchemaVersion;
            public long Revision;
            public long WrittenUnixMilliseconds;
            public string PayloadJson;
            public string IntegritySha256;
        }

        public static string RunAll()
        {
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            string testDirectory = Path.Combine(Path.GetTempPath(), "hollowfen-save-verifier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDirectory);
            SaveManager.EditorSaveDirectoryOverride = testDirectory;

            try
            {
                VerifyLegacyUpgrade();
                VerifyParseableCorruptionRecovery();
                VerifyChecksumRecovery();
                VerifyTempRevisionSelection();
                VerifyInvalidLoadIsolation();
                VerifyFutureVersionBarrier();
                VerifyNormalization();
                VerifyQuestIdentityPresentation();
                VerifyFullRoundTrip();
                VerifyRecoveredRewrite();
                return "SAVE INTEGRITY — PASS: legacy upgrade, authoritative quest identity, semantic corruption, checksum, temp/backup revision recovery, future-version barrier, load isolation, normalization, full round-trip, recovered rewrite";
            }
            finally
            {
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
            }
        }

        private static void VerifyLegacyUpgrade()
        {
            ResetSlot(0);
            var legacy = SampleMeta("legacy", 123);
            File.WriteAllText(SaveManager.SlotPath(0), JsonUtility.ToJson(legacy, true));
            var inspection = SaveManager.InspectSlot(0);
            Require(inspection.Status == SaveInspectionStatus.Ready && inspection.Meta.CurrentQuestId == "legacy",
                "historical flat save did not load");

            SaveManager.WriteSlot(0, inspection.Meta);
            string upgraded = File.ReadAllText(SaveManager.SlotPath(0));
            Require(upgraded.Contains("\"FormatId\": \"hollowfen.save\""), "historical save did not upgrade on write");
            Require(File.Exists(SaveManager.BackupPathForSlot(0)), "legacy primary was not retained as backup on upgrade");
        }

        private static void VerifyParseableCorruptionRecovery()
        {
            ResetSlot(0);
            SaveManager.WriteSlot(0, SampleMeta("backup-good", 10));
            SaveManager.WriteSlot(0, SampleMeta("primary-new", 20));
            File.WriteAllText(SaveManager.SlotPath(0), "{}");
            var recovered = SaveManager.InspectSlot(0);
            Require(recovered.Status == SaveInspectionStatus.Recovered &&
                    recovered.Meta.CurrentQuestId == "backup-good",
                "parseable empty primary bypassed backup recovery");
        }

        private static void VerifyChecksumRecovery()
        {
            ResetSlot(0);
            SaveManager.WriteSlot(0, SampleMeta("checksum-backup", 1));
            SaveManager.WriteSlot(0, SampleMeta("checksum-primary", 2));
            string path = SaveManager.SlotPath(0);
            string changed = File.ReadAllText(path).Replace("checksum-primary", "checksum-pr1mary");
            File.WriteAllText(path, changed);
            var recovered = SaveManager.InspectSlot(0);
            Require(recovered.Status == SaveInspectionStatus.Recovered &&
                    recovered.Meta.CurrentQuestId == "checksum-backup",
                "checksum mismatch did not select the intact backup");
        }

        private static void VerifyTempRevisionSelection()
        {
            ResetSlot(0);
            SaveManager.WriteSlot(0, SampleMeta("rev-one", 1));
            SaveManager.WriteSlot(0, SampleMeta("rev-two", 2));
            string newer = File.ReadAllText(SaveManager.SlotPath(0));
            string older = File.ReadAllText(SaveManager.BackupPathForSlot(0));
            File.WriteAllText(SaveManager.TempPathForSlot(0), newer);
            File.WriteAllText(SaveManager.SlotPath(0), older);
            var recovered = SaveManager.InspectSlot(0);
            Require(recovered.Status == SaveInspectionStatus.Recovered &&
                    recovered.Meta.CurrentQuestId == "rev-two",
                "higher-revision flushed temp did not beat older primary");

            File.WriteAllText(SaveManager.TempPathForSlot(0), older);
            File.WriteAllText(SaveManager.SlotPath(0), newer);
            var ready = SaveManager.InspectSlot(0);
            Require(ready.Status == SaveInspectionStatus.Ready && ready.Meta.CurrentQuestId == "rev-two",
                "lower-revision temp incorrectly beat primary");
        }

        private static void VerifyInvalidLoadIsolation()
        {
            ResetSlot(1);
            File.WriteAllText(SaveManager.SlotPath(1), "{}");
            File.WriteAllText(SaveManager.BackupPathForSlot(1), "{ still broken");
            string primaryBefore = File.ReadAllText(SaveManager.SlotPath(1));
            int activeBefore = SaveManager.ActiveSlot;
            Require(!SaveCoordinator.TryLoadSlot(1, out var inspection) &&
                    inspection.Status == SaveInspectionStatus.Corrupt,
                "invalid primary+backup was loadable");
            Require(SaveManager.ActiveSlot == activeBefore, "invalid load changed the active slot");

            SaveManager.SetActiveSlot(1);
            SaveManager.AutoSaveInventory(new InventorySnapshot { Ids = new[] { "fieldCap" }, Counts = new[] { 1 } });
            Require(File.ReadAllText(SaveManager.SlotPath(1)) == primaryBefore,
                "targeted autosave overwrote a damaged journal");
            SaveManager.SetActiveSlot(activeBefore);
        }

        private static void VerifyFutureVersionBarrier()
        {
            ResetSlot(2);
            SaveManager.WriteSlot(2, SampleMeta("older-compatible", 1));
            File.Copy(SaveManager.SlotPath(2), SaveManager.BackupPathForSlot(2), true);
            File.WriteAllText(SaveManager.SlotPath(2), FutureEnvelope(SampleMeta("future", 2), 99));
            var inspection = SaveManager.InspectSlot(2);
            Require(inspection.Status == SaveInspectionStatus.IncompatibleNewerVersion,
                "newer authoritative schema fell back to an older writable snapshot");
        }

        private static void VerifyNormalization()
        {
            ResetSlot(3);
            var broken = SampleMeta("normalize", 1);
            broken.TimestampUnix = long.MaxValue;
            broken.TotalPlayTimeSeconds = float.NaN;
            broken.CoinsCopper = -50;
            broken.HasPlayerTransform = true;
            broken.PlayerPosX = float.PositiveInfinity;
            broken.Inventory = new InventorySnapshot { Ids = new[] { "a", "b" }, Counts = new[] { -2 } };
            File.WriteAllText(SaveManager.SlotPath(3), JsonUtility.ToJson(broken, true));
            var inspection = SaveManager.InspectSlot(3);
            Require(inspection.CanLoad, "repairable historical metadata was rejected");
            var meta = inspection.Meta;
            Require(meta.TimestampUnix > 0 && meta.TimestampUnix < DateTimeOffset.UtcNow.AddYears(2).ToUnixTimeSeconds(),
                "invalid timestamp was not normalized");
            Require(meta.TotalPlayTimeSeconds == 0f && meta.CoinsCopper == 0 && !meta.HasPlayerTransform,
                "non-finite/negative scalar metadata was not normalized");
            Require(meta.Inventory.Ids.Length == 1 && meta.Inventory.Counts.Length == 1 && meta.Inventory.Counts[0] == 0,
                "parallel inventory arrays were not normalized deterministically");
        }

        private static void VerifyFullRoundTrip()
        {
            ResetSlot(0);
            var source = SampleMeta("arrive", 3661);
            source.KeyItemIds = new[] { "item.mill_key" };
            source.CompletedQuestIds = new[] { "arrive", "speakBram" };
            source.UnlockedStoryCardIds = new[] { "homecoming" };
            source.GameFlagIds = new[] { "act1_started", "ending_free_hollow_å" };
            source.Inventory = new InventorySnapshot { Ids = new[] { "fieldCap" }, Counts = new[] { 3 } };
            source.VillagerRelationships = new VillagerRelationshipSnapshot
            {
                MemoryNpcIds = new[] { "bram" },
                MemoryIds = new[] { "bram.shared_supper" },
                MemoryDays = new[] { 4 },
                BondNpcAIds = new[] { "bram" },
                BondNpcBIds = new[] { "marra" },
                BondValues = new[] { 2 },
                FavorIds = new[] { "favor.bram.after_hours" },
                FavorStages = new[] { 1 },
            };
            source.HasPlayerTransform = true;
            source.PlayerPosX = 12.5f; source.PlayerPosY = 3f; source.PlayerPosZ = -8f; source.PlayerYaw = 271f;
            SaveManager.WriteSlot(0, source);
            var loaded = SaveManager.InspectSlot(0).Meta;
            Require(loaded != null && loaded.CurrentQuestId == source.CurrentQuestId &&
                    string.IsNullOrEmpty(loaded.CurrentQuest) && loaded.TotalPlayTimeSeconds == 3661f &&
                    loaded.Inventory.Counts[0] == 3 && loaded.GameFlagIds.Length == 2 && loaded.HasPlayerTransform &&
                    loaded.VillagerRelationships != null && loaded.VillagerRelationships.MemoryDays[0] == 4 &&
                    loaded.VillagerRelationships.BondValues[0] == 2 &&
                    loaded.VillagerRelationships.FavorStages[0] == 1,
                "full save payload did not round-trip");
        }

        private static void VerifyQuestIdentityPresentation()
        {
            var current = new SaveSlotMeta { CurrentQuestId = "arrive", CurrentQuest = "Stale cached title" };
            Require(SaveQuestIdentity.ResolveDisplayName(current) == Localization.Get("quest.arrive.name"),
                "stable quest id did not override stale cached display text");

            var unknown = new SaveSlotMeta { CurrentQuestId = "removed-or-future-quest", CurrentQuest = "Stale cached title" };
            Require(SaveQuestIdentity.ResolveDisplayName(unknown) == Localization.Get("save.quest.unknown"),
                "unknown stable quest id fell back to cached display text");

            var legacy = new SaveSlotMeta { CurrentQuest = "Legacy chapter title" };
            Require(SaveQuestIdentity.ResolveDisplayName(legacy) == "Legacy chapter title",
                "id-less historical journal lost its cached compatibility label");

            SaveQuestIdentity.Set(current, SaveQuestIdentity.FinalChoiceAvailableId);
            Require(current.CurrentQuestId == SaveQuestIdentity.FinalChoiceAvailableId &&
                    string.IsNullOrEmpty(current.CurrentQuest) &&
                    SaveQuestIdentity.ResolveDisplayName(current) == Localization.Get("ending.save.choose"),
                "terminal quest identity did not retire cached display text");
        }

        private static void VerifyRecoveredRewrite()
        {
            ResetSlot(0);
            SaveManager.WriteSlot(0, SampleMeta("rewrite-backup", 1));
            SaveManager.WriteSlot(0, SampleMeta("rewrite-primary", 2));
            File.WriteAllText(SaveManager.SlotPath(0), "{}");
            var recovered = SaveManager.InspectSlot(0);
            Require(recovered.Status == SaveInspectionStatus.Recovered, "rewrite setup did not recover");
            SaveManager.WriteSlot(0, recovered.Meta);
            Require(SaveManager.InspectSlot(0).Status == SaveInspectionStatus.Ready,
                "recovered snapshot did not become a valid primary");
            Require(SaveManager.InspectSlot(0).CanLoad && File.Exists(SaveManager.BackupPathForSlot(0)),
                "recovered rewrite did not retain a valid recovery path");
            Require(Directory.GetFiles(SaveManager.SaveDirectory, "slot0.json.corrupt-*").Length == 1,
                "damaged primary was not quarantined for support");
        }

        private static SaveSlotMeta SampleMeta(string questId, float playTime)
        {
            return new SaveSlotMeta
            {
                SlotNumber = 0,
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CurrentQuestId = questId,
                CurrentAct = 2,
                TotalPlayTimeSeconds = playTime,
                CompletedQuestIds = Array.Empty<string>(),
                UnlockedStoryCardIds = Array.Empty<string>(),
                GameFlagIds = Array.Empty<string>(),
            };
        }

        private static string FutureEnvelope(SaveSlotMeta meta, long revision)
        {
            long milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string payload = JsonUtility.ToJson(meta, false);
            int version = 2;
            string input = version + "\n" + revision + "\n" + milliseconds + "\n" + payload;
            string checksum;
            using (var sha = SHA256.Create())
            {
                var builder = new StringBuilder();
                foreach (byte value in sha.ComputeHash(Encoding.UTF8.GetBytes(input)))
                    builder.Append(value.ToString("x2"));
                checksum = builder.ToString();
            }
            return JsonUtility.ToJson(new TestEnvelope
            {
                FormatId = "hollowfen.save",
                SchemaVersion = version,
                Revision = revision,
                WrittenUnixMilliseconds = milliseconds,
                PayloadJson = payload,
                IntegritySha256 = checksum,
            }, true);
        }

        private static void ResetSlot(int slot)
        {
            SaveManager.DeleteSlot(slot);
            Directory.CreateDirectory(SaveManager.SaveDirectory);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[SaveIntegrityVerifier] " + message);
        }
    }
}
#endif
