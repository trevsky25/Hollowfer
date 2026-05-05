using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Map
{
    // Full-screen map screen. Builds its UI programmatically on first Open() to match the parchment
    // journal aesthetic used by InspectScreen / InventoryScreen — header band with serif title, gold
    // double-rule frame, vignette, and 8 cardinal compass markers in gold pills around the map area.
    //
    // Old hand-authored children are torn down on first build; the existing MapImage's RenderTexture
    // (assigned to MapCamera.targetTexture) is captured and re-applied to the new RawImage.
    public class MapScreen : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private MapCamera _mapCamera;
        [SerializeField] private GameObject _miniMapRoot;
        [SerializeField] private bool _freezeTimeWhileOpen = true;

        [Header("Map art")]
        [SerializeField, Tooltip("Optional parchment background sprite. Falls back to flat HollowfenPalette.Parchment.")]
        private Sprite _parchmentSprite;

        [Header("Layout")]
        [SerializeField] private Vector2 _panelSize = new Vector2(1620f, 920f);
        [SerializeField, Tooltip("Map zone (RawImage) size — match the MapCamera RT aspect for non-stretched rendering. 1480×740 = 2:1 to match the default 2048×1024 RT. Hugs the panel width with ~70px margin each side for compass markers.")]
        private Vector2 _mapZoneSize = new Vector2(1480f, 740f);
        [SerializeField] private float _headerHeight = 110f;
        [SerializeField] private float _hintHeight = 50f;

        private bool _isOpen;
        private bool _built;
        private float _previousTimeScale = 1f;
        private RawImage _mapImage;
        private Texture _mapTexture;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_root == null) _root = gameObject;
            SetActiveSilent(false);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            BuildIfNeeded();
            _isOpen = true;
            SetActiveSilent(true);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(false);
            if (_freezeTimeWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetActiveSilent(false);
            if (_miniMapRoot != null) _miniMapRoot.SetActive(true);
            if (_freezeTimeWhileOpen)
                Time.timeScale = _previousTimeScale;
        }

        private void SetActiveSilent(bool active)
        {
            if (_root != null && _root.activeSelf != active)
                _root.SetActive(active);
        }

        // ----------------- UI BUILDER -----------------

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            // Pull the runtime RT from MapCamera (created at the configured landscape aspect).
            // Falls back to the project RT asset if MapCamera's runtime RT isn't ready yet.
            if (_mapCamera != null)
            {
                _mapTexture = _mapCamera.RenderTexture;
                if (_mapTexture == null)
                {
                    var camComp = _mapCamera.GetComponent<Camera>();
                    if (camComp != null) _mapTexture = camComp.targetTexture;
                }
            }
            if (_mapTexture == null)
            {
                var oldRaw = _root.GetComponentInChildren<RawImage>(true);
                if (oldRaw != null) _mapTexture = oldRaw.texture;
            }

            // Tear down all children of the canvas. The MapScreen component lives on the canvas itself
            // so we don't touch _root.
            for (int i = _root.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_root.transform.GetChild(i).gameObject);
            }

            BuildUI();
        }

        private void BuildUI()
        {
            var canvasRT = (RectTransform)_root.transform;

            // 1) Full-screen scrim
            var scrim = UICanvasUtil.NewImage("Scrim", canvasRT, new Color(0f, 0f, 0f, 0.78f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            // 2) Center panel with parchment background
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasRT, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.raycastTarget = true;
            if (_parchmentSprite != null)
            {
                panelImg.sprite = _parchmentSprite;
                panelImg.color = Color.white;
                panelImg.preserveAspect = false;
                panelImg.type = Image.Type.Simple;
            }
            else
            {
                panelImg.color = HollowfenPalette.Parchment;
            }
            var panelRT = (RectTransform)panel.transform;
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = _panelSize;
            panelRT.anchoredPosition = Vector2.zero;

            // 3) Vignette overlay (top + bottom darker)
            var vignette = UICanvasUtil.NewImage("Vignette", panelRT, Color.white, false);
            UICanvasUtil.Stretch((RectTransform)vignette.transform);
            vignette.GetComponent<Image>().sprite = UICanvasUtil.MakeVerticalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0.00f, new Color(0f, 0f, 0f, 0.32f)),
                new UICanvasUtil.GradientStop(0.18f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(0.82f, new Color(0f, 0f, 0f, 0.00f)),
                new UICanvasUtil.GradientStop(1.00f, new Color(0f, 0f, 0f, 0.28f)),
            }, 256);

            // 4) Double-rule frame
            BuildFrame(panelRT, 14f, HollowfenPalette.GoldFaint, 1.5f);
            BuildFrame(panelRT, 22f, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.22f), 1f);

            // 5) Header band: eyebrow + title + gold rule, anchored to panel TOP so we never overlap the map.
            float panelHalfH = _panelSize.y * 0.5f;

            var topEyebrow = UICanvasUtil.NewEyebrow("TopEyebrow", panelRT,
                "FIELD JOURNAL  ·  CARTOGRAPHY", 13f, HollowfenPalette.Gold, TMPro.TextAlignmentOptions.Center);
            var teRT = topEyebrow.rectTransform;
            teRT.anchorMin = new Vector2(0f, 1f); teRT.anchorMax = new Vector2(1f, 1f);
            teRT.pivot = new Vector2(0.5f, 1f);
            teRT.sizeDelta = new Vector2(0f, 18f);
            teRT.anchoredPosition = new Vector2(0f, -32f);

            var title = UICanvasUtil.NewHeading("Title", panelRT, "Hollowfen", 64f,
                HollowfenPalette.InkDeep, TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Center);
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 1f); tRT.anchorMax = new Vector2(1f, 1f);
            tRT.pivot = new Vector2(0.5f, 1f);
            tRT.sizeDelta = new Vector2(0f, 70f);
            tRT.anchoredPosition = new Vector2(0f, -52f);

            var underline = UICanvasUtil.NewImage("TitleRule", panelRT, HollowfenPalette.Gold, false);
            var urRT = (RectTransform)underline.transform;
            urRT.anchorMin = new Vector2(0.5f, 1f); urRT.anchorMax = new Vector2(0.5f, 1f);
            urRT.pivot = new Vector2(0.5f, 1f);
            urRT.sizeDelta = new Vector2(160f, 2f);
            urRT.anchoredPosition = new Vector2(0f, -126f);

            // 6) Map zone — landscape RawImage centered horizontally, offset down so it clears the
            // header band above and leaves room for the hint band below.
            float mapYOffset = -((_headerHeight - _hintHeight) * 0.5f);
            var mapZone = new GameObject("MapZone", typeof(RectTransform));
            mapZone.transform.SetParent(panelRT, false);
            var mapZoneRT = (RectTransform)mapZone.transform;
            mapZoneRT.anchorMin = new Vector2(0.5f, 0.5f);
            mapZoneRT.anchorMax = new Vector2(0.5f, 0.5f);
            mapZoneRT.pivot = new Vector2(0.5f, 0.5f);
            mapZoneRT.sizeDelta = _mapZoneSize;
            mapZoneRT.anchoredPosition = new Vector2(0f, mapYOffset);

            // Subtle gold inset frame around the map (separates map from parchment)
            var mapFrame = UICanvasUtil.NewImage("MapFrame", mapZoneRT, HollowfenPalette.GoldFaint, false);
            var mfRT = (RectTransform)mapFrame.transform;
            mfRT.anchorMin = new Vector2(0f, 0f); mfRT.anchorMax = new Vector2(1f, 1f);
            mfRT.pivot = new Vector2(0.5f, 0.5f);
            mfRT.offsetMin = new Vector2(-3f, -3f);
            mfRT.offsetMax = new Vector2(3f, 3f);

            // The actual map image
            var mapGO = new GameObject("MapImage", typeof(RectTransform));
            mapGO.transform.SetParent(mapZoneRT, false);
            _mapImage = mapGO.AddComponent<RawImage>();
            _mapImage.raycastTarget = false;
            _mapImage.texture = _mapTexture;
            UICanvasUtil.Stretch((RectTransform)mapGO.transform);

            // Player heading triangle in the center of the map (rotates with Wren's facing direction)
            var arrowGO = new GameObject("HeadingArrow", typeof(RectTransform));
            arrowGO.transform.SetParent(mapZoneRT, false);
            var tri = arrowGO.AddComponent<UITriangle>();
            tri.color = HollowfenPalette.Gold;
            tri.raycastTarget = false;
            arrowGO.AddComponent<PlayerHeadingArrow>();
            var arRT = (RectTransform)arrowGO.transform;
            arRT.anchorMin = new Vector2(0.5f, 0.5f); arRT.anchorMax = new Vector2(0.5f, 0.5f);
            arRT.pivot = new Vector2(0.5f, 0.5f);
            arRT.sizeDelta = new Vector2(28f, 36f);
            arRT.anchoredPosition = Vector2.zero;

            // 7) Compass markers — 8 cardinals + intercardinals around the map perimeter, OUTSIDE
            // the map rect on the parchment so they're always legible.
            BuildCompassMarker(mapZoneRT, "N",  new Vector2(0.5f, 1f),  new Vector2(0f,  22f), true);
            BuildCompassMarker(mapZoneRT, "S",  new Vector2(0.5f, 0f),  new Vector2(0f, -22f), true);
            BuildCompassMarker(mapZoneRT, "E",  new Vector2(1f, 0.5f),  new Vector2(22f,  0f), true);
            BuildCompassMarker(mapZoneRT, "W",  new Vector2(0f, 0.5f),  new Vector2(-22f, 0f), true);
            BuildCompassMarker(mapZoneRT, "NE", new Vector2(1f, 1f),    new Vector2(22f,  22f), false);
            BuildCompassMarker(mapZoneRT, "NW", new Vector2(0f, 1f),    new Vector2(-22f, 22f), false);
            BuildCompassMarker(mapZoneRT, "SE", new Vector2(1f, 0f),    new Vector2(22f, -22f), false);
            BuildCompassMarker(mapZoneRT, "SW", new Vector2(0f, 0f),    new Vector2(-22f,-22f), false);

            // 8) Hint band at panel bottom
            var hint = UICanvasUtil.NewBody("Hint", panelRT,
                "[M] · Touchpad · Esc — close", 14f,
                HollowfenPalette.Moss, TMPro.FontStyles.Italic, TMPro.TextAlignmentOptions.Center);
            var hintRT = hint.rectTransform;
            hintRT.anchorMin = new Vector2(0f, 0f); hintRT.anchorMax = new Vector2(1f, 0f);
            hintRT.pivot = new Vector2(0.5f, 0f);
            hintRT.sizeDelta = new Vector2(0f, 22f);
            hintRT.anchoredPosition = new Vector2(0f, 38f);
        }

        private static void BuildCompassMarker(RectTransform mapZoneRT, string label, Vector2 mapAnchor, Vector2 outwardOffset, bool isCardinal)
        {
            // Pill background + uppercase gold/cream label. Anchored to a point on the map perimeter,
            // pushed outward by `outwardOffset`. Pivot mirrors the anchor so the pill sits cleanly
            // outside the map rect (e.g. anchored at top-center → pivot bottom-center → grows upward).
            var pill = UICanvasUtil.NewImage("Compass_" + label, mapZoneRT, new Color(0f, 0f, 0f, 0.55f), false);
            var pRT = (RectTransform)pill.transform;
            pRT.anchorMin = mapAnchor;
            pRT.anchorMax = mapAnchor;
            pRT.pivot = new Vector2(
                outwardOffset.x > 0.01f ? 0f : (outwardOffset.x < -0.01f ? 1f : 0.5f),
                outwardOffset.y > 0.01f ? 0f : (outwardOffset.y < -0.01f ? 1f : 0.5f));
            pRT.sizeDelta = isCardinal ? new Vector2(36f, 30f) : new Vector2(40f, 30f);
            pRT.anchoredPosition = outwardOffset;

            var txt = UICanvasUtil.NewEyebrow("Txt", pRT, label,
                isCardinal ? 16f : 13f,
                isCardinal ? HollowfenPalette.GoldGlow : HollowfenPalette.Gold,
                TMPro.TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(txt.rectTransform);
            txt.fontStyle = TMPro.FontStyles.Bold;
            txt.raycastTarget = false;
        }

        private static void BuildFrame(RectTransform panelRT, float inset, Color color, float thickness)
        {
            var top = UICanvasUtil.NewImage("Frame.Top", panelRT, color, false);
            var tr = (RectTransform)top.transform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-inset * 2f, thickness); tr.anchoredPosition = new Vector2(0f, -inset);
            var bot = UICanvasUtil.NewImage("Frame.Bot", panelRT, color, false);
            var br = (RectTransform)bot.transform;
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f); br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(-inset * 2f, thickness); br.anchoredPosition = new Vector2(0f, inset);
            var left = UICanvasUtil.NewImage("Frame.Left", panelRT, color, false);
            var lr = (RectTransform)left.transform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f); lr.pivot = new Vector2(0f, 0.5f);
            lr.sizeDelta = new Vector2(thickness, -inset * 2f); lr.anchoredPosition = new Vector2(inset, 0f);
            var right = UICanvasUtil.NewImage("Frame.Right", panelRT, color, false);
            var rr = (RectTransform)right.transform;
            rr.anchorMin = new Vector2(1f, 0f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(1f, 0.5f);
            rr.sizeDelta = new Vector2(thickness, -inset * 2f); rr.anchoredPosition = new Vector2(-inset, 0f);
        }
    }
}
