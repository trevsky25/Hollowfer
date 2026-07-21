using Hollowfen.Foraging;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>Triangle/E interaction placed beside each purchased candle or lantern rig.</summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryLightSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField] private ApothecaryLightingController _controller;

        public ApothecaryLightingController Controller => _controller;
        public string PromptVerb => _controller != null && _controller.LightsOn
            ? "prompt.apothecary.lights.douse"
            : "prompt.apothecary.lights.light";
        public string PromptTarget => Localization.Get("apothecary.lights.name");

        public void Configure(ApothecaryLightingController controller) => _controller = controller;

        public bool CanInteract(GameObject actor) => actor != null && _controller != null &&
                                                     !_controller.IsTransitioning;

        public void Interact(GameObject actor)
        {
            if (CanInteract(actor)) _controller.Toggle();
        }
    }
}
