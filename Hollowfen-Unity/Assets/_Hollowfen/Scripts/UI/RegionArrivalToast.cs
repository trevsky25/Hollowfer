using System.Collections;
using Hollowfen.Settings;
using Hollowfen.Map;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Quiet, non-blocking region title shown when RegionTrigger changes the effective world region.
    /// It owns an overlay canvas below maps/dialogue and never captures input or pauses gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegionArrivalToast : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _initialDelay = .75f;
        [SerializeField, Min(.05f)] private float _fadeSeconds = .45f;
        [SerializeField, Min(0f)] private float _holdSeconds = 2.8f;
        [SerializeField] private int _sortingOrder = 14;

        public string LastRegionId { get; private set; } = "";
        public string DisplayedTitle => _title != null ? _title.text : "";
        public string DisplayedSubtitle => _subtitle != null ? _subtitle.text : "";
        public int PresentationCount { get; private set; }
        public bool IsShowing => _routine != null;
        public float ShownTopInset => -ShownPosition.y;

        private RectTransform _presentation;
        private RectTransform _panel;
        private CanvasGroup _group;
        private TMP_Text _title;
        private TMP_Text _subtitle;
        private Coroutine _routine;
        private bool _built;
        private bool _receivedRegionEvent;

        // The compass pill plus its optional waypoint/distance label own the first 112 reference
        // pixels at the top of the HUD. Keep a 28-pixel breathing band below both layers so the
        // arrival title remains separate even after the CanvasScaler adapts to a short display.
        private static readonly Vector2 HiddenPosition = new Vector2(0f, -120f);
        private static readonly Vector2 ShownPosition = new Vector2(0f, -140f);

        private void OnEnable()
        {
            LocationRegistry.RegionChanged += HandleRegionChanged;
        }

        private void OnDisable()
        {
            LocationRegistry.RegionChanged -= HandleRegionChanged;
        }

        private void Start()
        {
            BuildIfNeeded();
            if (!_receivedRegionEvent && !string.IsNullOrEmpty(LocationRegistry.CurrentRegion))
                PreviewRegion(LocationRegistry.CurrentRegion, false);
        }

        private void HandleRegionChanged(string regionId)
        {
            _receivedRegionEvent = true;
            if (string.IsNullOrEmpty(regionId) || !RegionCatalog.IsKnown(regionId)) return;
            if (regionId == LastRegionId && IsShowing) return;
            PreviewRegion(regionId, PresentationCount == 0 ? false : true);
        }

        /// <summary>Presentation/debug seam used by the focused verifier and editor visual QA.</summary>
        public void PreviewRegion(string regionId, bool skipInitialDelay = true)
        {
            if (!RegionCatalog.IsKnown(regionId)) return;
            BuildIfNeeded();
            LastRegionId = regionId;
            _title.text = RegionCatalog.DisplayName(regionId);
            _subtitle.text = RegionCatalog.Subtitle(regionId);
            PresentationCount++;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Presentation(skipInitialDelay ? 0f : _initialDelay));
        }

        public void HideImmediate()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (_group != null) _group.alpha = 0f;
            if (_presentation != null) _presentation.anchoredPosition = HiddenPosition;
        }

        private IEnumerator Presentation(float delay)
        {
            _group.alpha = 0f;
            _presentation.anchoredPosition = HiddenPosition;

            float t = 0f;
            while (t < delay)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (GameSettings.ReducedMotion)
            {
                _group.alpha = 1f;
                _presentation.anchoredPosition = ShownPosition;
                yield return new WaitForSecondsRealtime(_holdSeconds);
                _group.alpha = 0f;
                _presentation.anchoredPosition = HiddenPosition;
                _routine = null;
                yield break;
            }

            t = 0f;
            while (t < _fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Smooth(_fadeSeconds <= 0f ? 1f : t / _fadeSeconds);
                _group.alpha = k;
                _presentation.anchoredPosition = Vector2.LerpUnclamped(HiddenPosition, ShownPosition, k);
                yield return null;
            }

            _group.alpha = 1f;
            _presentation.anchoredPosition = ShownPosition;
            t = 0f;
            while (t < _holdSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < _fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Smooth(_fadeSeconds <= 0f ? 1f : t / _fadeSeconds);
                _group.alpha = 1f - k;
                _presentation.anchoredPosition = Vector2.LerpUnclamped(ShownPosition, HiddenPosition, k);
                yield return null;
            }

            _group.alpha = 0f;
            _presentation.anchoredPosition = HiddenPosition;
            _routine = null;
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            var canvasObject = new GameObject("RegionArrivalCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _sortingOrder;
            canvasObject.GetComponent<CanvasScaler>().Init1080();
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;

            // Keep the panel and its generated soft shadow beneath one CanvasGroup. AddShadow
            // intentionally creates a sibling of its target; grouping only the panel used to
            // leave a large black shadow permanently visible after the arrival card faded out.
            _presentation = UICanvasUtil.NewRect("RegionTitlePresentation", canvasObject.transform);
            UICanvasUtil.SetRect(_presentation, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(.5f, 1f), new Vector2(650f, 112f), HiddenPosition);
            _group = _presentation.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            var panelObject = UICanvasUtil.NewImage("RegionTitle", _presentation,
                new Color(HollowfenPalette.SurfaceBase.r, HollowfenPalette.SurfaceBase.g,
                    HollowfenPalette.SurfaceBase.b, .94f), false);
            _panel = (RectTransform)panelObject.transform;
            UICanvasUtil.SetRect(_panel, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(650f, 112f), Vector2.zero);
            var panelImage = panelObject.GetComponent<Image>();
            panelImage.sprite = UICanvasUtil.RoundedRect(18);
            panelImage.type = Image.Type.Sliced;
            UICanvasUtil.AddShadow(_panel, 18, 20, .38f, -6f);

            var wash = UICanvasUtil.NewImage("QuietGradient", _panel, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)wash.transform);
            wash.GetComponent<Image>().sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(0f, 0f, 0f, 0f)),
                new UICanvasUtil.GradientStop(.5f, new Color(HollowfenPalette.Gold.r,
                    HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, .07f)),
                new UICanvasUtil.GradientStop(1f, new Color(0f, 0f, 0f, 0f)),
            }, 256);

            var topLine = UICanvasUtil.NewImage("GoldHairline", _panel,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, .55f), false);
            UICanvasUtil.SetRect((RectTransform)topLine.transform, new Vector2(.18f, 1f),
                new Vector2(.82f, 1f), new Vector2(.5f, 1f), new Vector2(0f, 2f), Vector2.zero);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", _panel,
                Localization.Get("region.arrival.eyebrow"), 18f, HollowfenPalette.Gold,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), new Vector2(-40f, 18f), new Vector2(0f, -15f));

            _title = UICanvasUtil.NewHeading("Title", _panel, "", 31f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), new Vector2(-40f, 40f), new Vector2(0f, -34f));

            _subtitle = UICanvasUtil.NewBody("Subtitle", _panel, "", 18f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                    HollowfenPalette.Parchment.b, .72f), FontStyles.Italic,
                TextAlignmentOptions.Center);
            _subtitle.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(_subtitle.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(.5f, 0f), new Vector2(-46f, 24f), new Vector2(0f, 13f));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
