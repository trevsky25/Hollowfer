using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Quests;
using Hollowfen.Requests;
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
        public string PromptVerb
        {
            get
            {
                ResolveInteraction(out var request, out var dialog, out _);
                if (request == null || dialog != null) return "prompt.npc.talk";
                return VillageRequests.CanDeliver(request) ? "prompt.request.deliver" : "prompt.request.view";
            }
        }
        public string PromptTarget => _data != null
            ? Hollowfen.Localization.Get(_data.DisplayNameId)
            : "(unset)";

        public bool CanInteract(GameObject actor)
        {
            if (_data == null) return false;
            if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen) return false;
            if (VillageRequestScreen.Instance != null && VillageRequestScreen.Instance.IsOpen) return false;
            ResolveInteraction(out var request, out var dialog, out _);
            return request != null || dialog != null;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            ResolveInteraction(out var request, out var dialog, out var fallback);
            if (request != null && dialog == null)
            {
                VillageRequestScreen.Ensure().Open(request, PromptTarget, transform, fallback);
                return;
            }
            if (dialog == null) return;
            if (DialogueScreen.Instance == null) { Debug.LogWarning("NPCInteractable: no DialogueScreen in scene."); return; }
            DialogueScreen.Instance.Open(dialog, transform);
        }

        private void ResolveInteraction(out VillageRequestData request, out DialogueData dialog,
            out DialogueData fallback)
        {
            request = null;
            dialog = null;
            fallback = null;
            if (_data == null) return;

            NPCSchedule schedule = NPCSchedule.ForActor(gameObject);
            string scheduleSlot = schedule != null ? schedule.CurrentSlotLabel : null;
            request = VillageRequests.CurrentForNpc(_data.Id);
            var activeStory = _data.PickActiveQuestDialog(scheduleSlot);
            bool requestOwnsActiveQuest = request != null &&
                !string.IsNullOrWhiteSpace(request.ActiveQuestId) &&
                QuestManager.IsActive(request.ActiveQuestId);

            if (activeStory != null && !requestOwnsActiveQuest)
            {
                dialog = activeStory;
                request = null;
                return;
            }

            if (request != null)
            {
                // Story deliveries deliberately keep the player inside their objective. Ordinary
                // orders always offer the NPC's normal conversation/trade as a second button.
                fallback = requestOwnsActiveQuest ? null : _data.PickDialog(scheduleSlot);
                return;
            }

            dialog = _data.PickDialog(scheduleSlot);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.7f, 0.5f);
        }
    }
}
