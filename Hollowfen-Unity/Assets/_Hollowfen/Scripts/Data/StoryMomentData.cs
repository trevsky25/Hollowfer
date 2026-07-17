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
        // Dialogue interstitials are clean story-card paintings by design. Illustrated narration
        // and act breaks can opt into written copy; the timing caption still resolves for VO.
        public bool ShowStoryCardTitle => _presentation != StoryMomentPresentation.DialogueInterstitial && _showStoryCardTitle;
        public bool ShowCaptions => _presentation != StoryMomentPresentation.DialogueInterstitial && _showCaptions;
        public bool FadeIn => _fadeIn;
        public float HoldSeconds => _holdSeconds;
        public bool FocusContext => _focusContext;
        public float FocusDistance => _focusDistance;
        public float FocusHeight => _focusHeight;
        public float FocusFov => _focusFov;
        public float FocusPushSeconds => _focusPushSeconds;
        public float FocusHoldSeconds => _focusHoldSeconds;
        public float FocusRestoreSeconds => _focusRestoreSeconds;

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
