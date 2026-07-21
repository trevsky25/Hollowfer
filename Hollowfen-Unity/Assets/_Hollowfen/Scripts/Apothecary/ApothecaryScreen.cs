using System.Collections.Generic;
using Hollowfen.Cinematics;
using Hollowfen.Foraging;
using Hollowfen.Input;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollowfen.Apothecary
{
    /// <summary>
    /// A tactile recipe-book flow: read the known field pages, check the basket, then perform
    /// each authored bench action. No real-world medical or consumption directions are shown.
    /// </summary>
    public sealed class ApothecaryScreen : MonoBehaviour
    {
        private static readonly Color Paper = new Color(.91f, .87f, .78f, 1f);
        private static readonly Color PaperDeep = new Color(.84f, .79f, .68f, 1f);
        private static readonly Color Ink = new Color(.14f, .115f, .085f, 1f);
        private static readonly Color InkMuted = new Color(.28f, .24f, .18f, .82f);
        private static readonly Color Forest = new Color(.10f, .19f, .125f, 1f);
        private static readonly Color ForestRaised = new Color(.145f, .255f, .17f, 1f);
        private static readonly Color Gold = HollowfenPalette.Gold;
        private static readonly Color Success = new Color(.28f, .51f, .29f, 1f);

        public static ApothecaryScreen Instance { get; private set; }

        private readonly List<Button> _recipeButtons = new List<Button>();
        private readonly List<Image> _recipeBackgrounds = new List<Image>();
        private readonly List<TMP_Text> _recipeStatuses = new List<TMP_Text>();
        private readonly List<GameObject> _recipeRoots = new List<GameObject>();
        private readonly List<Image> _stepDots = new List<Image>();
        private readonly List<TMP_Text> _stepLabels = new List<TMP_Text>();

        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _recipeList;
        private RectTransform _stepsRoot;
        private Image _heroImage;
        private TMP_Text _heroLock;
        private TMP_Text _kind;
        private TMP_Text _title;
        private TMP_Text _summary;
        private TMP_Text _ingredients;
        private TMP_Text _stock;
        private TMP_Text _status;
        private Button _actionButton;
        private Image _actionBackground;
        private TMP_Text _actionLabel;
        private Button _closeButton;
        private RectTransform _successBadge;
        private TMP_Text _successCheck;
        private TMP_Text _successText;
        private InputActions _input;
        private ApothecaryStation _station;
        private PreparationRecipeData[] _recipes;
        private int _selected;
        private int _completedSteps;
        private bool _preparing;
        private bool _showingSuccess;
        private float _successStarted;
        private NarrativePresentationSession.Lease _presentationLease;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        public static ApothecaryScreen Ensure()
        {
            if (Instance != null) return Instance;
            return new GameObject("_ApothecaryScreen").AddComponent<ApothecaryScreen>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _input = new InputActions();
            Build();
            _canvas.enabled = false;
        }

        private void OnEnable()
        {
            _input?.UI.Enable();
            if (_input != null) _input.UI.Cancel.performed += OnCancel;
        }

        private void OnDisable()
        {
            if (_input == null) return;
            _input.UI.Cancel.performed -= OnCancel;
            _input.UI.Disable();
        }

        private void OnDestroy()
        {
            ReleasePresentation();
            if (Instance == this) Instance = null;
            _input?.Dispose();
        }

        public void Open(ApothecaryStation station)
        {
            if (station == null || IsOpen) return;
            _station = station;
            _recipes = station.Recipes ?? new PreparationRecipeData[0];
            _selected = Mathf.Clamp(_selected, 0, Mathf.Max(0, _recipes.Length - 1));
            _preparing = false;
            _showingSuccess = false;
            _completedSteps = 0;
            BuildRecipeRows();
            RefreshDetail();

            _presentationLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.Modal);
            EnsureEventSystem();
            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            EventSystem.current?.SetSelectedGameObject(
                _recipeButtons.Count > 0 ? _recipeButtons[_selected].gameObject : _closeButton.gameObject);
            UISfx.PageTurn();
        }

        public void Close()
        {
            if (!IsOpen) return;
            _station?.ResetPulse();
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _station = null;
            _recipes = null;
            _preparing = false;
            _showingSuccess = false;
            ReleasePresentation();
            EventSystem.current?.SetSelectedGameObject(null);
            UISfx.Back();
        }

        private void Update()
        {
            if (!IsOpen) return;
            UIFocusRecovery.RestoreIfLost(transform, PreferredFocus());
            if (!_showingSuccess || _successBadge == null) return;
            float age = Time.unscaledTime - _successStarted;
            float enter = Mathf.Clamp01(age / .28f);
            float overshoot = 1f + Mathf.Sin(enter * Mathf.PI) * .16f;
            _successBadge.localScale = Vector3.one * Mathf.Lerp(.55f, 1f, enter) * overshoot;
            _successCheck.alpha = enter;
            _successText.alpha = enter;
        }

        private GameObject PreferredFocus()
        {
            if ((_preparing || _showingSuccess) && _actionButton != null &&
                _actionButton.IsInteractable()) return _actionButton.gameObject;
            if (_recipeButtons.Count > 0)
            {
                int index = Mathf.Clamp(_selected, 0, _recipeButtons.Count - 1);
                if (_recipeButtons[index] != null && _recipeButtons[index].IsInteractable())
                    return _recipeButtons[index].gameObject;
            }
            return _closeButton != null ? _closeButton.gameObject : null;
        }

        private void ReleasePresentation()
        {
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private void OnCancel(InputAction.CallbackContext _) => Close();

        private void SelectRecipe(int index)
        {
            if (_preparing || _showingSuccess || _recipes == null || index < 0 || index >= _recipes.Length)
            {
                UISfx.Error(.30f);
                return;
            }
            _selected = index;
            _completedSteps = 0;
            RefreshDetail();
            UISfx.PageTurn(.25f);
        }

        private void PerformNextStep()
        {
            if (_showingSuccess)
            {
                _showingSuccess = false;
                _preparing = false;
                _completedSteps = 0;
                _successBadge.gameObject.SetActive(false);
                SetRecipeInteraction(true);
                RefreshDetail();
                EventSystem.current?.SetSelectedGameObject(_actionButton.gameObject);
                return;
            }

            PreparationRecipeData recipe = CurrentRecipe();
            if (recipe == null) { UISfx.Error(); return; }
            ApothecaryRuntime.CraftResult availability = ApothecaryRuntime.Availability(recipe);
            if (!_preparing && availability != ApothecaryRuntime.CraftResult.Prepared)
            {
                UISfx.Error();
                RefreshDetail();
                return;
            }

            _preparing = true;
            SetRecipeInteraction(false);
            int stepCount = Mathf.Max(1, recipe.StepIds?.Length ?? 0);
            int step = Mathf.Clamp(_completedSteps, 0, stepCount - 1);
            _station?.PreviewStep(step);
            _completedSteps++;
            UISfx.Select(.38f);

            if (_completedSteps < stepCount)
            {
                RefreshDetail();
                return;
            }

            ApothecaryRuntime.CraftResult result = ApothecaryRuntime.TryPrepare(recipe);
            if (result != ApothecaryRuntime.CraftResult.Prepared)
            {
                _preparing = false;
                _completedSteps = 0;
                SetRecipeInteraction(true);
                UISfx.Error();
                RefreshDetail();
                return;
            }

            ShowSuccess(recipe);
        }

        private void ShowSuccess(PreparationRecipeData recipe)
        {
            _preparing = false;
            _showingSuccess = true;
            _successStarted = Time.unscaledTime;
            _successBadge.gameObject.SetActive(true);
            _successBadge.localScale = Vector3.one * .55f;
            _successCheck.alpha = 0f;
            _successText.alpha = 0f;
            _successText.text = string.Format(Localization.Get("apothecary.success.body"),
                Localization.Get(recipe.ResultNameId));
            if (!string.IsNullOrWhiteSpace(recipe.ResultUseId))
                _successText.text += "\n" + Localization.Get(recipe.ResultUseId);
            _status.text = Localization.Get("apothecary.success.status");
            _status.color = Success;
            _actionLabel.text = Localization.Get("apothecary.prepare.another");
            _actionLabel.color = HollowfenPalette.Cream;
            _actionButton.interactable = true;
            _actionBackground.color = Forest;
            BuildRecipeRows();
            SetRecipeInteraction(false);
            UISfx.Confirm(.65f);
        }

        private PreparationRecipeData CurrentRecipe()
        {
            if (_recipes == null || _selected < 0 || _selected >= _recipes.Length) return null;
            return _recipes[_selected];
        }

        private void BuildRecipeRows()
        {
            foreach (GameObject root in _recipeRoots) if (root != null) Destroy(root);
            _recipeRoots.Clear();
            _recipeButtons.Clear();
            _recipeBackgrounds.Clear();
            _recipeStatuses.Clear();
            if (_recipes == null) return;

            for (int i = 0; i < _recipes.Length; i++)
            {
                int captured = i;
                PreparationRecipeData recipe = _recipes[i];
                RectTransform row = NewFixed("Recipe_" + (recipe != null ? recipe.Id : i.ToString()),
                    _recipeList, new Vector2(340f, 98f), new Vector2(0f, 222f - i * 112f));
                Image bg = row.gameObject.AddComponent<Image>();
                bg.sprite = UICanvasUtil.RoundedRect(14);
                bg.type = Image.Type.Sliced;
                bg.color = i == _selected ? new Color(.96f, .90f, .70f, 1f) :
                    new Color(.98f, .95f, .86f, .48f);

                TMP_Text number = UICanvasUtil.NewEyebrow("Number", row,
                    (i + 1).ToString("00"), 20f, i == _selected ? Forest : InkMuted);
                UICanvasUtil.SetRect(number.rectTransform, new Vector2(0f, .58f), new Vector2(.18f, 1f),
                    new Vector2(0f, 1f), Vector2.zero, new Vector2(18f, -14f));
                TMP_Text label = UICanvasUtil.NewHeading("Title", row,
                    recipe != null ? Localization.Get(recipe.TitleId) : "—", 24f, Ink,
                    FontStyles.Italic, TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(label.rectTransform, new Vector2(.15f, .36f), new Vector2(.96f, 1f),
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);

                ApothecaryRuntime.CraftResult state = ApothecaryRuntime.Availability(recipe);
                TMP_Text status = UICanvasUtil.NewBody("Status", row, RowStatus(recipe, state), 20f,
                    state == ApothecaryRuntime.CraftResult.Prepared ? ForestRaised : InkMuted,
                    FontStyles.Normal, TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(status.rectTransform, new Vector2(.15f, .05f), new Vector2(.96f, .38f),
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);

                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = button.colors;
                colors.normalColor = bg.color;
                colors.highlightedColor = new Color(.98f, .90f, .64f, 1f);
                colors.selectedColor = new Color(.98f, .90f, .64f, 1f);
                colors.pressedColor = new Color(.91f, .79f, .48f, 1f);
                button.colors = colors;
                button.onClick.AddListener(() => SelectRecipe(captured));
                _recipeRoots.Add(row.gameObject);
                _recipeButtons.Add(button);
                _recipeBackgrounds.Add(bg);
                _recipeStatuses.Add(status);
            }
            WireNavigation();
        }

        private string RowStatus(PreparationRecipeData recipe, ApothecaryRuntime.CraftResult state)
        {
            if (recipe == null) return Localization.Get("apothecary.status.locked");
            if (state == ApothecaryRuntime.CraftResult.SpeciesUnidentified)
                return Localization.Get("apothecary.status.unidentified");
            if (state == ApothecaryRuntime.CraftResult.RecipeLocked)
                return Localization.Get("apothecary.status.locked");
            if (state == ApothecaryRuntime.CraftResult.MissingIngredients)
                return Localization.Get("apothecary.status.missing");
            return ApothecaryRuntime.HasCrafted(recipe.Id)
                ? Localization.Get("apothecary.status.practiced")
                : Localization.Get("apothecary.status.ready");
        }

        private void RefreshDetail()
        {
            PreparationRecipeData recipe = CurrentRecipe();
            if (recipe == null)
            {
                _title.text = Localization.Get("apothecary.empty");
                _actionButton.interactable = false;
                return;
            }

            for (int i = 0; i < _recipeBackgrounds.Count; i++)
                _recipeBackgrounds[i].color = i == _selected ? new Color(.96f, .90f, .70f, 1f) :
                    new Color(.98f, .95f, .86f, .48f);

            _kind.text = Localization.Get("apothecary.kind." + recipe.Kind.ToString().ToLowerInvariant());
            _title.text = Localization.Get(recipe.TitleId);
            _summary.text = Localization.Get(recipe.SummaryId);
            bool heroKnown = recipe.HeroSpecies != null &&
                             MushroomKnowledge.IsFieldIdentified(recipe.HeroSpecies);
            _heroImage.sprite = heroKnown ? recipe.HeroSpecies.JournalPage : null;
            _heroImage.enabled = _heroImage.sprite != null;
            _heroImage.preserveAspect = true;
            _heroLock.gameObject.SetActive(!heroKnown);
            _ingredients.text = IngredientCopy(recipe);
            _stock.text = string.Format(Localization.Get("apothecary.stock"),
                Localization.Get(recipe.ResultNameId), ApothecaryRuntime.ProductCount(recipe.ResultId));

            ApothecaryRuntime.CraftResult availability = ApothecaryRuntime.Availability(recipe);
            _status.text = availability == ApothecaryRuntime.CraftResult.RecipeLocked &&
                !string.IsNullOrWhiteSpace(recipe.UnlockHintId)
                    ? Localization.Get(recipe.UnlockHintId)
                    : StatusCopy(availability);
            _status.color = availability == ApothecaryRuntime.CraftResult.Prepared ? Success :
                availability == ApothecaryRuntime.CraftResult.MissingIngredients ? InkMuted :
                new Color(.58f, .25f, .18f, 1f);
            BuildSteps(recipe);
            RefreshAction(recipe, availability);
        }

        private string IngredientCopy(PreparationRecipeData recipe)
        {
            var lines = new List<string>();
            for (int i = 0; i < recipe.Ingredients.Length; i++)
            {
                var species = recipe.Ingredients[i];
                int owned = species != null ? InventoryRuntime.GetCount(species.Id) : 0;
                int needed = recipe.Amounts[i];
                bool known = species != null && MushroomDiscovery.IsDiscovered(species.Id);
                string name = known && species != null ? JournalText.MushroomName(species) :
                    Localization.Get("journal.field.unknown");
                string mark = owned >= needed && known
                    ? "<color=#416E43><sprite name=\"ui_check\"></color>"
                    : "<color=#8B493C>○</color>";
                lines.Add(string.Format("{0}  {1}  <color=#665A47>{2}/{3}</color>",
                    mark, name, owned, needed));
            }
            return string.Join("\n", lines);
        }

        private string StatusCopy(ApothecaryRuntime.CraftResult result)
        {
            switch (result)
            {
                case ApothecaryRuntime.CraftResult.Prepared:
                    return _preparing ? Localization.Get("apothecary.status.working") :
                        Localization.Get("apothecary.status.ready_detail");
                case ApothecaryRuntime.CraftResult.SpeciesUnidentified:
                    return Localization.Get("apothecary.status.unidentified_detail");
                case ApothecaryRuntime.CraftResult.MissingIngredients:
                    return Localization.Get("apothecary.status.missing_detail");
                case ApothecaryRuntime.CraftResult.RecipeLocked:
                    return Localization.Get("apothecary.status.locked_detail");
                default:
                    return Localization.Get("apothecary.status.unavailable");
            }
        }

        private void BuildSteps(PreparationRecipeData recipe)
        {
            for (int i = _stepsRoot.childCount - 1; i >= 0; i--)
                Destroy(_stepsRoot.GetChild(i).gameObject);
            _stepDots.Clear();
            _stepLabels.Clear();
            string[] steps = recipe.StepIds ?? new string[0];
            int count = Mathf.Max(1, steps.Length);
            for (int i = 0; i < count; i++)
            {
                bool done = i < _completedSteps;
                bool active = _preparing && i == _completedSteps;
                RectTransform row = NewFixed("Step_" + i, _stepsRoot, new Vector2(470f, 31f),
                    new Vector2(0f, 50f - i * 37f));
                Image dot = NewFixed("Dot", row, new Vector2(20f, 20f), new Vector2(-220f, 0f))
                    .gameObject.AddComponent<Image>();
                dot.sprite = UICanvasUtil.RoundedRect(10);
                dot.type = Image.Type.Sliced;
                dot.color = done ? Success : active ? Gold : new Color(.36f, .33f, .27f, .28f);
                TMP_Text label = UICanvasUtil.NewBody("Label", row,
                    Localization.Get(steps.Length > i ? steps[i] : "apothecary.step.prepare"), 20f,
                    done ? ForestRaised : active ? Ink : InkMuted, done ? FontStyles.Bold : FontStyles.Normal,
                    TextAlignmentOptions.Left);
                UICanvasUtil.SetRect(label.rectTransform, new Vector2(.08f, 0f), new Vector2(1f, 1f),
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                _stepDots.Add(dot);
                _stepLabels.Add(label);
            }
        }

        private void RefreshAction(PreparationRecipeData recipe,
            ApothecaryRuntime.CraftResult availability)
        {
            if (_showingSuccess) return;
            int stepCount = Mathf.Max(1, recipe.StepIds?.Length ?? 0);
            if (availability != ApothecaryRuntime.CraftResult.Prepared && !_preparing)
            {
                _actionButton.interactable = false;
                _actionBackground.color = new Color(.30f, .28f, .24f, .38f);
                _actionLabel.color = InkMuted;
                _actionLabel.text = availability == ApothecaryRuntime.CraftResult.MissingIngredients
                    ? Localization.Get("apothecary.action.gather")
                    : availability == ApothecaryRuntime.CraftResult.SpeciesUnidentified
                        ? Localization.Get("apothecary.action.identify")
                        : Localization.Get("apothecary.action.locked");
                return;
            }

            _actionButton.interactable = true;
            _actionBackground.color = Forest;
            _actionLabel.color = HollowfenPalette.Cream;
            int step = Mathf.Clamp(_completedSteps, 0, stepCount - 1);
            string stepName = Localization.Get(recipe.StepIds != null && recipe.StepIds.Length > step
                ? recipe.StepIds[step]
                : "apothecary.step.prepare");
            _actionLabel.text = string.Format(Localization.Get("apothecary.action.step"),
                step + 1, stepCount, stepName);
        }

        private void SetRecipeInteraction(bool enabled)
        {
            foreach (Button button in _recipeButtons) if (button != null) button.interactable = enabled;
        }

        private void WireNavigation()
        {
            for (int i = 0; i < _recipeButtons.Count; i++)
            {
                _recipeButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? _recipeButtons[i - 1] : _closeButton,
                    selectOnDown = i + 1 < _recipeButtons.Count ? _recipeButtons[i + 1] : _actionButton,
                    selectOnRight = _actionButton,
                };
            }
            _actionButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = _recipeButtons.Count > 0 ? _recipeButtons[_selected] : _closeButton,
                selectOnUp = _recipeButtons.Count > 0 ? _recipeButtons[_recipeButtons.Count - 1] : _closeButton,
                selectOnDown = _closeButton,
            };
            _closeButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _actionButton,
                selectOnDown = _recipeButtons.Count > 0 ? _recipeButtons[0] : _actionButton,
            };
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 92;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();

            GameObject scrim = UICanvasUtil.NewImage("Scrim", transform,
                new Color(.018f, .022f, .016f, .74f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);

            RectTransform card = NewFixed("FieldWorkbench", transform, new Vector2(1580f, 900f), Vector2.zero);
            UICanvasUtil.MakeRoundedPanel(card, Paper, 28, .65f);
            UICanvasUtil.AddShadow(card, 24, 32, .52f, -10f);

            TMP_Text eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card,
                Localization.Get("apothecary.eyebrow"), 20f, HollowfenPalette.PaperAccentInk,
                TextAlignmentOptions.Center);
            SetFixed(eyebrow.rectTransform, new Vector2(900f, 28f), new Vector2(0f, 420f));
            TMP_Text heading = UICanvasUtil.NewHeading("Heading", card,
                Localization.Get("apothecary.title"), 42f, Ink, FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(heading.rectTransform, new Vector2(1000f, 58f), new Vector2(0f, 378f));

            RectTransform left = NewFixed("RecipeBook", card, new Vector2(400f, 740f),
                new Vector2(-560f, -52f));
            UICanvasUtil.MakeRoundedPanel(left, PaperDeep, 20, .45f);
            TMP_Text ledger = UICanvasUtil.NewEyebrow("Ledger", left,
                Localization.Get("apothecary.recipes"), 20f, HollowfenPalette.PaperAccentInk,
                TextAlignmentOptions.Center);
            SetFixed(ledger.rectTransform, new Vector2(340f, 30f), new Vector2(0f, 330f));
            _recipeList = NewFixed("RecipeList", left, new Vector2(360f, 560f), new Vector2(0f, 0f));

            _closeButton = MakeButton("Close", left, new Vector2(270f, 48f), new Vector2(0f, -326f),
                Localization.Get("apothecary.close"), new Color(.17f, .14f, .10f, .10f), Ink);
            _closeButton.onClick.AddListener(Close);

            RectTransform detail = NewFixed("PreparationPage", card, new Vector2(1080f, 740f),
                new Vector2(220f, -52f));
            UICanvasUtil.MakeRoundedPanel(detail, new Color(.965f, .935f, .86f, 1f), 20, .42f);

            RectTransform plate = NewFixed("JournalPlate", detail, new Vector2(430f, 315f),
                new Vector2(-293f, 170f));
            UICanvasUtil.MakeRoundedPanel(plate, new Color(.12f, .105f, .085f, 1f), 14, .3f);
            RectTransform imageRect = NewFixed("Illustration", plate, new Vector2(404f, 287f), Vector2.zero);
            _heroImage = imageRect.gameObject.AddComponent<Image>();
            _heroImage.color = Color.white;
            _heroImage.raycastTarget = false;
            _heroLock = UICanvasUtil.NewBody("SealedPlate", plate,
                Localization.Get("apothecary.plate.sealed"), 20f,
                new Color(.86f, .79f, .64f, .88f), FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(_heroLock.rectTransform, new Vector2(330f, 120f), Vector2.zero);

            _kind = UICanvasUtil.NewEyebrow("Kind", detail, "", 20f,
                HollowfenPalette.PaperAccentInk, TextAlignmentOptions.Left);
            SetFixed(_kind.rectTransform, new Vector2(510f, 26f), new Vector2(246f, 315f));
            _title = UICanvasUtil.NewHeading("RecipeTitle", detail, "", 36f, Ink,
                FontStyles.Italic, TextAlignmentOptions.TopLeft);
            SetFixed(_title.rectTransform, new Vector2(510f, 90f), new Vector2(246f, 244f));
            _summary = UICanvasUtil.NewBody("Summary", detail, "", 20f, InkMuted,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            SetFixed(_summary.rectTransform, new Vector2(510f, 142f), new Vector2(246f, 125f));
            _stock = UICanvasUtil.NewBody("Stock", detail, "", 20f, ForestRaised,
                FontStyles.Bold, TextAlignmentOptions.Left);
            SetFixed(_stock.rectTransform, new Vector2(510f, 34f), new Vector2(246f, 42f));

            TMP_Text ingredientHead = UICanvasUtil.NewEyebrow("IngredientHead", detail,
                Localization.Get("apothecary.ingredients"), 20f, HollowfenPalette.PaperAccentInk);
            SetFixed(ingredientHead.rectTransform, new Vector2(440f, 28f), new Vector2(-290f, -22f));
            _ingredients = UICanvasUtil.NewBody("Ingredients", detail, "", 20f, Ink,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _ingredients.lineSpacing = 9f;
            SetFixed(_ingredients.rectTransform, new Vector2(440f, 154f), new Vector2(-290f, -112f));

            TMP_Text stepsHead = UICanvasUtil.NewEyebrow("StepsHead", detail,
                Localization.Get("apothecary.method"), 20f, HollowfenPalette.PaperAccentInk);
            SetFixed(stepsHead.rectTransform, new Vector2(480f, 28f), new Vector2(260f, -22f));
            _stepsRoot = NewFixed("Steps", detail, new Vector2(490f, 175f), new Vector2(260f, -118f));

            _status = UICanvasUtil.NewBody("Status", detail, "", 20f, InkMuted,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(_status.rectTransform, new Vector2(930f, 46f), new Vector2(0f, -244f));

            _actionButton = MakeButton("Action", detail, new Vector2(560f, 62f),
                new Vector2(0f, -305f), "", Forest, HollowfenPalette.Cream);
            _actionBackground = _actionButton.targetGraphic as Image;
            _actionLabel = _actionButton.GetComponentInChildren<TMP_Text>();
            _actionButton.onClick.AddListener(PerformNextStep);

            TMP_Text disclaimer = UICanvasUtil.NewBody("Disclaimer", detail,
                Localization.Get("apothecary.disclaimer"), 20f, InkMuted, FontStyles.Italic,
                TextAlignmentOptions.Center);
            SetFixed(disclaimer.rectTransform, new Vector2(940f, 28f), new Vector2(0f, -350f));

            _successBadge = NewFixed("Success", detail, new Vector2(590f, 210f), new Vector2(0f, 30f));
            UICanvasUtil.MakeRoundedPanel(_successBadge, new Color(.94f, .90f, .78f, .98f), 24, .55f);
            _successCheck = UICanvasUtil.NewHeading("Check", _successBadge,
                "<sprite name=\"ui_check\">", 82f, Success,
                FontStyles.Bold, TextAlignmentOptions.Center);
            SetFixed(_successCheck.rectTransform, new Vector2(100f, 90f), new Vector2(0f, 48f));
            _successText = UICanvasUtil.NewHeading("SuccessText", _successBadge, "", 27f, Ink,
                FontStyles.Italic, TextAlignmentOptions.Center);
            SetFixed(_successText.rectTransform, new Vector2(520f, 70f), new Vector2(0f, -45f));
            _successBadge.gameObject.SetActive(false);
        }

        private Button MakeButton(string name, Transform parent, Vector2 size, Vector2 position,
            string text, Color background, Color foreground)
        {
            RectTransform rt = NewFixed(name, parent, size, position);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(14);
            image.type = Image.Type.Sliced;
            image.color = background;
            TMP_Text label = UICanvasUtil.NewBody("Label", rt, text, 20f, foreground,
                FontStyles.Bold, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, .12f);
            colors.selectedColor = Color.Lerp(background, Gold, .18f);
            colors.pressedColor = Color.Lerp(background, Color.black, .10f);
            colors.disabledColor = new Color(.30f, .28f, .24f, .38f);
            button.colors = colors;
            return button;
        }

        private static RectTransform NewFixed(string name, Transform parent, Vector2 size,
            Vector2 position)
        {
            RectTransform rt = UICanvasUtil.NewRect(name, parent);
            SetFixed(rt, size, position);
            return rt;
        }

        private static void SetFixed(RectTransform rt, Vector2 size, Vector2 position)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
