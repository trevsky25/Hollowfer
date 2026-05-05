using UnityEngine;

namespace Hollowfen.Foraging
{
    public interface IInteractable
    {
        string PromptVerb { get; }
        string PromptTarget { get; }
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
    }
}
