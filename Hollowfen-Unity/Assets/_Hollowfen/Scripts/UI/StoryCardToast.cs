using System.Collections;
using System.Collections.Generic;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using Hollowfen.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Ink-glass story notification that slides in from the right when a StoryCard unlocks.
    // Listens to QuestManager.StoryCardUnlocked; looks up the SO via the assigned database; queues
    // overlapping unlocks so they play sequentially rather than stomping each other.
    public class StoryCardToast : MonoBehaviour
    {
        public static StoryCardToast Instance { get; private set; }

        [SerializeField] private StoryCardDatabase _database;
        [SerializeField] private Sprite _parchmentSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(560f, 220f);
        [SerializeField] private float _restingMargin = 28f;   // offset from screen right when shown
        [SerializeField, Tooltip("Y position of the card (negative = below the top of the canvas). Set to -400 to clear the mini-map and day/time chip at 16:10.")]
        private float _anchoredY = -400f;
        [SerializeField] private float _slideInSeconds = 0.40f;
        [SerializeField] private float _holdSeconds    = 3.20f;
        [SerializeField] private float _slideOutSeconds= 0.40f;

        private readonly Queue<StoryCardData> _queue = new Queue<StoryCardData>();
        private Coroutine _playing;
        private RectTransform _card;
        private RectTransform _photoViewport;
        private Image _photoImg;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private TMP_Text _newEntryTag;
        private bool _built;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            QuestManager.StoryCardUnlocked += HandleUnlocked;
            if (_queue.Count > 0 && _playing == null) _playing = StartCoroutine(DrainQueue());
        }
        private void OnDisable()
        {
            QuestManager.StoryCardUnlocked -= HandleUnlocked;
            _playing = null;
        }

        private void HandleUnlocked(string cardId)
        {
            PresentById(cardId);
        }

        // Presentation is intentionally separate from persistence. Callers can preview an authored
        // card without unlocking it; QuestManager remains the only owner of journal progression.
        public bool PresentById(string cardId)
        {
            var card = FindCard(cardId);
            return Present(card);
        }

        public bool Present(StoryCardData card)
        {
            if (card == null) return false;
            _queue.Enqueue(card);
            if (_playing == null) _playing = StartCoroutine(DrainQueue());
            return true;
        }

        private StoryCardData FindCard(string id)
        {
            if (_database == null || string.IsNullOrEmpty(id)) return null;
            foreach (var c in _database.Cards) if (c != null && c.Id == id) return c;
            return null;
        }

        private IEnumerator DrainQueue()
        {
            BuildIfNeeded();
            // Let the interaction that unlocked the card establish its dialogue/cinematic ownership
            // before deciding whether the notification is safe to reveal.
            yield return null;
            while (_queue.Count > 0)
            {
                while (!CanPresentNow()) yield return null;
                var data = _queue.Dequeue();
                PopulateCard(data);
                yield return Animate();
            }
            _playing = null;
        }

        private bool CanPresentNow()
        {
            if (StoryMomentDirector.Instance != null && StoryMomentDirector.Instance.IsPresenting) return false;
            if (DialogueScreen.Instance != null && DialogueScreen.Instance.IsOpen) return false;
            if (NarrationOverlay.Instance != null && NarrationOverlay.Instance.IsShowing) return false;
            if (PropFocusCinematic.Instance != null && PropFocusCinematic.Instance.IsPlaying) return false;

            foreach (var group in GetComponentsInParent<CanvasGroup>(true))
                if (group != null && group.alpha < 0.99f) return false;
            return true;
        }

        private void PopulateCard(StoryCardData data)
        {
            if (_photoImg != null) {
                _photoImg.sprite = data.Image;
                _photoImg.enabled = data.Image != null;
                if (_photoViewport != null && data.Image != null && data.Image.rect.height > 0f)
                {
                    float frameW = Mathf.Max(1f, _photoViewport.rect.width);
                    float frameH = Mathf.Max(1f, _photoViewport.rect.height);
                    float imageAspect = data.Image.rect.width / data.Image.rect.height;
                    float frameAspect = frameW / frameH;
                    _photoImg.rectTransform.anchorMin = _photoImg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    _photoImg.rectTransform.anchoredPosition = Vector2.zero;
                    _photoImg.rectTransform.sizeDelta = imageAspect >= frameAspect
                        ? new Vector2(frameH * imageAspect, frameH)
                        : new Vector2(frameW, frameW / imageAspect);
                }
            }
            // Eyebrow: "ACT I  ·  SCENE 1" — fall back to data's raw strings
            string actLine = JournalText.StoryAct(data);
            string sceneLine = JournalText.StoryScene(data);
            string brow = actLine;
            if (!string.IsNullOrEmpty(sceneLine))
                brow = string.IsNullOrEmpty(brow)
                    ? sceneLine
                    : string.Format(Localization.Get("format.pair"), brow, sceneLine);
            _eyebrow.text = brow.ToUpperInvariant();
            _title.text = JournalText.StoryTitle(data);
            _subtitle.text = JournalText.StorySubtitle(data);
        }

        private IEnumerator Animate()
        {
            // Drive on unscaledTime so the toast still plays while time is frozen (dialog open, etc.).
            float hiddenX = _cardSize.x + 80f;
            float restX = -_restingMargin;
            float startTime = Time.unscaledTime;

            SetX(hiddenX);
            yield return null;

            float endIn = startTime + _slideInSeconds;
            while (Time.unscaledTime < endIn)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - startTime) / _slideInSeconds);
                float e = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic
                SetX(Mathf.Lerp(hiddenX, restX, e));
                yield return null;
            }
            SetX(restX);

            float endHold = Time.unscaledTime + _holdSeconds;
            while (Time.unscaledTime < endHold) yield return null;

            float startOut = Time.unscaledTime;
            float endOut = startOut + _slideOutSeconds;
            while (Time.unscaledTime < endOut)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - startOut) / _slideOutSeconds);
                float e = u * u * u; // ease-in cubic
                SetX(Mathf.Lerp(restX, hiddenX, e));
                yield return null;
            }
            SetX(hiddenX);
        }

        private void SetX(float x)
        {
            if (_card == null) return;
            var p = _card.anchoredPosition;
            p.x = x;
            _card.anchoredPosition = p;
        }

        // ---------------- UI BUILDER ----------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;
            transform.SetAsLastSibling(); // notification must paint over, never through, the gameplay HUD

            var canvasRT = (RectTransform)transform;

            // Card root anchored to TOP-RIGHT of canvas. anchoredPosition.x drives the slide.
            var cardGO = new GameObject("StoryCard_Toast", typeof(RectTransform));
            cardGO.transform.SetParent(canvasRT, false);
            _card = (RectTransform)cardGO.transform;
            _card.anchorMin = new Vector2(1f, 1f);
            _card.anchorMax = new Vector2(1f, 1f);
            _card.pivot = new Vector2(1f, 1f);
            _card.sizeDelta = _cardSize;
            _card.anchoredPosition = new Vector2(_cardSize.x + 80f, _anchoredY); // off-screen + configured Y

            // One quiet ink-glass silhouette. Later story unlocks now speak the same
            // visual language as the opening objective card instead of switching back
            // to a bordered parchment notification.
            UICanvasUtil.AddShadow(_card, 20, 28, 0.5f, -8f);
            var bg = cardGO.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(20);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(HollowfenPalette.SurfaceBase.r, HollowfenPalette.SurfaceBase.g,
                HollowfenPalette.SurfaceBase.b, 0.97f);

            var depth = UICanvasUtil.NewImage("SurfaceDepth", _card, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)depth.transform);
            depth.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.22f)),
                new UICanvasUtil.GradientStop(0.58f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(1f, 1f, 1f, 0.025f)),
            }, 128);

            var rail = UICanvasUtil.NewImage("StoryRail", _card, HollowfenPalette.FocusRail, false);
            var railImg = rail.GetComponent<Image>();
            railImg.sprite = UICanvasUtil.RoundedRect(2);
            railImg.type = Image.Type.Sliced;
            UICanvasUtil.SetRect((RectTransform)rail.transform, new Vector2(0f, 0.08f), new Vector2(0f, 0.92f),
                new Vector2(0f, 0.5f), new Vector2(4f, 0f), Vector2.zero);

            _newEntryTag = UICanvasUtil.NewEyebrow("NewEntry", _card,
                Localization.Get("journal.story.toast_added"), 18f, HollowfenPalette.Gold, TextAlignmentOptions.Right);
            var neRT = _newEntryTag.rectTransform;
            neRT.anchorMin = new Vector2(0f, 1f); neRT.anchorMax = new Vector2(1f, 1f);
            neRT.pivot = new Vector2(0.5f, 1f);
            neRT.sizeDelta = new Vector2(-34f, 14f);
            neRT.anchoredPosition = new Vector2(0f, -15f);

            // Full-bleed, softly cropped story image on the left.
            float padX = 14f;
            float photoSize = _cardSize.y - 50f;
            _photoViewport = UICanvasUtil.NewRect("PhotoViewport", _card);
            _photoViewport.anchorMin = new Vector2(0f, 0.5f); _photoViewport.anchorMax = new Vector2(0f, 0.5f);
            _photoViewport.pivot = new Vector2(0f, 0.5f);
            _photoViewport.sizeDelta = new Vector2(photoSize, photoSize);
            _photoViewport.anchoredPosition = new Vector2(padX, -8f);
            _photoViewport.gameObject.AddComponent<RectMask2D>();

            var photoGO = new GameObject("Photo", typeof(RectTransform));
            photoGO.transform.SetParent(_photoViewport, false);
            _photoImg = photoGO.AddComponent<Image>();
            _photoImg.preserveAspect = false;
            var phRT = (RectTransform)photoGO.transform;
            phRT.anchorMin = phRT.anchorMax = new Vector2(0.5f, 0.5f);
            phRT.pivot = new Vector2(0.5f, 0.5f);
            phRT.sizeDelta = new Vector2(photoSize, photoSize);
            phRT.anchoredPosition = Vector2.zero;

            // Right side text column
            float textLeft = padX + photoSize + 24f;
            float textWidth = _cardSize.x - textLeft - 20f;

            _eyebrow = UICanvasUtil.NewEyebrow("CardEyebrow", _card, "", 18f,
                HollowfenPalette.Gold, TextAlignmentOptions.TopLeft);
            _eyebrow.fontStyle = FontStyles.Bold;
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(0f, 1f);
            eRT.pivot = new Vector2(0f, 1f);
            eRT.sizeDelta = new Vector2(textWidth, 14f);
            eRT.anchoredPosition = new Vector2(textLeft, -42f);

            _title = UICanvasUtil.NewHeading("CardTitle", _card, "", 28f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(0f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(textWidth, 66f);
            tRT.anchoredPosition = new Vector2(textLeft, -62f);

            _subtitle = UICanvasUtil.NewBody("CardSubtitle", _card, "", 20f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g, HollowfenPalette.Parchment.b, 0.76f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _subtitle.textWrappingMode = TextWrappingModes.Normal;
            var sRT = _subtitle.rectTransform;
            sRT.anchorMin = new Vector2(0f, 1f); sRT.anchorMax = new Vector2(0f, 1f);
            sRT.pivot = new Vector2(0f, 1f);
            sRT.sizeDelta = new Vector2(textWidth, 58f);
            sRT.anchoredPosition = new Vector2(textLeft, -126f);

            var journalHint = UICanvasUtil.NewBody("JournalHint", _card,
                Localization.Get("journal.story.toast_hint"), 18f,
                new Color(HollowfenPalette.Cream.r, HollowfenPalette.Cream.g, HollowfenPalette.Cream.b, 0.62f),
                FontStyles.Italic, TextAlignmentOptions.BottomLeft);
            var jRT = journalHint.rectTransform;
            jRT.anchorMin = new Vector2(0f, 0f); jRT.anchorMax = new Vector2(0f, 0f);
            jRT.pivot = new Vector2(0f, 0f);
            jRT.sizeDelta = new Vector2(textWidth, 20f);
            jRT.anchoredPosition = new Vector2(textLeft, 18f);
        }

    }
}
