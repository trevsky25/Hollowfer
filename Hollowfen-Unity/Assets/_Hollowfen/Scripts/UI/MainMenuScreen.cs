using Hollowfen.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class MainMenuScreen : UIScreen
    {
        [SerializeField] private Button _newGameButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _storyButton;
        [SerializeField] private Button _wrenButton;
        [SerializeField] private Button _fieldGuideButton;

        public override GameObject DefaultSelected =>
            HasAnySave() && _continueButton != null
                ? _continueButton.gameObject
                : (_newGameButton != null ? _newGameButton.gameObject : base.DefaultSelected);

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_newGameButton  != null) _newGameButton.onClick.AddListener(OnNewGame);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinue);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_creditsButton  != null) _creditsButton.onClick.AddListener(OnCredits);
            if (_quitButton     != null) _quitButton.onClick.AddListener(OnQuit);
            if (_storyButton      != null) _storyButton.onClick.AddListener(OnStory);
            if (_wrenButton       != null) _wrenButton.onClick.AddListener(OnWren);
            if (_fieldGuideButton != null) _fieldGuideButton.onClick.AddListener(OnFieldGuide);
        }

        private void OnStory()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("story");
        }

        private void OnWren()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("wren");
        }

        private void OnFieldGuide()
        {
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("field-guide");
        }

        public override void OnOpen()
        {
            if (_continueButton != null) _continueButton.interactable = HasAnySave();
        }

        private const string GameplaySceneName = "Scene_Hollowfen";

        private void OnNewGame()
        {
            Debug.Log("[MainMenu] New Game");
            GameEvents.TriggerAchievement("ACH_NEWGAME_FIRST");
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("save-slot");
        }

        private void OnContinue()
        {
            int slot = Hollowfen.Save.SaveCoordinator.MostRecentSlot();
            if (slot < 0) return;
            Debug.Log($"[MainMenu] Continue → loading slot {slot}");
            Hollowfen.Save.SaveCoordinator.LoadSlot(slot);
            if (UIManager.Instance != null)
                UIManager.Instance.LoadSceneAndOpen(GameplaySceneName);
        }

        private void OnSettings()
        {
            Debug.Log("[MainMenu] Settings");
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
        }

        private void OnCredits()
        {
            Debug.Log("[MainMenu] Credits");
            // TODO: open credits screen
        }

        private void OnQuit()
        {
            ConfirmModal.Show(
                Localization.Get("ui.menu.quit_title"),
                Localization.Get("ui.menu.quit_message"),
                onConfirm: PerformQuit);
        }

        private static void PerformQuit()
        {
            Debug.Log("[MainMenu] Quit confirmed");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static bool HasAnySave()
        {
            for (int i = 0; i < SaveManager.TotalSlots; i++)
                if (SaveManager.SlotHasData(i)) return true;
            return false;
        }
    }
}
