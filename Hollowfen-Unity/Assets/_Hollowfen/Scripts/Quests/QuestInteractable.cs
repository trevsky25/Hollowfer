using Hollowfen.Foraging;
using Hollowfen.Items;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Generic examine-to-advance prop (father's journal, sealed letter, notice board...).
    // Only interactable while the configured quest is active; on use it grants an optional
    // key item, completes the quest, and (optionally) deactivates itself. Same interaction
    // convention as MushroomNode: trigger SphereCollider on the Foraging layer.
    [DisallowMultipleComponent]
    public class QuestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _promptVerbId = "prompt.examine.verb";
        [SerializeField, Tooltip("Localization id for the prop's display name.")]
        private string _promptTargetId;
        [SerializeField, Tooltip("Only interactable while this quest is active. Null = always.")]
        private QuestData _requiresActiveQuest;
        [SerializeField] private QuestData _completesQuestIfActive;
        [SerializeField, Tooltip("Key item granted on use (e.g. item.fathers_journal). Empty = none.")]
        private string _grantsItemId;
        [SerializeField] private bool _deactivateOnUse = true;

        private bool _used;

        public string PromptVerb => _promptVerbId;
        public string PromptTarget => Localization.Get(_promptTargetId);

        public bool CanInteract(GameObject actor)
        {
            if (_used) return false;
            if (_requiresActiveQuest != null && !QuestManager.IsActive(_requiresActiveQuest.Id)) return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            _used = true;

            if (!string.IsNullOrEmpty(_grantsItemId))
                KeyItems.Grant(_grantsItemId);

            if (_completesQuestIfActive != null && QuestManager.IsActive(_completesQuestIfActive.Id))
                QuestManager.CompleteQuest(_completesQuestIfActive.Id);

            if (_deactivateOnUse) gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.8f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.35f);
        }
    }
}
