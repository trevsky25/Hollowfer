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
            ForageNodeStates.HydrateFrom(null);
            Hollowfen.Requests.VillageRequests.HydrateFrom(null);
            Hollowfen.Restoration.RestorationProjects.HydrateFrom(null);
            Hollowfen.Apothecary.ApothecaryRuntime.HydrateFrom(null);
            Hollowfen.Apothecary.ApothecaryCases.HydrateFrom(null);
            Hollowfen.NPCs.VillagerRelationships.HydrateFrom(null);
            Map.LocationRegistry.HydrateFromSave(null);

            // Fresh meta so the slot row shows up immediately.
            SaveManager.WritePlaceholderToSlot(slot);
        }

        public static bool TryLoadSlot(int slot, out SaveSlotInspection inspection)
        {
            inspection = SaveManager.InspectSlot(slot);
            if (!inspection.CanLoad) return false;

            SaveManager.SetActiveSlot(slot);
            var meta = inspection.Meta;

            QuestManagerReset();
            QuestManagerHydrate(meta);
            InventoryRuntime.HydrateFrom(meta?.Inventory);
            KeyItems.HydrateFrom(meta?.KeyItemIds);
            CoinPurse.HydrateFrom(meta?.CoinsCopper ?? 0, meta?.CoinLedger);
            MushroomDiscovery.HydrateFrom(meta?.DiscoveredMushroomIds);
            GameScores.HydrateFrom(meta);
            Cultivation.GrowBeds.HydrateFrom(meta?.GrowBeds);
            ForageNodeStates.HydrateFrom(meta?.ForageNodes);
            Hollowfen.Requests.VillageRequests.HydrateFrom(meta?.VillageRequests);
            Hollowfen.Restoration.RestorationProjects.HydrateFrom(meta?.RestorationProjects);
            Hollowfen.Apothecary.ApothecaryRuntime.HydrateFrom(meta?.Apothecary);
            Hollowfen.Apothecary.ApothecaryCases.HydrateFrom(meta?.ApothecaryCases);
            Hollowfen.NPCs.VillagerRelationships.HydrateFrom(meta?.VillagerRelationships);
            Map.LocationRegistry.HydrateFromSave(meta?.DiscoveredLocationIds);
            return true;
        }

        // Compatibility entry point for editor verifiers and older call sites. Invalid slots
        // leave the current in-memory session untouched.
        public static void LoadSlot(int slot)
        {
            if (!TryLoadSlot(slot, out var inspection))
                Debug.LogError($"[SaveCoordinator] Refused to load slot {slot}: {inspection.Status} — {inspection.Detail}");
        }

        // Most recently written slot, or -1 when no saves exist.
        public static int MostRecentSlot()
        {
            int best = -1;
            long bestTime = long.MinValue;
            for (int i = 0; i < SaveManager.TotalSlots; i++)
            {
                var inspection = SaveManager.InspectSlot(i);
                if (!inspection.CanLoad) continue;
                var meta = inspection.Meta;
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
            meta.CoinLedger = CoinPurse.ToLedgerSnapshot();
            meta.DiscoveredMushroomIds = MushroomDiscovery.ToArray();
            meta.GrowBeds = Cultivation.GrowBeds.ToSnapshot();
            meta.ForageNodes = ForageNodeStates.ToSnapshot();
            meta.VillageRequests = Hollowfen.Requests.VillageRequests.ToSnapshot();
            meta.RestorationProjects = Hollowfen.Restoration.RestorationProjects.ToSnapshot();
            meta.Apothecary = Hollowfen.Apothecary.ApothecaryRuntime.ToSnapshot();
            meta.ApothecaryCases = Hollowfen.Apothecary.ApothecaryCases.ToSnapshot();
            meta.VillagerRelationships = Hollowfen.NPCs.VillagerRelationships.ToSnapshot();
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
            if (GameScores.HasFlag("game_complete"))
            {
                SaveQuestIdentity.Set(meta, SaveQuestIdentity.GameCompleteId);
                meta.CurrentAct = 4;
            }
            else if (active != null)
            {
                SaveQuestIdentity.Set(meta, active.Id);
                meta.CurrentAct = active.Act;
            }
            else if (QuestManager.IsCompleted("meetAldric"))
            {
                // The linear quest chain deliberately ends before the four-way decision.
                // Keep the slot identity truthful during that recoverable terminal fork.
                SaveQuestIdentity.Set(meta, SaveQuestIdentity.FinalChoiceAvailableId);
                meta.CurrentAct = 4;
            }
            else if (quests.Length > 0)
            {
                SaveQuestIdentity.Set(meta, SaveQuestIdentity.ActOneCompleteId);
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
