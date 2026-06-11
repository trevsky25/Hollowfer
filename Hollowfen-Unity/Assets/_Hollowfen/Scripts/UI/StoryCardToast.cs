using System.Collections;
using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Parchment toast that slides in from the right edge of the screen when a StoryCard unlocks.
    // Listens to QuestManager.StoryCardUnlocked; looks up the SO via the assigned database; queues
    // overlapping unlocks so they play sequentially rather than stomping each other.
    public class StoryCardToast : MonoBehaviour
    {
        [SerializeField] private StoryCardDatabase _database;
        [SerializeField] private Sprite _parchmentSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(560f, 220f);
        [SerializeField] private float _restingMargin = 28f;   // offset from screen right when shown
        [SerializeField, Tooltip("Y position of the card (negative = below the top of the canvas). Set to -340 to clear the 288px mini-map plus margin.")]
        private float _anchoredY = -340f;
        [SerializeField] private float _slideInSeconds = 0.40f;
        [SerializeField] private float _holdSeconds    = 3.20f;
        [SerializeField] private float _slideOutSeconds= 0.40f;

        private readonly Queue<StoryCardData> _queue = new Queue<StoryCardData>();
        private Coroutine _playing;
        private RectTransform _card;
        private Image _photoImg;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private TMP_Text _newEntryTag;
        private bool _built;

        private void OnEnable()
        {
            QuestManager.StoryCardUnlocked += HandleUnlocked;
        }
        private void OnDisable()
        {
            QuestManager.StoryCardUnlocked -= HandleUnlocked;
        }

        private void HandleUnlocked(string cardId)
        {
            var card = FindCard(cardId);
            if (card == null) return;
            _queue.Enqueue(card);
            if (_playing == null) _playing = StartCoroutine(DrainQueue());
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
            while (_queue.Count > 0)
            {
                var data = _queue.Dequeue();
                PopulateCard(data);
                yield return Animate();
            }
            _playing = null;
        }

        private void PopulateCard(StoryCardData data)
        {
            if (_photoImg != null) {
                _photoImg.sprite = data.Image;
                _photoImg.enabled = data.Image != null;
            }
            // Eyebrow: "ACT I  ·  SCENE 1" — fall back to data's raw strings
            string actLine = (string.IsNullOrEmpty(data.Act) ? "" : data.Act);
            string sceneLine = (string.IsNullOrEmpty(data.Scene) ? "" : data.Scene);
            string brow = actLine;
            if (!string.IsNullOrEmpty(sceneLine)) brow = string.IsNullOrEmpty(brow) ? sceneLine : brow + "  ·  " + sceneLine;
            _eyebrow.text = brow.ToUpperInvariant();
            _title.text = data.Title ?? "";
            _subtitle.text = data.Subtitle ?? "";
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

            // Rounded parchment background with soft shadow
            UICanvasUtil.AddShadow(_card, 18, 24, 0.38f, -7f);
            var bg = cardGO.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(16);
            bg.type = Image.Type.Sliced;
            bg.color = HollowfenPalette.Parchment;

            // Subtle vignette overlay on the parchment (sells the texture)
            var vign = UICanvasUtil.NewImage("Vignette", _card, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)vign.transform);
            vign.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.18f)),
                new UICanvasUtil.GradientStop(0.25f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.75f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.20f)),
            }, 128);

            // Hairline stroke
            var stroke = UICanvasUtil.NewImage("Hairline", _card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.4f), false);
            var strokeImg = stroke.GetComponent<Image>();
            strokeImg.sprite = UICanvasUtil.RoundedOutline(16, 1.6f);
            strokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            // "NEW ENTRY" eyebrow strip top-right
            _newEntryTag = UICanvasUtil.NewEyebrow("NewEntry", _card, "NEW ENTRY", 10f,
                HollowfenPalette.GoldGlow, TextAlignmentOptions.Right);
            _newEntryTag.fontStyle = FontStyles.Bold;
            var neRT = _newEntryTag.rectTransform;
            neRT.anchorMin = new Vector2(0f, 1f); neRT.anchorMax = new Vector2(1f, 1f);
            neRT.pivot = new Vector2(0.5f, 1f);
            neRT.sizeDelta = new Vector2(-30f, 14f);
            neRT.anchoredPosition = new Vector2(0f, -16f);

            // Photo on the LEFT — 180×180 square inset
            float padX = 18f, padY = 38f;
            var photoGO = new GameObject("Photo", typeof(RectTransform));
            photoGO.transform.SetParent(_card, false);
            _photoImg = photoGO.AddComponent<Image>();
            _photoImg.preserveAspect = true;
            var phRT = (RectTransform)photoGO.transform;
            phRT.anchorMin = new Vector2(0f, 0f); phRT.anchorMax = new Vector2(0f, 1f);
            phRT.pivot = new Vector2(0f, 0.5f);
            phRT.sizeDelta = new Vector2(_cardSize.y - padY - 16f, -padY - 16f);
            phRT.anchoredPosition = new Vector2(padX, -((padY - 16f) * 0.5f));

            // Thin gold inset around photo
            var photoFrame = UICanvasUtil.NewImage("PhotoFrame", phRT, HollowfenPalette.GoldFaint, false);
            var pfRT = (RectTransform)photoFrame.transform;
            pfRT.anchorMin = Vector2.zero; pfRT.anchorMax = Vector2.one;
            pfRT.offsetMin = new Vector2(-2f, -2f); pfRT.offsetMax = new Vector2(2f, 2f);

            // Right side text column
            float textLeft = padX + (_cardSize.y - padY - 16f) + 22f;
            float textWidth = _cardSize.x - textLeft - padX;

            _eyebrow = UICanvasUtil.NewEyebrow("CardEyebrow", _card, "", 11f,
                HollowfenPalette.Gold, TextAlignmentOptions.TopLeft);
            _eyebrow.fontStyle = FontStyles.Bold;
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(0f, 1f);
            eRT.pivot = new Vector2(0f, 1f);
            eRT.sizeDelta = new Vector2(textWidth, 14f);
            eRT.anchoredPosition = new Vector2(textLeft, -44f);

            _title = UICanvasUtil.NewHeading("CardTitle", _card, "", 28f,
                HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(0f, 1f);
            tRT.pivot = new Vector2(0f, 1f);
            tRT.sizeDelta = new Vector2(textWidth, 80f);
            tRT.anchoredPosition = new Vector2(textLeft, -60f);

            _subtitle = UICanvasUtil.NewBody("CardSubtitle", _card, "", 14f,
                HollowfenPalette.Moss, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            _subtitle.textWrappingMode = TextWrappingModes.Normal;
            var sRT = _subtitle.rectTransform;
            sRT.anchorMin = new Vector2(0f, 1f); sRT.anchorMax = new Vector2(0f, 1f);
            sRT.pivot = new Vector2(0f, 1f);
            sRT.sizeDelta = new Vector2(textWidth, 80f);
            sRT.anchoredPosition = new Vector2(textLeft, -130f);
        }

        private static void BuildFrame(RectTransform panelRT, float inset, Color color, float thickness)
        {
            var t = UICanvasUtil.NewImage("Frame.Top", panelRT, color, false);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-inset * 2f, thickness); tr.anchoredPosition = new Vector2(0f, -inset);
            var b = UICanvasUtil.NewImage("Frame.Bot", panelRT, color, false);
            var br = (RectTransform)b.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(-inset * 2f, thickness); br.anchoredPosition = new Vector2(0f, inset);
            var l = UICanvasUtil.NewImage("Frame.Left", panelRT, color, false);
            var lr = (RectTransform)l.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f); lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(thickness, -inset * 2f); lr.anchoredPosition = new Vector2(inset, 0f);
            var r = UICanvasUtil.NewImage("Frame.Right", panelRT, color, false);
            var rr = (RectTransform)r.transform;
            rr.anchorMin = new Vector2(1f, 0f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(1f, 0.5f);
            rr.sizeDelta = new Vector2(thickness, -inset * 2f); rr.anchoredPosition = new Vector2(-inset, 0f);
        }
    }
}
