using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class MainMenuScreen : UIScreen
    {
        // The hero "Forage" button opens the journal / save-slot picker (existing saves to continue +
        // empty slots to begin), so a separate Continue shortcut is redundant (removed batch-60). The
        // field keeps its serialized name (_newGameButton) to preserve the scene reference.
        [SerializeField] private Button _newGameButton;   // hero: "Forage →"
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _storyButton;
        [SerializeField] private Button _wrenButton;
        [SerializeField] private Button _fieldGuideButton;

        public override GameObject DefaultSelected =>
            _newGameButton != null ? _newGameButton.gameObject : base.DefaultSelected;

        protected override void OnInitialize()
        {
            base.OnInitialize();
            NormalizePointerTargets();
            if (_newGameButton  != null) _newGameButton.onClick.AddListener(OnForage);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
            if (_creditsButton  != null) _creditsButton.onClick.AddListener(OnCredits);
            if (_quitButton     != null) _quitButton.onClick.AddListener(OnQuit);
            if (_storyButton      != null) _storyButton.onClick.AddListener(OnStory);
            if (_wrenButton       != null) _wrenButton.onClick.AddListener(OnWren);
            if (_fieldGuideButton != null) _fieldGuideButton.onClick.AddListener(OnFieldGuide);
        }

        // The editorial navigation labels used to look generous but their actual raycast rects
        // collapsed to roughly six reference pixels because NavRow reserved 24 px of top padding
        // inside a 30 px layout slot. Give every label a production-sized pointer target while
        // preserving the same restrained text treatment.
        private void NormalizePointerTargets()
        {
            var navRow = transform.Find("Canvas/TextCard/NavRow");
            if (navRow != null)
            {
                var rowLayout = navRow.GetComponent<HorizontalLayoutGroup>();
                if (rowLayout != null) rowLayout.padding = new RectOffset(0, 0, 4, 4);
                var rowSize = navRow.GetComponent<LayoutElement>();
                if (rowSize != null) rowSize.preferredHeight = 56f;

                foreach (Button button in navRow.GetComponentsInChildren<Button>(true))
                {
                    foreach (LayoutElement size in button.GetComponents<LayoutElement>())
                    {
                        size.minHeight = Mathf.Max(size.minHeight, 48f);
                        size.preferredHeight = Mathf.Max(size.preferredHeight, 48f);
                    }
                }
            }

            if (_quitButton != null && _quitButton.transform is RectTransform quitRect)
                quitRect.sizeDelta = new Vector2(quitRect.sizeDelta.x, Mathf.Max(48f, quitRect.sizeDelta.y));
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
            // A queued Credits handoff that never consumed (e.g. OpenScreen dropped during
            // a transition) must not redirect the NEXT plain Settings open.
            SettingsScreen.NextOpenTab = null;
        }

        // Hero action: open the journal picker. Choose a saved journal to continue, or an empty slot
        // to begin. The "first new game" achievement fires in SaveSlotScreen when a new journal is
        // actually started (not merely on opening the picker).
        private void OnForage()
        {
            Debug.Log("[MainMenu] Forage → journal picker");
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("save-slot");
        }

        private void OnSettings()
        {
            Debug.Log("[MainMenu] Settings");
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
        }

        private void OnCredits()
        {
            Debug.Log("[MainMenu] Credits");
            SettingsScreen.NextOpenTab = SettingsScreen.Tab.Credits;
            if (UIManager.Instance != null) UIManager.Instance.OpenScreen("settings");
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
    }
}
