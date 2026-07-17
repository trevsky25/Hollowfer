using System.Collections.Generic;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.Input;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hollowfen.Cultivation
{
    /// <summary>
    /// Lightweight, runtime-built recipe picker shared by every grow bed. Recipes live on the
    /// mushroom data itself, so adding a crop needs no new UI branch or bed-specific script.
    /// </summary>
    public sealed class CultivationScreen : MonoBehaviour
    {
        private static MushroomFieldGuideDatabase _database;
        public static CultivationScreen Instance { get; private set; }

        private readonly List<GameObject> _optionRoots = new List<GameObject>();
        private readonly List<Button> _optionButtons = new List<Button>();
        private Canvas _canvas;
        private CanvasGroup _group;
        private RectTransform _optionsRoot;
        private Button _closeButton;
        private InputActions _input;
        private GrowBed _bed;
        private float _previousTimeScale;
        private CursorLockMode _previousCursorLock;
        private bool _previousCursorVisible;

        public bool IsOpen => _canvas != null && _canvas.enabled;

        public static CultivationScreen Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("_CultivationScreen");
            return go.AddComponent<CultivationScreen>();
        }

        public static MushroomFieldGuideData ResolveSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureDatabase();
            if (_database?.Entries == null) return null;
            foreach (var species in _database.Entries)
                if (species != null && species.Id == id) return species;
            return null;
        }

        public static bool HasPlantableSpecies()
        {
            EnsureDatabase();
            if (_database?.Entries == null) return false;
            foreach (var species in _database.Entries)
                if (species != null && MushroomRules.CanCultivate(species) &&
                    species.WorldPrefab != null && InventoryRuntime.GetCount(species.Id) > 0)
                    return true;
            return false;
        }

        private static void EnsureDatabase()
        {
            if (_database == null)
                _database = Resources.Load<MushroomFieldGuideDatabase>("MushroomFieldGuideDatabase");
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
            if (_input != null)
            {
                _input.UI.Cancel.performed -= OnCancel;
                _input.UI.Disable();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _input?.Dispose();
        }

        public void Open(GrowBed bed)
        {
            if (bed == null || IsOpen) return;
            _bed = bed;
            RefreshOptions();
            if (_optionButtons.Count == 0) { _bed = null; return; }

            _previousTimeScale = Time.timeScale;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
            _canvas.enabled = true;
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            EventSystem.current?.SetSelectedGameObject(_optionButtons[0].gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _canvas.enabled = false;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _bed = null;
            Time.timeScale = _previousTimeScale;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void Choose(MushroomFieldGuideData species)
        {
            var bed = _bed;
            Close();
            bed?.Plant(species);
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            if (IsOpen) Close();
        }

        private void RefreshOptions()
        {
            foreach (var root in _optionRoots) if (root != null) Destroy(root);
            _optionRoots.Clear();
            _optionButtons.Clear();
            EnsureDatabase();
            if (_database?.Entries == null) return;

            var candidates = new List<MushroomFieldGuideData>();
            foreach (var species in _database.Entries)
            {
                if (species == null || species.WorldPrefab == null || !MushroomRules.CanCultivate(species)) continue;
                if (InventoryRuntime.GetCount(species.Id) <= 0) continue;
                candidates.Add(species);
            }
            candidates.Sort((a, b) => a.Tier != b.Tier
                ? a.Tier.CompareTo(b.Tier)
                : string.CompareOrdinal(a.CommonName, b.CommonName));

            int shown = Mathf.Min(6, candidates.Count);
            for (int i = 0; i < shown; i++)
            {
                var species = candidates[i];
                var button = BuildOption(species, i);
                var selected = species;
                button.onClick.AddListener(() => Choose(selected));
                _optionButtons.Add(button);
                _optionRoots.Add(button.gameObject);
            }
            WireNavigation();
        }

        private Button BuildOption(MushroomFieldGuideData species, int index)
        {
            var rt = UICanvasUtil.NewRect("Recipe_" + species.Id, _optionsRoot);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(720f, 68f);
            rt.anchoredPosition = new Vector2(0f, -index * 78f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(12);
            image.type = Image.Type.Sliced;
            image.color = new Color(0.18f, 0.15f, 0.10f, 0.08f);

            var title = UICanvasUtil.NewHeading("Name", rt, species.CommonName, 23f,
                HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.46f, 1f),
                new Vector2(0f, 0.5f), new Vector2(-32f, 0f), new Vector2(16f, 0f));

            string details = string.Format(Localization.Get("cultivation.recipe.details"),
                InventoryRuntime.GetCount(species.Id), species.CultivationHours, species.CultivationYield);
            var stats = UICanvasUtil.NewBody("Details", rt, details, 16f,
                new Color(0.26f, 0.22f, 0.16f, 0.82f), FontStyles.Normal, TextAlignmentOptions.Right);
            UICanvasUtil.SetRect(stats.rectTransform, new Vector2(0.44f, 0f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(-16f, 0f));

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.58f, 0.44f, 0.16f, 0.22f);
            colors.selectedColor = new Color(0.58f, 0.44f, 0.16f, 0.26f);
            colors.pressedColor = new Color(0.58f, 0.44f, 0.16f, 0.34f);
            button.colors = colors;
            return button;
        }

        private void WireNavigation()
        {
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                _optionButtons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? _optionButtons[i - 1] : _closeButton,
                    selectOnDown = i + 1 < _optionButtons.Count ? _optionButtons[i + 1] : _closeButton,
                };
            }
            _closeButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnDown = _optionButtons.Count > 0 ? _optionButtons[0] : null,
                selectOnUp = _optionButtons.Count > 0 ? _optionButtons[_optionButtons.Count - 1] : null,
            };
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>();
            _canvas = canvas;
            _group = gameObject.AddComponent<CanvasGroup>();

            var scrim = UICanvasUtil.NewImage("Scrim", transform, new Color(0.02f, 0.018f, 0.014f, 0.82f), true);
            UICanvasUtil.Stretch((RectTransform)scrim.transform);
            var card = UICanvasUtil.NewRect("Card", transform);
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(860f, 650f);
            UICanvasUtil.MakeRoundedPanel(card, new Color(0.90f, 0.86f, 0.76f, 1f), 24, 0.7f);
            UICanvasUtil.AddShadow(card, 22, 30, 0.5f, -10f);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", card, Localization.Get("cultivation.eyebrow"),
                14f, HollowfenPalette.Gold, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-100f, 24f), new Vector2(0f, -36f));
            var title = UICanvasUtil.NewHeading("Title", card, Localization.Get("cultivation.title"),
                38f, HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(-100f, 52f), new Vector2(0f, -64f));

            _optionsRoot = UICanvasUtil.NewRect("Options", card);
            UICanvasUtil.SetRect(_optionsRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(720f, 390f), new Vector2(0f, -142f));

            _closeButton = BuildClose(card);
            _closeButton.onClick.AddListener(Close);
        }

        private Button BuildClose(RectTransform parent)
        {
            var rt = UICanvasUtil.NewRect("Close", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(220f, 48f);
            rt.anchoredPosition = new Vector2(0f, 28f);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = UICanvasUtil.RoundedRect(12);
            image.type = Image.Type.Sliced;
            image.color = new Color(0.18f, 0.15f, 0.10f, 0.08f);
            var label = UICanvasUtil.NewBody("Label", rt, Localization.Get("cultivation.cancel"), 18f,
                HollowfenPalette.InkDeep, FontStyles.Italic, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(label.rectTransform);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
