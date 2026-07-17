using System;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using Hollowfen.Quests;
using Hollowfen.Requests;
using UnityEngine;

namespace Hollowfen.NPCs
{
    /// <summary>
    /// One destination in an NPC's first-match-wins daily routine. Schedule state is derived
    /// from the saved clock, quest state, and flags; it never needs its own save payload.
    /// </summary>
    [Serializable]
    public struct NPCScheduleSlot
    {
        public string label;
        public Transform destination;
        public bool allDay;
        [Range(0f, 24f)] public float startHour;
        [Range(0f, 24f)] public float endHour;
        public QuestData activeQuest;
        public QuestData requiresQuestCompleted;
        public string requiresFlagId;
        public string blockedByFlagId;

        public bool Matches(float hour)
        {
            if (destination == null) return false;
            if (activeQuest != null && !QuestManager.IsActive(activeQuest.Id)) return false;
            if (requiresQuestCompleted != null && !QuestManager.IsCompleted(requiresQuestCompleted.Id)) return false;
            if (!string.IsNullOrEmpty(requiresFlagId) && !GameScores.HasFlag(requiresFlagId)) return false;
            if (!string.IsNullOrEmpty(blockedByFlagId) && GameScores.HasFlag(blockedByFlagId)) return false;
            if (allDay) return true;

            hour = Mathf.Repeat(hour, 24f);
            float start = Mathf.Repeat(startHour, 24f);
            float end = Mathf.Repeat(endHour, 24f);
            if (Mathf.Approximately(start, end)) return true;
            return start < end ? hour >= start && hour < end : hour >= start || hour < end;
        }
    }

    /// <summary>
    /// Always-active host that places one NPC actor at its current schedule destination. It
    /// defers relocation while a presentation owns the actor or while either end of the move is
    /// visible to the player, preventing clock-boundary pop-in during ordinary exploration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NPCSchedule : MonoBehaviour
    {
        [SerializeField] private GameObject _actor;
        [SerializeField, Tooltip("First matching slot wins. Put story overrides before ordinary time windows.")]
        private NPCScheduleSlot[] _slots;
        [SerializeField] private bool _hideWhenNoSlotMatches;
        [SerializeField, Min(1f)] private float _safeRelocationDistance = 12f;
        [SerializeField, Min(.1f)] private float _pollInterval = .35f;

        public GameObject Actor => _actor;
        public int CurrentSlotIndex => _currentSlotIndex;
        public int PendingSlotIndex => _pendingSlotIndex;
        public int SlotCount => _slots != null ? _slots.Length : 0;
        public string CurrentSlotLabel => _currentSlotIndex >= 0 && _currentSlotIndex < SlotCount
            ? _slots[_currentSlotIndex].label
            : "";
        public Transform CurrentDestination => _currentSlotIndex >= 0 && _currentSlotIndex < SlotCount
            ? _slots[_currentSlotIndex].destination
            : null;

        private int _currentSlotIndex = -2;
        private int _pendingSlotIndex = -2;
        private float _nextPoll;
        private Transform _player;

        private void OnEnable()
        {
            GameScores.OnChanged += OnStateChanged;
            QuestManager.QuestStarted += OnQuestChanged;
            QuestManager.QuestCompleted += OnQuestChanged;
            TimeManager.OnDayChanged += OnDayChanged;
            TimeManager.OnSundown += OnStateChanged;
            Refresh(false);
        }

        private void OnDisable()
        {
            GameScores.OnChanged -= OnStateChanged;
            QuestManager.QuestStarted -= OnQuestChanged;
            QuestManager.QuestCompleted -= OnQuestChanged;
            TimeManager.OnDayChanged -= OnDayChanged;
            TimeManager.OnSundown -= OnStateChanged;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + _pollInterval;
            Refresh(false);
        }

        private void OnStateChanged() => Refresh(false);
        private void OnQuestChanged(QuestData _) => Refresh(false);
        private void OnDayChanged(int _) => Refresh(false);

        public bool RefreshImmediate() => Refresh(true);

        /// <summary>Re-evaluates the routine. Force is reserved for hidden transitions and tests.</summary>
        public bool Refresh(bool force)
        {
            if (_actor == null || TimeManager.Instance == null) return false;
            int desired = FindMatchingSlot(TimeManager.Instance.Hour);
            bool correctVisibility = desired >= 0
                ? _actor.activeSelf
                : !_hideWhenNoSlotMatches || !_actor.activeSelf;
            bool correctTransform = desired < 0 || AtDestination(_slots[desired].destination);
            if (desired == _currentSlotIndex && correctVisibility && correctTransform)
            {
                _pendingSlotIndex = -2;
                return false;
            }

            if (!force && !CanRelocate(desired))
            {
                _pendingSlotIndex = desired;
                return false;
            }

            _pendingSlotIndex = -2;
            _currentSlotIndex = desired;
            if (desired < 0)
            {
                if (_hideWhenNoSlotMatches && _actor.activeSelf) _actor.SetActive(false);
                return true;
            }

            Transform destination = _slots[desired].destination;
            _actor.transform.SetPositionAndRotation(destination.position, destination.rotation);
            if (!_actor.activeSelf) _actor.SetActive(true);
            return true;
        }

        public Transform GetSlotDestination(int index) =>
            index >= 0 && index < SlotCount ? _slots[index].destination : null;

        public int FindSlot(string label)
        {
            if (_slots == null || string.IsNullOrEmpty(label)) return -1;
            for (int i = 0; i < _slots.Length; i++)
                if (string.Equals(_slots[i].label, label, StringComparison.Ordinal)) return i;
            return -1;
        }

        private int FindMatchingSlot(float hour)
        {
            if (_slots != null)
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i].Matches(hour)) return i;
            return _hideWhenNoSlotMatches ? -1 : _currentSlotIndex;
        }

        private bool CanRelocate(int desired)
        {
            if (!Application.isPlaying) return true;
            if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen) return false;
            if (VillageRequestScreen.Instance != null && VillageRequestScreen.Instance.IsOpen) return false;
            if (PlayerInteractor.Suspended) return false;

            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) _player = playerObject.transform;
            }
            if (_player == null) return true;

            float safeSqr = _safeRelocationDistance * _safeRelocationDistance;
            if (_actor.activeSelf && FlatSqr(_player.position, _actor.transform.position) < safeSqr)
                return false;
            if (desired >= 0 && FlatSqr(_player.position, _slots[desired].destination.position) < safeSqr)
                return false;
            return true;
        }

        private bool AtDestination(Transform destination)
        {
            if (destination == null) return false;
            return (_actor.transform.position - destination.position).sqrMagnitude < .0001f &&
                   Quaternion.Angle(_actor.transform.rotation, destination.rotation) < .1f;
        }

        private static float FlatSqr(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
