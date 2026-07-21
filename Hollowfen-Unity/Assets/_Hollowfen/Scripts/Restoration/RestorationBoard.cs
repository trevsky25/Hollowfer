using Hollowfen.Foraging;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Restoration
{
    /// <summary>The persistent village-square archive unlocked by the first completed restoration.</summary>
    [DisallowMultipleComponent]
    public sealed class RestorationBoard : MonoBehaviour, IInteractable
    {
        [SerializeField] private RestorationProjectData _unlockProject;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Collider _interactionCollider;
        [SerializeField] private RestorationStage _unlockStage = RestorationStage.Occupied;

        public string PromptVerb => "prompt.restoration.review";
        public string PromptTarget => Localization.Get("restoration.board.prompt_target");

        private void OnEnable()
        {
            RestorationProjects.OnStageChanged += HandleStageChanged;
            Apply();
        }

        private void OnDisable()
        {
            RestorationProjects.OnStageChanged -= HandleStageChanged;
        }

        public bool CanInteract(GameObject actor)
        {
            return actor != null && IsUnlocked() &&
                   (RestorationLedgerScreen.Instance == null || !RestorationLedgerScreen.Instance.IsOpen);
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            RestorationLedgerScreen.OpenProject(_unlockProject);
        }

        private void HandleStageChanged(string _, RestorationStage __) => Apply();

        private bool IsUnlocked() => _unlockProject != null &&
                                     RestorationProjects.GetStage(_unlockProject) >= _unlockStage;

        private void Apply()
        {
            bool active = IsUnlocked();
            if (_visualRoot != null && _visualRoot.activeSelf != active) _visualRoot.SetActive(active);
            if (_interactionCollider != null && _interactionCollider.enabled != active)
                _interactionCollider.enabled = active;
        }
    }
}
