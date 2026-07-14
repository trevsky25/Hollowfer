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
                    _slotLabels[i].text = "Journal " + (i + 1);

                if (_slotMetas[i] == null) continue;

                if (SaveManager.SlotHasData(i))
                {
                    var meta = SaveManager.GetSlotMeta(i);
                    if (meta != null)
                    {
                        var play = TimeSpan.FromSeconds(Mathf.Max(0f, meta.TotalPlayTimeSeconds));
                        var date = DateTimeOffset.FromUnixTimeSeconds(meta.TimestampUnix).LocalDateTime;
                        _slotMetas[i].text = string.Format(
                            "{0}  ·  Act {1}  ·  {2:00}:{3:00}  ·  {4:yyyy-MM-dd}",
                            string.IsNullOrEmpty(meta.CurrentQuest) ? "—" : meta.CurrentQuest,
                            meta.CurrentAct,
                            (int)play.TotalHours, play.Minutes,
                            date);
                    }
                    else _slotMetas[i].text = "(corrupted)";
                }
                else _slotMetas[i].text = "New Game";
            }
        }

        private const string GameplaySceneName = "Scene_Hollowfen";

        private void OnSlotSelected(int slot)
        {
            bool newGame = !SaveManager.SlotHasData(slot);
            if (newGame)
            {
                Debug.Log($"[SaveSlot] Start new game in slot {slot}");
                SaveCoordinator.StartNewGame(slot);
            }
            else
            {
                Debug.Log($"[SaveSlot] Load slot {slot}");
                SaveCoordinator.LoadSlot(slot);
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
                title: "Delete Save?",
                message: "Journal " + (slot + 1) + " will be permanently deleted.",
                onConfirm: () =>
                {
                    SaveManager.DeleteSlot(slotCopy);
                    Debug.Log($"[SaveSlot] Deleted slot {slotCopy}");
                    RefreshDisplay();
                });
        }
    }
}
