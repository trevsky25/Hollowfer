using System;
using System.IO;
using UnityEngine;

namespace Hollowfen.Save
{
    // Reads/writes SaveSlotMeta JSON per slot. The ACTIVE slot is where all incremental
    // autosaves land — slot 0 by default; New Game / Load switch it. Full-state gather and
    // hydrate live in SaveCoordinator; the targeted AutoSave* writers here keep individual
    // systems (inventory, coins, quests...) persisted on every change.
    public static class SaveManager
    {
        public const int TotalSlots = 4; // 3 manual + 1 autosave
        public const int AutosaveSlot = 0;

        // The slot the current play session reads from and writes to.
        public static int ActiveSlot { get; private set; } = AutosaveSlot;

        public static void SetActiveSlot(int slot)
        {
            ActiveSlot = Mathf.Clamp(slot, 0, TotalSlots - 1);
        }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() { ActiveSlot = AutosaveSlot; }
#endif

        public static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, "saves");

        public static string SlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"slot{slot}.json");

        private static string BackupPath(int slot) => SlotPath(slot) + ".bak";
        private static string TempPath(int slot) => SlotPath(slot) + ".tmp";

        public static bool SlotHasData(int slot) =>
            File.Exists(SlotPath(slot)) || File.Exists(BackupPath(slot));

        public static SaveSlotMeta GetSlotMeta(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path)) return ReadBackup(slot, "primary save is missing");
            try
            {
                var meta = JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(path));
                if (meta == null) throw new InvalidDataException("save JSON produced no slot data");
                return meta;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to read slot {slot}: {e.Message}");
                return ReadBackup(slot, "primary save could not be read");
            }
        }

        public static void DeleteSlot(int slot)
        {
            var path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
            var backup = BackupPath(slot);
            if (File.Exists(backup)) File.Delete(backup);
            var temp = TempPath(slot);
            if (File.Exists(temp)) File.Delete(temp);
        }

        public static void WritePlaceholderToSlot(int slot)
        {
            EnsureDirectory();
            var meta = new SaveSlotMeta
            {
                SlotNumber = slot,
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            WriteJsonAtomically(slot, meta);
        }

        // Full meta write (preserves inventory + future fields). Used by gameplay autosave.
        public static void WriteSlot(int slot, SaveSlotMeta meta)
        {
            if (meta == null) return;
            EnsureDirectory();
            meta.SlotNumber = slot;
            if (meta.TimestampUnix == 0) meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(slot, meta);
        }

        // Targeted autosave for inventory: read existing meta on the autosave slot (or create one),
        // overwrite Inventory, refresh timestamp, write back. Throttled by callers if needed.
        public static void AutoSaveInventory(InventorySnapshot snap)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.Inventory = snap;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Marks the homecoming intro as seen on the autosave slot.
        public static void AutoSaveIntroSeen()
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.HomecomingIntroSeen = true;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for coins — same recipe as AutoSaveInventory.
        public static void AutoSaveCoins(int totalCopper, CoinLedgerSnapshot ledger = null)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.CoinsCopper = totalCopper;
            meta.CoinLedger = ledger;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for quest progression — same recipe as AutoSaveInventory.
        public static void AutoSaveQuestState(string[] completedQuestIds, string[] unlockedStoryCardIds)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.CompletedQuestIds = completedQuestIds;
            meta.UnlockedStoryCardIds = unlockedStoryCardIds;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for key items — same recipe as AutoSaveInventory.
        public static void AutoSaveKeyItems(string[] ids)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.KeyItemIds = ids;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for the score engine. Pulls current values from GameScores
        // (which calls this) to avoid a parallel parameter list.
        public static void AutoSaveScores()
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            Hollowfen.Quests.GameScores.WriteTo(meta);
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for map-location discovery — same recipe as AutoSaveInventory.
        public static void AutoSaveDiscoveredLocations(string[] ids)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.DiscoveredLocationIds = ids;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for grow beds — same recipe as AutoSaveInventory.
        public static void AutoSaveGrowBeds(GrowBedSnapshot snap)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.GrowBeds = snap;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        // Targeted autosave for field-guide discovery — same recipe as AutoSaveInventory.
        public static void AutoSaveDiscovery(string[] ids)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.DiscoveredMushroomIds = ids;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveForageNodes(ForageNodeSnapshot snapshot)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.ForageNodes = snapshot;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        public static void AutoSaveVillageRequests(VillageRequestSnapshot snapshot)
        {
            EnsureDirectory();
            var meta = GetSlotMeta(ActiveSlot) ?? new SaveSlotMeta
            {
                SlotNumber = ActiveSlot,
                CurrentQuest = "Act I — Arrival",
                CurrentAct = 1,
                TotalPlayTimeSeconds = 0f
            };
            meta.VillageRequests = snapshot;
            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteJsonAtomically(ActiveSlot, meta);
        }

        private static SaveSlotMeta ReadBackup(int slot, string reason)
        {
            var backup = BackupPath(slot);
            if (!File.Exists(backup)) return null;
            try
            {
                var meta = JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(backup));
                if (meta == null) throw new InvalidDataException("backup JSON produced no slot data");
                Debug.LogWarning($"[SaveManager] Recovered slot {slot} from its backup because the {reason}.");
                return meta;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to recover slot {slot}: {e.Message}");
                return null;
            }
        }

        private static void WriteJsonAtomically(int slot, SaveSlotMeta meta)
        {
            EnsureDirectory();
            var path = SlotPath(slot);
            var temp = TempPath(slot);
            var backup = BackupPath(slot);
            var json = JsonUtility.ToJson(meta, prettyPrint: true);

            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
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
                else
                {
                    File.Move(temp, path);
                }
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static void ReplaceWithPortableFallback(string temp, string path, string backup)
        {
            File.Copy(path, backup, true);
            File.Delete(path);
            File.Move(temp, path);
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }
    }
}
