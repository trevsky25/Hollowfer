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
        }

        public override void OnOpen()
        {
            // Continue stays interactable while we don't yet have real save state;
            // it doubles as the "enter gameplay" path for testing the cross-scene
            // pause flow. Re-enable HasAnySave gating once real save data lands.
            if (_continueButton != null) _continueButton.interactable = true;
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
            Debug.Log("[MainMenu] Continue → loading gameplay scene");
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
