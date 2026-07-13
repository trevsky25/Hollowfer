using Hollowfen.Save;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Scene-level narrative beats that frame Act I: the once-per-save homecoming intro
    // (web playHomecomingIntro parity) and the act-break journal narration after meetAlmy.
    // Caption copy is verbatim from docs/story.md.
    public class StoryBeats : MonoBehaviour
    {
        [SerializeField, Tooltip("Voice-over for the FIRST and LAST intro captions (index 0 → clip[0], last → clip[1]). Missing/null entries are silent.")]
        private AudioClip[] _introVoiceClips;

        [SerializeField, Tooltip("Cinematic hero image for the opening (homecoming.png). Null → plain black narration.")]
        private Sprite _introHeroImage;

        // The homecoming passage, restored to the bible's fuller opening (story.md Scene 1,
        // verbatim) — batch-36. Painted over the homecoming hero image with a slow Ken Burns.
        private static readonly string[] IntroCaptions =
        {
            "It had been three years since Wren Tobin walked the east road into Hollowfen.",
            "At first, the valley looked as it always had from the ridge: the low roofs tucked into the hollow, the dark shoulder of the Old Wood behind them, the pale line of the Wend cutting through the fields.",
            "Then the road dipped, and the old picture came apart.",
            "The river was wrong.",
            "Smoke rose from fewer chimneys than Wren remembered. Two cottages near the well had boards nailed over their windows.",
            "The village did not greet her. No children ran the lane. No cart rattled down from the Slatemoor road.",
        };

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

        private void Start()
        {
            // Homecoming intro: once per save, only before the arrive beat.
            if (QuestManager.IsCompleted("arrive")) return;
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            if (meta != null && meta.HomecomingIntroSeen)
            {
                // Player quit between the intro and the guide — the guide still owes them
                // its one showing (ShowOnce self-gates on its own flag + arrive completion).
                if (IntroGuide.Instance != null) IntroGuide.Instance.ShowOnce();
                return;
            }
            SaveManager.AutoSaveIntroSeen();
            if (NarrationOverlay.Instance != null)
            {
                // The two existing VO clips voice the first and last captions (the emotional
                // anchors); the restored middle beats play over the moving image (silent for now
                // — full-passage VO pends the Q10 audio-direction call).
                var clips = new AudioClip[IntroCaptions.Length];
                if (_introVoiceClips != null && _introVoiceClips.Length > 0) clips[0] = _introVoiceClips[0];
                if (_introVoiceClips != null && _introVoiceClips.Length > 1) clips[IntroCaptions.Length - 1] = _introVoiceClips[1];
                NarrationOverlay.Instance.ShowCinematic(IntroCaptions, clips, _introHeroImage,
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
