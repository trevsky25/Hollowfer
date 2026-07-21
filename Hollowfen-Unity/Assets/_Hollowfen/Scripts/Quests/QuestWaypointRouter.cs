using System;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.Map;
using UnityEngine;

namespace Hollowfen.Quests
{
    /// <summary>
    /// Routes multi-step quests to the place that owns the player's current story action.
    /// QuestData remains the durable linear-chain definition; these ordered rules provide
    /// stage-specific objective copy and compass destinations as flags and inventory change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestWaypointRouter : MonoBehaviour
    {
        [Serializable]
        public struct Route
        {
            public QuestData quest;
            public LocationData location;
            [Tooltip("Optional localized objective override for this stage.")]
            public string objectiveTextId;
            [Tooltip("All listed flags must be set. Empty matches regardless of flags.")]
            public string[] requiresFlagIds;
            [Tooltip("None of these flags may be set.")]
            public string[] blockedByFlagIds;
            [Tooltip("Optional basket specimen required for this stage.")]
            public MushroomFieldGuideData requiresForage;
            [Tooltip("Optional key item required for this stage.")]
            public string requiresItemId;

            public bool Matches(QuestData activeQuest)
            {
                if (quest == null || activeQuest != quest || location == null) return false;
                if (requiresFlagIds != null)
                    for (int i = 0; i < requiresFlagIds.Length; i++)
                        if (!string.IsNullOrEmpty(requiresFlagIds[i]) &&
                            !GameScores.HasFlag(requiresFlagIds[i])) return false;
                if (blockedByFlagIds != null)
                    for (int i = 0; i < blockedByFlagIds.Length; i++)
                        if (!string.IsNullOrEmpty(blockedByFlagIds[i]) &&
                            GameScores.HasFlag(blockedByFlagIds[i])) return false;
                if (requiresForage != null && InventoryRuntime.GetCount(requiresForage) <= 0)
                    return false;
                if (!string.IsNullOrEmpty(requiresItemId) && !KeyItems.Has(requiresItemId))
                    return false;
                return true;
            }
        }

        [SerializeField, Tooltip("First matching stage wins. Author specific stages before fallbacks.")]
        private Route[] _routes;

        public static QuestWaypointRouter Instance { get; private set; }
        public static event Action RouteChanged;

        public int RouteCount => _routes != null ? _routes.Length : 0;
        public Route GetRoute(int index) => index >= 0 && index < RouteCount ? _routes[index] : default;

        private int _activeRouteIndex = -2;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Instance = null;
            RouteChanged = null;
        }

        private void OnEnable()
        {
            Instance = this;
            QuestManager.QuestStarted += HandleQuestChanged;
            QuestManager.QuestCompleted += HandleQuestChanged;
            GameScores.OnChanged += Refresh;
            InventoryRuntime.OnChanged += HandleInventoryChanged;
            KeyItems.OnGranted += HandleItemGranted;
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            QuestManager.QuestStarted -= HandleQuestChanged;
            QuestManager.QuestCompleted -= HandleQuestChanged;
            GameScores.OnChanged -= Refresh;
            InventoryRuntime.OnChanged -= HandleInventoryChanged;
            KeyItems.OnGranted -= HandleItemGranted;
            if (Instance == this) Instance = null;
        }

        private void HandleQuestChanged(QuestData _) => Refresh();
        private void HandleInventoryChanged(string _, int __) => Refresh();
        private void HandleItemGranted(string _) => Refresh();

        public void Refresh()
        {
            int next = FindRouteIndex(QuestManager.ActiveQuest);
            bool changed = next != _activeRouteIndex;
            _activeRouteIndex = next;
            if (next >= 0) ApplyWaypoint(_routes[next].location);
            if (changed) RouteChanged?.Invoke();
        }

        public string ResolveObjectiveTextId(QuestData quest)
        {
            if (quest == null) return null;
            int index = quest == QuestManager.ActiveQuest ? _activeRouteIndex : FindRouteIndex(quest);
            if (index >= 0 && !string.IsNullOrEmpty(_routes[index].objectiveTextId))
                return _routes[index].objectiveTextId;
            return quest.ObjectiveTextId;
        }

        public LocationData ResolveLocation(QuestData quest)
        {
            int index = FindRouteIndex(quest);
            return index >= 0 ? _routes[index].location : quest != null ? quest.WaypointLocation : null;
        }

        private int FindRouteIndex(QuestData quest)
        {
            if (quest == null || _routes == null) return -1;
            for (int i = 0; i < _routes.Length; i++)
                if (_routes[i].Matches(quest)) return i;
            return -1;
        }

        private static void ApplyWaypoint(LocationData location)
        {
            if (location == null) return;
            for (int i = 0; i < LocationRegistry.Markers.Count; i++)
            {
                LocationMarker marker = LocationRegistry.Markers[i];
                if (marker != null && marker.Data == location)
                {
                    LocationRegistry.SetWaypoint(marker);
                    return;
                }
            }
        }
    }
}
