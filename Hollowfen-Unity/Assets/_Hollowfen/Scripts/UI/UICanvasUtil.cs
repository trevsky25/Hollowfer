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

        // ---------- Crisp-shape primitives (procedural, AA-edged, cached) ----------

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _shapeCache
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        // White rounded-rect 9-slice with ~1.5px anti-aliased edge. Tint via Image.color.
        // Use Image.type = Sliced so corners stay crisp at any size.
        public static Sprite RoundedRect(int radius)
        {
            string key = "rr" + radius;
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;

            int pad = 2;
            int size = radius * 2 + pad * 2 + 8; // center band for slicing
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max((pad + r) - (x + 0.5f), (x + 0.5f) - (size - pad - r)));
                float dy = Mathf.Max(0f, Mathf.Max((pad + r) - (y + 0.5f), (y + 0.5f) - (size - pad - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.75f); // ~1.5px AA falloff
                if (r <= 0f) a = (x >= pad && x < size - pad && y >= pad && y < size - pad) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            int border = radius + pad + 2;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _shapeCache[key] = sp;
            return sp;
        }

        // Rounded-rect outline ring (hairline stroke), 9-sliced, AA on both edges.
        public static Sprite RoundedOutline(int radius, float thickness)
        {
            string key = "ro" + radius + "_" + thickness.ToString("F1");
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;

            int pad = 2;
            int size = radius * 2 + pad * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max((pad + r) - (x + 0.5f), (x + 0.5f) - (size - pad - r)));
                float dy = Mathf.Max(0f, Mathf.Max((pad + r) - (y + 0.5f), (y + 0.5f) - (size - pad - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // signed distance to the rounded-rect edge: positive outside, negative inside
                float sd = d - r;
                float a = Mathf.Clamp01(0.75f - Mathf.Abs(sd + thickness * 0.5f) + thickness * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            int border = radius + pad + 2;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _shapeCache[key] = sp;
            return sp;
        }

        // Soft drop-shadow blob: rounded rect with wide gaussian-ish falloff. 9-sliced.
        public static Sprite SoftShadow(int radius, int blur)
        {
            string key = "sh" + radius + "_" + blur;
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;

            int pad = blur + 2;
            int size = (radius + pad) * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max((pad + r) - (x + 0.5f), (x + 0.5f) - (size - pad - r)));
                float dy = Mathf.Max(0f, Mathf.Max((pad + r) - (y + 0.5f), (y + 0.5f) - (size - pad - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy) - r;
                float t = Mathf.Clamp01(1f - d / blur);
                float a = t * t * (3f - 2f * t); // smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            int border = radius + pad + 2;
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _shapeCache[key] = sp;
            return sp;
        }

        // AA filled circle. Tint via Image.color.
        public static Sprite Circle(int diameter = 128)
        {
            string key = "ci" + diameter;
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = diameter * 0.5f - 1.5f;
            float c = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float a = Mathf.Clamp01(r - d + 0.75f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            _shapeCache[key] = sp;
            return sp;
        }

        // AA circle ring (for the minimap's hairline frame).
        public static Sprite Ring(int diameter, float thickness)
        {
            string key = "ri" + diameter + "_" + thickness.ToString("F1");
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var tex = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float r = diameter * 0.5f - thickness * 0.5f - 1.5f;
            float c = diameter * 0.5f;
            for (int y = 0; y < diameter; y++)
            for (int x = 0; x < diameter; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float a = Mathf.Clamp01(0.75f - Mathf.Abs(d - r) + thickness * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            _shapeCache[key] = sp;
            return sp;
        }

        // Subtle paper-grain speckle: per-pixel alpha noise, fixed seed (deterministic across
        // sessions). Stretch over a parchment fill and tint via Image.color with a dark ink —
        // the sprite's alpha carries the grain. First consumer: the IntroGuide journal page.
        public static Sprite PaperGrain(int size = 256, float strength = 0.06f)
        {
            string key = "pg" + size + "_" + strength.ToString("F2");
            if (_shapeCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var rng = new System.Random(7161221);   // fixed seed — same paper every boot
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Two samples averaged → soft clumps rather than white noise.
                float n = ((float)rng.NextDouble() + (float)rng.NextDouble()) * 0.5f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, n * strength));
            }
            tex.Apply(false, false);
            var sp = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            _shapeCache[key] = sp;
            return sp;
        }

        // ---------- High-level factories ----------

        // Soft shadow behind a RectTransform (slightly larger, offset down).
        public static Image AddShadow(RectTransform target, int cornerRadius = 18, int blur = 26, float alpha = 0.35f, float yOffset = -8f)
        {
            var go = NewImage("Shadow", target.parent, new Color(0f, 0f, 0f, alpha), false);
            var img = go.GetComponent<Image>();
            img.sprite = SoftShadow(cornerRadius, blur);
            img.type = Image.Type.Sliced;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = target.anchorMin; rt.anchorMax = target.anchorMax; rt.pivot = target.pivot;
            rt.sizeDelta = target.sizeDelta + new Vector2(blur * 1.4f, blur * 1.4f);
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0f, yOffset);
            go.transform.SetSiblingIndex(target.GetSiblingIndex());
            return img;
        }

        // Give an existing Image the rounded design-system corners (batch-47 square sweep).
        public static void Roundify(Image img, int radius)
        {
            if (img == null) return;
            img.sprite = RoundedRect(radius);
            img.type = Image.Type.Sliced;
        }

        // Give an existing Image a rounded hairline-outline sprite (batch-47 square sweep).
        public static void RoundifyOutline(Image img, int radius, float thickness)
        {
            if (img == null) return;
            img.sprite = RoundedOutline(radius, thickness);
            img.type = Image.Type.Sliced;
        }

        // Rounded parchment panel: fill + hairline gold stroke. Returns the fill image.
        public static Image MakeRoundedPanel(RectTransform rt, Color fill, int radius = 18, float strokeAlpha = 0.38f)
        {
            var img = rt.gameObject.GetComponent<Image>();
            if (img == null) img = rt.gameObject.AddComponent<Image>();
            img.sprite = RoundedRect(radius);
            img.type = Image.Type.Sliced;
            img.color = fill;

            var stroke = NewImage("Hairline", rt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, strokeAlpha), false);
            var simg = stroke.GetComponent<Image>();
            simg.sprite = RoundedOutline(radius, 2f);
            simg.type = Image.Type.Sliced;
            Stretch((RectTransform)stroke.transform);
            return img;
        }

        // Dark "ink glass" HUD pill with hairline. Returns the root rect.
        public static RectTransform MakePill(string name, Transform parent, Vector2 size, out Image fill, int radius = -1)
        {
            if (radius < 0) radius = Mathf.RoundToInt(size.y * 0.5f);
            var rt = NewRect(name, parent);
            rt.sizeDelta = size;
            fill = rt.gameObject.AddComponent<Image>();
            fill.sprite = RoundedRect(radius);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0.07f, 0.06f, 0.045f, 0.72f);
            fill.raycastTarget = false;

            var stroke = NewImage("Hairline", rt, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.30f), false);
            var simg = stroke.GetComponent<Image>();
            simg.sprite = RoundedOutline(radius, 1.6f);
            simg.type = Image.Type.Sliced;
            Stretch((RectTransform)stroke.transform);
            return rt;
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
