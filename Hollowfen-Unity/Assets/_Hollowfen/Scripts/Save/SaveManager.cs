using System;
using System.IO;
using UnityEngine;

namespace Hollowfen.Save
{
    // Stub. Real game-state serialization comes in a later session — this only
    // reads/writes SaveSlotMeta so the Save Slot UI has something to display.
    public static class SaveManager
    {
        public const int TotalSlots = 4; // 3 manual + 1 autosave
        public const int AutosaveSlot = 0;

        public static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, "saves");

        public static string SlotPath(int slot) =>
            Path.Combine(SaveDirectory, $"slot{slot}.json");

        public static bool SlotHasData(int slot) =>
            File.Exists(SlotPath(slot));

        public static SaveSlotMeta GetSlotMeta(int slot)
        {
            var path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                return JsonUtility.FromJson<SaveSlotMeta>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to read slot {slot}: {e.Message}");
                return null;
            }
        }

        public static void DeleteSlot(int slot)
        {
            var path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
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
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(meta, prettyPrint: true));
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }
    }
}
