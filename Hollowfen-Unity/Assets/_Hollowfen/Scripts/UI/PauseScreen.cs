using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class PauseScreen : UIScreen
    {
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _saveGameButton;
        [SerializeField] private Button _quitButton;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            if (_resumeButton   != null) _resumeButton.onClick.AddListener(OnResume);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_saveGameButton != null) _saveGameButton.onClick.AddListener(OnSaveGame);
            if (_quitButton     != null) _quitButton.onClick.AddListener(OnQuit);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Time.timeScale = 0f;
        }

        public override void OnClose()
        {
            base.OnClose();
            Time.timeScale = 1f;
        }

        private void OnResume()
        {
            Debug.Log("[Pause] Resume");
            if (UIManager.Instance != null) UIManager.Instance.Back();
        }

        private void OnSettings()
        {
            Debug.Log("[Pause] Settings");
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
        }

        private void OnSaveGame()
        {
            Debug.Log("[Pause] Save Game");
            // TODO: real save flow when game state lands.
        }

        private const string MainMenuSceneName = "Scene_MainMenu";

        private void OnQuit()
        {
            ConfirmModal.Show(
                title:   Localization.Get("ui.pause.quit_title"),
                message: Localization.Get("ui.pause.quit_message"),
                onConfirm: () =>
                {
                    Debug.Log("[Pause] Quit to Main Menu confirmed");
                    Time.timeScale = 1f; // restore before scene load so the next scene starts unpaused
                    if (UIManager.Instance != null)
                        UIManager.Instance.LoadSceneAndOpen(MainMenuSceneName, "main-menu");
                });
        }
    }
}
