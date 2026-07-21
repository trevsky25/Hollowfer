using System.Collections;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.UI
{
    /// <summary>Owns the terminal flow after an eligible choice leaves the Aldric dialogue.</summary>
    public sealed class EndingDirector : MonoBehaviour
    {
        public static EndingDirector Instance { get; private set; }

        public bool IsPresenting { get; private set; }
        public EndingData CurrentEnding { get; private set; }
        private NarrativePresentationSession.Lease _presentationLease;

        public static EndingDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("_EndingDirector");
            DontDestroyOnLoad(go);
            return go.AddComponent<EndingDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (Instance == this) Instance = null;
        }

        public void Begin(EndingData ending)
        {
            if (IsPresenting || ending == null) return;
            if (!EndingResolver.TryResolve(ending, out var failure))
            {
                Debug.LogWarning("[Ending] Could not resolve ending: " + failure);
                UISfx.Error();
                return;
            }

            StartPresentation(ending);
        }

        /// <summary>Replays a previously committed finale after an interrupted session.</summary>
        public void ResumeCommitted(EndingData ending)
        {
            if (IsPresenting || ending == null) return;
            if (!EndingResolver.ReconcileCommittedEnding(ending))
            {
                Debug.LogWarning("[Ending] Refused to resume an ending that is not committed.");
                return;
            }

            StartPresentation(ending);
        }

        private void StartPresentation(EndingData ending)
        {
            CurrentEnding = ending;
            IsPresenting = true;
            _presentationLease = NarrativePresentationSession.AcquireIfGameplay(
                this,
                NarrativePresentationSession.Modal
                    .With(NarrativePresentationSession.Claim.HideGameplayHud));
            StartCoroutine(BeginSequence());
        }

        private IEnumerator BeginSequence()
        {
            // Let the meeting dialogue finish its camera/time/input teardown before the ending
            // dialogue takes ownership on the next frame.
            yield return null;
            if (CurrentEnding != null && CurrentEnding.ResolutionDialogue != null && DialogueScreen.Instance != null)
                DialogueScreen.Instance.Open(CurrentEnding.ResolutionDialogue, null, ShowEpilogue);
            else
                ShowEpilogue();
        }

        private void ShowEpilogue()
        {
            if (CurrentEnding == null) { FinishPresentation(); return; }
            var overlay = NarrationOverlay.Instance;
            var card = CurrentEnding.StoryCard;
            if (overlay != null && card != null && card.Image != null &&
                CurrentEnding.EpilogueCaptions != null && CurrentEnding.EpilogueCaptions.Length > 0)
            {
                overlay.ShowCinematic(JournalText.EndingEpilogue(CurrentEnding), null, card.Image, ShowCredits);
                return;
            }
            ShowCredits();
        }

        private void ShowCredits()
        {
            if (CurrentEnding == null || !EndingCreditsScreen.Show(CurrentEnding, FinishPresentation))
                FinishPresentation();
        }

        public void FinishPresentation()
        {
            IsPresenting = false;
            CurrentEnding = null;
            ReleasePresentation();
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }
    }
}
