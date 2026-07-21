# Game Settings & Audio
Runtime user preferences backed by PlayerPrefs: `Hollowfen.Settings.GameSettings` owns look sensitivity and accessibility presentation; `AccessibilityPresentationPolicy` applies readable scale across runtime canvases; `ProductionPerformancePolicy` owns the build-wide 60fps/native-quality contract; mixer volumes and source-trimmed Ambience/Voice levels are bound to Settings UI sliders.
Key scripts: `Assets/_Hollowfen/Scripts/Settings/` — GameSettings, AccessibilityPresentationPolicy, LookSensitivityHook, ProductionPerformancePolicy; `UI/SettingsScreen.cs` (code-built). Audio asset: `Assets/_Hollowfen/Audio/MainMixer.mixer`.
Exposed mixer params: `MasterVolume`, `MusicVolume`, `SFXVolume`; prefs keys `audio.*`, `graphics.*`, `controls.lookSensitivity`, and `accessibility.*`. Ambience and Voice remain independent source-level trims. Voice sources resolve the mixer's Master group, so turning SFX off no longer silences speech.
Entry point: SettingsScreen tabs (see ui-framework.md) write PlayerPrefs + apply live; MainMenu's Credits button opens it pre-switched via `SettingsScreen.NextOpenTab`.
Biggest gotchas: interface scaling changes the shared CanvasScaler reference resolution, not every RectTransform; temporary verifiers must restore or delete the exact preference keys they found; Reduced Motion removes spatial animation but must preserve timing, input locks, VO, focus color/glow, and the final cinematic composition. Existing look-sensitivity, screen-stack, resolution-index, and centralized-VSync constraints still apply.
Status: shipped + verified through 2026-07-18. Settings now has five controller-linked tabs, including Accessibility with 100/108/115% Interface Size, Reduced Motion, and Caption Backing. Standard and 115% layouts were visually audited, and a runtime verifier proved all active scale-aware canvases adopt the preference.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Settings inventory

| Setting | UI | Range | Notes |
|---|---|---|---|
| Master/Music/SFX volume | Settings → Audio sliders | mixer dB | `mixer.SetFloat`, persisted to PlayerPrefs, % readout. |
| Voice volume | Settings → Audio slider | source level under Master | `VoiceAudio` applies `audio.voice` to all narration/dialogue sources; independent of SFX. |
| Ambience volume | Settings → Audio slider | source level under Master | `AmbienceManager.SetUserVolume`; independent of Music/SFX. |
| Fullscreen | Settings → Graphics cycler | On/Off | `Screen.fullScreen`; localized value strings. |
| Resolution | Settings → Graphics cycler | deduped w×h list | `Screen.SetResolution(w, h, Screen.fullScreenMode)`; list deduped from `Screen.resolutions` (raw list repeats per refresh rate). |
| Quality | Settings → Graphics cycler | project levels | Row HIDDEN when the project defines <2 levels (currently 1: "PC") — a dead control shouldn't ship. Display names via `settings.quality.<name>` with raw fallback. |
| Look sensitivity | Settings → Controls slider (1–10, whole steps) | 0.75× … 1.25× | Two-segment lerp; slider 5 maps exactly to 1.0× (tested baseline). PlayerPrefs key `controls.lookSensitivity`. |
| Interface Size | Settings → Accessibility cycler | 100% / 108% / 115% | Changes the reference resolution for every `ScaleWithScreenSize` canvas, including screens built later. PlayerPrefs key `accessibility.interfaceScale`. |
| Reduced Motion | Settings → Accessibility cycler | Off / On | Removes Ken Burns, motes/mist, focus scaling, camera arcs, bouncing/sliding toasts, and long narration fades while preserving stable final frames and gameplay timing. |
| Caption Backing | Settings → Accessibility cycler | Off / On | Adds an opaque rounded backing behind cinematic captions without covering image-only beats. |

## Production performance policy (batch-89)

`ProductionPerformancePolicy` is bootstrapped before the first scene and survives scene loads. Its contract is:

- Target 60fps in Editor Game view and standalone players.
- Prefer hardware VSync on displays that are a clean 60Hz multiple: 60→1, 120→2, 180→3, 240→4. Re-sample at runtime because fullscreen, monitor, and refresh rate can change. Non-multiples such as 75/144/165Hz use Unity's 60fps software cap rather than drifting to a different frame target.
- Keep `OnDemandRendering.renderFrameInterval = 1`; never skip rendered frames.
- Preserve the selected quality level while forcing full anisotropic texture sampling. PC quality uses a measured 1.25 LOD bias; Mobile remains 1.0.
- Configure only the tagged main camera: native scale, HDR, occlusion culling, SMAA High, dithering, NaN suppression, and shadows. The post pipeline stays active because URP runs SMAA/stop-NaN/dithering there, but the camera's volume layer mask is deliberately zero: Hollowfen keeps its sun/sky/ambient/fog palette instead of stacking the scene's `Medieval Fantasy Volume Profile` (warm gain/white balance, DOF, motion blur, and grain). Preview/Map/journal RenderTexture cameras retain their bespoke settings.
- Fullscreen, resolution, and quality changes call `RequestDisplayRefresh()` so frame pacing is restored after Unity completes the mode switch.

The PC URP asset remains the quality source of truth: render scale 1.0, full texture mipmaps, HDR, Forward+, four-cascade 2K high-quality soft shadows, reflection blending/box projection, full-resolution SSAO, SRP Batcher, and LOD bias 1.25. The July 19 audit found 46,762 vendor LOD groups in the building set; reducing the former 2.0 bias cut the sampled view from roughly 6.0M to 3.8M rendered triangles and 1,446 to 835 shadow casters without a visible composition loss in 4K A/B captures. The policy reapplies the budget after quality/display changes. It deliberately does not inflate shadow maps or enable realtime reflection probes without a measured standalone frame-time budget.

## Accessibility presentation

`AccessibilityPresentationPolicy` bootstraps before scene load and survives transitions. It applies immediately and again on the following frame so both scene-authored and code-built screens receive `1920×1080 / InterfaceScale`, match 0.5. `UICanvasUtil.Init1080()` uses the same projection at creation time, avoiding a one-frame size jump for late overlays.

Reduced Motion is intentionally consumed by the animation owner rather than expressed as a global time-scale hack:

- Main menu disables drifting particles/mist, resets hero transforms, and reveals its title without a Ken Burns/ink-bleed pass.
- `FocusHighlight` keeps color/outline/glow but never scales the control.
- `PropFocusCinematic` cuts to the authored final shot, holds it, and restores immediately; reveal/gameplay callbacks still occur in order.
- restoration and region toasts hold a stable card without bounce, slide, or alpha travel.
- cinematic narration retains VO/captions/beat duration while using stable art and short cut-safe fades. Caption Backing owns only the caption surface.

`Hollowfen > Verify > Accessibility Presentation` runs in Play Mode, temporarily selects the largest scale and both boolean preferences, steps the policy across live canvases, validates reduced-motion focus behavior, then restores the exact PlayerPrefs presence/value state.

## SettingsScreen structure (batch-28, visual polish batch-64, accessibility 2026-07-18)

Code-built in `OnInitialize` (StoryScreen idiom): Canvas on the host + scrim + left gradient over
the serialized hero sprite, editorial header (sage eyebrow / IM Fell English title / gold rule), shared
top-right close control, text-nav tab row (active = gold + underline), per-tab panels, moss hint
footer. Sliders use a small gold focus rail plus handle color/scale instead of a boxed row; cyclers use
quiet neutral surfaces with the shared rail/wash state. Controls tab carries the full binding reference table (UI / Player / Dialogue sections,
copy preserved verbatim); Accessibility explains the three presentation preferences without implying gameplay assistance; Credits renders the shipped credits copy with editorial hierarchy.
Gamepad: tabs row ↔ content column wired explicitly per tab; sliders adjust natively on
left/right; cyclers cycle on stick/d-pad left-right (axis-dominance guarded) or Submit; ‹ ›
arrow buttons are mouse-only (`Navigation.None` — clicking them doesn't steal pad focus). Up from a
tab reaches Close; Down/Left from Close returns to the active tab.

## LookSensitivityHook

`_Hollowfen/Scripts/Settings/LookSensitivityHook.cs` lives on `PlayerArmature`. `[DefaultExecutionOrder(-100)]` so its `LateUpdate` runs before `ThirdPersonController.LateUpdate` (default 0); scales `StarterAssetsInputs.look *= GameSettings.LookSensitivity` in place each frame. No third-party StarterAssets code is touched — this is the pattern for all StarterAssets behavior modification.
