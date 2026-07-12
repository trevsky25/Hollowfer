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
        [SerializeField, Tooltip("Voice-over per intro caption, index-matched (batch-29 VO test). Missing/null entries are silent.")]
        private AudioClip[] _introVoiceClips;

        private static readonly string[] IntroCaptions =
        {
            "It had been three years since Wren Tobin walked the east road into Hollowfen.",
            "The village did not greet her.",
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
                NarrationOverlay.Instance.Show(IntroCaptions, _introVoiceClips,
                    () => { if (IntroGuide.Instance != null) IntroGuide.Instance.ShowOnce(); });
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
