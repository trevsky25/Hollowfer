using Hollowfen.Foraging;
using Hollowfen.Items;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Locked door gating a story beat (Father's Mill). Becomes interactable once the player
    // carries the required key item; on unlock it plays the pack door's open animation, drops
    // the door collider so the doorway is passable, and completes the configured quest.
    // Same interaction convention as MushroomNode: trigger SphereCollider on the Foraging layer.
    [DisallowMultipleComponent]
    public class KeyLockedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _requiredItemId = "item.mill_key";
        [SerializeField, Tooltip("Quest completed on unlock if currently active.")]
        private QuestData _completesQuestIfActive;
        [SerializeField] private string _promptVerbId = "prompt.door.unlock";
        [SerializeField, Tooltip("Localization id for the door's display name.")]
        private string _promptTargetId = "loc.fathers_mill.name";
        [SerializeField, Tooltip("The pack Door object carrying DemoDoor + Animation + BoxCollider.")]
        private GameObject _door;

        private bool _opened;

        public string PromptVerb => _promptVerbId;
        public string PromptTarget => Localization.Get(_promptTargetId);

        public bool CanInteract(GameObject actor) => !_opened && KeyItems.Has(_requiredItemId);

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            _opened = true;

            if (_door != null)
            {
                var demo = _door.GetComponent<InfinityPBR.DemoDoor>();
                bool animated = false;
                if (demo != null && demo.open != null)
                {
                    demo.Open();
                    animated = true;
                }
                var col = _door.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                // No open clip on this prefab variant — swing it out of the way so the
                // doorway still reads as opened.
                if (!animated) _door.transform.Rotate(0f, 110f, 0f, Space.Self);
            }

            if (_completesQuestIfActive != null && QuestManager.IsActive(_completesQuestIfActive.Id))
                QuestManager.CompleteQuest(_completesQuestIfActive.Id);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.2f, 0.4f);
        }
    }
}
