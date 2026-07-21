using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Foraging
{
    // Minimal cinematic HUD for the cutting interaction. It deliberately avoids a large
    // modal card: the knife, stem, and Wren remain the visual subject while the lower-third
    // teaches the two-handed gesture and reflects live stick position.
    internal sealed class ForageCuttingHUD : MonoBehaviour
    {
        private CanvasGroup _rootGroup;
        private CanvasGroup _panelGroup;
        private CanvasGroup _curtainGroup;
        private TMP_Text _instruction;
        private TMP_Text _status;
        private TMP_Text _deviceHint;
        private TMP_Text _strokeCounter;
        private Image _progressFill;
        private Image _leftWell;
        private Image _rightWell;
        private RectTransform _leftDot;
        private RectTransform _rightDot;
        private string _statusKey;
        private bool _showingGamepad;

        public float RootAlpha
        {
            get => _rootGroup != null ? _rootGroup.alpha : 0f;
            set { if (_rootGroup != null) _rootGroup.alpha = value; }
        }

        public float PanelAlpha
        {
            get => _panelGroup != null ? _panelGroup.alpha : 0f;
            set { if (_panelGroup != null) _panelGroup.alpha = value; }
        }

        public float CurtainAlpha
        {
            get => _curtainGroup != null ? _curtainGroup.alpha : 0f;
            set { if (_curtainGroup != null) _curtainGroup.alpha = value; }
        }

        public void Build(string speciesName, bool gamepad)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 78;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            _rootGroup = gameObject.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
            _rootGroup.interactable = false;

            BuildLetterbox();
            BuildTransitionCurtain();
            BuildPanel(speciesName);
            SetDeviceMode(gamepad, true);
            SetProgress(0f, 0, ForageCuttingChallenge.RequiredStrokes);
            SetStatus("forage.cut.status.brace", HollowfenPalette.Parchment);
        }

        private void BuildTransitionCurtain()
        {
            var curtain = UICanvasUtil.NewImage("MatchCutCurtain", transform,
                new Color(0.004f, 0.006f, 0.005f, 1f), false);
            var rt = (RectTransform)curtain.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _curtainGroup = curtain.AddComponent<CanvasGroup>();
            _curtainGroup.alpha = 0f;
            _curtainGroup.blocksRaycasts = false;
            _curtainGroup.interactable = false;
        }

        private void BuildLetterbox()
        {
            var top = UICanvasUtil.NewImage("LetterboxTop", transform,
                new Color(0.006f, 0.009f, 0.007f, 0.96f), false);
            UICanvasUtil.SetRect((RectTransform)top.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 64f), Vector2.zero);

            var bottom = UICanvasUtil.NewImage("LetterboxBottom", transform,
                new Color(0.006f, 0.009f, 0.007f, 0.96f), false);
            UICanvasUtil.SetRect((RectTransform)bottom.transform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 34f), Vector2.zero);
        }

        private void BuildPanel(string speciesName)
        {
            // Keep every visual belonging to the lower-third under one CanvasGroup. Previously
            // AddShadow placed the blur beside the panel, so PanelAlpha hid the card but left a
            // large black shadow floating over Wren's kneeling shot.
            var presentation = UICanvasUtil.NewRect("CuttingPanelPresentation", transform);
            UICanvasUtil.SetRect(presentation,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(1040f, 250f), new Vector2(0f, 58f));
            _panelGroup = presentation.gameObject.AddComponent<CanvasGroup>();
            _panelGroup.alpha = 0f;
            _panelGroup.blocksRaycasts = false;
            _panelGroup.interactable = false;

            var panel = UICanvasUtil.NewRect("CuttingPanel", presentation);
            panel.sizeDelta = presentation.sizeDelta;
            UICanvasUtil.AddShadow(panel, 24, 34, 0.48f, -8f);
            var fill = panel.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(22);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(HollowfenPalette.SurfaceBase.r, HollowfenPalette.SurfaceBase.g,
                HollowfenPalette.SurfaceBase.b, 0.965f);
            fill.raycastTarget = false;

            var rail = UICanvasUtil.NewImage("GoldRail", panel, HollowfenPalette.FocusRail, false);
            var railImage = rail.GetComponent<Image>();
            railImage.sprite = UICanvasUtil.RoundedRect(2);
            railImage.type = Image.Type.Sliced;
            UICanvasUtil.SetRect((RectTransform)rail.transform,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(4f, -30f), Vector2.zero);

            var title = UICanvasUtil.NewEyebrow("Title", panel,
                string.Format(Localization.Get("forage.cut.title"), speciesName), 14f,
                HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(title.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(650f, 24f), new Vector2(38f, -25f));

            _status = UICanvasUtil.NewEyebrow("Status", panel, "", 13f,
                HollowfenPalette.Parchment, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(_status.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(360f, 24f), new Vector2(-38f, -25f));

            var rule = UICanvasUtil.NewImage("Rule", panel, HollowfenPalette.DividerLine, false);
            UICanvasUtil.SetRect((RectTransform)rule.transform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-76f, 1.5f), new Vector2(0f, -61f));

            BuildStickWell(panel, "BraceStick", new Vector2(116f, -145f), out _leftWell, out _leftDot);
            BuildStickWell(panel, "SawStick", new Vector2(924f, -145f), out _rightWell, out _rightDot);

            var leftLabel = UICanvasUtil.NewEyebrow("BraceLabel", panel,
                Localization.Get("forage.cut.brace_label"), 12.5f, HollowfenPalette.Moss,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(leftLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                new Vector2(190f, 20f), new Vector2(116f, -78f));

            var rightLabel = UICanvasUtil.NewEyebrow("SawLabel", panel,
                Localization.Get("forage.cut.saw_label"), 12.5f, HollowfenPalette.Moss,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(rightLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 1f),
                new Vector2(190f, 20f), new Vector2(924f, -78f));

            _instruction = UICanvasUtil.NewBody("Instruction", panel, "", 19f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_instruction.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(620f, 34f), new Vector2(0f, -83f));

            var progressTrack = UICanvasUtil.NewImage("CutProgressTrack", panel,
                new Color(1f, 1f, 1f, 0.10f), false);
            var trackImage = progressTrack.GetComponent<Image>();
            trackImage.sprite = UICanvasUtil.RoundedRect(6);
            trackImage.type = Image.Type.Sliced;
            var trackRt = (RectTransform)progressTrack.transform;
            UICanvasUtil.SetRect(trackRt,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(570f, 12f), new Vector2(0f, -146f));

            var progressFill = UICanvasUtil.NewImage("Fill", trackRt, HollowfenPalette.FocusRail, false);
            _progressFill = progressFill.GetComponent<Image>();
            _progressFill.sprite = UICanvasUtil.RoundedRect(6);
            _progressFill.type = Image.Type.Sliced;
            var fillRt = (RectTransform)progressFill.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            for (int i = 1; i < ForageCuttingChallenge.RequiredStrokes; i++)
            {
                var notch = UICanvasUtil.NewImage("Notch" + i, trackRt,
                    new Color(HollowfenPalette.InkDeep.r, HollowfenPalette.InkDeep.g,
                        HollowfenPalette.InkDeep.b, 0.70f), false);
                float x = -285f + 570f * i / ForageCuttingChallenge.RequiredStrokes;
                UICanvasUtil.SetRect((RectTransform)notch.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(2f, 16f), new Vector2(x, 0f));
            }

            _strokeCounter = UICanvasUtil.NewEyebrow("StrokeCounter", panel, "", 12.5f,
                HollowfenPalette.Moss, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_strokeCounter.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(320f, 18f), new Vector2(0f, -169f));

            _deviceHint = UICanvasUtil.NewBody("DeviceHint", panel, "", 12.5f,
                HollowfenPalette.Moss, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_deviceHint.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(720f, 22f), new Vector2(0f, 20f));
        }

        private static void BuildStickWell(RectTransform parent, string name, Vector2 position,
            out Image well, out RectTransform dot)
        {
            var wellGo = UICanvasUtil.NewImage(name, parent,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                    HollowfenPalette.Parchment.b, 0.16f), false);
            well = wellGo.GetComponent<Image>();
            well.sprite = UICanvasUtil.Ring(88, 3f);
            UICanvasUtil.SetRect((RectTransform)wellGo.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(88f, 88f), position);

            var dotGo = UICanvasUtil.NewImage("Thumb", wellGo.transform, HollowfenPalette.Parchment, false);
            var dotImage = dotGo.GetComponent<Image>();
            dotImage.sprite = UICanvasUtil.Circle(48);
            dot = (RectTransform)dotGo.transform;
            dot.sizeDelta = new Vector2(22f, 22f);
            dot.anchoredPosition = Vector2.zero;
        }

        public void SetDeviceMode(bool gamepad, bool force = false)
        {
            if (!force && _showingGamepad == gamepad) return;
            _showingGamepad = gamepad;
            if (_deviceHint != null)
                _deviceHint.text = Localization.Get(gamepad
                    ? "forage.cut.hint.gamepad"
                    : "forage.cut.hint.keyboard");
            if (_instruction != null)
                _instruction.text = Localization.Get(gamepad
                    ? "forage.cut.instruction.gamepad"
                    : "forage.cut.instruction.keyboard");
        }

        public void UpdateInput(Vector2 brace, Vector2 saw, float braceAmount, bool braced)
        {
            if (_leftDot != null) _leftDot.anchoredPosition = Vector2.ClampMagnitude(brace, 1f) * 28f;
            if (_rightDot != null) _rightDot.anchoredPosition = Vector2.ClampMagnitude(saw, 1f) * 28f;
            if (_leftWell != null)
                _leftWell.color = braced
                    ? new Color(HollowfenPalette.FocusRail.r, HollowfenPalette.FocusRail.g,
                        HollowfenPalette.FocusRail.b, 0.92f)
                    : new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                        HollowfenPalette.Parchment.b, 0.16f + braceAmount * 0.30f);
            if (_rightWell != null)
                _rightWell.color = braced
                    ? new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                        HollowfenPalette.Gold.b, 0.48f)
                    : new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                        HollowfenPalette.Parchment.b, 0.16f);
        }

        public void SetProgress(float progress, int strokes, int required)
        {
            if (_progressFill != null)
            {
                var rt = _progressFill.rectTransform;
                rt.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
                rt.offsetMax = Vector2.zero;
            }
            if (_strokeCounter != null)
                _strokeCounter.text = string.Format(Localization.Get("forage.cut.strokes"), strokes, required);
        }

        public void SetStatus(string key, Color color)
        {
            if (_status == null || _statusKey == key) return;
            _statusKey = key;
            _status.text = Localization.Get(key);
            _status.color = color;
        }

        public void ShowSuccess(string speciesName)
        {
            SetStatus("forage.cut.status.clean", HollowfenPalette.Sage);
            if (_instruction != null)
            {
                _instruction.text = string.Format(Localization.Get("forage.cut.collected"), speciesName);
                _instruction.color = HollowfenPalette.Cream;
                _instruction.fontStyle = FontStyles.Italic;
            }
            if (_deviceHint != null) _deviceHint.text = Localization.Get("forage.cut.release");
            if (_leftDot != null) _leftDot.anchoredPosition = Vector2.zero;
            if (_rightDot != null) _rightDot.anchoredPosition = Vector2.zero;
        }
    }
}
