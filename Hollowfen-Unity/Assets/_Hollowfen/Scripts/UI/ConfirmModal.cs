using System;
using Hollowfen.Cinematics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Reusable confirm/cancel dialog. One instance per UIManager (registered as
    // a UIScreen with IsModal=true so the underlying screen stays visible).
    //
    // Call site:
    //   ConfirmModal.Show("Delete Save?", "This cannot be undone.",
    //       onConfirm: () => SaveManager.DeleteSlot(slot));
    //
    // Batch-44: rebuilt programmatically in the journal-paper register (the batch-28
    // SettingsScreen template for legacy menu chrome) — the legacy scene-authored gray
    // panel is wiped at initialize and replaced with the parchment card: ink scrim,
    // clean rounded paper surface, inset gold hairline frame, IM Fell serif title
    // over a ledger double-rule, and Cancel (ink ghost) / Confirm (gold accent) buttons
    // with FocusHighlight. API unchanged.
    public class ConfirmModal : UIScreen
    {
        public static ConfirmModal Instance { get; private set; }

        // Paper-surface palette (parchment card, walnut ink — the pause/journal paper family).
        private static readonly Color Paper    = new Color(0.902f, 0.867f, 0.788f, 0.985f);
        private static readonly Color Ink      = new Color(0.160f, 0.130f, 0.095f, 1f);
        private static readonly Color InkSoft  = new Color(0.160f, 0.130f, 0.095f, 0.82f);
        private static readonly Color Bronze   = new Color(0.520f, 0.385f, 0.110f, 1f);
        private static readonly Color MossDark = new Color(0.365f, 0.365f, 0.290f, 1f);

        private const float CardW = 640f;
        private const float CardH = 330f;

        private TMP_Text _titleText;
        private TMP_Text _messageText;
        private Button _confirmButton;
        private Button _cancelButton;
        private GameObject _defaultFocus;

        private Action _onConfirm;
        private Action _onCancel;
        private NarrativePresentationSession.Lease _presentationLease;

        public override GameObject DefaultSelected => _defaultFocus;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildCard();

            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(HandleCancel);

            if (_cancelButton != null && _confirmButton != null)
            {
                _cancelButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnRight = _confirmButton
                };
                _confirmButton.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnLeft = _cancelButton
                };
            }
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (Instance == this) Instance = null;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_presentationLease == null)
                _presentationLease = NarrativePresentationSession.AcquireIfGameplay(
                    this, NarrativePresentationSession.Modal);
        }

        public override void OnClose()
        {
            ReleasePresentation();
            base.OnClose();
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        public static bool Show(string title, string message, Action onConfirm, Action onCancel = null)
        {
            if (Instance == null)
            {
                Debug.LogError("[ConfirmModal] No instance registered with UIManager.");
                return false;
            }
            if (UIManager.Instance == null)
            {
                Debug.LogError("[ConfirmModal] UIManager.Instance missing.");
                return false;
            }
            Instance.Configure(title, message, onConfirm, onCancel);
            UIManager.Instance.OpenScreen(Instance.ScreenId);
            return true;
        }

        public void Configure(string title, string message, Action onConfirm, Action onCancel)
        {
            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
        }

        public override void OnBack() => HandleCancel();

        private void HandleConfirm()
        {
            UISfx.Confirm();   // decisive affirmative cue — distinct from a plain Back on cancel
            var cb = _onConfirm;
            _onConfirm = null;
            _onCancel = null;
            cb?.Invoke();
            // Only pop the modal if it's still on top — the callback may have already
            // navigated away (e.g., via UIManager.LoadSceneAndOpen).
            if (UIManager.Instance != null && UIManager.Instance.TopScreen == this)
            {
                UIManager.Instance.SuppressNextTransitionSfx();   // don't double Confirm with the pop's Back
                UIManager.Instance.Back();
            }
        }

        private void HandleCancel()
        {
            var cb = _onCancel;
            _onConfirm = null;
            _onCancel = null;
            cb?.Invoke();
            if (UIManager.Instance != null && UIManager.Instance.TopScreen == this)
                UIManager.Instance.Back();
        }

        // ------------------------------------------------------------------- build

        private void BuildCard()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            // Wipe the legacy scene-authored panel — this card fully replaces it.
            var root = canvas.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);

            // Ink scrim — dims the world/menu behind, eats stray clicks.
            var scrim = UICanvasUtil.NewImage("Scrim", root, new Color(0.030f, 0.027f, 0.024f, 0.62f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // The parchment card, centered.
            var card = UICanvasUtil.NewRect("Card", root);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(CardW, CardH);
            card.anchoredPosition = Vector2.zero;
            // Keep modal cards shadow-free and let the rounded fill own the entire silhouette.
            // A generated sibling shadow exposed a rectangular block against the pause card, while
            // the full-card grain/sheen images painted back into the fill's transparent corners.
            // The scrim, paper contrast, and inset rule already provide sufficient separation.
            UICanvasUtil.MakeRoundedPanel(card, Paper, 24, 0.55f);

            // Inset gold frame — a hairline sitting just inside the page edge.
            var frame = UICanvasUtil.NewImage("InnerFrame", card, new Color(Bronze.r, Bronze.g, Bronze.b, 0.4f), false);
            var frameImg = frame.GetComponent<Image>();
            frameImg.sprite = UICanvasUtil.RoundedOutline(16, 1.4f);
            frameImg.type = Image.Type.Sliced;
            var frameRt = (RectTransform)frame.transform;
            frameRt.anchorMin = Vector2.zero; frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = new Vector2(14f, 14f); frameRt.offsetMax = new Vector2(-14f, -14f);

            // Title — IM Fell serif, ink, centered.
            _titleText = UICanvasUtil.NewHeading("Title", card, "", 34f, Ink, FontStyles.Normal, TextAlignmentOptions.Center);
            var tRT = _titleText.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(-100f, 46f);
            tRT.anchoredPosition = new Vector2(0f, -44f);

            // Ledger double-rule: gold over ink hairline, centered.
            var ruleGold = UICanvasUtil.NewImage("RuleGold", card, new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f), false);
            var rgRT = (RectTransform)ruleGold.transform;
            rgRT.anchorMin = rgRT.anchorMax = new Vector2(0.5f, 1f);
            rgRT.pivot = new Vector2(0.5f, 1f);
            rgRT.sizeDelta = new Vector2(380f, 1.6f);
            rgRT.anchoredPosition = new Vector2(0f, -98f);
            var ruleInk = UICanvasUtil.NewImage("RuleInk", card, new Color(Ink.r, Ink.g, Ink.b, 0.22f), false);
            var riRT = (RectTransform)ruleInk.transform;
            riRT.anchorMin = riRT.anchorMax = new Vector2(0.5f, 1f);
            riRT.pivot = new Vector2(0.5f, 1f);
            riRT.sizeDelta = new Vector2(380f, 0.9f);
            riRT.anchoredPosition = new Vector2(0f, -102.5f);

            // Message — italic body ink, centered, up to ~3 lines.
            _messageText = UICanvasUtil.NewBody("Message", card, "", 20f, InkSoft, FontStyles.Italic, TextAlignmentOptions.Center);
            _messageText.textWrappingMode = TextWrappingModes.Normal;
            _messageText.lineSpacing = 8f;
            var mRT = _messageText.rectTransform;
            mRT.anchorMin = new Vector2(0f, 1f); mRT.anchorMax = new Vector2(1f, 1f);
            mRT.pivot = new Vector2(0.5f, 1f);
            mRT.sizeDelta = new Vector2(-120f, 84f);
            mRT.anchoredPosition = new Vector2(0f, -116f);

            // Buttons — Cancel (ink ghost, safe default) · Confirm (gold accent).
            _cancelButton = BuildButton(card, "CancelButton", Localization.Get("confirm.cancel"),
                new Vector2(-118f, 58f), accent: false);
            _confirmButton = BuildButton(card, "ConfirmButton", Localization.Get("confirm.confirm"),
                new Vector2(118f, 58f), accent: true);
            _defaultFocus = _cancelButton.gameObject;

            // Quiet hint under the buttons.
            var hint = UICanvasUtil.NewBody("Hint", card, Localization.Get("confirm.hint"), 18f,
                MossDark, FontStyles.Italic, TextAlignmentOptions.Center);
            var hRT = hint.rectTransform;
            hRT.anchorMin = new Vector2(0.5f, 0f); hRT.anchorMax = new Vector2(0.5f, 0f);
            hRT.pivot = new Vector2(0.5f, 0f);
            hRT.sizeDelta = new Vector2(400f, 16f);
            hRT.anchoredPosition = new Vector2(0f, 16f);
        }

        // One card button in the journal action grammar (IntroGuide's "Set out →"):
        // rounded fill + hairline + EB Garamond italic label + gold FocusHighlight glow.
        private Button BuildButton(RectTransform card, string name, string label, Vector2 anchoredPos, bool accent)
        {
            var btnRt = UICanvasUtil.NewRect(name, card);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(212f, 52f);
            btnRt.anchoredPosition = anchoredPos;

            var fill = btnRt.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(12);
            fill.type = Image.Type.Sliced;
            fill.color = accent
                ? new Color(Bronze.r, Bronze.g, Bronze.b, 0.16f)
                : new Color(Ink.r, Ink.g, Ink.b, 0.05f);
            fill.raycastTarget = true;

            var stroke = UICanvasUtil.NewImage("Stroke", btnRt, accent
                ? new Color(Bronze.r, Bronze.g, Bronze.b, 0.55f)
                : new Color(Ink.r, Ink.g, Ink.b, 0.30f), false);
            var strokeImg = stroke.GetComponent<Image>();
            strokeImg.sprite = UICanvasUtil.RoundedOutline(12, 1.3f);
            strokeImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            var labelText = UICanvasUtil.NewHeading("Label", btnRt, label, 22f, Ink, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(labelText.rectTransform);

            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = fill;

            // Focus glow — gold wash + slight scale, wired via the FocusHighlight reflection
            // pattern (its fields are inspector-serialized; IntroGuide batch-30 established this).
            var glowGo = UICanvasUtil.NewImage("FocusGlow", btnRt,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UICanvasUtil.RoundedRect(12);
            glowImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)glowGo.transform);

            var fh = btnRt.gameObject.AddComponent<FocusHighlight>();
            var fhT = typeof(FocusHighlight);
            Action<string, object> setF = (n, v) =>
            {
                var f = fhT.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", glowImg);
            setF("_baseColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f));
            setF("_focusedColor", new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.24f));
            setF("_focusedScale", 1.03f);
            setF("_swapColor", true);
            setF("_swapScale", true);

            return btn;
        }
    }
}
