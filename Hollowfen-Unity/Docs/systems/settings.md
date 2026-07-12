# Game Settings & Audio
Runtime user preferences backed by PlayerPrefs: `Hollowfen.Settings.GameSettings` (static) is the single home for tunables; audio routes through an AudioMixer with exposed volume params bound to Settings UI sliders. SettingsScreen rebuilt to the code-built house idiom in batch-28 (TMP/Georgia, Localization.Get everywhere, ‹ › cyclers instead of dropdowns, PlayerPrefs.Save on close).
Key scripts: `Assets/_Hollowfen/Scripts/Settings/` — GameSettings, LookSensitivityHook; `UI/SettingsScreen.cs` (code-built). Audio asset: `Assets/_Hollowfen/Audio/MainMixer.mixer` (scene-serialized ref on the screen, alongside `_backgroundSprite` = the menu hero).
Exposed mixer params: `MasterVolume`, `MusicVolume`, `SFXVolume` (Master / Music / SFX groups); prefs keys unchanged (`audio.*`, `graphics.*`, `controls.lookSensitivity`).
Entry point: SettingsScreen tabs (see ui-framework.md) write PlayerPrefs + apply live; MainMenu's Credits button opens it pre-switched via `SettingsScreen.NextOpenTab`.
Biggest gotchas: look sensitivity uses a deliberately tight 0.75×–1.25× range (StarterAssets feeds raw mouse pixels into yaw); UIManager's PUSH path deactivates covered screens WITHOUT OnClose, so screen-level input handlers must gate on `TopScreen == this`; resolution prefs index the DEDUPED w×h list (stale raw-list indices are distrusted when they disagree with the actual screen); in the editor the game-view size never matches monitor modes, so the resolution cycler shows native there.
Status: shipped + verified (batch-28 play-verified: mixer dB math, prefs writes, cyclers, per-tab pad focus, Credits handoff).

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Settings inventory

| Setting | UI | Range | Notes |
|---|---|---|---|
| Master/Music/SFX volume | Settings → Audio sliders | mixer dB | `mixer.SetFloat`, persisted to PlayerPrefs, % readout. |
| Fullscreen | Settings → Graphics cycler | On/Off | `Screen.fullScreen`; localized value strings. |
| Resolution | Settings → Graphics cycler | deduped w×h list | `Screen.SetResolution(w, h, Screen.fullScreenMode)`; list deduped from `Screen.resolutions` (raw list repeats per refresh rate). |
| Quality | Settings → Graphics cycler | project levels | Row HIDDEN when the project defines <2 levels (currently 1: "PC") — a dead control shouldn't ship. Display names via `settings.quality.<name>` with raw fallback. |
| Look sensitivity | Settings → Controls slider (1–10, whole steps) | 0.75× … 1.25× | Two-segment lerp; slider 5 maps exactly to 1.0× (tested baseline). PlayerPrefs key `controls.lookSensitivity`. |

## SettingsScreen structure (batch-28)

Code-built in `OnInitialize` (StoryScreen idiom): Canvas on the host + scrim + left gradient over
the serialized hero sprite, editorial header (sage eyebrow / Georgia title / gold rule), text-nav
tab row (active = gold + underline; FocusHighlight = gold wash pill), per-tab panels, moss hint
footer. Controls tab carries the full binding reference table (UI / Player / Dialogue sections,
copy preserved verbatim); Credits tab renders the shipped credits copy with editorial hierarchy.
Gamepad: tabs row ↔ content column wired explicitly per tab; sliders adjust natively on
left/right; cyclers cycle on stick/d-pad left-right (axis-dominance guarded) or Submit; ‹ ›
arrow buttons are mouse-only (`Navigation.None` — clicking them doesn't steal pad focus).

## LookSensitivityHook

`_Hollowfen/Scripts/Settings/LookSensitivityHook.cs` lives on `PlayerArmature`. `[DefaultExecutionOrder(-100)]` so its `LateUpdate` runs before `ThirdPersonController.LateUpdate` (default 0); scales `StarterAssetsInputs.look *= GameSettings.LookSensitivity` in place each frame. No third-party StarterAssets code is touched — this is the pattern for all StarterAssets behavior modification.
