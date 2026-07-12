using System;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Save
{
    // Full-state save/load orchestration over the per-system stores. The targeted AutoSave*
    // writers keep each system persisted incrementally; this class handles the slot-level
    // operations: New Game (reset everything into a fresh slot), Load (hydrate everything
    // from a chosen slot), and SaveAll (gather + write, including the player transform).
    public static class SaveCoordinator
    {
        public static void StartNewGame(int slot)
        {
            SaveManager.SetActiveSlot(slot);
            SaveManager.DeleteSlot(slot);

            QuestManagerReset();
            InventoryRuntime.HydrateFrom(null);
            KeyItems.HydrateFrom(null);
            CoinPurse.HydrateFrom(0);
            MushroomDiscovery.HydrateFrom(null);
            GameScores.HydrateFrom(null);
            Cultivation.GrowBeds.HydrateFrom(null);
            Map.LocationRegistry.HydrateFromSave(null);

            // Fresh meta so the slot row shows up immediately.
            SaveManager.WritePlaceholderToSlot(slot);
        }

        public static void LoadSlot(int slot)
        {
            SaveManager.SetActiveSlot(slot);
            var meta = SaveManager.GetSlotMeta(slot);

            QuestManagerReset();
            QuestManagerHydrate(meta);
            InventoryRuntime.HydrateFrom(meta?.Inventory);
            KeyItems.HydrateFrom(meta?.KeyItemIds);
            CoinPurse.HydrateFrom(meta?.CoinsCopper ?? 0);
            MushroomDiscovery.HydrateFrom(meta?.DiscoveredMushroomIds);
            GameScores.HydrateFrom(meta);
            Cultivation.GrowBeds.HydrateFrom(meta?.GrowBeds);
            Map.LocationRegistry.HydrateFromSave(meta?.DiscoveredLocationIds);
        }

        // Most recently written slot, or -1 when no saves exist.
        public static int MostRecentSlot()
        {
            int best = -1;
            long bestTime = long.MinValue;
            for (int i = 0; i < SaveManager.TotalSlots; i++)
            {
                var meta = SaveManager.GetSlotMeta(i);
                if (meta == null) continue;
                if (meta.TimestampUnix > bestTime) { bestTime = meta.TimestampUnix; best = i; }
            }
            return best;
        }

        // Gather current state and write the active slot. Pass the player transform from
        // gameplay saves so Continue restores position; menu-side saves pass nulls.
        public static void SaveAll(Vector3? playerPosition = null, float playerYaw = 0f)
        {
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot) ?? new SaveSlotMeta();

            meta.Inventory = InventoryRuntime.ToSnapshot();
            meta.KeyItemIds = KeyItems.ToArray();
            meta.CoinsCopper = CoinPurse.TotalCopper;
            meta.DiscoveredMushroomIds = MushroomDiscovery.ToArray();
            meta.GrowBeds = Cultivation.GrowBeds.ToSnapshot();
            meta.DiscoveredLocationIds = Map.LocationRegistry.DiscoveredToArray();

            var quests = new string[QuestManager.CompletedQuestIds.Count];
            int qi = 0;
            foreach (var q in QuestManager.CompletedQuestIds) quests[qi++] = q;
            meta.CompletedQuestIds = quests;
            var cards = new string[QuestManager.UnlockedStoryCardIds.Count];
            int ci = 0;
            foreach (var c in QuestManager.UnlockedStoryCardIds) cards[ci++] = c;
            meta.UnlockedStoryCardIds = cards;

            GameScores.WriteTo(meta);
            if (Hollowfen.GameTime.TimeManager.Instance != null)
                Hollowfen.GameTime.TimeManager.Instance.WriteTo(meta);

            var active = QuestManager.ActiveQuest;
            if (active != null)
            {
                meta.CurrentQuest = Localization.Get(active.DisplayNameId);
                meta.CurrentAct = active.Act;
            }
            else if (quests.Length > 0)
            {
                meta.CurrentQuest = "Act I complete";
            }

            if (playerPosition.HasValue)
            {
                meta.HasPlayerTransform = true;
                meta.PlayerPosX = playerPosition.Value.x;
                meta.PlayerPosY = playerPosition.Value.y;
                meta.PlayerPosZ = playerPosition.Value.z;
                meta.PlayerYaw = playerYaw;
            }

            meta.TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            SaveManager.WriteSlot(SaveManager.ActiveSlot, meta);
        }

        // Convenience for gameplay callers: finds the player and includes its transform.
        public static void SaveAllWithPlayer()
        {
            var player = GameObject.Find("PlayerArmature");
            if (player != null)
                SaveAll(player.transform.position, player.transform.eulerAngles.y);
            else
                SaveAll();
        }

        private static void QuestManagerReset() => QuestManager.ResetForSlotSwitch();

        private static void QuestManagerHydrate(SaveSlotMeta meta)
        {
            if (meta == null) return;
            QuestManager.HydrateFrom(meta.CompletedQuestIds, meta.UnlockedStoryCardIds);
        }
    }
}
