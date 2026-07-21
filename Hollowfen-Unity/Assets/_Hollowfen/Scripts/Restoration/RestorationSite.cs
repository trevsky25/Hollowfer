using System;
using Hollowfen.Foraging;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Restoration
{
    [Serializable]
    public struct RestorationPresentation
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private RestorationStage _fromStage;
        [SerializeField] private RestorationStage _throughStage;

        public GameObject Root => _root;
        public RestorationStage FromStage => _fromStage;
        public RestorationStage ThroughStage => _throughStage;

        public RestorationPresentation(GameObject root, RestorationStage fromStage,
            RestorationStage throughStage)
        {
            _root = root;
            _fromStage = fromStage;
            _throughStage = throughStage;
        }
    }

    /// <summary>Physical worksite entry point and stage-driven owned world dressing.</summary>
    [DisallowMultipleComponent]
    public sealed class RestorationSite : MonoBehaviour, IInteractable
    {
        [SerializeField] private RestorationProjectData _project;
        [SerializeField] private string _promptTargetId;
        [SerializeField] private RestorationStage _interactableFromStage = RestorationStage.Surveyed;
        [SerializeField] private RestorationPresentation[] _presentations = Array.Empty<RestorationPresentation>();

        public RestorationProjectData Project => _project;
        public string PromptVerb => "prompt.restoration.review";
        public string PromptTarget => Localization.Get(string.IsNullOrWhiteSpace(_promptTargetId)
            ? _project != null ? _project.PromptTargetId : "restoration.site.unknown"
            : _promptTargetId);

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
            return actor != null && _project != null &&
                   RestorationProjects.GetStage(_project) >= _interactableFromStage &&
                   (RestorationLedgerScreen.Instance == null || !RestorationLedgerScreen.Instance.IsOpen);
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            RestorationLedgerScreen.OpenProject(_project);
        }

        private void HandleStageChanged(string projectId, RestorationStage _)
        {
            if (_project != null && string.Equals(projectId, _project.Id, StringComparison.Ordinal)) Apply();
        }

        private void Apply()
        {
            if (_project == null) return;
            var stage = RestorationProjects.GetStage(_project);
            if (_presentations == null) return;
            foreach (var presentation in _presentations)
            {
                if (presentation.Root == null) continue;
                bool active = stage >= presentation.FromStage && stage <= presentation.ThroughStage;
                if (presentation.Root.activeSelf != active) presentation.Root.SetActive(active);
            }
        }
    }
}
