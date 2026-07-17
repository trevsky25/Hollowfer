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
        [SerializeField, Tooltip("Only interactable while holding this key item (the tonic delivery needs the tonic). Empty = ignore.")]
        private string _requiresItemId;
        [SerializeField, Tooltip("Game flag set on use (e.g. tonic_delivered — lets DayFlagScheduler stage a next-day beat). Empty = none.")]
        private string _setsFlagId;
        [SerializeField, Tooltip("Field-guide entries unlocked on use (Sable's seedbook teaches Moonring/Hollowheart/Wendlight). Fires OnDiscovered → ScoreHooks Knowledge/flags.")]
        private Data.MushroomFieldGuideData[] _discoversSpecies;
        [SerializeField, Tooltip("Dialogue opened on use (the seedbook scene, Wren's riverbed lines). Play quest completion via the DIALOGUE's outcome when this is set.")]
        private Dialogue.DialogueData _playsDialogue;
        [SerializeField, Tooltip("Authored story presentation for this interaction. Media and timing live on the asset.")]
        private Data.StoryMomentData _storyMoment;
        [SerializeField, Tooltip("Optional world-space context passed to the story director for an authored focus shot. Null uses this prop.")]
        private Transform _storyMomentContext;

        [SerializeField] private bool _deactivateOnUse = true;

        private bool _used;

        public string PromptVerb => _promptVerbId;
        public string PromptTarget => Localization.Get(_promptTargetId);

        public bool CanInteract(GameObject actor)
        {
            if (_used) return false;
            if (_requiresActiveQuest != null && !QuestManager.IsActive(_requiresActiveQuest.Id)) return false;
            if (!string.IsNullOrEmpty(_requiresItemId) && !KeyItems.Has(_requiresItemId)) return false;
            return true;
        }

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            _used = true;

            if (!string.IsNullOrEmpty(_grantsItemId))
                KeyItems.Grant(_grantsItemId);

            if (!string.IsNullOrEmpty(_setsFlagId))
                GameScores.SetFlag(_setsFlagId);

            if (_discoversSpecies != null)
                foreach (var species in _discoversSpecies)
                    if (species != null) MushroomDiscovery.MarkDiscovered(species.Id);

            if (_completesQuestIfActive != null && QuestManager.IsActive(_completesQuestIfActive.Id))
                QuestManager.CompleteQuest(_completesQuestIfActive.Id);

            if (_storyMoment != null)
            {
                UI.StoryMomentDirector.Ensure().Play(_storyMoment,
                    _storyMomentContext != null ? _storyMomentContext : transform,
                    ContinueAfterPresentation);
                return;
            }

            ContinueAfterPresentation();
        }

        private void ContinueAfterPresentation()
        {
            if (_playsDialogue != null && Dialogue.DialogueScreen.Instance != null)
            {
                Dialogue.DialogueScreen.Instance.Open(_playsDialogue, transform, Retire);
                return;
            }
            Retire();
        }

        private void Retire()
        {
            if (_deactivateOnUse)
                gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.8f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.35f);
        }
    }
}
