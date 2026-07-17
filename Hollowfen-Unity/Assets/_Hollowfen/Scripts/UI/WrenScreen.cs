using System.Collections.Generic;
using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // A shorter, profile-first dossier. The supplied study plates live in one
    // controller-driven gallery instead of a four-thousand-pixel passive scroll.
    public class WrenScreen : UIScreen
    {
        [SerializeField] private CharacterProfileData _profile;

        private static readonly Color Bg = HollowfenPalette.JournalBackdrop;
        private static readonly Color Cream = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color Body = new Color(0.961f, 0.925f, 0.855f, 0.92f);
        private static readonly Color Subtle = new Color(0.961f, 0.925f, 0.855f, 0.68f);
        private static readonly Color Faint = new Color(0.961f, 0.925f, 0.855f, 0.44f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);
        private static readonly Color GoldDim = HollowfenPalette.DividerLine;
        private static readonly Color Panel = HollowfenPalette.SurfaceBase;

        private const float ContentWidth = 1600f;
        private const float GamepadRotateSpeed = 112f;
        private const float GamepadZoomSpeed = 1.05f;

        private readonly List<Button> _plateButtons = new List<Button>();
        private readonly List<Image> _plateSelection = new List<Image>();
        private Sprite[] _plates;
        private string[] _plateCaptionIds;
        private ScrollRect _scroll;
        private RectTransform _heroDrift;
        private Image _deepScrim;
        private JournalArtPresenter _platePresenter;
        private TMP_Text _plateCaption;
        private JournalWrenModelPresenter _model;
        private TMP_Text _modelPending;
        private Button _closeButton;
        private Selectable _identity;
        private Selectable _background;
        private Selectable _perspective;
        private Selectable _kit;
        private Selectable _quote;
        private GameObject _firstSelectable;
        private bool _built;

        public override GameObject DefaultSelected => _firstSelectable != null ? _firstSelectable : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try
            {
                BuildLayout();
                _built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[WrenScreen] OnInitialize failed: " + e);
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
            if (_model != null) _model.ResetView();
            ShowPlate(0);
        }

        private void Update()
        {
            HandleModelInput();
            if (_heroDrift == null) return;
            float time = Time.unscaledTime;
            float scale = 1.035f + 0.014f * Mathf.Sin(time * 0.065f);
            float x = 9f * Mathf.Sin(time * 0.04f + 1.2f);
            float scrolled = _scroll != null && _scroll.content != null
                ? Mathf.Max(0f, _scroll.content.anchoredPosition.y)
                : 0f;
            _heroDrift.localScale = new Vector3(scale, scale, 1f);
            _heroDrift.anchoredPosition = new Vector2(x, Mathf.Min(54f, scrolled * 0.035f));
            if (_deepScrim != null)
            {
                var color = _deepScrim.color;
                color.a = Mathf.Clamp01((scrolled - 280f) / 620f) * 0.76f;
                _deepScrim.color = color;
            }
        }

        private void HandleModelInput()
        {
            if (!_built || _model == null || !_model.HasModel) return;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float dt = Time.unscaledDeltaTime;
                Vector2 orbit = pad.rightStick.ReadValue();
                if (orbit.sqrMagnitude > 0.0025f)
                {
                    _model.ApplyRotationDelta(
                        orbit.x * GamepadRotateSpeed * dt,
                        -orbit.y * GamepadRotateSpeed * dt);
                }

                float zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(zoom) > 0.05f)
                    _model.ApplyZoomDelta(-zoom * GamepadZoomSpeed * dt);

                if (pad.rightStickButton.wasPressedThisFrame)
                    _model.ResetView();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                _model.ResetView();
        }

        private void BuildLayout()
        {
            EnsureCanvas();
            var bg = UICanvasUtil.NewImage("BG", transform, Bg, false);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());
            BuildBackdrop();
            BuildScrims();

            var scrollRt = UICanvasUtil.NewRect("Scroll", transform);
            UICanvasUtil.Stretch(scrollRt);
            var scrollImage = scrollRt.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = true;
            _scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 44f;
            scrollRt.gameObject.AddComponent<ScrollFocusFollower>();
            var viewport = UICanvasUtil.NewRect("Viewport", scrollRt);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            _scroll.viewport = viewport;

            var content = UICanvasUtil.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(ContentWidth, 0f);
            content.anchoredPosition = Vector2.zero;
            _scroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 112, 110);
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _identity = BuildIdentity(content);
            BuildDossier(content, out _background, out _perspective);
            _kit = BuildKit(content);
            _quote = BuildPullquote(content);
            BuildPlateGallery(content);

            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.wren");
            _firstSelectable = _identity.gameObject;
            WireNavigation();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void BuildBackdrop()
        {
            _heroDrift = UICanvasUtil.NewRect("HeroDrift", transform);
            UICanvasUtil.Stretch(_heroDrift);
            var hero = JournalArtPresenter.Create("Hero", _heroDrift, true, Bg);
            UICanvasUtil.Stretch(hero.Frame);
            hero.SetSprite(_profile != null ? _profile.HeroPortrait : null, Bg);
            hero.SetTint(new Color(0.52f, 0.61f, 0.54f, 0.30f));
        }

        private void BuildScrims()
        {
            var leftStops = new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(Bg.r, Bg.g, Bg.b, 0.86f)),
                new UICanvasUtil.GradientStop(0.46f, new Color(Bg.r, Bg.g, Bg.b, 0.52f)),
                new UICanvasUtil.GradientStop(1f, new Color(Bg.r, Bg.g, Bg.b, 0.91f))
            };
            var left = UICanvasUtil.NewRect("LeftScrim", transform);
            UICanvasUtil.Stretch(left);
            var leftImage = left.gameObject.AddComponent<Image>();
            leftImage.sprite = UICanvasUtil.MakeHorizontalGradient(leftStops, 512);
            leftImage.raycastTarget = false;

            var bottomStops = new[]
            {
                new UICanvasUtil.GradientStop(0f, new Color(Bg.r, Bg.g, Bg.b, 0.94f)),
                new UICanvasUtil.GradientStop(0.56f, new Color(Bg.r, Bg.g, Bg.b, 0.40f)),
                new UICanvasUtil.GradientStop(1f, new Color(Bg.r, Bg.g, Bg.b, 0f))
            };
            var bottom = UICanvasUtil.NewRect("BottomScrim", transform);
            UICanvasUtil.SetRect(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 500f), Vector2.zero);
            var bottomImage = bottom.gameObject.AddComponent<Image>();
            bottomImage.sprite = UICanvasUtil.MakeVerticalGradient(bottomStops, 256);
            bottomImage.raycastTarget = false;

            var deep = UICanvasUtil.NewImage("DeepScrim", transform, new Color(Bg.r, Bg.g, Bg.b, 0f), false);
            UICanvasUtil.Stretch(deep.GetComponent<RectTransform>());
            _deepScrim = deep.GetComponent<Image>();
        }

        private Selectable BuildIdentity(Transform parent)
        {
            var root = UICanvasUtil.NewRect("Identity", parent);
            Pin(root.gameObject, ContentWidth, 760f);
            BuildModelStudy(root);

            var column = UICanvasUtil.NewRect("Column", root);
            UICanvasUtil.SetRect(column, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(770f, 712f), new Vector2(-18f, 0f));

            string role = JournalText.Character(_profile, "role", _profile != null ? _profile.Role : "");
            string home = JournalText.Character(_profile, "home", _profile != null ? _profile.Home : "");
            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", column, role + " · " + home, 13f, Gold);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(0f, -4f));
            string nameCopy = _profile != null ? Localization.Get(_profile.DisplayNameId, _profile.CharacterName) : Localization.Get("journal.wren.title");
            var name = UICanvasUtil.NewHeading("Name", column, nameCopy, 92f, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 108f), new Vector2(0f, -42f));
            var tagline = UICanvasUtil.NewBody("Tagline", column, JournalText.Character(_profile, "tagline", _profile != null ? _profile.Tagline : ""), 21f, Subtle, FontStyles.Italic);
            UICanvasUtil.SetRect(tagline.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 52f), new Vector2(0f, -154f));
            var rule = UICanvasUtil.NewImage("FocusRule", column, new Color(Gold.r, Gold.g, Gold.b, 0.55f), false);
            UICanvasUtil.SetRect(rule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, 2f), new Vector2(0f, -222f));
            string leadCopy = _profile != null
                ? Localization.Get(_profile.DescriptionId, _profile.LeadParagraph)
                : "";
            var lead = UICanvasUtil.NewBody("Lead", column, leadCopy, 19.5f, Body);
            lead.lineSpacing = 6f;
            UICanvasUtil.SetRect(lead.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 178f), new Vector2(0f, -252f));
            BuildStats(column);
            return AddWaypoint(root.gameObject, rule.GetComponent<Image>());
        }

        private void BuildModelStudy(RectTransform parent)
        {
            var leaf = UICanvasUtil.NewRect("LivingStudy", parent);
            UICanvasUtil.SetRect(leaf, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(746f, 744f), new Vector2(10f, 0f));
            var face = leaf.gameObject.AddComponent<Image>();
            face.color = new Color(0.021f, 0.035f, 0.027f, 0.82f);
            UICanvasUtil.Roundify(face, 24);
            JournalChrome.AddStructuralBorder(leaf, 24, 0.15f);

            var badge = UICanvasUtil.NewEyebrow("Badge", leaf, Localization.Get("journal.wren.model_badge"), 13f, Gold);
            UICanvasUtil.SetRect(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0.70f, 1f), new Vector2(0f, 1f), new Vector2(-48f, 24f), new Vector2(30f, -24f));
            var edition = UICanvasUtil.NewEyebrow("Edition", leaf, "01 / WREN TOBIN", 10f, Faint, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(edition.rectTransform, new Vector2(0.70f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, 24f), new Vector2(-30f, -24f));

            var stage = UICanvasUtil.NewRect("ModelStage", leaf);
            UICanvasUtil.SetRect(stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(696f, 628f), new Vector2(0f, 8f));
            var arch = stage.gameObject.AddComponent<Image>();
            arch.color = new Color(Cream.r, Cream.g, Cream.b, 0.12f);
            arch.raycastTarget = false;
            UICanvasUtil.RoundifyOutline(arch, 190, 1.15f);
            stage.gameObject.AddComponent<RectMask2D>();
            JournalChrome.AddSpecimenHalo(stage, new Vector2(610f, 420f), new Vector2(0f, -92f));

            var horizon = UICanvasUtil.NewImage("GroundLine", stage, new Color(Gold.r, Gold.g, Gold.b, 0.24f), false);
            UICanvasUtil.SetRect(horizon.GetComponent<RectTransform>(), new Vector2(0.14f, 0f), new Vector2(0.86f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(0f, 54f));
            var modelRt = UICanvasUtil.NewRect("WrenModel", stage);
            UICanvasUtil.Stretch(modelRt);
            modelRt.offsetMin = new Vector2(18f, 30f);
            modelRt.offsetMax = new Vector2(-18f, -16f);
            var raw = modelRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = true;
            _model = modelRt.gameObject.AddComponent<JournalWrenModelPresenter>();
            // Match the landscape stage so the RenderTexture is never stretched.
            _model.Configure(896, 768, 7f);
            _model.SetProfile(_profile);

            _modelPending = UICanvasUtil.NewBody("ModelPending", stage, Localization.Get("journal.wren.model_pending"), 20f, Faint, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_modelPending.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-80f, 80f), Vector2.zero);
            _modelPending.gameObject.SetActive(_profile == null || _profile.JournalModelPrefab == null);

            var caption = UICanvasUtil.NewBody("ModelCaption", leaf, Localization.Get("journal.wren.model_caption"), 14.5f, Subtle, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(caption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-48f, 30f), new Vector2(0f, 20f));
        }

        private void BuildStats(Transform parent)
        {
            var stats = UICanvasUtil.NewRect("Stats", parent);
            UICanvasUtil.SetRect(stats, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 172f), new Vector2(0f, 14f));
            var grid = stats.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(372f, 78f);
            grid.spacing = new Vector2(16f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperLeft;
            BuildStat(stats, "journal.wren.age", JournalText.Character(_profile, "age", _profile != null ? _profile.Age : ""), 0f);
            BuildStat(stats, "journal.wren.home", JournalText.Character(_profile, "home", _profile != null ? _profile.Home : ""), 0f);
            BuildStat(stats, "journal.wren.work", JournalText.Character(_profile, "work", _profile != null ? _profile.Work : ""), 0f);
            BuildStat(stats, "journal.wren.keepsake", JournalText.Character(_profile, "keepsake", _profile != null ? _profile.Keepsake : ""), 0f);
        }

        private void BuildStat(Transform parent, string labelId, string value, float width)
        {
            var cell = UICanvasUtil.NewRect("Stat", parent);
            var face = cell.gameObject.AddComponent<Image>();
            face.color = new Color(0.029f, 0.046f, 0.035f, 0.76f);
            face.raycastTarget = false;
            UICanvasUtil.Roundify(face, 10);
            var layout = cell.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 14, 11, 8);
            layout.spacing = 3f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var label = UICanvasUtil.NewEyebrow("Label", cell, Localization.Get(labelId), 13f, Faint);
            Pin(label.gameObject, 0f, 17f, true);
            var copy = UICanvasUtil.NewHeading("Value", cell, value, 21f, Gold, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            copy.enableWordWrapping = false;
            copy.enableAutoSizing = true;
            copy.fontSizeMin = 15f;
            copy.fontSizeMax = 21f;
            Pin(copy.gameObject, 0f, 30f, true);
        }

        private void BuildDivider(Transform parent)
        {
            var divider = UICanvasUtil.NewImage("Divider", parent, GoldDim, false);
            var layout = divider.AddComponent<LayoutElement>();
            layout.preferredWidth = 1f;
            layout.preferredHeight = 56f;
        }

        private void BuildDossier(Transform parent, out Selectable background, out Selectable perspective)
        {
            var row = UICanvasUtil.NewRect("Dossier", parent);
            Pin(row.gameObject, ContentWidth, 246f);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            background = BuildDossierCard(row, "journal.wren.background",
                JournalText.Character(_profile, "background", _profile != null ? _profile.BackgroundParagraph : ""));
            perspective = BuildDossierCard(row, "journal.wren.perspective",
                JournalText.Character(_profile, "perspective", _profile != null ? _profile.PerspectiveParagraph : ""));
        }

        private Selectable BuildDossierCard(Transform parent, string headingId, string copy)
        {
            var root = UICanvasUtil.NewRect("DossierCard", parent).gameObject;
            var face = root.AddComponent<Image>();
            face.color = Panel;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 16);
            var inner = UICanvasUtil.NewRect("Inner", root.transform);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(34f, 26f);
            inner.offsetMax = new Vector2(-34f, -28f);
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var heading = UICanvasUtil.NewEyebrow("Heading", inner, Localization.Get(headingId), 13f, Gold);
            Pin(heading.gameObject, 0f, 22f, true);
            var rule = UICanvasUtil.NewImage("Rule", inner, GoldDim, false);
            Pin(rule, 60f, 1f);
            var text = UICanvasUtil.NewBody("Body", inner, copy, 17.5f, Body);
            var textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleHeight = 1f;
            textLayout.minHeight = 130f;
            var selectable = root.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            JournalChrome.AddSurfaceFocus(root, 16, 1.008f);
            return selectable;
        }

        private Selectable BuildKit(Transform parent)
        {
            var section = UICanvasUtil.NewRect("Kit", parent);
            Pin(section.gameObject, ContentWidth, 174f);
            var layout = section.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var heading = UICanvasUtil.NewEyebrow("Heading", section, Localization.Get("journal.wren.carries"), 13f, Gold);
            Pin(heading.gameObject, 0f, 22f, true);
            var row = UICanvasUtil.NewRect("Items", section);
            Pin(row.gameObject, 0f, 128f, true);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 16f;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;

            if (_profile != null && _profile.KitItems != null)
            {
                for (int i = 0; i < _profile.KitItems.Length; i++)
                {
                    var item = _profile.KitItems[i];
                    BuildKitTile(row,
                        JournalText.CharacterKit(_profile, i, "name", item.Name),
                        JournalText.CharacterKit(_profile, i, "line", item.OneLine));
                }
            }
            return AddWaypoint(section.gameObject, heading);
        }

        private void BuildKitTile(Transform parent, string itemName, string copy)
        {
            var tile = UICanvasUtil.NewRect("Item", parent);
            var image = tile.gameObject.AddComponent<Image>();
            image.color = new Color(0.031f, 0.047f, 0.039f, 0.72f);
            UICanvasUtil.Roundify(image, 12);
            var layout = tile.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 20, 18, 16);
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var name = UICanvasUtil.NewEyebrow("Name", tile, itemName, 13f, Gold);
            Pin(name.gameObject, 0f, 18f, true);
            var body = UICanvasUtil.NewBody("Copy", tile, copy, 15.5f, Body);
            Pin(body.gameObject, 0f, 52f, true);
        }

        private Selectable BuildPullquote(Transform parent)
        {
            var root = UICanvasUtil.NewRect("Pullquote", parent);
            Pin(root.gameObject, 1240f, 126f);
            var face = root.gameObject.AddComponent<Image>();
            face.color = HollowfenPalette.SurfaceQuiet;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 14);
            var ruleGo = UICanvasUtil.NewImage("FocusRule", root, GoldDim, false);
            UICanvasUtil.SetRect(ruleGo.GetComponent<RectTransform>(), new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(30f, 0f));
            string quote = JournalText.Character(_profile, "pullquote", _profile != null ? _profile.Pullquote : "");
            var copy = UICanvasUtil.NewHeading("Copy", root, quote, 25f, Cream, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(copy.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-100f, -30f), new Vector2(24f, 0f));
            copy.enableAutoSizing = true;
            copy.fontSizeMin = 19f;
            copy.fontSizeMax = 25f;
            return AddWaypoint(root.gameObject, ruleGo.GetComponent<Image>());
        }

        private void BuildPlateGallery(Transform parent)
        {
            _plates = new[]
            {
                _profile != null ? _profile.StudySheet : null,
                _profile != null ? _profile.FigureFront : null,
                _profile != null ? _profile.FigureBack : null,
                _profile != null ? _profile.FigureThreeQuarter : null,
                _profile != null ? _profile.KnifePlate : null
            };
            _plateCaptionIds = new[]
            {
                "journal.wren.plate.study",
                "journal.wren.plate.front",
                "journal.wren.plate.back",
                "journal.wren.plate.three_quarter",
                "journal.wren.plate.knife"
            };

            var gallery = UICanvasUtil.NewRect("PlateGallery", parent);
            Pin(gallery.gameObject, ContentWidth, 820f);
            var face = gallery.gameObject.AddComponent<Image>();
            face.color = new Color(0.025f, 0.039f, 0.030f, 0.92f);
            UICanvasUtil.Roundify(face, 18);
            JournalChrome.AddStructuralBorder(gallery, 18, 0.10f);
            var heading = UICanvasUtil.NewEyebrow("Heading", gallery, Localization.Get("journal.wren.studies"), 13f, Gold);
            UICanvasUtil.SetRect(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-64f, 24f), new Vector2(32f, -26f));

            _platePresenter = JournalArtPresenter.Create("SelectedPlate", gallery, false, new Color(0.035f, 0.047f, 0.038f, 1f));
            UICanvasUtil.SetRect(_platePresenter.Frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1460f, 560f), new Vector2(0f, -64f));
            _plateCaption = UICanvasUtil.NewHeading("Caption", gallery, "", 25f, Cream, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(_plateCaption.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-64f, 40f), new Vector2(0f, 150f));

            var strip = UICanvasUtil.NewRect("PlateStrip", gallery);
            UICanvasUtil.SetRect(strip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1460f, 112f), new Vector2(0f, 24f));
            var horizontal = strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 14f;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;

            for (int i = 0; i < _plates.Length; i++)
            {
                int index = i;
                var button = BuildPlateButton(strip, _plates[i], Localization.Get(_plateCaptionIds[i]));
                button.onClick.AddListener(() => ShowPlate(index));
                _plateButtons.Add(button);
            }
        }

        private Button BuildPlateButton(Transform parent, Sprite sprite, string caption)
        {
            var root = UICanvasUtil.NewRect("PlateButton", parent);
            var face = root.gameObject.AddComponent<Image>();
            face.color = new Color(0.043f, 0.063f, 0.047f, 0.94f);
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 10);
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;
            var thumb = JournalArtPresenter.Create("Thumb", root, false, new Color(0.025f, 0.035f, 0.028f, 1f));
            UICanvasUtil.SetRect(thumb.Frame, new Vector2(0f, 0f), new Vector2(0.34f, 1f), new Vector2(0f, 0.5f), new Vector2(-8f, -12f), new Vector2(8f, 0f));
            thumb.SetSprite(sprite, new Color(0.025f, 0.035f, 0.028f, 1f));
            var label = UICanvasUtil.NewBody("Label", root, caption, 15f, Body, FontStyles.Italic, TextAlignmentOptions.Left);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = 15f;
            UICanvasUtil.SetRect(label.rectTransform, new Vector2(0.34f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(-18f, -20f), new Vector2(10f, 0f));
            var selection = UICanvasUtil.NewImage("Selected", root, new Color(Gold.r, Gold.g, Gold.b, 0f), false);
            UICanvasUtil.SetRect(selection.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-16f, 3f), new Vector2(0f, 4f));
            _plateSelection.Add(selection.GetComponent<Image>());
            JournalChrome.AddSurfaceFocus(root.gameObject, 10, 1.012f);
            return button;
        }

        private void ShowPlate(int index)
        {
            if (_plates == null || index < 0 || index >= _plates.Length || _platePresenter == null) return;
            _platePresenter.SetSprite(_plates[index], new Color(0.035f, 0.047f, 0.038f, 1f));
            _plateCaption.text = Localization.Get(_plateCaptionIds[index]);
            for (int i = 0; i < _plateSelection.Count; i++)
            {
                var color = _plateSelection[i].color;
                color.a = i == index ? 0.95f : 0f;
                _plateSelection[i].color = color;
            }
        }

        private Selectable AddWaypoint(GameObject root, Graphic highlight)
        {
            var selectable = root.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            var focus = root.AddComponent<FocusHighlight>();
            focus.Configure(highlight, root.transform as RectTransform, Gold, 1f, true, false);
            return selectable;
        }

        private void WireNavigation()
        {
            if (_closeButton == null || _identity == null || _background == null || _perspective == null || _kit == null || _quote == null || _plateButtons.Count == 0) return;
            JournalChrome.SetNavigation(_closeButton, _plateButtons[0], _identity);
            JournalChrome.SetNavigation(_identity, _closeButton, _background);
            JournalChrome.SetNavigation(_background, _identity, _kit, null, _perspective);
            JournalChrome.SetNavigation(_perspective, _identity, _kit, _background, null);
            JournalChrome.SetNavigation(_kit, _background, _quote);
            JournalChrome.SetNavigation(_quote, _kit, _plateButtons[0]);
            for (int i = 0; i < _plateButtons.Count; i++)
            {
                Selectable left = i > 0 ? _plateButtons[i - 1] : null;
                Selectable right = i < _plateButtons.Count - 1 ? _plateButtons[i + 1] : null;
                JournalChrome.SetNavigation(_plateButtons[i], _quote, _closeButton, left, right);
            }
        }

        private static void Pin(GameObject go, float width, float height, bool flexibleWidth = false)
        {
            var layout = go.GetComponent<LayoutElement>();
            if (layout == null) layout = go.AddComponent<LayoutElement>();
            if (width > 0f) layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleWidth = flexibleWidth ? 1f : 0f;
            layout.flexibleHeight = 0f;
        }

        private void EnsureCanvas()
        {
            if (GetComponent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                gameObject.AddComponent<CanvasScaler>().Init1080();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }
}
