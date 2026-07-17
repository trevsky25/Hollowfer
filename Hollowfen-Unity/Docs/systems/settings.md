# Game Settings & Audio
Runtime user preferences backed by PlayerPrefs: `Hollowfen.Settings.GameSettings` (static) is the single home for tunables; `ProductionPerformancePolicy` owns the build-wide 60fps/native-quality contract; audio routes through an AudioMixer with exposed volume params bound to Settings UI sliders. SettingsScreen rebuilt to the code-built house idiom in batch-28 (TMP, Localization.Get everywhere, ‹ › cyclers instead of dropdowns, PlayerPrefs.Save on close) and visually unified with the journal/menu surface system in batch-64.
Key scripts: `Assets/_Hollowfen/Scripts/Settings/` — GameSettings, LookSensitivityHook, ProductionPerformancePolicy; `UI/SettingsScreen.cs` (code-built). Audio asset: `Assets/_Hollowfen/Audio/MainMixer.mixer` (scene-serialized ref on the screen, alongside `_backgroundSprite` = the menu hero).
Exposed mixer params: `MasterVolume`, `MusicVolume`, `SFXVolume` (Master / Music / SFX groups); prefs keys `audio.*`, `graphics.*`, `controls.lookSensitivity`. **batch-57**: added an **Ambience** slider (Audio tab, 4th row; pref `audio.ambience`) — it is NOT a mixer param; it trims `AmbienceManager` at the source level via `SetUserVolume` (see audio.md for why source-level over a mixer node).
Entry point: SettingsScreen tabs (see ui-framework.md) write PlayerPrefs + apply live; MainMenu's Credits button opens it pre-switched via `SettingsScreen.NextOpenTab`.
Biggest gotchas: look sensitivity uses a deliberately tight 0.75×–1.25× range (StarterAssets feeds raw mouse pixels into yaw); UIManager's PUSH path deactivates covered screens WITHOUT OnClose, so screen-level input handlers must gate on `TopScreen == this`; resolution prefs index the DEDUPED w×h list (stale raw-list indices are distrusted when they disagree with the actual screen); in the editor the game-view size never matches monitor modes, so the resolution cycler shows native there; Unity ignores `Application.targetFrameRate` while VSync is active, so frame pacing must stay centralized in ProductionPerformancePolicy.
Status: shipped + verified through batch-91. The settings screen shares the menu close affordance, quiet row surfaces, and gold focus rail/wash language. Explicit navigation connects content → active tab → close → active tab. Graphics changes notify the production policy after Unity finishes switching display modes. The tagged main camera retains the production quality passes without sampling scene volume profiles, preventing the legacy vendor fantasy grade from tinting the world.

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

## Production performance policy (batch-89)

`ProductionPerformancePolicy` is bootstrapped before the first scene and survives scene loads. Its contract is:

- Target 60fps in Editor Game view and standalone players.
- Prefer hardware VSync on displays that are a clean 60Hz multiple: 60→1, 120→2, 180→3, 240→4. Re-sample at runtime because fullscreen, monitor, and refresh rate can change. Non-multiples such as 75/144/165Hz use Unity's 60fps software cap rather than drifting to a different frame target.
- Keep `OnDemandRendering.renderFrameInterval = 1`; never skip rendered frames.
- Preserve the selected quality level while forcing full anisotropic texture sampling.
- Configure only the tagged main camera: native scale, HDR, occlusion culling, SMAA High, dithering, NaN suppression, and shadows. The post pipeline stays active because URP runs SMAA/stop-NaN/dithering there, but the camera's volume layer mask is deliberately zero: Hollowfen keeps its sun/sky/ambient/fog palette instead of stacking the scene's `Medieval Fantasy Volume Profile` (warm gain/white balance, DOF, motion blur, and grain). Preview/Map/journal RenderTexture cameras retain their bespoke settings.
- Fullscreen, resolution, and quality changes call `RequestDisplayRefresh()` so frame pacing is restored after Unity completes the mode switch.

The PC URP asset remains the quality source of truth: render scale 1.0, full texture mipmaps, HDR, Forward+, four-cascade 2K high-quality soft shadows, reflection blending/box projection, full-resolution SSAO, SRP Batcher, and LOD bias 2. The policy deliberately does not inflate shadow maps or enable realtime reflection probes without a measured standalone frame-time budget.

## SettingsScreen structure (batch-28, visual polish batch-64)

Code-built in `OnInitialize` (StoryScreen idiom): Canvas on the host + scrim + left gradient over
the serialized hero sprite, editorial header (sage eyebrow / IM Fell English title / gold rule), shared
top-right close control, text-nav tab row (active = gold + underline), per-tab panels, moss hint
footer. Sliders use a small gold focus rail plus handle color/scale instead of a boxed row; cyclers use
quiet neutral surfaces with the shared rail/wash state. Controls tab carries the full binding reference table (UI / Player / Dialogue sections,
copy preserved verbatim); Credits tab renders the shipped credits copy with editorial hierarchy.
Gamepad: tabs row ↔ content column wired explicitly per tab; sliders adjust natively on
left/right; cyclers cycle on stick/d-pad left-right (axis-dominance guarded) or Submit; ‹ ›
arrow buttons are mouse-only (`Navigation.None` — clicking them doesn't steal pad focus). Up from a
tab reaches Close; Down/Left from Close returns to the active tab.

## LookSensitivityHook

`_Hollowfen/Scripts/Settings/LookSensitivityHook.cs` lives on `PlayerArmature`. `[DefaultExecutionOrder(-100)]` so its `LateUpdate` runs before `ThirdPersonController.LateUpdate` (default 0); scales `StarterAssetsInputs.look *= GameSettings.LookSensitivity` in place each frame. No third-party StarterAssets code is touched — this is the pattern for all StarterAssets behavior modification.
