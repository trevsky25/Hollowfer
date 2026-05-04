using Hollowfen.Input;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    public class SettingsScreen : UIScreen
    {
        public enum Tab { Audio = 0, Graphics = 1, Controls = 2 }

        [Header("Tabs")]
        [SerializeField] private Button _audioTabButton;
        [SerializeField] private Button _graphicsTabButton;
        [SerializeField] private Button _controlsTabButton;
        [SerializeField] private GameObject _audioPanel;
        [SerializeField] private GameObject _graphicsPanel;
        [SerializeField] private GameObject _controlsPanel;

        [Header("Audio")]
        [SerializeField] private AudioMixer _audioMixer;
        [SerializeField] private Slider _masterSlider;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Slider _sfxSlider;

        [Header("Per-tab default focus")]
        [SerializeField] private GameObject _audioDefaultSelected;
        [SerializeField] private GameObject _graphicsDefaultSelected;
        [SerializeField] private GameObject _controlsDefaultSelected;

        private const string PrefMaster = "audio.master";
        private const string PrefMusic  = "audio.music";
        private const string PrefSFX    = "audio.sfx";
        private const float DefaultVolume = 0.8f;

        private InputActions _input;
        private Tab _currentTab = Tab.Audio;

        public override GameObject DefaultSelected
        {
            get
            {
                switch (_currentTab)
                {
                    case Tab.Graphics: return _graphicsDefaultSelected != null ? _graphicsDefaultSelected : base.DefaultSelected;
                    case Tab.Controls: return _controlsDefaultSelected != null ? _controlsDefaultSelected : base.DefaultSelected;
                    default:           return _audioDefaultSelected != null    ? _audioDefaultSelected    : base.DefaultSelected;
                }
            }
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            _input = new InputActions();
            _input.UI.TabLeft.performed  += OnTabLeftInput;
            _input.UI.TabRight.performed += OnTabRightInput;

            if (_audioTabButton    != null) _audioTabButton.onClick.AddListener(SwitchToAudio);
            if (_graphicsTabButton != null) _graphicsTabButton.onClick.AddListener(SwitchToGraphics);
            if (_controlsTabButton != null) _controlsTabButton.onClick.AddListener(SwitchToControls);

            if (_masterSlider != null) _masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (_musicSlider  != null) _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (_sfxSlider    != null) _sfxSlider.onValueChanged.AddListener(OnSFXChanged);

            float master = PlayerPrefs.GetFloat(PrefMaster, DefaultVolume);
            float music  = PlayerPrefs.GetFloat(PrefMusic,  DefaultVolume);
            float sfx    = PlayerPrefs.GetFloat(PrefSFX,    DefaultVolume);

            if (_masterSlider != null) _masterSlider.SetValueWithoutNotify(master);
            if (_musicSlider  != null) _musicSlider.SetValueWithoutNotify(music);
            if (_sfxSlider    != null) _sfxSlider.SetValueWithoutNotify(sfx);

            ApplyVolume("MasterVolume", master);
            ApplyVolume("MusicVolume",  music);
            ApplyVolume("SFXVolume",    sfx);

            ApplyTab();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (_input != null) _input.UI.Enable();
            ApplyTab();
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
                _input.UI.TabLeft.performed  -= OnTabLeftInput;
                _input.UI.TabRight.performed -= OnTabRightInput;
                _input.Dispose();
                _input = null;
            }
        }

        private void OnTabLeftInput(UnityEngine.InputSystem.InputAction.CallbackContext _)  => SwitchTab((Tab)(((int)_currentTab + 2) % 3));
        private void OnTabRightInput(UnityEngine.InputSystem.InputAction.CallbackContext _) => SwitchTab((Tab)(((int)_currentTab + 1) % 3));

        private void SwitchToAudio()    => SwitchTab(Tab.Audio);
        private void SwitchToGraphics() => SwitchTab(Tab.Graphics);
        private void SwitchToControls() => SwitchTab(Tab.Controls);

        private void SwitchTab(Tab tab)
        {
            _currentTab = tab;
            ApplyTab();

            var es = EventSystem.current;
            if (es != null)
            {
                es.SetSelectedGameObject(null);
                es.SetSelectedGameObject(DefaultSelected);
            }
        }

        private void ApplyTab()
        {
            if (_audioPanel    != null) _audioPanel.SetActive(_currentTab == Tab.Audio);
            if (_graphicsPanel != null) _graphicsPanel.SetActive(_currentTab == Tab.Graphics);
            if (_controlsPanel != null) _controlsPanel.SetActive(_currentTab == Tab.Controls);
        }

        private void OnMasterChanged(float v) { PlayerPrefs.SetFloat(PrefMaster, v); ApplyVolume("MasterVolume", v); }
        private void OnMusicChanged(float v)  { PlayerPrefs.SetFloat(PrefMusic,  v); ApplyVolume("MusicVolume",  v); }
        private void OnSFXChanged(float v)    { PlayerPrefs.SetFloat(PrefSFX,    v); ApplyVolume("SFXVolume",    v); }

        private void ApplyVolume(string param, float linear)
        {
            if (_audioMixer == null) return;
            float db = linear > 0.0001f ? Mathf.Log10(linear) * 20f : -80f;
            _audioMixer.SetFloat(param, db);
        }
    }
}
