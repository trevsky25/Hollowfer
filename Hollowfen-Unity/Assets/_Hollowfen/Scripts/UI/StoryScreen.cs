using System.Collections.Generic;
using Hollowfen.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class StoryScreen : UIScreen
    {
        [SerializeField] private StoryCardDatabase _database;
        [SerializeField] private StoryDetailScreen _detailScreen;

        private static readonly Color Bg = HollowfenPalette.JournalBackdrop;
        private static readonly Color Card = HollowfenPalette.SurfaceBase;
        private static readonly Color Cream = new Color(0.961f, 0.925f, 0.855f, 1f);
        private static readonly Color Subtle = new Color(0.961f, 0.925f, 0.855f, 0.64f);
        private static readonly Color Faint = new Color(0.961f, 0.925f, 0.855f, 0.54f);
        private static readonly Color Gold = new Color(0.851f, 0.741f, 0.427f, 1f);

        private readonly List<(GameObject go, StoryCardData card)> _cells = new List<(GameObject, StoryCardData)>();
        private RectTransform _content;
        private TMP_Text _counter;
        private Button _closeButton;
        private GameObject _firstUnlocked;
        private GameObject _lastSelected;
        private bool _built;

        public override GameObject DefaultSelected
        {
            get
            {
                if (IsSelectable(_lastSelected)) return _lastSelected;
                if (IsSelectable(_firstUnlocked)) return _firstUnlocked;
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
                PopulateCards();
                _built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[StoryScreen] OnInitialize failed: " + e);
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshLockStates();
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

            JournalChrome.BuildIndexHeader(page, Localization.Get("journal.story.title"), BuildCounterCopy(), out _counter);
            _closeButton = JournalChrome.BuildCloseButton(transform, () =>
            {
                if (UIManager.Instance != null) UIManager.Instance.Back();
            });
            JournalChrome.BuildBottomHint(transform, "journal.hint.index");

            var scrollRt = UICanvasUtil.NewRect("Scroll", page);
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
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

            var viewport = UICanvasUtil.NewRect("Viewport", scrollRt);
            UICanvasUtil.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            _content = UICanvasUtil.NewRect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            _content.anchoredPosition = Vector2.zero;
            scroll.content = _content;
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 18, 90);
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void PopulateCards()
        {
            if (_database == null || _content == null) return;
            var groups = new List<(StoryCardData first, List<StoryCardData> cards)>();
            string currentAct = null;
            foreach (var card in _database.Cards)
            {
                if (card == null) continue;
                if (card.Act != currentAct)
                {
                    groups.Add((card, new List<StoryCardData>()));
                    currentAct = card.Act;
                }
                groups[groups.Count - 1].cards.Add(card);
            }

            foreach (var group in groups)
            {
                BuildActHeader(_content, JournalText.StoryAct(group.first), group.cards.Count);
                var grid = BuildGrid(_content, group.first.Id);
                foreach (var card in group.cards) BuildCard(grid, card);
            }
        }

        private void BuildActHeader(Transform parent, string act, int count)
        {
            var row = UICanvasUtil.NewRect("ActHeader", parent);
            var rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.minHeight = 44f;
            rowLayout.preferredHeight = 44f;
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(2, 4, 10, 10);
            horizontal.spacing = 18f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            var title = UICanvasUtil.NewEyebrow("Label", row, act, 18f, Gold);
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 22f;
            var rule = UICanvasUtil.NewImage("Rule", row, new Color(Gold.r, Gold.g, Gold.b, 0.34f), false);
            var ruleLayout = rule.AddComponent<LayoutElement>();
            ruleLayout.preferredHeight = 1f;
            ruleLayout.flexibleWidth = 1f;
            var countLabel = UICanvasUtil.NewEyebrow("Count", row,
                string.Format(Localization.Get("journal.story.cards"), count), 18f, Faint, TextAlignmentOptions.Right);
            var countLayout = countLabel.gameObject.AddComponent<LayoutElement>();
            countLayout.preferredWidth = 130f;
            countLayout.preferredHeight = 20f;
        }

        private RectTransform BuildGrid(Transform parent, string id)
        {
            var gridRt = UICanvasUtil.NewRect("Grid_" + id, parent);
            var grid = gridRt.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(489f, 400f);
            grid.spacing = new Vector2(16f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            var fitter = gridRt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return gridRt;
        }

        private void BuildCard(Transform parent, StoryCardData card)
        {
            var root = UICanvasUtil.NewRect("Card_" + card.Id, parent).gameObject;
            var face = root.AddComponent<Image>();
            face.color = Card;
            face.raycastTarget = true;
            UICanvasUtil.Roundify(face, 14);
            var button = root.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = face;

            var art = JournalArtPresenter.Create("Thumb", root.transform, true, new Color(0.06f, 0.07f, 0.05f, 1f));
            UICanvasUtil.SetRect(art.Frame, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-16f, 220f), new Vector2(0f, -8f));
            art.SetSprite(card.Image, new Color(0.08f, 0.08f, 0.07f, 1f));

            var body = UICanvasUtil.NewRect("Body", root.transform);
            UICanvasUtil.SetRect(body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(-48f, 156f), new Vector2(0f, -234f));
            var scene = UICanvasUtil.NewEyebrow("Eyebrow", body, JournalText.StoryScene(card), 18f, Gold);
            UICanvasUtil.SetRect(scene.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 20f), Vector2.zero);
            var title = UICanvasUtil.NewHeading("Title", body, JournalText.StoryTitle(card), 34f, Cream, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 48f), new Vector2(0f, -32f));
            // Keep the three-column index rhythm while allowing the longest canonical title
            // ("The First Festival in Three Years") to fit at the 1280x800 Deck target.
            title.enableAutoSizing = true;
            title.fontSizeMin = 26f;
            title.fontSizeMax = 34f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Truncate;
            var subtitle = UICanvasUtil.NewBody("Subtitle", body, JournalText.StorySubtitle(card), 20f, Subtle);
            UICanvasUtil.SetRect(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 58f), new Vector2(0f, -90f));
            subtitle.overflowMode = TextOverflowModes.Truncate;

            JournalChrome.AddSurfaceFocus(root, 14, 1.012f);

            var cell = root.AddComponent<StoryCardCell>();
            cell.Bind(card, OnCardClicked);
            button.onClick.AddListener(cell.HandleClick);
            _cells.Add((root, card));
        }

        private void RefreshLockStates()
        {
            _firstUnlocked = null;
            if (_counter != null) _counter.text = BuildCounterCopy();
            foreach (var pair in _cells)
            {
                if (pair.go == null || pair.card == null) continue;
                bool unlocked = IsAvailable(pair.card);
                var button = pair.go.GetComponent<Button>();
                button.interactable = unlocked;
                if (unlocked && _firstUnlocked == null) _firstUnlocked = pair.go;

                var art = pair.go.transform.Find("Thumb/Art")?.GetComponent<JournalArtPresenter>();
                if (art != null) art.SetTint(unlocked ? Color.white : new Color(0.16f, 0.14f, 0.11f, 1f));
                var body = pair.go.transform.Find("Body");
                if (body == null) continue;
                var eyebrow = body.Find("Eyebrow")?.GetComponent<TMP_Text>();
                var title = body.Find("Title")?.GetComponent<TMP_Text>();
                var subtitle = body.Find("Subtitle")?.GetComponent<TMP_Text>();
                if (eyebrow != null)
                {
                    eyebrow.text = unlocked ? JournalText.StoryScene(pair.card).ToUpperInvariant() : Localization.Get("journal.story.locked").ToUpperInvariant();
                    eyebrow.color = unlocked ? Gold : Faint;
                }
                if (title != null) title.text = unlocked ? JournalText.StoryTitle(pair.card) : Localization.Get("journal.story.locked_title", "· · ·");
                if (subtitle != null) subtitle.text = unlocked ? JournalText.StorySubtitle(pair.card) : Localization.Get("journal.story.locked_body");
            }
            if (!IsSelectable(_lastSelected)) _lastSelected = null;
        }

        private void OnCardClicked(StoryCardData card)
        {
            if (!IsAvailable(card)) return;
            foreach (var pair in _cells)
                if (pair.card == card) { _lastSelected = pair.go; break; }
            if (_detailScreen != null) _detailScreen.SetCard(card, _database);
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("story-detail");
        }

        private string BuildCounterCopy()
        {
            int total = _database != null ? _database.Count : 0;
            int unlocked = _database != null ? JournalNavigation.CountAvailable(_database.Cards, IsAvailable) : 0;
            return string.Format(Localization.Get("journal.story.counter"), unlocked, total);
        }

        private static bool IsAvailable(StoryCardData card)
        {
            return card != null && Hollowfen.Quests.QuestManager.IsStoryCardUnlocked(card.Id);
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
