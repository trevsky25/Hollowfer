using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.GameTime
{
    // Converts "come back tomorrow" beats into flags the dialogue system can gate on.
    // Each pair: when the game day rolls over and `whenFlag` is set, set `thenFlag`.
    // Batch 11 use: knife_commissioned → knife_ready (Joren's overnight forging).
    public class DayFlagScheduler : MonoBehaviour
    {
        [SerializeField, Tooltip("Source flags, parallel with Then Flags.")]
        private string[] _whenFlags;
        [SerializeField, Tooltip("Flags set on the first day rollover after the source flag exists.")]
        private string[] _thenFlags;

        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        private void HandleDayChanged(int day)
        {
            if (_whenFlags == null || _thenFlags == null) return;
            int n = Mathf.Min(_whenFlags.Length, _thenFlags.Length);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(_whenFlags[i]) || string.IsNullOrEmpty(_thenFlags[i])) continue;
                if (!GameScores.HasFlag(_whenFlags[i])) continue;
                if (GameScores.SetFlag(_thenFlags[i]))
                    Debug.Log("[DayFlagScheduler] Day " + day + ": " + _whenFlags[i] + " -> " + _thenFlags[i]);
            }
        }
    }
}
