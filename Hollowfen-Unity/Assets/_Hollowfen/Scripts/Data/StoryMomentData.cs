using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollowfen.Data
{
    public enum StoryMomentPresentation
    {
        IllustratedNarration = 0,
        DialogueInterstitial = 1,
        ActBreak = 2,
    }

    public enum StoryMomentTrigger
    {
        DialogueTransition = 0,
        ManualInteraction = 1,
    }

    // Authored presentation for a quest's hero beat. StoryCardData remains the canonical
    // title/art/reward; this asset describes how that material is presented in the world.
    // Runtime scene context (an NPC or prop transform) is deliberately supplied by the caller.
    [CreateAssetMenu(fileName = "StoryMoment_New", menuName = "Hollowfen/Story/Story Moment")]
    public sealed class StoryMomentData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private StoryMomentPresentation _presentation;
        [SerializeField] private StoryMomentTrigger _trigger;
        [SerializeField] private StoryCardData _storyCard;

        [Header("Narration")]
        [SerializeField, Tooltip("Localized caption or passage IDs. Passages split on blank lines into beats.")]
        private string[] _captionIds;
        [SerializeField, TextArea(2, 6), Tooltip("English fallbacks, index-matched to Caption Ids.")]
        private string[] _captionFallbacks;
        [SerializeField, Tooltip("Optional voice-over, index-matched to the resolved caption beats.")]
        private AudioClip[] _voiceClips;

        [Header("Illustration")]
        [SerializeField, Tooltip("Optional painted sequence. Empty uses the Story Card image.")]
        private Sprite[] _images;
        [SerializeField, Tooltip("Per-caption image index. Empty keeps the first image.")]
        private int[] _beatImages;
        [SerializeField] private bool _showStoryCardTitle;
        [SerializeField, Tooltip("Show narration copy and the continue hint over the painting. Disable for image-only story-card beats.")]
        private bool _showCaptions = true;
        [SerializeField] private bool _fadeIn = true;
        [SerializeField, Min(0f), Tooltip("Optional fixed hold per caption. Zero uses VO/read-time pacing.")]
        private float _holdSeconds;
        [SerializeField, Tooltip("Keep page-like illustrations nearly full-frame instead of applying the wider cinematic crop.")]
        private bool _preserveFullFrameImages;

        [Header("Live page text (optional)")]
        [SerializeField, Tooltip("Localized text drawn directly on one painted image.")]
        private string _pageTextId;
        [SerializeField, TextArea(3, 10), Tooltip("English fallback for the live painted-page text.")]
        private string _pageTextFallback;
        [SerializeField, Tooltip("Image index that owns the live text. Negative disables it.")]
        private int _pageTextImageIndex = -1;
        [SerializeField, Min(0), Tooltip("First caption beat that reveals the live page text.")]
        private int _pageTextStartBeat;
        [SerializeField, Tooltip("Optional reveal beat for each blank-line-separated page-text paragraph. Empty reveals the full page at the start beat.")]
        private int[] _pageTextParagraphRevealBeats;
        [SerializeField, Tooltip("Normalized x/y/width/height within the painted image.")]
        private Rect _pageTextRect = new Rect(0.2f, 0.2f, 0.6f, 0.6f);
        [SerializeField, Range(-15f, 15f)] private float _pageTextRotation;
        [SerializeField, Range(18f, 64f)] private float _pageTextFontSize = 30f;
        [SerializeField] private Color _pageTextColor = new Color(0.16f, 0.1f, 0.05f, 0.86f);
        [SerializeField, Tooltip("Render live page copy with the dedicated handwritten/cursive UI font role.")]
        private bool _useCursivePageText;

        [Header("Context focus (optional)")]
        [SerializeField, Tooltip("Push into the runtime context transform before the painted reveal.")]
        private bool _focusContext;
        [SerializeField] private float _focusDistance = 0.4f;
        [SerializeField] private float _focusHeight = 0.05f;
        [SerializeField] private float _focusFov = 30f;
        [SerializeField] private float _focusPushSeconds = 1.2f;
        [SerializeField] private float _focusHoldSeconds = 1.6f;
        [SerializeField] private float _focusRestoreSeconds = 0.2f;

        public string Id => _id;
        public StoryMomentPresentation Presentation => _presentation;
        public StoryMomentTrigger Trigger => _trigger;
        public StoryCardData StoryCard => _storyCard;
        public string[] CaptionIds => _captionIds;
        public string[] CaptionFallbacks => _captionFallbacks;
        public AudioClip[] VoiceClips => _voiceClips;
        public Sprite[] Images => _images;
        public int[] BeatImages => _beatImages;
        // Dialogue interstitials may remain clean story-card paintings when silent, but spoken
        // content must never become audio-only: any authored VO forces its matching caption on.
        public bool ShowStoryCardTitle => _presentation != StoryMomentPresentation.DialogueInterstitial && _showStoryCardTitle;
        public bool ShowCaptions
        {
            get
            {
                if (_presentation != StoryMomentPresentation.DialogueInterstitial && _showCaptions) return true;
                if (_voiceClips == null) return false;
                foreach (var clip in _voiceClips)
                    if (clip != null) return true;
                return false;
            }
        }
        public bool FadeIn => _fadeIn;
        public float HoldSeconds => _holdSeconds;
        public bool PreserveFullFrameImages => _preserveFullFrameImages;
        public string PageTextId => _pageTextId;
        public string PageTextFallback => _pageTextFallback;
        public string PageText => Localization.Get(_pageTextId, _pageTextFallback);
        public int PageTextImageIndex => _pageTextImageIndex;
        public int PageTextStartBeat => _pageTextStartBeat;
        public int[] PageTextParagraphRevealBeats => _pageTextParagraphRevealBeats;
        public Rect PageTextRect => _pageTextRect;
        public float PageTextRotation => _pageTextRotation;
        public float PageTextFontSize => _pageTextFontSize;
        public Color PageTextColor => _pageTextColor;
        public bool UseCursivePageText => _useCursivePageText;
        public bool HasPageText => _pageTextImageIndex >= 0 && !string.IsNullOrWhiteSpace(PageText);
        public bool FocusContext => _focusContext;
        public float FocusDistance => _focusDistance;
        public float FocusHeight => _focusHeight;
        public float FocusFov => _focusFov;
        public float FocusPushSeconds => _focusPushSeconds;
        public float FocusHoldSeconds => _focusHoldSeconds;
        public float FocusRestoreSeconds => _focusRestoreSeconds;

        public string PageTextForBeat(int beatIndex)
        {
            string fullText = PageText;
            if (string.IsNullOrWhiteSpace(fullText) || _pageTextParagraphRevealBeats == null ||
                _pageTextParagraphRevealBeats.Length == 0)
                return fullText;

            string[] paragraphs = fullText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var visible = new List<string>();
            for (int i = 0; i < paragraphs.Length; i++)
            {
                int revealBeat = i < _pageTextParagraphRevealBeats.Length
                    ? _pageTextParagraphRevealBeats[i]
                    : _pageTextStartBeat;
                if (beatIndex >= revealBeat)
                    visible.Add(paragraphs[i].Trim());
            }
            return string.Join("\n\n", visible);
        }

        public string[] ResolveCaptions()
        {
            var resolved = new List<string>();
            int count = Math.Max(_captionIds != null ? _captionIds.Length : 0,
                _captionFallbacks != null ? _captionFallbacks.Length : 0);

            for (int i = 0; i < count; i++)
            {
                string id = _captionIds != null && i < _captionIds.Length ? _captionIds[i] : null;
                string fallback = _captionFallbacks != null && i < _captionFallbacks.Length
                    ? _captionFallbacks[i]
                    : null;
                string passage = Localization.Get(id, fallback);
                if (string.IsNullOrWhiteSpace(passage)) continue;

                var beats = passage.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var beat in beats)
                {
                    string trimmed = beat.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) resolved.Add(trimmed);
                }
            }

            return resolved.ToArray();
        }

        public Sprite[] ResolveImages()
        {
            if (_images != null && _images.Length > 0) return _images;
            return _storyCard != null && _storyCard.Image != null
                ? new[] { _storyCard.Image }
                : Array.Empty<Sprite>();
        }
    }
}
