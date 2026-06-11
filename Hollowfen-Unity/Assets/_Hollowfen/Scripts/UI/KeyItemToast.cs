using System.Collections;
using System.Collections.Generic;
using Hollowfen.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Compact parchment toast for key-item grants ("Received: Mill Key"). Same slide-in
    // grammar as StoryCardToast but smaller and photo-less; sits below the story toast's
    // band so simultaneous unlocks (key dialogue grants card + key) don't overlap.
    public class KeyItemToast : MonoBehaviour
    {
        [SerializeField] private Sprite _parchmentSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _cardSize = new Vector2(460f, 110f);
        [SerializeField] private float _restingMargin = 28f;
        [SerializeField, Tooltip("Below StoryCardToast's band (-340 minus its 220 height minus margin).")]
        private float _anchoredY = -590f;
        [SerializeField] private float _slideInSeconds = 0.40f;
        [SerializeField] private float _holdSeconds    = 2.60f;
        [SerializeField] private float _slideOutSeconds= 0.40f;

        private readonly Queue<string> _queue = new Queue<string>();
        private Coroutine _playing;
        private RectTransform _card;
        private TMP_Text _eyebrow;
        private TMP_Text _title;
        private bool _built;

        private void OnEnable()
        {
            KeyItems.OnGranted += HandleGranted;
        }

        private void OnDisable()
        {
            KeyItems.OnGranted -= HandleGranted;
        }

        private void HandleGranted(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            _queue.Enqueue(itemId);
            if (_playing == null) _playing = StartCoroutine(DrainQueue());
        }

        private IEnumerator DrainQueue()
        {
            BuildIfNeeded();
            while (_queue.Count > 0)
            {
                var id = _queue.Dequeue();
                _eyebrow.text = Localization.Get("toast.received").ToUpperInvariant();
                _title.text = Localization.Get(id + ".name");
                yield return Animate();
            }
            _playing = null;
        }

        private IEnumerator Animate()
        {
            float hiddenX = _cardSize.x + 80f;
            float restX = -_restingMargin;
            float startTime = Time.unscaledTime;

            SetX(hiddenX);
            yield return null;

            float endIn = startTime + _slideInSeconds;
            while (Time.unscaledTime < endIn)
            {
                float u = Mathf.Clamp01((Time.unscaledTime - startTime) / _slideInSeconds);
                float e = 1f - Mathf.Pow(1f - u, 3f);
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
                float e = u * u * u;
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

            var cardGO = new GameObject("KeyItem_Toast", typeof(RectTransform));
            cardGO.transform.SetParent(canvasRT, false);
            _card = (RectTransform)cardGO.transform;
            _card.anchorMin = new Vector2(1f, 1f);
            _card.anchorMax = new Vector2(1f, 1f);
            _card.pivot = new Vector2(1f, 1f);
            _card.sizeDelta = _cardSize;
            _card.anchoredPosition = new Vector2(_cardSize.x + 80f, _anchoredY);

            UICanvasUtil.AddShadow(_card, 16, 22, 0.35f, -6f);
            var bg = cardGO.AddComponent<Image>();
            bg.sprite = UICanvasUtil.RoundedRect(14);
            bg.type = Image.Type.Sliced;
            bg.color = HollowfenPalette.Parchment;

            var stroke = UICanvasUtil.NewImage("Hairline", _card, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.4f), false);
            var strokeImg = stroke.GetComponent<Image>();
            strokeImg.sprite = UICanvasUtil.RoundedOutline(14, 1.6f);
            strokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            _eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", _card, "", 11f,
                HollowfenPalette.GoldGlow, TextAlignmentOptions.TopLeft);
            _eyebrow.fontStyle = FontStyles.Bold;
            var eRT = _eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 1f); eRT.anchorMax = new Vector2(1f, 1f);
            eRT.pivot = new Vector2(0.5f, 1f);
            eRT.sizeDelta = new Vector2(-56f, 14f);
            eRT.anchoredPosition = new Vector2(0f, -20f);

            _title = UICanvasUtil.NewHeading("Title", _card, "", 30f,
                HollowfenPalette.InkDeep, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            var tRT = _title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-56f, 44f);
            tRT.anchoredPosition = new Vector2(0f, -40f);
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
