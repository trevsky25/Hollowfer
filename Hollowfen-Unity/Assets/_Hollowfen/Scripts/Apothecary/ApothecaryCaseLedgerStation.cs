using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Requests;
using Hollowfen.Restoration;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>World interaction attached to the purchased open ledger inside Tobin's workshop.</summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryCaseLedgerStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _restorationProjectId = "tobin_workshop";
        [SerializeField] private RestorationStage _requiredStage = RestorationStage.Occupied;

        public string PromptVerb => "prompt.apothecary.casework";
        public string PromptTarget => Localization.Get("apothecary.casework.station");

        public bool CanInteract(GameObject actor)
        {
            if (actor == null || ApothecaryCaseScreen.Instance != null && ApothecaryCaseScreen.Instance.IsOpen)
                return false;
            if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen) return false;
            if (VillageRequestScreen.Instance != null && VillageRequestScreen.Instance.IsOpen) return false;
            var project = RestorationProjects.Resolve(_restorationProjectId);
            return project != null && RestorationProjects.GetStage(project) >= _requiredStage;
        }

        public void Interact(GameObject actor)
        {
            if (CanInteract(actor)) ApothecaryCaseScreen.Ensure().Open(this);
        }
    }
}
