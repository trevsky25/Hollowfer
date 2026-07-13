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
        [SerializeField, Tooltip("Narration passage read on use (Tobin's journal note). The localized text is split on blank lines into NarrationOverlay captions — live Georgia text. Empty = none.")]
        private string _playsNarrationId;
        [SerializeField, Tooltip("Painted spreads for the narration (journal interior close-ups). When set, the passage plays as NarrationOverlay.ShowCinematic — crossfade + Ken Burns over these paintings — instead of black captions.")]
        private Sprite[] _narrationHeroes;
        [SerializeField, Tooltip("Per-caption painting index (which _narrationHeroes image each caption sits over). Same length as the caption count; missing/over-range entries clamp.")]
        private int[] _narrationBeatImages;

        [Header("Focus push-in (optional, batch-53)")]
        [SerializeField, Tooltip("Prop pushed in on before the reveal plays (the 3D journal book). Null = no push-in.")]
        private Transform _focusTarget;
        [SerializeField] private float _focusDistance = 0.4f;
        [SerializeField] private float _focusHeight = 0.05f;
        [SerializeField] private float _focusFov = 30f;
        [SerializeField] private float _focusPush = 1.2f;
        [SerializeField] private float _focusHold = 1.6f;
        [SerializeField] private float _focusRestore = 0.2f;

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

            // Optional cinematic push-in on the 3D prop (the journal book) before the reveal — the camera
            // moves close on the book, then hands to PlayReveal which fades to the painted-spread finale.
            if (_focusTarget != null && Cinematics.PropFocusCinematic.Ensure() != null
                && !Cinematics.PropFocusCinematic.Instance.IsPlaying)
                Cinematics.PropFocusCinematic.Instance.Play(_focusTarget, _focusDistance, _focusHeight, _focusFov,
                    _focusPush, _focusHold, _focusRestore, null, PlayReveal);
            else
                PlayReveal();
        }

        // Dialogue / narration payload + self-deactivate. Runs immediately, or after the focus push-in.
        private void PlayReveal()
        {
            if (_playsDialogue != null && Dialogue.DialogueScreen.Instance != null)
                // Anchor = this prop, so monologues get the cinematic frame too (batch-45).
                Dialogue.DialogueScreen.Instance.Open(_playsDialogue, transform);

            // Read a passage as live Georgia narration captions (Tobin's journal note) — same overlay as
            // the opening intro. With painted spreads set (_narrationHeroes), it plays as the multi-image
            // ShowCinematic (crossfade + Ken Burns over the paintings, captions on top); else black captions.
            if (!string.IsNullOrEmpty(_playsNarrationId) && UI.NarrationOverlay.Instance != null)
            {
                string passage = Localization.Get(_playsNarrationId);
                if (!string.IsNullOrEmpty(passage))
                {
                    var captions = passage.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (_narrationHeroes != null && _narrationHeroes.Length > 0)
                        UI.NarrationOverlay.Instance.ShowCinematic(captions, null, _narrationHeroes, _narrationBeatImages, null);
                    else
                        UI.NarrationOverlay.Instance.Show(captions);
                }
            }

            if (_deactivateOnUse) gameObject.SetActive(false);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.55f, 0.8f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.6f, 0.35f);
        }
    }
}
