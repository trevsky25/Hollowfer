using System;
using System.Collections.Generic;
using Hollowfen.Map;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Central quest state. One active quest at a time (mirrors story.md's linear Act I chain);
    // completed quest ids and unlocked story cards hydrate from the autosave slot on first
    // access and persist immediately on change (same recipe as InventoryRuntime / KeyItems).
    // Completing a quest optionally unlocks a story card and auto-starts the next in the chain.
    public static class QuestManager
    {
        private static QuestData _activeQuest;
        private static readonly HashSet<string> _completedIds = new HashSet<string>();
        private static readonly HashSet<string> _unlockedStoryCardIds = new HashSet<string>();
        private static bool _hydrated;

        public static event Action<QuestData> QuestStarted;
        public static event Action<QuestData> QuestCompleted;
        public static event Action<string>    StoryCardUnlocked;

        public static QuestData ActiveQuest => _activeQuest;
        public static IReadOnlyCollection<string> CompletedQuestIds => _completedIds;
        public static IReadOnlyCollection<string> UnlockedStoryCardIds => _unlockedStoryCardIds;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _activeQuest = null;
            _completedIds.Clear();
            _unlockedStoryCardIds.Clear();
            _hydrated = false;
            QuestStarted = null;
            QuestCompleted = null;
            StoryCardUnlocked = null;
        }

        private static void EnsureHydrated()
        {
            if (_hydrated) return;
            _hydrated = true;
            try
            {
                var meta = Save.SaveManager.GetSlotMeta(Save.SaveManager.ActiveSlot);
                if (meta == null) return;
                if (meta.CompletedQuestIds != null)
                    foreach (var id in meta.CompletedQuestIds)
                        if (!string.IsNullOrEmpty(id)) _completedIds.Add(id);
                if (meta.UnlockedStoryCardIds != null)
                    foreach (var id in meta.UnlockedStoryCardIds)
                        if (!string.IsNullOrEmpty(id)) _unlockedStoryCardIds.Add(id);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[QuestManager] Hydration failed: " + e.Message);
            }
        }

        private static void Persist()
        {
            try
            {
                var quests = new string[_completedIds.Count];
                _completedIds.CopyTo(quests);
                var cards = new string[_unlockedStoryCardIds.Count];
                _unlockedStoryCardIds.CopyTo(cards);
                Save.SaveManager.AutoSaveQuestState(quests, cards);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[QuestManager] Autosave failed: " + e.Message);
            }
        }

        public static void StartQuest(QuestData quest)
        {
            if (quest == null) return;
            EnsureHydrated();
            if (_completedIds.Contains(quest.Id)) return;
            if (_activeQuest == quest) return;

            _activeQuest = quest;
            ApplyWaypointHint(quest);
            QuestStarted?.Invoke(quest);
        }

        public static void CompleteQuest(string id)
        {
            if (_activeQuest == null || _activeQuest.Id != id) return;
            var done = _activeQuest;
            _completedIds.Add(id);
            Persist();

            // Score deltas (story.md tables)
            if (done.VillageHopeDelta != 0) GameScores.AddVillageHope(done.VillageHopeDelta);
            if (done.KnowledgeDelta != 0) GameScores.AddKnowledge(done.KnowledgeDelta);
            if (done.RelationshipNpcIds != null && done.RelationshipDeltas != null)
            {
                int n = Mathf.Min(done.RelationshipNpcIds.Length, done.RelationshipDeltas.Length);
                for (int i = 0; i < n; i++)
                    GameScores.AddRelationship(done.RelationshipNpcIds[i], done.RelationshipDeltas[i]);
            }

            if (done.UnlockStoryCardOnComplete != null)
                UnlockStoryCard(done.UnlockStoryCardOnComplete.Id);

            QuestCompleted?.Invoke(done);
            _activeQuest = null;

            if (done.NextQuest != null) StartQuest(done.NextQuest);
            else if (LocationRegistry.ActiveWaypoint != null) LocationRegistry.ClearWaypoint();
        }

        // Slot switch (New Game / Load): clear in-memory progression but keep event
        // subscribers — scene objects stay alive across the menu round-trip.
        public static void ResetForSlotSwitch()
        {
            _activeQuest = null;
            _completedIds.Clear();
            _unlockedStoryCardIds.Clear();
            _hydrated = true; // caller hydrates explicitly (or leaves empty for New Game)
        }

        // Used by save load to reset in-memory state to a snapshot.
        public static void HydrateFrom(string[] completedQuestIds, string[] unlockedStoryCardIds)
        {
            _completedIds.Clear();
            _unlockedStoryCardIds.Clear();
            if (completedQuestIds != null)
                foreach (var id in completedQuestIds)
                    if (!string.IsNullOrEmpty(id)) _completedIds.Add(id);
            if (unlockedStoryCardIds != null)
                foreach (var id in unlockedStoryCardIds)
                    if (!string.IsNullOrEmpty(id)) _unlockedStoryCardIds.Add(id);
            _hydrated = true;
        }

        public static bool IsCompleted(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            EnsureHydrated();
            return _completedIds.Contains(id);
        }

        public static bool IsActive(string id) => _activeQuest != null && _activeQuest.Id == id;

        public static bool UnlockStoryCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            EnsureHydrated();
            if (!_unlockedStoryCardIds.Add(cardId)) return false;
            Persist();
            StoryCardUnlocked?.Invoke(cardId);
            // Mirror Steam-style achievement hook so this can fan out later.
            GameEvents.TriggerAchievement("ACH_STORY_" + cardId.ToUpperInvariant());
            return true;
        }

        public static bool IsStoryCardUnlocked(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            EnsureHydrated();
            return _unlockedStoryCardIds.Contains(cardId);
        }

        private static void ApplyWaypointHint(QuestData quest)
        {
            if (quest == null || quest.WaypointLocation == null) return;
            foreach (var m in LocationRegistry.Markers)
            {
                if (m != null && m.Data == quest.WaypointLocation)
                {
                    LocationRegistry.SetWaypoint(m);
                    return;
                }
            }
        }
    }
}
