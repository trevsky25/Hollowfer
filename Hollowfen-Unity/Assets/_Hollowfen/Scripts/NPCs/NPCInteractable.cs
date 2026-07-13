using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using UnityEngine;

namespace Hollowfen.NPCs
{
    // Drop on an NPC GameObject (e.g., the Bram placeholder capsule) with a trigger SphereCollider
    // on the Foraging layer — the same convention MushroomNode uses, so PlayerInteractor picks it up.
    // When the player presses Interact in range, opens DialogueScreen with the dialog returned by
    // NPCData.PickDialog() (which evaluates the player's current quest state).
    [DisallowMultipleComponent]
    public class NPCInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private NPCData _data;

        public NPCData Data => _data;
        public string PromptVerb => "prompt.npc.talk";
        public string PromptTarget => _data != null
            ? Hollowfen.Localization.Get(_data.DisplayNameId)
            : "(unset)";

        public bool CanInteract(GameObject actor)
        {
            if (_data == null) return false;
            if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen) return false;
            return _data.PickDialog() != null;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            var dlg = _data.PickDialog();
            if (dlg == null) return;
            if (DialogueScreen.Instance == null) { Debug.LogWarning("NPCInteractable: no DialogueScreen in scene."); return; }
            // Anchor = this NPC, so the cinematic camera can frame speaker vs listener (batch-45).
            DialogueScreen.Instance.Open(dlg, transform);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.7f, 0.5f);
        }
    }
}
