using System.Collections.Generic;
using Hollowfen.GameTime;
using Hollowfen.Weather;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Compact status card beneath the minimap. Time and weather use silhouette icons while every
    /// text run owns a bounded rect with autosizing, so long forecasts cannot escape the frame.
    /// </summary>
    public class ClockHUD : MonoBehaviour
    {
        private const int IconResolution = 64;
        private static readonly Dictionary<int, Sprite> TimeIcons = new Dictionary<int, Sprite>();
        private static readonly Dictionary<WeatherKind, Sprite> WeatherIcons =
            new Dictionary<WeatherKind, Sprite>();

        private TMP_Text _dayLabel;
        private TMP_Text _timeLabel;
        private TMP_Text _weatherLabel;
        private TMP_Text _forecastArrow;
        private TMP_Text _forecastLabel;
        private Image _timeIcon;
        private Image _weatherIcon;
        private string _last;
        private bool _built;

        private void Update()
        {
            TimeManager time = TimeManager.Instance;
            if (time == null) return;
            BuildIfNeeded();

            WeatherSystem weather = WeatherSystem.Instance;
            string period = Period(time.Hour);
            WeatherKind current = weather != null ? weather.CurrentKind : WeatherKind.Clear;
            WeatherKind next = weather != null ? weather.NextKind : current;
            int nextPeriod = (Mathf.FloorToInt(Mathf.Repeat(time.Hour, 24f) /
                WeatherSystem.PeriodHours) + 1) % 6;
            string signature = time.Day + "|" + period + "|" + current + "|" + next +
                               "|" + nextPeriod;
            if (signature == _last) return;
            _last = signature;

            _dayLabel.text = string.Format(Localization.Get("hud.clock.day"), time.Day);
            _timeLabel.text = period.ToUpperInvariant();
            _weatherLabel.text = Localization.Get(WeatherSystem.NameId(current)).ToUpperInvariant();
            if (current == next)
            {
                _forecastArrow.text = "·";
                _forecastLabel.text = Localization.Get("hud.clock.forecast.holding");
            }
            else
            {
                _forecastArrow.text = "→";
                _forecastLabel.text = string.Format(
                    Localization.Get("hud.clock.forecast.change"),
                    Localization.Get(WeatherSystem.NameId(next)),
                    WeatherSystem.PeriodName(nextPeriod));
            }
            _timeIcon.sprite = TimeIcon(time.Hour);
            _weatherIcon.sprite = WeatherIcon(current);
            _weatherIcon.color = WeatherTint(current);
        }

        private static string Period(float hour)
        {
            if (hour < 5f) return Localization.Get("hud.clock.night");
            if (hour < 7f) return Localization.Get("hud.clock.dawn");
            if (hour < 11f) return Localization.Get("hud.clock.morning");
            if (hour < 14f) return Localization.Get("hud.clock.midday");
            if (hour < 17f) return Localization.Get("hud.clock.afternoon");
            if (hour < 19f) return Localization.Get("hud.clock.evening");
            if (hour < 21f) return Localization.Get("hud.clock.dusk");
            return Localization.Get("hud.clock.night");
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            RectTransform canvas = (RectTransform)transform;
            var miniMap = FindAnyObjectByType<Hollowfen.Map.MiniMapWidget>(FindObjectsInactive.Include);
            RectTransform miniMapRect = miniMap != null ? miniMap.transform as RectTransform : null;
            Transform parent = miniMapRect != null ? miniMapRect : canvas;
            GameObject pill = UICanvasUtil.NewImage("ClockPill", parent,
                new Color(.07f, .06f, .045f, .78f), false);
            Image background = pill.GetComponent<Image>();
            background.sprite = UICanvasUtil.RoundedRect(16);
            background.type = Image.Type.Sliced;
            RectTransform root = (RectTransform)pill.transform;
            root.sizeDelta = new Vector2(306f, 78f);
            root.pivot = new Vector2(.5f, 1f);

            if (miniMapRect != null)
            {
                root.anchorMin = new Vector2(.5f, 0f);
                root.anchorMax = new Vector2(.5f, 0f);
                root.anchoredPosition = new Vector2(0f, -36f);
            }
            else
            {
                root.anchorMin = Vector2.one;
                root.anchorMax = Vector2.one;
                root.anchoredPosition = new Vector2(-176f, -352f);
            }

            GameObject stroke = UICanvasUtil.NewImage("Hairline", root,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, .34f), false);
            Image strokeImage = stroke.GetComponent<Image>();
            strokeImage.sprite = UICanvasUtil.RoundedOutline(16, 1.5f);
            strokeImage.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)stroke.transform);

            GameObject divider = UICanvasUtil.NewImage("RowDivider", root,
                HollowfenPalette.DividerLine, false);
            Place((RectTransform)divider.transform, new Vector2(0f, 0f), new Vector2(278f, 1f));

            GameObject dayBadge = UICanvasUtil.NewImage("DayBadge", root,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, .11f), false);
            Image dayImage = dayBadge.GetComponent<Image>();
            dayImage.sprite = UICanvasUtil.RoundedRect(11);
            dayImage.type = Image.Type.Sliced;
            Place((RectTransform)dayBadge.transform, new Vector2(-112f, 19f),
                new Vector2(68f, 25f));
            _dayLabel = UICanvasUtil.NewEyebrow("Day", dayBadge.transform, "", 11f,
                HollowfenPalette.GoldGlow, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(_dayLabel.rectTransform);
            Bound(_dayLabel, 9f, 11f, 3f);

            _timeIcon = NewIcon("TimeIcon", root, HollowfenPalette.GoldGlow,
                new Vector2(-65f, 19f), 22f);
            _timeLabel = UICanvasUtil.NewHeading("TimeOfDay", root, "", 15f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Left);
            Place(_timeLabel.rectTransform, new Vector2(32f, 19f), new Vector2(164f, 27f));
            Bound(_timeLabel, 11f, 15f, 1f);

            _weatherIcon = NewIcon("WeatherIcon", root, HollowfenPalette.Parchment,
                new Vector2(-132f, -19f), 23f);
            _weatherLabel = UICanvasUtil.NewEyebrow("Weather", root, "", 11.5f,
                HollowfenPalette.Cream, TextAlignmentOptions.Left);
            Place(_weatherLabel.rectTransform, new Vector2(-72f, -19f),
                new Vector2(96f, 25f));
            Bound(_weatherLabel, 10f, 11.5f, 2f);

            _forecastArrow = UICanvasUtil.NewBody("ForecastArrow", root, "", 12f,
                HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.Center);
            Place(_forecastArrow.rectTransform, new Vector2(-15f, -19f), new Vector2(18f, 24f));
            Bound(_forecastArrow, 10f, 12f, 0f);

            _forecastLabel = UICanvasUtil.NewBody("Forecast", root, "", 11.5f,
                HollowfenPalette.Parchment, FontStyles.Italic, TextAlignmentOptions.Left);
            Place(_forecastLabel.rectTransform, new Vector2(65f, -19f),
                new Vector2(150f, 25f));
            Bound(_forecastLabel, 9.5f, 11.5f, 0f);
        }

        private static Image NewIcon(string name, Transform parent, Color color,
            Vector2 position, float size)
        {
            GameObject icon = UICanvasUtil.NewImage(name, parent, color, false);
            Image image = icon.GetComponent<Image>();
            image.preserveAspect = true;
            Place((RectTransform)icon.transform, position, new Vector2(size, size));
            return image;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Bound(TMP_Text text, float minimum, float maximum, float spacing)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minimum;
            text.fontSizeMax = maximum;
            text.characterSpacing = spacing;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(2f, 0f, 2f, 0f);
        }

        private static Color WeatherTint(WeatherKind kind)
        {
            switch (kind)
            {
                case WeatherKind.Clear: return HollowfenPalette.GoldGlow;
                case WeatherKind.Storm: return new Color(.68f, .76f, .86f, 1f);
                case WeatherKind.Rain:
                case WeatherKind.Drizzle: return new Color(.72f, .82f, .87f, 1f);
                case WeatherKind.MorningMist: return new Color(.82f, .84f, .80f, 1f);
                default: return HollowfenPalette.Parchment;
            }
        }

        private static Sprite TimeIcon(float hour)
        {
            int key = hour < 5f || hour >= 21f ? 0 : hour < 7f ? 1 :
                hour < 17f ? 2 : hour < 21f ? 3 : 0;
            if (TimeIcons.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            Color32[] pixels = Blank();
            if (key == 0) DrawCrescent(pixels);
            else if (key == 1 || key == 3) DrawHorizonSun(pixels);
            else DrawSun(pixels, 32f, 32f, 11f);
            sprite = MakeSprite("ClockTimeIcon_" + key, pixels);
            TimeIcons[key] = sprite;
            return sprite;
        }

        private static Sprite WeatherIcon(WeatherKind kind)
        {
            if (WeatherIcons.TryGetValue(kind, out Sprite sprite) && sprite != null) return sprite;
            Color32[] pixels = Blank();
            switch (kind)
            {
                case WeatherKind.Clear:
                    DrawSun(pixels, 32f, 32f, 11f);
                    break;
                case WeatherKind.MorningMist:
                    DrawCloud(pixels, 37f);
                    DrawLine(pixels, new Vector2(12f, 21f), new Vector2(51f, 21f), 2f, true);
                    DrawLine(pixels, new Vector2(18f, 14f), new Vector2(46f, 14f), 2f, true);
                    break;
                case WeatherKind.Drizzle:
                    DrawCloud(pixels, 39f);
                    DrawLine(pixels, new Vector2(22f, 25f), new Vector2(19f, 18f), 1.7f, true);
                    DrawLine(pixels, new Vector2(34f, 25f), new Vector2(31f, 18f), 1.7f, true);
                    DrawLine(pixels, new Vector2(46f, 25f), new Vector2(43f, 18f), 1.7f, true);
                    break;
                case WeatherKind.Rain:
                    DrawCloud(pixels, 41f);
                    DrawLine(pixels, new Vector2(20f, 26f), new Vector2(15f, 13f), 2.3f, true);
                    DrawLine(pixels, new Vector2(34f, 26f), new Vector2(29f, 13f), 2.3f, true);
                    DrawLine(pixels, new Vector2(48f, 26f), new Vector2(43f, 13f), 2.3f, true);
                    break;
                case WeatherKind.Storm:
                    DrawCloud(pixels, 42f);
                    FillTriangle(pixels, new Vector2(34f, 29f), new Vector2(25f, 13f),
                        new Vector2(34f, 16f));
                    FillTriangle(pixels, new Vector2(31f, 18f), new Vector2(41f, 20f),
                        new Vector2(28f, 7f));
                    break;
                default:
                    DrawCloud(pixels, 34f);
                    break;
            }
            sprite = MakeSprite("ClockWeatherIcon_" + kind, pixels);
            WeatherIcons[kind] = sprite;
            return sprite;
        }

        private static Color32[] Blank() => new Color32[IconResolution * IconResolution];

        private static Sprite MakeSprite(string name, Color32[] pixels)
        {
            var texture = new Texture2D(IconResolution, IconResolution, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, IconResolution, IconResolution), new Vector2(.5f, .5f),
                IconResolution);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void DrawSun(Color32[] pixels, float x, float y, float radius)
        {
            DrawDisc(pixels, x, y, radius);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * .25f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(pixels, new Vector2(x, y) + direction * (radius + 4f),
                    new Vector2(x, y) + direction * (radius + 10f), 1.8f, true);
            }
        }

        private static void DrawCrescent(Color32[] pixels)
        {
            DrawDisc(pixels, 30f, 33f, 17f);
            ClearDisc(pixels, 38f, 39f, 15f);
        }

        private static void DrawHorizonSun(Color32[] pixels)
        {
            DrawDisc(pixels, 32f, 27f, 11f);
            ClearRect(pixels, 0, 0, 63, 26);
            DrawLine(pixels, new Vector2(10f, 26f), new Vector2(54f, 26f), 2.2f, true);
            DrawLine(pixels, new Vector2(17f, 19f), new Vector2(47f, 19f), 1.7f, true);
            for (int i = -2; i <= 2; i++)
            {
                float angle = Mathf.Lerp(.2f, Mathf.PI - .2f, (i + 2) / 4f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(pixels, new Vector2(32f, 27f) + direction * 15f,
                    new Vector2(32f, 27f) + direction * 21f, 1.7f, true);
            }
        }

        private static void DrawCloud(Color32[] pixels, float y)
        {
            DrawDisc(pixels, 23f, y, 10f);
            DrawDisc(pixels, 34f, y + 7f, 14f);
            DrawDisc(pixels, 47f, y, 10f);
            FillRect(pixels, 15, Mathf.RoundToInt(y - 9f), 55, Mathf.RoundToInt(y + 5f));
        }

        private static void DrawDisc(Color32[] pixels, float cx, float cy, float radius)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius - 1f));
            int maxX = Mathf.Min(IconResolution - 1, Mathf.CeilToInt(cx + radius + 1f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius - 1f));
            int maxY = Mathf.Min(IconResolution - 1, Mathf.CeilToInt(cy + radius + 1f));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float alpha = Mathf.Clamp01(radius + .65f -
                        Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy)));
                    SetAlpha(pixels, x, y, alpha);
                }
        }

        private static void ClearDisc(Color32[] pixels, float cx, float cy, float radius)
        {
            for (int y = 0; y < IconResolution; y++)
                for (int x = 0; x < IconResolution; x++)
                    if (Vector2.Distance(new Vector2(x + .5f, y + .5f),
                            new Vector2(cx, cy)) <= radius)
                        pixels[y * IconResolution + x] = new Color32(255, 255, 255, 0);
        }

        private static void DrawLine(Color32[] pixels, Vector2 a, Vector2 b,
            float radius, bool rounded)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            for (int y = 0; y < IconResolution; y++)
                for (int x = 0; x < IconResolution; x++)
                {
                    Vector2 point = new Vector2(x + .5f, y + .5f);
                    float t = lengthSquared > .001f
                        ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared) : 0f;
                    float distance = Vector2.Distance(point, a + segment * t);
                    if (!rounded && (t <= 0f || t >= 1f)) continue;
                    SetAlpha(pixels, x, y, Mathf.Clamp01(radius + .65f - distance));
                }
        }

        private static void FillTriangle(Color32[] pixels, Vector2 a, Vector2 b, Vector2 c)
        {
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < .001f) return;
            for (int y = 0; y < IconResolution; y++)
                for (int x = 0; x < IconResolution; x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    float u = Cross(b - p, c - p) / area;
                    float v = Cross(c - p, a - p) / area;
                    float w = Cross(a - p, b - p) / area;
                    if (u >= 0f && v >= 0f && w >= 0f) SetAlpha(pixels, x, y, 1f);
                }
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static void FillRect(Color32[] pixels, int minX, int minY, int maxX, int maxY)
        {
            for (int y = Mathf.Max(0, minY); y <= Mathf.Min(IconResolution - 1, maxY); y++)
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(IconResolution - 1, maxX); x++)
                    SetAlpha(pixels, x, y, 1f);
        }

        private static void ClearRect(Color32[] pixels, int minX, int minY, int maxX, int maxY)
        {
            for (int y = Mathf.Max(0, minY); y <= Mathf.Min(IconResolution - 1, maxY); y++)
                for (int x = Mathf.Max(0, minX); x <= Mathf.Min(IconResolution - 1, maxX); x++)
                    pixels[y * IconResolution + x] = new Color32(255, 255, 255, 0);
        }

        private static void SetAlpha(Color32[] pixels, int x, int y, float alpha)
        {
            int index = y * IconResolution + x;
            byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            if (value <= pixels[index].a) return;
            pixels[index] = new Color32(255, 255, 255, value);
        }
    }
}
