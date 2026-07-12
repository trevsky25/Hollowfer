# Game Settings & Audio
Runtime user preferences backed by PlayerPrefs: `Hollowfen.Settings.GameSettings` (static) is the single home for tunables; audio routes through an AudioMixer with exposed volume params bound to Settings UI sliders.
Key scripts: `Assets/_Hollowfen/Scripts/Settings/` — GameSettings, LookSensitivityHook. Audio asset: `Assets/_Hollowfen/Audio/MainMixer.mixer`.
Exposed mixer params: `MasterVolume`, `MusicVolume`, `SFXVolume` (Master / Music / SFX groups).
Entry point: SettingsScreen tabs (see ui-framework.md) write PlayerPrefs + apply live.
Biggest gotcha: look sensitivity uses a deliberately tight 0.75×–1.25× range — StarterAssets feeds raw mouse pixels into yaw, wider ranges break feel.
Status: shipped + verified.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Settings inventory

| Setting | UI | Range | Notes |
|---|---|---|---|
| Master/Music/SFX volume | Settings → Audio sliders | mixer dB | `mixer.SetFloat`, persisted to PlayerPrefs. |
| Fullscreen / resolution / quality | Settings → Graphics | — | Standard Unity APIs. |
| Look sensitivity | Settings → Controls slider (1–10) | 0.75× … 1.25× | Two-segment lerp; slider 5 maps exactly to 1.0× (tested baseline). PlayerPrefs key `controls.lookSensitivity`. |

## LookSensitivityHook

`_Hollowfen/Scripts/Settings/LookSensitivityHook.cs` lives on `PlayerArmature`. `[DefaultExecutionOrder(-100)]` so its `LateUpdate` runs before `ThirdPersonController.LateUpdate` (default 0); scales `StarterAssetsInputs.look *= GameSettings.LookSensitivity` in place each frame. No third-party StarterAssets code is touched — this is the pattern for all StarterAssets behavior modification.
