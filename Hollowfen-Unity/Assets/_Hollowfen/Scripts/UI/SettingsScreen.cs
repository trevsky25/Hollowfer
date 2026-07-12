using System.Collections.Generic;
using Hollowfen.Input;
using Hollowfen.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Production settings screen, rebuilt to the code-built house idiom (batch-28).
    // Echoes the main menu's editorial column: sage eyebrow, Georgia serif title, gold
    // rule, text-nav tab row, content rows below — so settings reads as the same "page"
    // of the menu. Gamepad-first: styled sliders and ‹ value › cyclers (no dropdowns),
    // FocusHighlight on every selectable, LB/RB (Q/E) tab cycling preserved.
    //
    // Behavior contract preserved from the legacy screen: PlayerPrefs keys, mixer params
    // + linear→dB math, GameSettings 1–10 sensitivity mapping, Tab enum, per-tab default
    // focus, ScreenId "settings" (the in-game pause menu opens this same instance).
    public class SettingsScreen : UIScreen
    {
        public enum Tab { Audio = 0, Graphics = 1, Controls = 2, Credits = 3 }
        private const int TabCount = 4;

        // The main menu's Credits entry opens settings pre-switched to a tab (consumed in OnOpen).
        public static Tab? NextOpenTab;

        [Header("Audio")]
        [SerializeField] private AudioMixer _audioMixer;

        [Header("Backdrop")]
        [SerializeField, Tooltip("The menu hero (same art as MainMenu's BG_WrenImage). Falls back to solid ink when unset.")]
        private Sprite _backgroundSprite;

        // ---- persistence (keys unchanged — existing player prefs keep working) ----
        private const string PrefMaster = "audio.master";
        private const string PrefMusic  = "audio.music";
        private const string PrefSFX    = "audio.sfx";
        private const float DefaultVolume = 0.8f;
        private const string PrefFullscreen = "graphics.fullscreen";
        private const string PrefResolution = "graphics.resolutionIndex";
        private const string PrefQuality    = "graphics.qualityIndex";

        // ---- layout constants (1920×1080 reference, left editorial column) ----
        private const float ColX = 180f;
        private const float ColW = 760f;
        private const float HeaderEyebrowY = -140f;
        private const float TitleY = -196f;
        private const float RuleY = -262f;
        private const float TabsY = -302f;
        private const float ContentTopY = -352f;
        private const float ContentBottomY = 64f;

        private Tab _currentTab = Tab.Audio;
        private bool _built;
        private InputActions _input;
        private float _cycleCooldownUntil;

        private readonly Button[] _tabButtons = new Button[TabCount];
        private readonly TMP_Text[] _tabLabels = new TMP_Text[TabCount];
        private readonly GameObject[] _tabUnderlines = new GameObject[TabCount];
        private readonly GameObject[] _panels = new GameObject[TabCount];

        private Slider _masterSlider, _musicSlider, _sfxSlider, _sensSlider;
        private TMP_Text _masterValue, _musicValue, _sfxValue, _sensValue;

        private readonly List<Cycler> _cyclers = new List<Cycler>();
        private Cycler _fullscreenCyc, _resolutionCyc, _qualityCyc;
        private List<Vector2Int> _resolutionSizes;   // deduped w×h, ascending
        private string[] _qualityNames;

        // A ‹ value › row: Submit or stick/d-pad left-right cycles; ‹ › buttons serve the mouse.
        private class Cycler
        {
            public Button Row;
            public TMP_Text Value;
            public System.Func<int> Count;
            public System.Func<int, string> Display;
            public System.Action<int> Apply;
            public int Index;

            public void Cycle(int dir)
            {
                int n = Count != null ? Mathf.Max(1, Count()) : 1;
                Index = (Index + dir + n) % n;
                Refresh();
                if (Apply != null) Apply(Index);
            }

            public void Set(int index, bool apply)
            {
                int n = Count != null ? Mathf.Max(1, Count()) : 1;
                Index = Mathf.Clamp(index, 0, n - 1);
                Refresh();
                if (apply && Apply != null) Apply(Index);
            }

            public void Refresh()
            {
                if (Value != null && Display != null) Value.text = Display(Index);
            }
        }

        public override GameObject DefaultSelected
        {
            get
            {
                switch (_currentTab)
                {
                    case Tab.Graphics: return _fullscreenCyc != null ? _fullscreenCyc.Row.gameObject : base.DefaultSelected;
                    case Tab.Controls: return _sensSlider != null ? _sensSlider.gameObject : base.DefaultSelected;
                    case Tab.Credits:  return _tabButtons[(int)Tab.Credits] != null ? _tabButtons[(int)Tab.Credits].gameObject : base.DefaultSelected;
                    default:           return _masterSlider != null ? _masterSlider.gameObject : base.DefaultSelected;
                }
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;

            _input = new InputActions();
            _input.UI.TabLeft.performed  += OnTabLeftInput;
            _input.UI.TabRight.performed += OnTabRightInput;
            _input.UI.Navigate.performed += OnNavigateInput;

            try { EnsureCanvas(); BuildLayout(); _built = true; }
            catch (System.Exception e) { Debug.LogError("[SettingsScreen] OnInitialize failed: " + e); }

            ApplyTab();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_input != null) _input.UI.Enable();

            if (NextOpenTab.HasValue)
            {
                _currentTab = NextOpenTab.Value;
                NextOpenTab = null;
            }

            RefreshGraphicsValues();
            ApplyTab();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (_input != null) _input.UI.Disable();
            PlayerPrefs.Save();   // don't lose settings to a crash between here and app quit
        }

        protected void OnDestroy()
        {
            if (_input != null)
            {
                _input.UI.TabLeft.performed  -= OnTabLeftInput;
                _input.UI.TabRight.performed -= OnTabRightInput;
                _input.UI.Navigate.performed -= OnNavigateInput;
                _input.Dispose();
                _input = null;
            }
        }

        // ------------------------------------------------------------------ build

        private void EnsureCanvas()
        {
            if (GetComponent<Canvas>() == null)
            {
                var c = gameObject.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().Init1080();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var rt = transform as RectTransform;
            if (rt != null) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        }

        private void BuildLayout()
        {
            // Bottom layer: the menu hero — settings shares the menu's backdrop so it
            // reads as the same "page". Solid ink fallback when the sprite is unset.
            var bg = UICanvasUtil.NewImage("BG_Hero", transform, HollowfenPalette.InkDeep, false);
            if (_backgroundSprite != null)
            {
                var bgHero = bg.GetComponent<Image>();
                bgHero.sprite = _backgroundSprite;
                bgHero.color = Color.white;
            }
            UICanvasUtil.Stretch((RectTransform)bg.transform);

            // Readability over the hero: gentle full scrim + a stronger left gradient
            // under the editorial column (the hero's subject lives right of center).
            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0.03f, 0.027f, 0.024f, 0.30f), false);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            var grad = UICanvasUtil.NewImage("LeftGradient", transform, Color.white, false);
            var gimg = grad.GetComponent<Image>();
            gimg.sprite = UICanvasUtil.MakeHorizontalGradient(new[]
            {
                new UICanvasUtil.GradientStop(0f,    new Color(0.03f, 0.027f, 0.024f, 0.80f)),
                new UICanvasUtil.GradientStop(0.55f, new Color(0.03f, 0.027f, 0.024f, 0.38f)),
                new UICanvasUtil.GradientStop(1f,    new Color(0.03f, 0.027f, 0.024f, 0f)),
            });
            UICanvasUtil.Stretch((RectTransform)grad.transform);

            // Header — the main menu's column grammar: eyebrow, serif title, gold rule.
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", transform, Localization.Get("settings.eyebrow"), 16f, HollowfenPalette.Sage);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 24f), new Vector2(ColX, HeaderEyebrowY));

            var title = UICanvasUtil.NewHeading("Title", transform, Localization.Get("settings.title"), 62f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 78f), new Vector2(ColX, TitleY));

            var rule = UICanvasUtil.NewImage("GoldRule", transform, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.SetRect((RectTransform)rule.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 2f), new Vector2(ColX, RuleY));

            BuildTabRow();

            for (int i = 0; i < TabCount; i++)
            {
                // Column anchored left, stretched vertically between the tab row and the hint bar.
                var panel = UICanvasUtil.NewRect("Panel_" + (Tab)i, transform);
                panel.anchorMin = new Vector2(0f, 0f);
                panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.offsetMin = new Vector2(ColX, ContentBottomY);
                panel.offsetMax = new Vector2(ColX + ColW, ContentTopY);
                _panels[i] = panel.gameObject;
            }

            BuildAudioPanel(_panels[(int)Tab.Audio].transform);
            BuildGraphicsPanel(_panels[(int)Tab.Graphics].transform);
            BuildControlsPanel(_panels[(int)Tab.Controls].transform);
            BuildCreditsPanel(_panels[(int)Tab.Credits].transform);

            var hint = UICanvasUtil.NewBody("Hint", transform, Localization.Get("settings.hint"), 13f,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.85f),
                FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(900f, 20f), new Vector2(0f, 26f));
        }

        private void BuildTabRow()
        {
            string[] keys = { "settings.tab.audio", "settings.tab.graphics", "settings.tab.controls", "settings.tab.credits" };
            float x = ColX;
            for (int i = 0; i < TabCount; i++)
            {
                var label = UICanvasUtil.NewEyebrow("Tab_" + (Tab)i, transform, Localization.Get(keys[i]), 15f, HollowfenPalette.Moss);
                label.rectTransform.anchorMin = new Vector2(0f, 1f);
                label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.pivot = new Vector2(0f, 1f);
                label.ForceMeshUpdate();
                float w = label.preferredWidth + 4f;
                UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(w, 26f), new Vector2(x, TabsY));
                label.raycastTarget = true;

                var btn = label.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = label;
                int tab = i;
                btn.onClick.AddListener(() => SwitchTab((Tab)tab));

                // Focus visual: soft gold wash pill behind the text (ApplyTab owns text color,
                // so FocusHighlight must NOT drive the label color — the two would fight).
                var glowGo = UICanvasUtil.NewImage("FocusGlow", label.transform, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
                var glowImg = glowGo.GetComponent<Image>();
                glowImg.sprite = UICanvasUtil.RoundedRect(8);
                glowImg.type = Image.Type.Sliced;
                var glowRt = (RectTransform)glowGo.transform;
                glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
                glowRt.offsetMin = new Vector2(-8f, -5f); glowRt.offsetMax = new Vector2(8f, 5f);
                glowGo.transform.SetAsFirstSibling();
                AddFocusHighlight(label.gameObject, glowImg,
                    new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f),
                    new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.16f),
                    1.03f);

                var underline = UICanvasUtil.NewImage("Underline", label.transform, HollowfenPalette.Gold, false);
                UICanvasUtil.SetRect((RectTransform)underline.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(0f, -6f));

                _tabButtons[i] = btn;
                _tabLabels[i] = label;
                _tabUnderlines[i] = underline;
                x += w + 40f;
            }

            // Horizontal navigation across tabs (down into content is wired per-tab in ApplyTab).
            for (int i = 0; i < TabCount; i++)
            {
                var nav = _tabButtons[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft  = _tabButtons[(i + TabCount - 1) % TabCount];
                nav.selectOnRight = _tabButtons[(i + 1) % TabCount];
                _tabButtons[i].navigation = nav;
            }
        }

        // ---------------------------------------------------------------- panels

        private void BuildAudioPanel(Transform parent)
        {
            float y = 0f;
            _masterSlider = BuildSliderRow(parent, ref y, "settings.audio.master", 0f, 1f, false, out _masterValue);
            _musicSlider  = BuildSliderRow(parent, ref y, "settings.audio.music",  0f, 1f, false, out _musicValue);
            _sfxSlider    = BuildSliderRow(parent, ref y, "settings.audio.sfx",    0f, 1f, false, out _sfxValue);

            float master = PlayerPrefs.GetFloat(PrefMaster, DefaultVolume);
            float music  = PlayerPrefs.GetFloat(PrefMusic,  DefaultVolume);
            float sfx    = PlayerPrefs.GetFloat(PrefSFX,    DefaultVolume);

            _masterSlider.SetValueWithoutNotify(master);
            _musicSlider.SetValueWithoutNotify(music);
            _sfxSlider.SetValueWithoutNotify(sfx);

            _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            _sfxSlider.onValueChanged.AddListener(OnSFXChanged);

            UpdateVolumeLabel(_masterValue, master);
            UpdateVolumeLabel(_musicValue, music);
            UpdateVolumeLabel(_sfxValue, sfx);

            ApplyVolume("MasterVolume", master);
            ApplyVolume("MusicVolume",  music);
            ApplyVolume("SFXVolume",    sfx);

            WireVertical(new Selectable[] { _masterSlider, _musicSlider, _sfxSlider });
        }

        private void BuildGraphicsPanel(Transform parent)
        {
            float y = 0f;

            _fullscreenCyc = BuildCyclerRow(parent, ref y, "settings.graphics.fullscreen");
            _fullscreenCyc.Count = () => 2;
            _fullscreenCyc.Display = i => Localization.Get(i == 1 ? "settings.value.on" : "settings.value.off");
            _fullscreenCyc.Apply = i =>
            {
                PlayerPrefs.SetInt(PrefFullscreen, i);
                Screen.fullScreen = i == 1;
            };

            // Deduped resolution list (Screen.resolutions repeats each size per refresh rate).
            _resolutionSizes = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!_resolutionSizes.Contains(size)) _resolutionSizes.Add(size);
            }
            if (_resolutionSizes.Count == 0) _resolutionSizes.Add(new Vector2Int(Screen.width, Screen.height));

            _resolutionCyc = BuildCyclerRow(parent, ref y, "settings.graphics.resolution");
            _resolutionCyc.Count = () => _resolutionSizes.Count;
            _resolutionCyc.Display = i => _resolutionSizes[i].x + " × " + _resolutionSizes[i].y;
            _resolutionCyc.Apply = i =>
            {
                PlayerPrefs.SetInt(PrefResolution, i);
                var s = _resolutionSizes[i];
                Screen.SetResolution(s.x, s.y, Screen.fullScreenMode);
            };

            // Quality — hidden when the project defines fewer than two levels (a dead
            // control that cycles nothing shouldn't ship).
            _qualityNames = QualitySettings.names;
            if (_qualityNames != null && _qualityNames.Length >= 2)
            {
                _qualityCyc = BuildCyclerRow(parent, ref y, "settings.graphics.quality");
                _qualityCyc.Count = () => _qualityNames.Length;
                _qualityCyc.Display = i =>
                {
                    string key = "settings.quality." + _qualityNames[i].ToLowerInvariant().Replace(" ", "_");
                    string loc = Localization.Get(key);
                    return loc == key ? _qualityNames[i] : loc;
                };
                _qualityCyc.Apply = i =>
                {
                    PlayerPrefs.SetInt(PrefQuality, i);
                    QualitySettings.SetQualityLevel(i, true);
                };
            }

            RefreshGraphicsValues();

            var order = new List<Selectable> { _fullscreenCyc.Row, _resolutionCyc.Row };
            if (_qualityCyc != null) order.Add(_qualityCyc.Row);
            WireVertical(order.ToArray());
        }

        private void RefreshGraphicsValues()
        {
            if (_fullscreenCyc != null)
                _fullscreenCyc.Set(PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1 ? 1 : 0, false);

            if (_resolutionCyc != null && _resolutionSizes != null)
            {
                int current = _resolutionSizes.Count - 1;
                for (int i = 0; i < _resolutionSizes.Count; i++)
                    if (_resolutionSizes[i].x == Screen.width && _resolutionSizes[i].y == Screen.height) { current = i; break; }
                int saved = PlayerPrefs.GetInt(PrefResolution, current);
                if (saved < 0 || saved >= _resolutionSizes.Count) saved = current;
                // Pre-batch-28 prefs indexed the RAW resolution list (one entry per refresh
                // rate); an in-range stale index would mislabel the current resolution and
                // make the first cycle jump from the wrong base. Reality wins over the pref.
                if (_resolutionSizes[saved].x != Screen.width || _resolutionSizes[saved].y != Screen.height) saved = current;
                _resolutionCyc.Set(saved, false);
            }

            if (_qualityCyc != null && _qualityNames != null)
            {
                int saved = PlayerPrefs.GetInt(PrefQuality, QualitySettings.GetQualityLevel());
                if (saved < 0 || saved >= _qualityNames.Length) saved = QualitySettings.GetQualityLevel();
                _qualityCyc.Set(saved, false);
            }
        }

        private void BuildControlsPanel(Transform parent)
        {
            float y = 0f;
            _sensSlider = BuildSliderRow(parent, ref y, "settings.controls.sensitivity", GameSettings.MinSlider, GameSettings.MaxSlider, true, out _sensValue);
            float sliderVal = GameSettings.MultiplierToSlider(GameSettings.LookSensitivity);
            _sensSlider.SetValueWithoutNotify(Mathf.Round(sliderVal));
            UpdateSensitivityLabel(sliderVal);
            _sensSlider.onValueChanged.AddListener(OnSensitivityChanged);
            WireVertical(new Selectable[] { _sensSlider });

            // Binding reference table (copy preserved verbatim from the shipped screen).
            float colAction = 0f, colPad = 250f, colKb = 520f;
            y -= 6f;

            AddTableText(parent, colAction, y, 240f, "settings.controls.action", true);
            AddTableText(parent, colPad,    y, 260f, "settings.controls.gamepad", true);
            AddTableText(parent, colKb,     y, 240f, "settings.controls.keyboard", true);
            y -= 26f;

            var div = UICanvasUtil.NewImage("TableRule", parent, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.SetRect((RectTransform)div.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 1.5f), new Vector2(0f, y));
            y -= 12f;

            y = AddBindSection(parent, y, "settings.controls.section.ui", new[]
            {
                new[] { "settings.bind.navigate", "settings.bind.navigate.pad", "settings.bind.navigate.kb" },
                new[] { "settings.bind.submit",   "settings.bind.submit.pad",   "settings.bind.submit.kb" },
                new[] { "settings.bind.cancel",   "settings.bind.cancel.pad",   "settings.bind.cancel.kb" },
                new[] { "settings.bind.tabLeft",  "settings.bind.tabLeft.pad",  "settings.bind.tabLeft.kb" },
                new[] { "settings.bind.tabRight", "settings.bind.tabRight.pad", "settings.bind.tabRight.kb" },
                new[] { "settings.bind.delete",   "settings.bind.delete.pad",   "settings.bind.delete.kb" },
            }, colAction, colPad, colKb);

            y = AddBindSection(parent, y, "settings.controls.section.player", new[]
            {
                new[] { "settings.bind.move",     "settings.bind.move.pad",     "settings.bind.move.kb" },
                new[] { "settings.bind.look",     "settings.bind.look.pad",     "settings.bind.look.kb" },
                new[] { "settings.bind.interact", "settings.bind.interact.pad", "settings.bind.interact.kb" },
                new[] { "settings.bind.jump",     "settings.bind.jump.pad",     "settings.bind.jump.kb" },
                new[] { "settings.bind.journal",  "settings.bind.journal.pad",  "settings.bind.journal.kb" },
                new[] { "settings.bind.pause",    "settings.bind.pause.pad",    "settings.bind.pause.kb" },
            }, colAction, colPad, colKb);

            y = AddBindSection(parent, y, "settings.controls.section.dialogue", new[]
            {
                new[] { "settings.bind.advance",  "settings.bind.advance.pad",  "settings.bind.advance.kb" },
                new[] { "settings.bind.skip",     "settings.bind.skip.pad",     "settings.bind.skip.kb" },
                new[] { "settings.bind.choices",  "settings.bind.choices.pad",  "settings.bind.choices.kb" },
            }, colAction, colPad, colKb);
        }

        private float AddBindSection(Transform parent, float y, string headerKey, string[][] rows, float colAction, float colPad, float colKb)
        {
            var head = UICanvasUtil.NewHeading("Section", parent, Localization.Get(headerKey), 21f, HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(head.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 28f), new Vector2(0f, y));
            y -= 32f;

            foreach (var row in rows)
            {
                AddTableText(parent, colAction, y, 240f, row[0], false, HollowfenPalette.Parchment);
                AddTableText(parent, colPad,    y, 260f, row[1], false, HollowfenPalette.Moss);
                AddTableText(parent, colKb,     y, 240f, row[2], false, HollowfenPalette.Moss);
                y -= 25f;
            }
            y -= 8f;
            return y;
        }

        private void AddTableText(Transform parent, float x, float y, float w, string key, bool header, Color? color = null)
        {
            if (header)
            {
                var t = UICanvasUtil.NewEyebrow("H", parent, Localization.Get(key), 12f,
                    new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.9f));
                UICanvasUtil.SetRect(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(w, 18f), new Vector2(x, y));
            }
            else
            {
                var t = UICanvasUtil.NewBody("C", parent, Localization.Get(key), 15f, color ?? HollowfenPalette.Parchment, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                t.enableWordWrapping = false;
                UICanvasUtil.SetRect(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(w, 20f), new Vector2(x, y));
            }
        }

        private void BuildCreditsPanel(Transform parent)
        {
            // Shipped copy, restructured with editorial hierarchy (no new words —
            // final credits copy remains Trevor's open backlog item).
            float y = -4f;

            var eyebrow = UICanvasUtil.NewEyebrow("Heading", parent, Localization.Get("credits.heading"), 15f, HollowfenPalette.Gold);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 22f), new Vector2(0f, y));
            y -= 34f;

            var sub = UICanvasUtil.NewHeading("Sub", parent, Localization.Get("credits.sub"), 30f, HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 42f), new Vector2(0f, y));
            y -= 52f;

            var rule = UICanvasUtil.NewImage("Rule", parent, HollowfenPalette.GoldFaint, false);
            UICanvasUtil.SetRect((RectTransform)rule.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(300f, 1.5f), new Vector2(0f, y));
            y -= 26f;

            var build = UICanvasUtil.NewBody("Build", parent, Localization.Get("credits.build"), 15f,
                new Color(HollowfenPalette.Moss.r, HollowfenPalette.Moss.g, HollowfenPalette.Moss.b, 0.95f),
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(build.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 24f), new Vector2(0f, y));
            y -= 56f;

            string[] lineKeys = { "credits.copyright", "credits.photos", "credits.wren", "credits.engine" };
            foreach (var key in lineKeys)
            {
                var line = UICanvasUtil.NewBody("Line", parent, Localization.Get(key), 17f, HollowfenPalette.Parchment, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                UICanvasUtil.SetRect(line.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 26f), new Vector2(0f, y));
                y -= 40f;
            }
            y -= 30f;

            var thanks = UICanvasUtil.NewHeading("Thanks", parent, Localization.Get("credits.thanks"), 24f, HollowfenPalette.Cream, FontStyles.Italic, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(thanks.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 36f), new Vector2(0f, y));
        }

        // ---------------------------------------------------------------- widgets

        private Slider BuildSliderRow(Transform parent, ref float y, string labelKey, float min, float max, bool whole, out TMP_Text valueText)
        {
            var label = UICanvasUtil.NewEyebrow("Label", parent, Localization.Get(labelKey), 15f, HollowfenPalette.Parchment);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW - 120f, 22f), new Vector2(0f, y));

            valueText = UICanvasUtil.NewBody("Value", parent, "", 16f, HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.TopRight);
            UICanvasUtil.SetRect(valueText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(120f, 22f), new Vector2(ColW, y));

            float sy = y - 32f;
            var sliderRt = UICanvasUtil.NewRect("Slider_" + labelKey, parent);
            UICanvasUtil.SetRect(sliderRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 24f), new Vector2(0f, sy));

            // Track background.
            var bg = UICanvasUtil.NewImage("Background", sliderRt, new Color(1f, 1f, 1f, 0.10f), false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = UICanvasUtil.RoundedRect(3);
            bgImg.type = Image.Type.Sliced;
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = new Vector2(0f, 0.5f); bgRt.anchorMax = new Vector2(1f, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(0f, 6f); bgRt.anchoredPosition = Vector2.zero;

            // Gold fill.
            var fillArea = UICanvasUtil.NewRect("Fill Area", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.sizeDelta = new Vector2(-20f, 6f); fillArea.anchoredPosition = Vector2.zero;
            var fill = UICanvasUtil.NewImage("Fill", fillArea, HollowfenPalette.Gold, false);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = UICanvasUtil.RoundedRect(3);
            fillImg.type = Image.Type.Sliced;
            var fillRt = (RectTransform)fill.transform;
            fillRt.sizeDelta = new Vector2(10f, 0f);

            // Cream circular handle.
            var handleArea = UICanvasUtil.NewRect("Handle Slide Area", sliderRt);
            handleArea.anchorMin = new Vector2(0f, 0.5f); handleArea.anchorMax = new Vector2(1f, 0.5f);
            handleArea.pivot = new Vector2(0.5f, 0.5f);
            handleArea.sizeDelta = new Vector2(-20f, 0f); handleArea.anchoredPosition = Vector2.zero;
            var handle = UICanvasUtil.NewImage("Handle", handleArea, HollowfenPalette.Cream, true);
            var handleImg = handle.GetComponent<Image>();
            handleImg.sprite = UICanvasUtil.Circle(40);
            var handleRt = (RectTransform)handle.transform;
            handleRt.sizeDelta = new Vector2(20f, 20f);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.transition = Selectable.Transition.None;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = whole;

            AddFocusHighlight(sliderRt.gameObject, handleImg, HollowfenPalette.Cream, HollowfenPalette.GoldGlow, 1f, handleRt, 1.3f);

            y -= 92f;
            return slider;
        }

        private Cycler BuildCyclerRow(Transform parent, ref float y, string labelKey)
        {
            var row = UICanvasUtil.NewRect("Cycler_" + labelKey, parent);
            UICanvasUtil.SetRect(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ColW, 52f), new Vector2(0f, y));

            var fill = row.gameObject.AddComponent<Image>();
            fill.sprite = UICanvasUtil.RoundedRect(10);
            fill.type = Image.Type.Sliced;
            fill.color = new Color(0f, 0f, 0f, 0.001f);   // resting: invisible; focus paints it
            fill.raycastTarget = true;

            var btn = row.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = fill;

            var label = UICanvasUtil.NewEyebrow("Label", row, Localization.Get(labelKey), 15f, HollowfenPalette.Parchment);
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(320f, 22f), new Vector2(8f, 11f));

            // ‹ value › cluster, right-aligned.
            var valueText = UICanvasUtil.NewBody("Value", row, "", 19f, HollowfenPalette.Gold, FontStyles.Normal, TextAlignmentOptions.Center);
            valueText.enableWordWrapping = false;
            UICanvasUtil.SetRect(valueText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(230f, 26f), new Vector2(-44f, 0f));

            var cyc = new Cycler { Row = btn, Value = valueText };
            _cyclers.Add(cyc);

            BuildArrowButton(row, "‹", new Vector2(-282f, 0f), () => cyc.Cycle(-1));
            BuildArrowButton(row, "›", new Vector2(-10f, 0f), () => cyc.Cycle(1));

            // Submit also cycles forward — every input path can drive the row.
            btn.onClick.AddListener(() => cyc.Cycle(1));

            // Focus visual: gold wash over the row (PauseScreen row idiom).
            var glowGo = UICanvasUtil.NewImage("FocusGlow", row, new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f), false);
            var glowImg = glowGo.GetComponent<Image>();
            glowImg.sprite = UICanvasUtil.RoundedRect(10);
            glowImg.type = Image.Type.Sliced;
            UICanvasUtil.Stretch((RectTransform)glowGo.transform);
            AddFocusHighlight(row.gameObject, glowImg,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0f),
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.13f),
                1.01f);

            y -= 66f;
            return cyc;
        }

        private void BuildArrowButton(RectTransform row, string glyph, Vector2 anchored, System.Action onClick)
        {
            var t = UICanvasUtil.NewBody("Arrow" + glyph, row, glyph, 26f, HollowfenPalette.Moss, FontStyles.Normal, TextAlignmentOptions.Center);
            t.raycastTarget = true;
            UICanvasUtil.SetRect(t.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(34f, 34f), anchored);
            var b = t.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            b.targetGraphic = t;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.2f, 0.9f, 1f);
            colors.pressedColor = new Color(1.5f, 1.35f, 1f, 1f);
            b.colors = colors;
            // Mouse-only affordance — keep it out of the pad navigation graph.
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(() => onClick());
        }

        private void AddFocusHighlight(GameObject host, Graphic target, Color baseColor, Color focusedColor, float focusedScale, RectTransform scaleTarget = null, float scaleAmount = 0f)
        {
            var fh = host.AddComponent<FocusHighlight>();
            var fhT = typeof(FocusHighlight);
            System.Action<string, object> setF = (n, v) =>
            {
                var f = fhT.GetField(n, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (f != null) f.SetValue(fh, v);
            };
            setF("_targetGraphic", target);
            setF("_baseColor", baseColor);       // Awake already cached — reflection re-point (known gotcha)
            setF("_focusedColor", focusedColor);
            if (scaleTarget != null)
            {
                setF("_scaleTarget", scaleTarget);
                setF("_focusedScale", scaleAmount);
            }
            else
            {
                setF("_focusedScale", focusedScale);
            }
            setF("_swapColor", true);
            setF("_swapScale", true);
            setF("_underlineText", false);
        }

        private void WireVertical(Selectable[] order)
        {
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] == null) continue;
                var nav = order[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp   = i > 0 ? order[i - 1] : _tabButtons[(int)_currentTab];
                nav.selectOnDown = i < order.Length - 1 ? order[i + 1] : null;
                order[i].navigation = nav;
            }
        }

        // ------------------------------------------------------------------ tabs

        // UIManager's push path deactivates covered screens without OnClose, so these
        // handlers stay subscribed while another screen sits on top — gate on being top.
        private bool IsTopScreen => UIManager.Instance != null && UIManager.Instance.TopScreen == this;

        private void OnTabLeftInput(UnityEngine.InputSystem.InputAction.CallbackContext _)  { if (IsTopScreen) SwitchTab((Tab)(((int)_currentTab + TabCount - 1) % TabCount)); }
        private void OnTabRightInput(UnityEngine.InputSystem.InputAction.CallbackContext _) { if (IsTopScreen) SwitchTab((Tab)(((int)_currentTab + 1) % TabCount)); }

        // Stick/d-pad left-right on a focused cycler row cycles its value.
        private void OnNavigateInput(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            if (!IsTopScreen) return;
            if (Time.unscaledTime < _cycleCooldownUntil) return;
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return;
            Vector2 v = ctx.ReadValue<Vector2>();
            float x = v.x;
            // Horizontal only — a 45° diagonal must navigate, not also cycle.
            if (Mathf.Abs(x) < 0.55f || Mathf.Abs(x) <= Mathf.Abs(v.y)) return;
            foreach (var cyc in _cyclers)
            {
                if (cyc.Row != null && cyc.Row.gameObject == es.currentSelectedGameObject)
                {
                    cyc.Cycle(x > 0f ? 1 : -1);
                    _cycleCooldownUntil = Time.unscaledTime + 0.22f;
                    return;
                }
            }
        }

        private void SwitchTab(Tab tab)
        {
            _currentTab = tab;
            ApplyTab();

            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(DefaultSelected);
            }
        }

        private void ApplyTab()
        {
            if (!_built) return;
            for (int i = 0; i < TabCount; i++)
            {
                bool active = i == (int)_currentTab;
                if (_panels[i] != null) _panels[i].SetActive(active);
                if (_tabLabels[i] != null) _tabLabels[i].color = active ? HollowfenPalette.Gold : HollowfenPalette.Moss;
                if (_tabUnderlines[i] != null) _tabUnderlines[i].SetActive(active);
            }

            // Tabs drop into the active tab's first control.
            Selectable firstControl = null;
            switch (_currentTab)
            {
                case Tab.Audio:    firstControl = _masterSlider; break;
                case Tab.Graphics: firstControl = _fullscreenCyc != null ? _fullscreenCyc.Row : null; break;
                case Tab.Controls: firstControl = _sensSlider; break;
            }
            for (int i = 0; i < TabCount; i++)
            {
                if (_tabButtons[i] == null) continue;
                var nav = _tabButtons[i].navigation;
                nav.selectOnDown = firstControl;
                _tabButtons[i].navigation = nav;
            }

            // Content column's top-most controls climb back to the ACTIVE tab button.
            if (firstControl != null)
            {
                var nav = firstControl.navigation;
                nav.selectOnUp = _tabButtons[(int)_currentTab];
                firstControl.navigation = nav;
            }
        }

        // ---------------------------------------------------------------- values

        private void OnMasterChanged(float v) { PlayerPrefs.SetFloat(PrefMaster, v); ApplyVolume("MasterVolume", v); UpdateVolumeLabel(_masterValue, v); }
        private void OnMusicChanged(float v)  { PlayerPrefs.SetFloat(PrefMusic,  v); ApplyVolume("MusicVolume",  v); UpdateVolumeLabel(_musicValue, v); }
        private void OnSFXChanged(float v)    { PlayerPrefs.SetFloat(PrefSFX,    v); ApplyVolume("SFXVolume",    v); UpdateVolumeLabel(_sfxValue, v); }

        private static void UpdateVolumeLabel(TMP_Text label, float v)
        {
            if (label != null) label.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        private void OnSensitivityChanged(float sliderValue)
        {
            GameSettings.LookSensitivity = GameSettings.SliderToMultiplier(sliderValue);
            UpdateSensitivityLabel(sliderValue);
        }

        private void UpdateSensitivityLabel(float sliderValue)
        {
            if (_sensValue != null) _sensValue.text = Mathf.RoundToInt(sliderValue) + " / 10";
        }

        private void ApplyVolume(string param, float linear)
        {
            if (_audioMixer == null) return;
            float db = linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
            _audioMixer.SetFloat(param, db);
        }
    }
}
