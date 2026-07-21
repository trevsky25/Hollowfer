using System;
using Hollowfen.Input;
using Hollowfen.Save;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class SaveSlotScreen : UIScreen
    {
        [SerializeField] private Button[]  _slotButtons = new Button[SaveManager.TotalSlots];
        // Migrated legacy uGUI Text → TMP (batch-58) so slot rows pick up the EBGaramond body font.
        [SerializeField] private TMP_Text[] _slotLabels = new TMP_Text[SaveManager.TotalSlots];
        [SerializeField] private TMP_Text[] _slotMetas  = new TMP_Text[SaveManager.TotalSlots];

        private InputActions _input;
        private Button _closeButton;
        private RectTransform _presentationRoot;
        private const float LegacyCanvasScale = 1.423f;

        public override GameObject DefaultSelected
        {
            get
            {
                int firstEmpty = -1;
                for (int i = 0; i < _slotButtons.Length; i++)
                {
                    if (_slotButtons[i] == null) continue;
                    if (!SaveManager.SlotHasData(i)) { firstEmpty = i; break; }
                }
                int idx = firstEmpty >= 0 ? firstEmpty : 0;
                return _slotButtons[idx] != null ? _slotButtons[idx].gameObject : base.DefaultSelected;
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _input = new InputActions();
            _input.UI.Delete.performed += OnDeleteInput;

            for (int i = 0; i < _slotButtons.Length; i++)
            {
                if (_slotButtons[i] == null) continue;
                int slot = i;
                _slotButtons[i].onClick.AddListener(() => OnSlotSelected(slot));
                // The scene-authored slot rows are plain square Images — round them into the
                // design system at init (batch-47 square sweep; legacy TMP-migration pending).
                var rowImg = _slotButtons[i].GetComponent<UnityEngine.UI.Image>();
                if (rowImg != null) UICanvasUtil.Roundify(rowImg, 16);
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                LocalizeSceneCopy(canvas);
                NormalizeLegacyCanvas(canvas);
                _closeButton = JournalChrome.BuildCloseButton(_presentationRoot != null
                    ? _presentationRoot : canvas.transform, () =>
                {
                    if (UIManager.Instance != null) UIManager.Instance.Back();
                });
                WireNavigation();
            }
            else
            {
                Debug.LogError("[SaveSlotScreen] Missing child Canvas; close control could not be built.");
            }
        }

        private static void LocalizeSceneCopy(Canvas canvas)
        {
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "Title") label.text = Localization.Get("save.title");
                else if (label.name == "Footer")
                {
                    label.text = Localization.Get("save.footer");
                    label.fontSize = 18f;
                }
            }
        }

        private void NormalizeLegacyCanvas(Canvas canvas)
        {
            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null) return;

            // Preserve the approved 1280×800 journal composition while putting its canvas onto
            // the shared 1920×1080 scaling contract. A centered presentation root provides the
            // exact inverse scale, including at non-16:9 resolutions, without resizing every
            // scene-authored row by hand.
            var existing = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < canvas.transform.childCount; i++)
                existing.Add(canvas.transform.GetChild(i));

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.Init1080();
            _presentationRoot = UICanvasUtil.NewRect("SaveSlotPresentation", canvas.transform);
            // These scene-authored children were laid out against an explicit 1280×800 canvas.
            // Unity does not refresh canvasRect.rect synchronously when a scaler changes during
            // Awake, so deriving this size here intermittently shrank the whole screen to 70%.
            _presentationRoot.sizeDelta = new Vector2(1280f, 800f);
            _presentationRoot.localScale = Vector3.one * LegacyCanvasScale;

            foreach (Transform child in existing)
                if (child != null) child.SetParent(_presentationRoot, false);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_input != null) _input.UI.Enable();
            RefreshDisplay();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (_input != null) _input.UI.Disable();
        }

        protected void OnDestroy()
        {
            if (_input != null)
            {
                _input.UI.Delete.performed -= OnDeleteInput;
                _input.Dispose();
                _input = null;
            }
        }

        private void RefreshDisplay()
        {
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                // Autosaves follow the ACTIVE slot (see systems/save.md), so no slot is "the"
                // autosave — neutral journal naming instead (decision: QUESTIONS.md Q2, 2026-07-11).
                if (_slotLabels[i] != null)
                    _slotLabels[i].text = string.Format(Localization.Get("save.slot.label"), i + 1);

                if (_slotMetas[i] == null) continue;

                var inspection = SaveManager.InspectSlot(i);
                if (inspection.CanLoad)
                {
                    var meta = inspection.Meta;
                    if (meta != null)
                    {
                        var play = TimeSpan.FromSeconds(Mathf.Max(0f, meta.TotalPlayTimeSeconds));
                        DateTime date;
                        try { date = DateTimeOffset.FromUnixTimeSeconds(meta.TimestampUnix).LocalDateTime; }
                        catch (ArgumentOutOfRangeException) { date = DateTime.MinValue; }
                        string dateLabel = date == DateTime.MinValue
                            ? Localization.Get("save.date.unknown")
                            : date.ToString(Localization.Get("save.date.format"));
                        _slotMetas[i].text = string.Format(
                            Localization.Get("save.slot.meta"),
                            ResolveQuestLabel(meta),
                            meta.CurrentAct,
                            (int)play.TotalHours, play.Minutes,
                            dateLabel);
                    }
                    else _slotMetas[i].text = Localization.Get("save.corrupt.short");
                }
                else if (inspection.Status == SaveInspectionStatus.Corrupt)
                    _slotMetas[i].text = Localization.Get("save.corrupt.body");
                else if (inspection.Status == SaveInspectionStatus.IncompatibleNewerVersion)
                    _slotMetas[i].text = Localization.Get("save.newer_version");
                else
                    _slotMetas[i].text = Localization.Get("save.new_game");
            }
        }

        private static string ResolveQuestLabel(SaveSlotMeta meta)
        {
            return SaveQuestIdentity.ResolveDisplayName(meta);
        }

        private const string GameplaySceneName = "Scene_Hollowfen";

        private void WireNavigation()
        {
            Button first = null;
            Button previous = null;
            Button beforePrevious = null;
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                var current = _slotButtons[i];
                if (current == null) continue;
                if (first == null) first = current;
                if (previous != null)
                    JournalChrome.SetNavigation(previous, beforePrevious != null ? beforePrevious : _closeButton, current);
                beforePrevious = previous;
                previous = current;
            }

            if (previous != null)
                JournalChrome.SetNavigation(previous, beforePrevious != null ? beforePrevious : _closeButton, null);
            if (_closeButton != null && first != null)
                JournalChrome.SetNavigation(_closeButton, previous, first, first, null);
        }

        private void OnSlotSelected(int slot)
        {
            var inspection = SaveManager.InspectSlot(slot);
            bool newGame = inspection.Status == SaveInspectionStatus.Empty;
            if (newGame)
            {
                Debug.Log($"[SaveSlot] Start new game in slot {slot}");
                GameEvents.TriggerAchievement("ACH_NEWGAME_FIRST");   // moved from the menu (batch-60)
                SaveCoordinator.StartNewGame(slot);
            }
            else
            {
                if (!inspection.CanLoad)
                {
                    UISfx.Error();
                    string message = inspection.Status == SaveInspectionStatus.IncompatibleNewerVersion
                        ? Localization.Get("save.unavailable.newer")
                        : Localization.Get("save.unavailable.corrupt");
                    ConfirmModal.Show(Localization.Get("save.unavailable.title"), message, () => { });
                    return;
                }
                Debug.Log($"[SaveSlot] Load slot {slot}");
                if (!SaveCoordinator.TryLoadSlot(slot, out inspection))
                {
                    UISfx.Error();
                    return;
                }
            }
            // New game gets the cinematic welcome→intro handoff (batch-38); load/continue gets the SAME
            // cinematic welcome card but fades to the game (no seamless handoff) — batch-50.
            LoadingScreen.NextIsCinematic = newGame;
            LoadingScreen.NextIsContinue = !newGame;
            if (UIManager.Instance != null)
                UIManager.Instance.LoadSceneAndOpen(GameplaySceneName, null, newGame);
        }

        private void OnDeleteInput(InputAction.CallbackContext ctx)
        {
            var es = EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return;

            int slot = -1;
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                if (_slotButtons[i] != null && _slotButtons[i].gameObject == es.currentSelectedGameObject)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0 || !SaveManager.SlotHasData(slot)) return;

            int slotCopy = slot;
            ConfirmModal.Show(
                title: Localization.Get("save.delete.title"),
                message: string.Format(Localization.Get("save.delete.message"), slot + 1),
                onConfirm: () =>
                {
                    SaveManager.DeleteSlot(slotCopy);
                    Debug.Log($"[SaveSlot] Deleted slot {slotCopy}");
                    RefreshDisplay();
                });
        }
    }
}
