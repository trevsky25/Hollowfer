using System;
using Hollowfen.Data;
using Hollowfen.Quests;
using UnityEngine;

namespace Hollowfen.Dialogue
{
    // Matches the data shape documented in docs/dialog-system.md. Ordinary consecutive
    // same-speaker prose is merged with "\n\n"; separate voiced lines are retained when the pause
    // is an authored dramatic beat. The `isCloseup` flag marks emotional-climax camera beats.
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 6)] public string text;
        public bool isCloseup;
        [Tooltip("Optional voice-over played when this line shows (batch-29 VO pipeline). Null = silent — pre-VO dialogues keep working unchanged.")]
        public AudioClip voiceClip;
    }

    // A player choice offered after the dialog's last line (and after its outcomes fire).
    // Picking one optionally sets a flag, then branches into `next` (null = just close).
    // Max 4 — the input scheme is number keys 1-4 / D-pad + confirm.
    [Serializable]
    public struct DialogueChoice
    {
        [TextArea(1, 3)] public string text;
        public DialogueData next;
        [Tooltip("Game flag set when this choice is picked (e.g. theo_offer_accepted). Empty = none.")]
        public string setsFlagId;
        [Tooltip("Optional terminal ending selected by this choice. Mutually exclusive with Next.")]
        public EndingData ending;
    }

    // Optional live prop beat played immediately before a dialogue line. The cue is deliberately
    // presentation-only: inventory outcomes still fire from the dialogue's one-shot outcome block,
    // so skipping/cancelling a cinematic can never duplicate or lose forage.
    [Serializable]
    public struct DialogueMushroomHandoffCue
    {
        [Min(0), Tooltip("Zero-based line index shown after the handoff completes.")]
        public int beforeLineIndex;
        [Tooltip("Named live dialogue participant receiving the mushroom, e.g. Marra.")]
        public string recipientSpeaker;
        [Tooltip("Canonical species whose journal-preview model is used for the handoff.")]
        public MushroomFieldGuideData mushroom;
        [Range(0.12f, 0.50f), Tooltip("Presented mushroom height in world metres.")]
        public float presentationHeight;

        public bool IsConfigured => mushroom != null;
        public float PresentationHeight => presentationHeight > 0f ? presentationHeight : 0.26f;
    }

    [Serializable]
    public struct DialogueMemoryOutcome
    {
        public string npcId;
        public string memoryId;
    }

    [Serializable]
    public struct DialogueBondOutcome
    {
        public string firstNpcId;
        public string secondNpcId;
        public int delta;
    }

    [Serializable]
    public struct DialogueFavorOutcome
    {
        public string favorId;
        [Min(1)] public int stage;
    }

    [CreateAssetMenu(fileName = "Dialogue_New", menuName = "Hollowfen/Dialogue/Dialogue Data")]
    public class DialogueData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private DialogueLine[] _lines;

        [Header("Live cinematic cues")]
        [SerializeField, Tooltip("Optional in-dialogue 3D mushroom transfer; presentation only.")]
        private DialogueMushroomHandoffCue _mushroomHandoff;

        [Header("Outcome (fires when the dialog finishes)")]
        [SerializeField] private StoryCardData _unlockStoryCard;
        [SerializeField] private QuestData _completeQuest;
        [SerializeField] private string _giveItemId;
        [SerializeField, Tooltip("Coins granted on finish, in total copper (12c = 1s). 0 = none.")]
        private int _grantCoinsCopper;
        [SerializeField, Tooltip("Coins SPENT on finish, in total copper (Joren's commission, Voss's tax). Best-effort TrySpend.")]
        private int _spendsCoinsCopper;
        [SerializeField, Tooltip("Empties Wren's mushroom basket on finish (firstSale: Marra takes the lot for the pot).")]
        private bool _sellsForageBasket;
        [SerializeField, Tooltip("With Sells Forage Basket: copper paid PER mushroom in the basket (repeatable Marra sale loop).")]
        private int _basketCopperPerItem;
        [SerializeField, Tooltip("Species-aware buyer. None preserves the legacy flat basket payout for authored one-off scenes.")]
        private MushroomBuyer _basketBuyer;
        [SerializeField, Tooltip("Forage granted on finish (Almy's Wood Ear spawn plugs). Null = none.")]
        private MushroomFieldGuideData _grantForage;
        [SerializeField] private int _grantForageCount = 1;
        [SerializeField, Tooltip("Forage CONSUMED from the basket on finish (Marra's Brightspore tonic). Best-effort remove; gate availability with the NPC entry's Requires Forage.")]
        private MushroomFieldGuideData _consumeForage;
        [SerializeField] private int _consumeForageCount = 1;
        [SerializeField, Tooltip("Game flags set on finish (story.md flag tables, e.g. voss_first_visit_seen).")]
        private string[] _setFlagIds;

        [Header("Score deltas (story.md relationship tables)")]
        [SerializeField] private int _villageHopeDelta;
        [SerializeField] private int _knowledgeDelta;
        [SerializeField, Tooltip("NPC ids, parallel with the deltas array (e.g. bram, marra).")]
        private string[] _relationshipNpcIds;
        [SerializeField] private int[] _relationshipDeltas;

        [Header("Relationship memory")]
        [SerializeField, Tooltip("Specific moments remembered by the named villager after this dialogue.")]
        private DialogueMemoryOutcome[] _memoryOutcomes;
        [SerializeField, Tooltip("Changes to relationships between villagers (not Wren's relationship score).")]
        private DialogueBondOutcome[] _bondOutcomes;
        [SerializeField, Tooltip("Monotonic progress through optional personal favor chains.")]
        private DialogueFavorOutcome[] _favorOutcomes;
        [SerializeField, Min(0), Tooltip("Quiet activities may pass time without using a cutscene-specific clock script.")]
        private int _advanceMinutes;
        [SerializeField, Tooltip("Commit flags, score changes, relationship memories/bonds, and clock advance as one durable save. Used by one-shot living-village scenes.")]
        private bool _atomicSocialOutcomes;

        [Header("Presentation transition")]
        [SerializeField, Tooltip("Optional story moment shown after this node's outcomes and before choices or Next Dialog.")]
        private StoryMomentData _transitionMoment;
        [SerializeField] private DialogueData _nextDialog;
        [SerializeField, Tooltip("Player choices shown after the last line (outcomes fire first). Non-empty = _nextDialog is ignored; each choice branches on its own. Max 4.")]
        private DialogueChoice[] _choices;

        public string Id => _id;
        public DialogueLine[] Lines => _lines;
        public DialogueMushroomHandoffCue MushroomHandoff => _mushroomHandoff;
        public StoryCardData UnlockStoryCard => _unlockStoryCard;
        public QuestData CompleteQuest => _completeQuest;
        public string GiveItemId => _giveItemId;
        public int GrantCoinsCopper => _grantCoinsCopper;
        public int SpendsCoinsCopper => _spendsCoinsCopper;
        public bool SellsForageBasket => _sellsForageBasket;
        public int BasketCopperPerItem => _basketCopperPerItem;
        public MushroomBuyer BasketBuyer => _basketBuyer;
        public MushroomFieldGuideData GrantForage => _grantForage;
        public int GrantForageCount => _grantForageCount;
        public MushroomFieldGuideData ConsumeForage => _consumeForage;
        public int ConsumeForageCount => _consumeForageCount;
        public string[] SetFlagIds => _setFlagIds;
        public int VillageHopeDelta => _villageHopeDelta;
        public int KnowledgeDelta => _knowledgeDelta;
        public string[] RelationshipNpcIds => _relationshipNpcIds;
        public int[] RelationshipDeltas => _relationshipDeltas;
        public DialogueMemoryOutcome[] MemoryOutcomes => _memoryOutcomes;
        public DialogueBondOutcome[] BondOutcomes => _bondOutcomes;
        public DialogueFavorOutcome[] FavorOutcomes => _favorOutcomes;
        public int AdvanceMinutes => Mathf.Max(0, _advanceMinutes);
        public bool AtomicSocialOutcomes => _atomicSocialOutcomes;
        public StoryMomentData TransitionMoment => _transitionMoment;
        public DialogueData NextDialog => _nextDialog;
        public DialogueChoice[] Choices => _choices;
    }
}
