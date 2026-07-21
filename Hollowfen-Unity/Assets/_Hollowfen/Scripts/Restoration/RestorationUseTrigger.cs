using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Restoration
{
    /// <summary>Inks a restored public work into village life when Wren first uses it.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RestorationUseTrigger : MonoBehaviour
    {
        [SerializeField] private RestorationProjectData _project;
        [SerializeField] private RestorationStage _requiredStage = RestorationStage.Restored;
        [SerializeField] private RestorationStage _completedStage = RestorationStage.Occupied;
        [SerializeField] private string _completionFlagId;
        [SerializeField, Tooltip("Additional permanent flags committed with first use.")]
        private string[] _consequenceFlagIds = System.Array.Empty<string>();

        public RestorationProjectData Project => _project;
        public string CompletionFlagId => _completionFlagId;

        private void Reset()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null) trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_project == null || string.IsNullOrWhiteSpace(_completionFlagId) || other == null) return;
            if (GameScores.HasFlag(_completionFlagId) ||
                RestorationProjects.GetStage(_project) < _requiredStage) return;
            var actor = other.transform.root != null ? other.transform.root.gameObject : other.gameObject;
            if (!actor.CompareTag("Player") && actor.name != "PlayerArmature") return;
            if (RestorationProjects.CompleteFirstUse(_project, _completionFlagId,
                    _consequenceFlagIds, _requiredStage, _completedStage))
                RestorationCompletionToast.Show(_project);
        }
    }
}
