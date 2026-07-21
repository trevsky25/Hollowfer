using Hollowfen.Foraging;
using Hollowfen.Restoration;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>World entry point for the restored, physical preparation bench.</summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private PreparationRecipeData[] _recipes;
        [SerializeField] private string _restorationProjectId = "tobin_workshop";
        [SerializeField] private RestorationStage _requiredStage = RestorationStage.Occupied;
        [SerializeField] private Transform[] _stepProps;

        private Transform _pulseProp;
        private Vector3 _pulseScale;
        private float _pulseUntil;

        public PreparationRecipeData[] Recipes => _recipes;
        public string PromptVerb => "prompt.apothecary.prepare";
        public string PromptTarget => Localization.Get("apothecary.station.name");

        public bool CanInteract(GameObject actor)
        {
            if (actor == null || ApothecaryScreen.Instance != null && ApothecaryScreen.Instance.IsOpen)
                return false;
            var project = RestorationProjects.Resolve(_restorationProjectId);
            return project != null && RestorationProjects.GetStage(project) >= _requiredStage;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            ApothecaryScreen.Ensure().Open(this);
        }

        public void PreviewStep(int step)
        {
            ResetPulse();
            if (_stepProps == null || _stepProps.Length == 0) return;
            _pulseProp = _stepProps[Mathf.Clamp(step, 0, _stepProps.Length - 1)];
            if (_pulseProp == null) return;
            _pulseScale = _pulseProp.localScale;
            _pulseUntil = Time.unscaledTime + .72f;
        }

        public void ResetPulse()
        {
            if (_pulseProp != null) _pulseProp.localScale = _pulseScale;
            _pulseProp = null;
            _pulseUntil = 0f;
        }

        private void Update()
        {
            if (_pulseProp == null) return;
            if (Time.unscaledTime >= _pulseUntil) { ResetPulse(); return; }
            float remaining = Mathf.Clamp01((_pulseUntil - Time.unscaledTime) / .72f);
            float pulse = Mathf.Sin((1f - remaining) * Mathf.PI * 3f) * remaining * .055f;
            _pulseProp.localScale = _pulseScale * (1f + pulse);
        }

        private void OnDisable() => ResetPulse();
    }
}
