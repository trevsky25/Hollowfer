using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public enum TypeRole { Heading, Body }

    public static class UICanvasUtil
    {
        private static TMP_FontAsset _heading;
        private static TMP_FontAsset _body;

        public static void SetHeadingFont(TMP_FontAsset f) { if (f != null) _heading = f; }
        public static void SetBodyFont(TMP_FontAsset f)    { if (f != null) _body = f; }

        public static TMP_FontAsset HeadingFont
        {
            get
            {
                if (_heading != null) return _heading;
#if UNITY_EDITOR
                _heading = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/Georgia SDF.asset");
#endif
                if (_heading == null) _heading = BodyFont;
                return _heading;
            }
        }

        public static TMP_FontAsset BodyFont
        {
            get
            {
                if (_body != null) return _body;
                _body = TMP_Settings.defaultFontAsset;
                if (_body == null) _body = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                return _body;
            }
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 100f);
            return rt;
        }

        public static GameObject NewImage(string name, Transform parent, Color color, bool raycastTarget)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycastTarget;
            return rt.gameObject;
        }

        // Legacy API: returns TMP_Text but accepts the old (FontStyle, TextAnchor) shape.
        // Internal calls translate to TMP equivalents and use the body font by default.
        public static TMP_Text NewText(string name, Transform parent, string content, int fontSize, Color color, FontStyle style, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = BodyFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = TranslateFontStyle(style);
            text.alignment = TranslateAnchor(anchor);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        // Heading: Georgia SDF, optical-style sizing for serif display copy.
        public static TMP_Text NewHeading(string name, Transform parent, string content, float fontSize, Color color, TMPro.FontStyles style, TMPro.TextAlignmentOptions align)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = HeadingFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.characterSpacing = -10f;     // tighten serif display
            t.lineSpacing = -8f;           // tighter leading at large sizes
            return t;
        }

        // Eyebrow / small-caps label: bold body font, UPPERCASE, wide letter-spacing.
        public static TMP_Text NewEyebrow(string name, Transform parent, string content, float fontSize, Color color, TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.Left)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = BodyFont;
            t.text = string.IsNullOrEmpty(content) ? "" : content.ToUpperInvariant();
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.alignment = align;
            t.characterSpacing = 24f;     // ~0.32em letter-spacing feel
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // Body copy with comfortable defaults.
        public static TMP_Text NewBody(string name, Transform parent, string content, float fontSize, Color color, TMPro.FontStyles style = TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.TopLeft)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = BodyFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
            t.lineSpacing = 4f;
            t.raycastTarget = false;
            return t;
        }

        public static TMPro.FontStyles TranslateFontStyle(FontStyle s)
        {
            switch (s)
            {
                case FontStyle.Bold:          return TMPro.FontStyles.Bold;
                case FontStyle.Italic:        return TMPro.FontStyles.Italic;
                case FontStyle.BoldAndItalic: return TMPro.FontStyles.Bold | TMPro.FontStyles.Italic;
                default:                      return TMPro.FontStyles.Normal;
            }
        }

        public static TMPro.TextAlignmentOptions TranslateAnchor(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft:    return TMPro.TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:  return TMPro.TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:   return TMPro.TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:   return TMPro.TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TMPro.TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight:  return TMPro.TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:    return TMPro.TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:  return TMPro.TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:   return TMPro.TextAlignmentOptions.BottomRight;
                default:                      return TMPro.TextAlignmentOptions.TopLeft;
            }
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchored)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchored;
        }

        public static CanvasScaler Init1080(this CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return scaler;
        }

        // Build a tall vertical gradient sprite from a list of (t, color) stops.
        // Stops are sorted by t in [0..1]; t=0 is the BOTTOM of the sprite, t=1 is the TOP.
        // Stretch the resulting sprite over a RectTransform with preserveAspect=false.
        public static Sprite MakeVerticalGradient(GradientStop[] stops, int height = 256)
        {
            if (stops == null || stops.Length == 0)
                stops = new[] { new GradientStop(0f, new Color(0f, 0f, 0f, 0f)), new GradientStop(1f, new Color(0f, 0f, 0f, 1f)) };
            System.Array.Sort(stops, (a, b) => a.T.CompareTo(b.T));
            var tex = new Texture2D(2, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / Mathf.Max(1, height - 1);
                Color c = SampleStops(stops, t);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }

        public static Sprite MakeHorizontalGradient(GradientStop[] stops, int width = 256)
        {
            if (stops == null || stops.Length == 0)
                stops = new[] { new GradientStop(0f, new Color(0f, 0f, 0f, 1f)), new GradientStop(1f, new Color(0f, 0f, 0f, 0f)) };
            System.Array.Sort(stops, (a, b) => a.T.CompareTo(b.T));
            var tex = new Texture2D(width, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < width; x++)
            {
                float t = (float)x / Mathf.Max(1, width - 1);
                Color c = SampleStops(stops, t);
                tex.SetPixel(x, 0, c);
                tex.SetPixel(x, 1, c);
            }
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
        }

        public struct GradientStop
        {
            public float T;
            public Color C;
            public GradientStop(float t, Color c) { T = t; C = c; }
        }

        private static Color SampleStops(GradientStop[] stops, float t)
        {
            if (stops.Length == 1) return stops[0].C;
            if (t <= stops[0].T) return stops[0].C;
            if (t >= stops[stops.Length - 1].T) return stops[stops.Length - 1].C;
            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t >= stops[i].T && t <= stops[i + 1].T)
                {
                    float span = Mathf.Max(0.0001f, stops[i + 1].T - stops[i].T);
                    float k = (t - stops[i].T) / span;
                    return Color.Lerp(stops[i].C, stops[i + 1].C, k);
                }
            }
            return stops[stops.Length - 1].C;
        }
    }
}
