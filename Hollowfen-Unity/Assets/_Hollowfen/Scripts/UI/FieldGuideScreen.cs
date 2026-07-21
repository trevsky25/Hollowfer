using System.Collections.Generic;
using Hollowfen.Cinematics;
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
        private static readonly Color Faint = new Color(0.961f, 0.925f, 0.855f, 0.54f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);

        private readonly List<(GameObject go, MushroomFieldGuideData entry)> _cells = new List<(GameObject, MushroomFieldGuideData)>();
        private RectTransform _content;
        private RectTransform _viewport;
        private TMP_Text _counter;
        private Button _closeButton;
        private GameObject _firstDiscovered;
        private GameObject _lastSelected;
        private bool _built;
        private NarrativePresentationSession.Lease _presentationLease;

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
            if (_presentationLease == null)
                _presentationLease = NarrativePresentationSession.AcquireIfGameplay(
                    this, NarrativePresentationSession.Modal);
            RefreshLockStates();
        }

        public override void OnClose()
        {
            base.OnClose();
            ReleaseVisibleModels();
            ReleasePresentation();
        }

        private void LateUpdate()
        {
            if (_built && isActiveAndEnabled) RefreshVisibleModels();
        }

        private void OnDestroy() => ReleasePresentation();

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
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
            page.sizeDelta = new Vector2(1560f, 0f);
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
            grid.spacing = new Vector2(20f, 24f);
            grid.cellSize = new Vector2(770f, 500f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
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
            JournalChrome.AddStructuralBorder(root.transform as RectTransform, 14, 0.08f);

            var art = JournalArtPresenter.Create("Thumb", root.transform, false, Card);
            UICanvasUtil.SetRect(art.Frame, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(734f, 413f), new Vector2(0f, -18f));
            art.SetSprite(entry.JournalPage != null ? entry.JournalPage : entry.Photo, Card);

            JournalChrome.AddSpecimenHalo(art.Frame, new Vector2(620f, 240f), new Vector2(0f, -78f));

            var modelRt = UICanvasUtil.NewRect("ModelThumb", art.Frame);
            UICanvasUtil.Stretch(modelRt);
            var modelImage = modelRt.gameObject.AddComponent<RawImage>();
            modelImage.raycastTarget = false;
            var model = modelRt.gameObject.AddComponent<JournalMushroomModelPresenter>();
            model.Configure(512, Color.clear, 14f);
            modelRt.gameObject.SetActive(false);

            var modelBadge = UICanvasUtil.NewRect("ModelBadge", art.Frame);
            UICanvasUtil.SetRect(modelBadge, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(146f, 30f), new Vector2(-10f, -10f));
            var modelBadgeLabel = UICanvasUtil.NewEyebrow("Label", modelBadge, Localization.Get("journal.field.model_badge"), 13f,
                new Color(Gold.r, Gold.g, Gold.b, 0.78f), TextAlignmentOptions.Right);
            UICanvasUtil.Stretch(modelBadgeLabel.rectTransform);
            modelBadge.gameObject.SetActive(false);

            var mark = UICanvasUtil.NewHeading("UnknownMark", art.Frame, "", 74f, Faint, FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(mark.rectTransform);

            var body = UICanvasUtil.NewRect("Body", root.transform);
            UICanvasUtil.SetRect(body, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-48f, 58f), new Vector2(0f, -432f));
            var name = UICanvasUtil.NewHeading("Name", body, JournalText.MushroomName(entry), 30f,
                Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(name.rectTransform, new Vector2(0f, 1f), new Vector2(0.62f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 36f), Vector2.zero);
            name.enableAutoSizing = true;
            name.fontSizeMin = 21f;
            name.fontSizeMax = 30f;
            var latin = UICanvasUtil.NewBody("Latin", body, JournalText.MushroomLatin(entry), 15.5f,
                Faint, FontStyles.Italic);
            UICanvasUtil.SetRect(latin.rectTransform, new Vector2(0f, 1f), new Vector2(0.62f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 21f), new Vector2(0f, -35f));

            var dotRt = UICanvasUtil.NewRect("Dot", body);
            UICanvasUtil.SetRect(dotRt, new Vector2(0.64f, 1f), new Vector2(0.64f, 1f),
                new Vector2(0f, 1f), new Vector2(10f, 10f), new Vector2(0f, -17f));
            var dot = dotRt.gameObject.AddComponent<Image>();
            dot.sprite = UICanvasUtil.Circle();
            dot.color = HollowfenPalette.Edibility(entry.Edibility);
            dot.raycastTarget = false;
            var edibility = UICanvasUtil.NewEyebrow("Edibility", body,
                JournalText.MushroomEdibility(entry), 12.5f,
                HollowfenPalette.Edibility(entry.Edibility), TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(edibility.rectTransform, new Vector2(0.66f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(0f, 24f), new Vector2(0f, -10f));

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
                bool available = IsAvailable(pair.entry);
                bool verified = MushroomDiscovery.IsDiscovered(pair.entry.Id);
                var button = pair.go.GetComponent<Button>();
                button.interactable = available;
                if (available && _firstDiscovered == null) _firstDiscovered = pair.go;

                var art = pair.go.transform.Find("Thumb/Art")?.GetComponent<JournalArtPresenter>();
                bool hasPage = available && pair.entry.JournalPage != null;
                bool hasModel = available && !hasPage && pair.entry.JournalPreviewPrefab != null;
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
                    Sprite preview = pair.entry.JournalPage != null ? pair.entry.JournalPage : pair.entry.Photo;
                    art.SetSprite(preview, Card);
                    art.SetTint(available && preview != null ? Color.white : new Color(0.10f, 0.09f, 0.08f, 1f));
                }
                var mark = pair.go.transform.Find("Thumb/UnknownMark")?.GetComponent<TMP_Text>();
                if (mark != null)
                {
                    bool reference = available && !verified;
                    bool missing = available && verified && !hasModel &&
                                   pair.entry.JournalPage == null && pair.entry.Photo == null;
                    mark.text = !available ? "?" : reference
                        ? Localization.Get("journal.field.reference_short")
                        : missing ? Localization.Get("journal.field.missing_photo") : "";
                    if (!available || missing)
                    {
                        UICanvasUtil.Stretch(mark.rectTransform);
                        mark.alignment = TextAlignmentOptions.Center;
                        mark.fontSize = !available ? 74f : 18f;
                        mark.color = Faint;
                    }
                    else
                    {
                        UICanvasUtil.SetRect(mark.rectTransform, new Vector2(0f, 1f),
                            new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(170f, 30f), new Vector2(18f, -18f));
                        mark.alignment = TextAlignmentOptions.Left;
                        mark.fontSize = 13f;
                        mark.color = new Color(Gold.r, Gold.g, Gold.b, 0.88f);
                    }
                }

                var body = pair.go.transform.Find("Body");
                if (body == null) continue;
                var name = body.Find("Name")?.GetComponent<TMP_Text>();
                var latin = body.Find("Latin")?.GetComponent<TMP_Text>();
                var dot = body.Find("Dot")?.GetComponent<Image>();
                var edibility = body.Find("Edibility")?.GetComponent<TMP_Text>();
                Color chip = available ? HollowfenPalette.Edibility(pair.entry.Edibility) : HollowfenPalette.EdUnknown;
                if (name != null) name.text = available ? JournalText.MushroomName(pair.entry) : "?";
                if (latin != null) latin.text = available ? JournalText.MushroomLatin(pair.entry) : Localization.Get("journal.field.unknown");
                if (dot != null) dot.color = chip;
                if (edibility != null)
                {
                    edibility.text = available ? JournalText.MushroomEdibility(pair.entry).ToUpperInvariant() : Localization.Get("journal.field.unknown_label").ToUpperInvariant();
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
                               pair.entry.JournalPage == null &&
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
            int found = _database != null
                ? JournalNavigation.CountAvailable(_database.Entries,
                    entry => entry != null && MushroomDiscovery.IsDiscovered(entry.Id))
                : 0;
            return string.Format(Localization.Get("journal.field.counter"), found, total);
        }

        private static bool IsAvailable(MushroomFieldGuideData entry)
        {
            return entry != null && MushroomKnowledge.CanReadPage(entry);
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
