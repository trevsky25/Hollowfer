using UnityEngine;

namespace Hollowfen.Quests
{
    // Declarative world-state switch: keeps a target object's active state in sync with a
    // GameScores flag. Lives on an ALWAYS-ACTIVE host (a deactivated component can't hear
    // OnChanged) and toggles a separate target — never itself or an ancestor.
    // First uses: Theo's wagon appears on theo_wagon_arrived, Edda appears on
    // theo_trade_unlocked. Act II C's cottage-reopening swaps reuse it with Deactivate When Set.
    [DisallowMultipleComponent]
    public class FlagActivatedObject : MonoBehaviour
    {
        [SerializeField, Tooltip("GameScores flag to mirror (e.g. theo_wagon_arrived).")]
        private string _flagId;
        [SerializeField, Tooltip("Optional second flag that OVERRIDES to inactive when set (inn-Hollin yields to mill-Hollin on hollin_at_mill). Empty = ignore.")]
        private string _offFlagId;
        [SerializeField, Tooltip("Object toggled to match the flag. Must NOT be this object or an ancestor of it.")]
        private GameObject _target;
        [SerializeField, Tooltip("Off: target is ACTIVE while the flag is set. On: target is INACTIVE while set (boarded-up planks coming down).")]
        private bool _deactivateWhenSet;

        private void OnEnable()
        {
            GameScores.OnChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameScores.OnChanged -= Apply;
        }

        private void Apply()
        {
            if (_target == null || string.IsNullOrEmpty(_flagId)) return;
            bool active = GameScores.HasFlag(_flagId) != _deactivateWhenSet;
            if (!string.IsNullOrEmpty(_offFlagId) && GameScores.HasFlag(_offFlagId)) active = false;
            if (_target.activeSelf != active) _target.SetActive(active);
        }
    }
}
