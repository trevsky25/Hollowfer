using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Foraging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class FieldGuideScreen : UIScreen
    {
        [SerializeField] private MushroomFieldGuideDatabase _database;
        [SerializeField] private MushroomDetailScreen _detailScreen;

        private static readonly Color Bg = HollowfenPalette.JournalBackdrop;
        private static readonly Color Card = HollowfenPalette.SurfaceBase;
        private static readonly Color Cream = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color Subtle = new Color(0.961f, 0.925f, 0.855f, 0.62f);
        private static readonly Color Faint = new Color(0.961f, 0.925f, 0.855f, 0.44f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);

        private readonly List<(GameObject go, MushroomFieldGuideData entry)> _cells = new List<(GameObject, MushroomFieldGuideData)>();
        private RectTransform _content;
        private RectTransform _viewport;
        private TMP_Text _counter;
        private Button _closeButton;
        private GameObject _firstDiscovered;
        private GameObject _lastSelected;
        private bool _built;
        private bool _ownsGameplayPause;
        private float _previousTimeScale = 1f;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public override GameObject DefaultSelected
        {
            get
            {
                if (IsSelectable(_lastSelected)) return _lastSelected;
                if (IsSelectable(_firstDiscovered)) return _firstDiscovered;
                return _closeButton != null ? _closeButton.gameObject : base.DefaultSelected;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_built) return;
            try
            {
                BuildLayout();
                PopulateGrid();
                _built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FieldGuideScreen] OnInitialize failed: " + e);
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            AcquireGameplayPauseIfNeeded();
            RefreshLockStates();
        }

        public override void OnClose()
        {
            base.OnClose();
            ReleaseVisibleModels();
            ReleaseGameplayPause();
        }

        private void LateUpdate()
        {
            if (_built && isActiveAndEnabled) RefreshVisibleModels();
        }

        // The guide also lives in the main-menu journal and under Pause. Only a direct
        // gameplay shortcut owns its pause/cursor state; existing menu contexts keep theirs.
        private void AcquireGameplayPauseIfNeeded()
        {
            if (_ownsGameplayPause || Time.timeScale <= 0f) return;
            if (GameObject.FindGameObjectWithTag("Player") == null) return;

            _ownsGameplayPause = true;
            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void ReleaseGameplayPause()
        {
            if (!_ownsGameplayPause) return;
            _ownsGameplayPause = false;

            Time.timeScale = _previousTimeScale;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        private void BuildLayout()
        {
            EnsureCanvas();
            var bg = UICanvasUtil.NewImage("BG", transform, Bg, true);
            UICanvasUtil.Stretch(bg.GetComponent<RectTransform>());

            var page = UICanvasUtil.NewRect("JournalPage", transform);
            page.anchorMin = new Vector2(0.5f, 0f);
            page.anchorMax = new Vector2(0.5f, 1f);
            page.pivot = new Vector2(0.5f, 0.5f);
            page.sizeDelta = new Vector2(1500f, 0f);
            page.offsetMin = new Vector2(page.offsetMin.x, 52f);
            page.offsetMax = new Vector2(page.offsetMax.x, -46f);

            JournalChrome.BuildIndexHeader(page, Localization.Get("journal.field.title"), BuildCounterCopy(), out _counter);
            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.index");

            var scrollRt = UICanvasUtil.NewRect("Scroll", page);
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 34f);
            scrollRt.offsetMax = new Vector2(0f, -182f);
            var scrollImage = scrollRt.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);
            scrollImage.raycastTarget = true;
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;
            scrollRt.gameObject.AddComponent<ScrollFocusFollower>();

            _viewport = UICanvasUtil.NewRect("Viewport", scrollRt);
            UICanvasUtil.Stretch(_viewport);
            _viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = _viewport;

            _content = UICanvasUtil.NewRect("Content", _viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            _content.anchoredPosition = Vector2.zero;
            scroll.content = _content;
            var grid = _content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 18, 90);
            grid.spacing = new Vector2(16f, 18f);
            grid.cellSize = new Vector2(489f, 520f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PopulateGrid()
        {
            if (_database == null || _content == null) return;
            foreach (var entry in _database.Entries)
                if (entry != null) BuildCard(_content, entry);
        }

        private void BuildCard(Transform parent, MushroomFieldGuideData entry)
        {
            var root = UICanvasUtil.NewRect("Card_" + entry.Id, parent).gameObject;
            var face = root.AddComponent<Image>();
            face.color = Card;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 14);
            var button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;

            var art = JournalArtPresenter.Create("Thumb", root.transform, false, Card);
            UICanvasUtil.SetRect(art.Frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(314f, 314f), new Vector2(0f, -18f));
            art.SetSprite(entry.Photo, Card);

            JournalChrome.AddSpecimenHalo(art.Frame, new Vector2(284f, 150f), new Vector2(0f, -58f));

            var modelRt = UICanvasUtil.NewRect("ModelThumb", art.Frame);
            UICanvasUtil.Stretch(modelRt);
            var modelImage = modelRt.gameObject.AddComponent<RawImage>();
            modelImage.raycastTarget = false;
            var model = modelRt.gameObject.AddComponent<JournalMushroomModelPresenter>();
            model.Configure(384, Color.clear, 14f);
            modelRt.gameObject.SetActive(false);

            var modelBadge = UICanvasUtil.NewRect("ModelBadge", art.Frame);
            UICanvasUtil.SetRect(modelBadge, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(122f, 30f), new Vector2(-8f, -8f));
            var modelBadgeLabel = UICanvasUtil.NewEyebrow("Label", modelBadge, Localization.Get("journal.field.model_badge"), 13f,
                new Color(Gold.r, Gold.g, Gold.b, 0.78f), TextAlignmentOptions.Right);
            UICanvasUtil.Stretch(modelBadgeLabel.rectTransform);
            modelBadge.gameObject.SetActive(false);

            var mark = UICanvasUtil.NewHeading("UnknownMark", art.Frame, "", 74f, Faint, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(mark.rectTransform);

            var body = UICanvasUtil.NewRect("Body", root.transform);
            UICanvasUtil.SetRect(body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-52f, 166f), new Vector2(0f, -338f));
            var name = UICanvasUtil.NewHeading("Name", body, JournalText.MushroomName(entry), 34f, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 44f), Vector2.zero);
            name.enableAutoSizing = true;
            name.fontSizeMin = 24f;
            name.fontSizeMax = 34f;
            var latin = UICanvasUtil.NewBody("Latin", body, JournalText.MushroomLatin(entry), 17f, Faint, FontStyles.Italic);
            UICanvasUtil.SetRect(latin.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 24f), new Vector2(0f, -52f));

            var dotRt = UICanvasUtil.NewRect("Dot", body);
            UICanvasUtil.SetRect(dotRt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(11f, 11f), new Vector2(0f, -96f));
            var dot = dotRt.gameObject.AddComponent<Image>();
            dot.sprite = UICanvasUtil.Circle();
            dot.color = HollowfenPalette.Edibility(entry.Edibility);
            dot.raycastTarget = false;
            var edibility = UICanvasUtil.NewEyebrow("Edibility", body, JournalText.MushroomEdibility(entry), 13f, HollowfenPalette.Edibility(entry.Edibility));
            UICanvasUtil.SetRect(edibility.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-20f, 22f), new Vector2(20f, -91f));

            JournalChrome.AddSurfaceFocus(root, 14, 1.012f);

            var cell = root.AddComponent<MushroomCardCell>();
            cell.Bind(entry, OnCellClicked);
            button.onClick.AddListener(cell.HandleClick);
            _cells.Add((root, entry));
        }

        private void RefreshLockStates()
        {
            _firstDiscovered = null;
            if (_counter != null) _counter.text = BuildCounterCopy();
            foreach (var pair in _cells)
            {
                if (pair.go == null || pair.entry == null) continue;
                bool discovered = IsAvailable(pair.entry);
                var button = pair.go.GetComponent<Button>();
                button.interactable = discovered;
                if (discovered && _firstDiscovered == null) _firstDiscovered = pair.go;

                var art = pair.go.transform.Find("Thumb/Art")?.GetComponent<JournalArtPresenter>();
                bool hasModel = discovered && pair.entry.JournalPreviewPrefab != null;
                var modelRoot = pair.go.transform.Find("Thumb/ModelThumb");
                if (modelRoot != null)
                {
                    modelRoot.gameObject.SetActive(false);
                    var model = modelRoot.GetComponent<JournalMushroomModelPresenter>();
                    model.Clear();
                }
                var modelBadge = pair.go.transform.Find("Thumb/ModelBadge");
                if (modelBadge != null) modelBadge.gameObject.SetActive(hasModel);
                var modelHalo = pair.go.transform.Find("Thumb/SpecimenHalo");
                if (modelHalo != null) modelHalo.gameObject.SetActive(hasModel);
                if (art != null)
                {
                    art.Image.enabled = !hasModel;
                    art.SetTint(discovered && pair.entry.Photo != null ? Color.white : new Color(0.10f, 0.09f, 0.08f, 1f));
                }
                var mark = pair.go.transform.Find("Thumb/UnknownMark")?.GetComponent<TMP_Text>();
                if (mark != null)
                {
                    mark.text = !discovered
                        ? "?"
                        : (!hasModel && pair.entry.Photo == null ? Localization.Get("journal.field.missing_photo") : "");
                    mark.fontSize = !discovered ? 74f : 20f;
                }

                var body = pair.go.transform.Find("Body");
                if (body == null) continue;
                var name = body.Find("Name")?.GetComponent<TMP_Text>();
                var latin = body.Find("Latin")?.GetComponent<TMP_Text>();
                var dot = body.Find("Dot")?.GetComponent<Image>();
                var edibility = body.Find("Edibility")?.GetComponent<TMP_Text>();
                Color chip = discovered ? HollowfenPalette.Edibility(pair.entry.Edibility) : HollowfenPalette.EdUnknown;
                if (name != null) name.text = discovered ? JournalText.MushroomName(pair.entry) : "?";
                if (latin != null) latin.text = discovered ? JournalText.MushroomLatin(pair.entry) : Localization.Get("journal.field.unknown");
                if (dot != null) dot.color = chip;
                if (edibility != null)
                {
                    edibility.text = discovered ? JournalText.MushroomEdibility(pair.entry).ToUpperInvariant() : Localization.Get("journal.field.unknown_label").ToUpperInvariant();
                    edibility.color = chip;
                }
            }
            if (!IsSelectable(_lastSelected)) _lastSelected = null;
            Canvas.ForceUpdateCanvases();
            RefreshVisibleModels();
        }

        // Full guide coverage can reach twenty high-detail specimens. Keep preview
        // rigs only for cards intersecting the masked viewport rather than paying for
        // twenty models, RenderTextures, cameras, and light rigs at once.
        private void RefreshVisibleModels()
        {
            if (_viewport == null) return;
            foreach (var pair in _cells)
            {
                if (pair.go == null || pair.entry == null) continue;
                var modelRoot = pair.go.transform.Find("Thumb/ModelThumb") as RectTransform;
                if (modelRoot == null) continue;
                bool visible = IsAvailable(pair.entry) &&
                               pair.entry.JournalPreviewPrefab != null &&
                               IntersectsViewport(pair.go.transform as RectTransform);
                modelRoot.gameObject.SetActive(visible);
                var presenter = modelRoot.GetComponent<JournalMushroomModelPresenter>();
                if (visible) presenter.SetEntry(pair.entry);
                else presenter.Clear();
            }
        }

        private void ReleaseVisibleModels()
        {
            foreach (var pair in _cells)
            {
                if (pair.go == null) continue;
                var modelRoot = pair.go.transform.Find("Thumb/ModelThumb");
                if (modelRoot == null) continue;
                var presenter = modelRoot.GetComponent<JournalMushroomModelPresenter>();
                if (presenter != null) presenter.Clear();
                modelRoot.gameObject.SetActive(false);
            }
        }

        private bool IntersectsViewport(RectTransform card)
        {
            if (card == null || _viewport == null) return false;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_viewport, card);
            Rect viewportRect = _viewport.rect;
            const float preloadMargin = 32f;
            return bounds.max.x >= viewportRect.xMin - preloadMargin &&
                   bounds.min.x <= viewportRect.xMax + preloadMargin &&
                   bounds.max.y >= viewportRect.yMin - preloadMargin &&
                   bounds.min.y <= viewportRect.yMax + preloadMargin;
        }

        private void OnCellClicked(MushroomFieldGuideData entry)
        {
            if (!IsAvailable(entry)) return;
            foreach (var pair in _cells)
                if (pair.entry == entry) { _lastSelected = pair.go; break; }
            if (_detailScreen != null) _detailScreen.SetEntry(entry, _database);
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("mushroom-detail");
        }

        private string BuildCounterCopy()
        {
            int total = _database != null ? _database.Count : 0;
            int found = _database != null ? JournalNavigation.CountAvailable(_database.Entries, IsAvailable) : 0;
            return string.Format(Localization.Get("journal.field.counter"), found, total);
        }

        private static bool IsAvailable(MushroomFieldGuideData entry)
        {
            return entry != null && Hollowfen.Foraging.MushroomDiscovery.IsDiscovered(entry.Id);
        }

        private static bool IsSelectable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            var selectable = go.GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
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
