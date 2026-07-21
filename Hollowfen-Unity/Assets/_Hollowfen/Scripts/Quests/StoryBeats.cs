using Hollowfen.Save;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Scene-level narrative beats that frame Act I: the once-per-save homecoming intro
    // (web playHomecomingIntro parity) and the act-break journal narration after meetAlmy.
    // Caption copy follows docs/story.md and resolves through Localization at presentation time.
    public class StoryBeats : MonoBehaviour
    {
        [SerializeField, Tooltip("Voice-over per intro caption, index-matched to IntroCaptions (batch-39: full 6-beat set). Missing/null entries are silent.")]
        private AudioClip[] _introVoiceClips;

        [SerializeField, Tooltip("Cinematic hero image for the opening (homecoming.png). Null → plain black narration.")]
        private Sprite _introHeroImage;

        [SerializeField, Tooltip("Second hero image, swapped in at _introSwitchBeat so the long intro isn't one picture (batch-40). Null → stays on image 1. Superseded by _introImages when that is set.")]
        private Sprite _introHeroImage2;
        [SerializeField, Tooltip("Caption index at which the narration swaps to image 2 (legacy 2-image path).")]
        private int _introSwitchBeat = 3;

        [SerializeField, Tooltip("Ordered intro paintings (batch-41): ridge, river, cottages, square. When set (length > 0) the intro plays this 4-image crossfade sequence via IntroBeatImage; the two-image fields above are the fallback.")]
        private Sprite[] _introImages;

        // Which painting each of the 6 homecoming beats is shown over (index into _introImages):
        // ridge (beats 0-1) → wrong river (2-3) → boarded cottages (4) → silent square/Bram (5).
        private static readonly int[] IntroBeatImage = { 0, 0, 1, 1, 2, 3 };

        private static string[] ResolveIntroCaptions()
        {
            var captions = new string[6];
            for (int i = 0; i < captions.Length; i++)
                captions[i] = Localization.Get("act1.homecoming.intro." + i);
            return captions;
        }

        private static readonly string[] Act1CompleteCaptions =
        {
            "From Wren's journal —",
            "“Bram gave me the key. Father's house is colder than I remembered. His journal was hidden in the bottom drawer, wrapped like a thing that could still be protected.”",
            "“I found Field Caps, Wood Ear, Pinecrest, and three Goldfoots I was not entirely brave enough to name until Marra did. This morning Sister Almy came to the door and said my grandmother's name like a key turning.”",
            "“I do not know what opens next.”",
        };

        private void OnEnable()
        {
            QuestManager.QuestCompleted += HandleQuestCompleted;
        }

        private void OnDisable()
        {
            QuestManager.QuestCompleted -= HandleQuestCompleted;
        }

        private System.Collections.IEnumerator Start()
        {
            // Scene activation precedes the loading card's smoothed visual completion. For a normal
            // new-game transition, wait until UIManager has visibly landed the card on 100% before
            // constructing the cinematic or starting its first voice clip. Direct scene play has no
            // hold and continues immediately.
            while (UIManager.IsCinematicIntroHeld)
                yield return null;

            // Homecoming intro: once per save, only before the arrive beat.
            if (QuestManager.IsCompleted("arrive")) yield break;
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            if (meta != null && meta.HomecomingIntroSeen)
            {
                // Player quit between the intro and the guide — the guide still owes them
                // its one showing (ShowOnce self-gates on its own flag + arrive completion).
                if (IntroGuide.Instance != null) IntroGuide.Instance.ShowOnce();
                yield break;
            }
            SaveManager.AutoSaveIntroSeen();
            if (NarrationOverlay.Instance != null)
            {
                string[] introCaptions = ResolveIntroCaptions();
                // Full-passage VO (batch-39). 4-image crossfade sequence (batch-41) when _introImages
                // is populated; otherwise the legacy 2-image swap (batch-40) is the fallback.
                if (_introImages != null && _introImages.Length > 0)
                    NarrationOverlay.Instance.ShowCinematic(introCaptions, _introVoiceClips, _introImages, IntroBeatImage,
                        () => { if (IntroGuide.Instance != null) IntroGuide.Instance.ShowOnce(); });
                else
                    NarrationOverlay.Instance.ShowCinematic(introCaptions, _introVoiceClips, _introHeroImage,
                        _introHeroImage2, _introSwitchBeat,
                        () => { if (IntroGuide.Instance != null) IntroGuide.Instance.ShowOnce(); });
            }
        }

        private void HandleQuestCompleted(QuestData quest)
        {
            if (quest == null) return;

            // Checkpoint: every quest completion snapshots full state incl. player position.
            SaveCoordinator.SaveAllWithPlayer();

            if (quest.Id != "meetAlmy") return;
            if (NarrationOverlay.Instance != null)
                NarrationOverlay.Instance.Show(Act1CompleteCaptions);
        }
    }
}
