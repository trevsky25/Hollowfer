using System;
using System.Collections.Generic;
using Hollowfen.Map;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Central quest state. One active quest at a time (mirrors story.md's linear Act I chain);
    // completed quest IDs persist for the play session via _completedIds. Completing a quest
    // optionally unlocks a story card and auto-starts the next quest in the chain.
    public static class QuestManager
    {
        private static QuestData _activeQuest;
        private static readonly HashSet<string> _completedIds = new HashSet<string>();
        private static readonly HashSet<string> _unlockedStoryCardIds = new HashSet<string>();

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
            QuestStarted = null;
            QuestCompleted = null;
            StoryCardUnlocked = null;
        }

        public static void StartQuest(QuestData quest)
        {
            if (quest == null) return;
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

            if (done.UnlockStoryCardOnComplete != null)
                UnlockStoryCard(done.UnlockStoryCardOnComplete.Id);

            QuestCompleted?.Invoke(done);
            _activeQuest = null;

            if (done.NextQuest != null) StartQuest(done.NextQuest);
            else if (LocationRegistry.ActiveWaypoint != null) LocationRegistry.ClearWaypoint();
        }

        public static bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && _completedIds.Contains(id);
        public static bool IsActive(string id) => _activeQuest != null && _activeQuest.Id == id;

        public static bool UnlockStoryCard(string cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return false;
            if (!_unlockedStoryCardIds.Add(cardId)) return false;
            StoryCardUnlocked?.Invoke(cardId);
            // Mirror Steam-style achievement hook so this can fan out later.
            GameEvents.TriggerAchievement("ACH_STORY_" + cardId.ToUpperInvariant());
            return true;
        }

        public static bool IsStoryCardUnlocked(string cardId)
            => !string.IsNullOrEmpty(cardId) && _unlockedStoryCardIds.Contains(cardId);

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
