# UI Framework
Screen-stack UI system: `UIManager` singleton (DDOL) owns a push/pop stack of `UIScreen`s, fade transitions, scene-transition orchestration, and pause input. All UI is built programmatically — screen GameObjects are empty Canvas hosts; `OnInitialize` constructs everything from code.
Key scripts: `Assets/_Hollowfen/Scripts/UI/` — UIManager, UIScreen, MainMenuScreen, SettingsScreen, SaveSlotScreen, ConfirmModal, LoadingScreen, FocusHighlight, UICanvasUtil, HollowfenPalette.
Scene flow: `Scene_MainMenu` (boot, DDOL menu screens) ⇄ `Scene_Hollowfen` (gameplay) via `UIManager.LoadSceneAndOpen`.
Entry points: any screen change goes through `UIManager.Push/Pop`; scene changes through `LoadSceneAndOpen(sceneName, nextScreenId)`.
Biggest gotchas: FocusHighlight `_baseColor` caches at Awake (reflection reassignment trap); use `RectMask2D` not `Mask` for scroll viewports; Georgia SDF loads editor-only via AssetDatabase (ship blocker until moved to Resources or serialized); UIManager's PUSH deactivates the covered screen WITHOUT calling OnClose — a covered screen's InputActions stay enabled, so screen-level handlers must gate on `UIManager.Instance.TopScreen == this` (batch-28 review catch).
Status: shipped + verified. Pause prefab root is a plain Transform — build UI into a Canvas child. SettingsScreen rebuilt code-built in batch-28 (menu chrome still on legacy scene-UI: MainMenu, SaveSlot, Loading, ConfirmModal — the TMP-migration backlog).

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Scene flow

```
Scene_MainMenu  ──Continue──►  Scene_Hollowfen  ──Pause→Quit──►  Scene_MainMenu
       │                              │
   (boot scene)               (gameplay village; Wren spawns)
       │                              │
       └─ MainMenu / Settings / Save Slot / Pause / ConfirmModal / Loading
```

- `Scene_MainMenu` (`Assets/_Hollowfen/Scenes/Scene_MainMenu.unity`) is the boot scene: UIManager + ScreenRoot + all menu UIScreens as DDOL'd children.
- `Scene_Hollowfen` (`Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`) is the actual village — a copy of Magic Pig Games' "Medieval Environment - Demo 1" moved into our owned folder so package updates don't clobber it. Contains `PlayerArmature` (Wren) with `ThirdPersonController` + `StarterAssetsInputs` + `PlayerInput` on `Assets/Starter Assets/.../StarterAssets.inputactions`. The original demo stays untouched at its package path.
- Legacy prototype `Assets/Scenes/MainMenu.unity` is visual reference only (intentional design — port the look, don't regenerate art).
- Build Settings order: Scene_MainMenu (0), MainMenu (legacy), Village, Scene_Hollowfen.

## Core components

| Type | Path | Role |
|---|---|---|
| `UIManager` | `_Hollowfen/Scripts/UI/UIManager.cs` | Singleton, DDOL'd. Owns the screen stack, fade overlay, EventSystem creation, InputSystemUIInputModule auto-wiring, scene-transition orchestration (`LoadSceneAndOpen(sceneName, nextScreenId=null)`), pause-input subscription, modal-aware push/pop, dynamic canvas sortingOrder by stack depth. Subscribes to `SceneManager.sceneLoaded` to clean up duplicate EventSystems. |
| `UIScreen` (base) | `_Hollowfen/Scripts/UI/UIScreen.cs` | Non-abstract base. Inspector fields: `_screenId`, `_defaultSelected`, `_canvasGroup`, `_isModal`. Lifecycle hooks: `OnInitialize` (setup before activation), `OnOpen`, `OnClose`, `OnBack`. `EnsureInitialized` is invoked by `RegisterScreen` so subclass setup runs even if the GameObject was deactivated before Unity `Awake` fired. |
| `MainMenuScreen` | `_Hollowfen/Scripts/UI/MainMenuScreen.cs` | New Game CTA + NavRow `Continue · Story · Wren · Field Guide · Settings` with gold `|` dividers. Save-aware default focus. Continue → gameplay via `LoadSceneAndOpen`. `Quit` is a floating bottom-left button (`Btn_Quit_Floating`), muted red `#A04338` italic, hover/focus glides to `#E36B5E` + 6% scale via FocusHighlight (Button.transition = `None` so the ColorBlock doesn't fight FH). Quit routes through ConfirmModal. |
| `SettingsScreen` | `_Hollowfen/Scripts/UI/SettingsScreen.cs` | 4 tabs: AUDIO · GRAPHICS · CONTROLS · CREDITS, LB/RB cycle (mod 4). Audio tab wired to `AudioMixer` + PlayerPrefs. Graphics: fullscreen toggle + resolution + quality dropdowns. Controls: read-only binding scheme + look-sensitivity slider. Credits: placeholder copy (cloned from AudioPanel). Active-tab visual via background tint. |
| `SaveSlotScreen` | `_Hollowfen/Scripts/UI/SaveSlotScreen.cs` | 4 rows (1 autosave + 3 manual). Reads `SaveManager.GetSlotMeta`, refreshes on open. Submit = load (or new game if empty); `UI/Delete` (West/Square/Delete) = ConfirmModal → `SaveManager.DeleteSlot` → refresh. |
| `PauseScreen` | `_Hollowfen/Prefabs/UI/PauseMenu.prefab` (lazy-instantiated by UIManager) | Resume / Settings / Save Game / Quit to Main Menu. `Time.timeScale = 0` on open, `1` on close. Quit routes ConfirmModal → `LoadSceneAndOpen` back to Scene_MainMenu. `IsModal=true` so gameplay stays visible. **Prefab root is a plain Transform** — build new UI into a Canvas child. |
| `ConfirmModal` | `_Hollowfen/Scripts/UI/ConfirmModal.cs` | Reusable. Static `Show(title, message, onConfirm, onCancel)`. Only pops itself in `HandleConfirm`/`HandleCancel` if still on top — callbacks that navigate elsewhere don't get clobbered. |
| `LoadingScreen` | `_Hollowfen/Scripts/UI/LoadingScreen.cs` | Wren forest hero + animated "Traveling to Hollowfen…" rolling dots on `WaitForSecondsRealtime`. Used by `LoadSceneAndOpen`. |
| `FocusHighlight` | `_Hollowfen/Scripts/UI/FocusHighlight.cs` | Per-Selectable focus visual: color tint + scale + optional glow. `_underlineText` rich-text mode exists but is disabled scene-wide (UI.Text renders literal `<u>` tags in our setup). Routes `OnPointerEnter` through `EventSystem.SetSelectedGameObject` so mouse hover and gamepad focus share state. |
| `UICanvasUtil` | `_Hollowfen/Scripts/UI/UICanvasUtil.cs` | Programmatic UI factories: `NewRect / NewImage / NewText / NewHeading / NewEyebrow / NewBody`. `NewHeading` uses Georgia SDF; `NewEyebrow` is bold uppercase with `+24` characterSpacing (≈0.32em). Procedural gradient sprites (`MakeVerticalGradient` / `MakeHorizontalGradient`) for scrims/panels. Also procedural rounded-rect / shadow / circle primitives (see UI design system notes). |
| `HollowfenPalette` | `_Hollowfen/Scripts/UI/HollowfenPalette.cs` | Single source of truth for cream / parchment / moss / gold / sage tokens AND verbatim edibility colors (`#7ec38a` edible, `#d36a5b` deadly, `#a47bd0` psychoactive, `#7da7c8` medicinal, `#bbb190` unknown). |
| `CompassStrip` | `_Hollowfen/Scripts/UI/CompassStrip.cs` | Top-of-screen compass: N/NE/E/SE/S/SW/W/NW marks slide under a center notch as Wren turns. 120° visible, `RectMask2D` clips. |
| `UITriangle` | `_Hollowfen/Scripts/UI/UITriangle.cs` | Custom `MaskableGraphic` drawing a solid triangle via `OnPopulateMesh`. Used for heading arrows + compass notch. |
| `ScrollFocusFollower` | `_Hollowfen/Scripts/UI/ScrollFocusFollower.cs` | Watches `EventSystem.currentSelectedGameObject`, `SmoothDamp`s the parent ScrollRect to keep focus in view. **Math is content-local space, not world** — world coords break under `CanvasScaler`. |

## Gotchas

- **All UI built programmatically.** `_built` flag prevents rebuilding on repeat OnInitialize. Re-running the scene rebuilds from current code — no prefabs to keep in sync.
- **`FocusHighlight._baseColor` caches at Awake.** Reassigning `_targetGraphic` via reflection AFTER AddComponent paints the old target's cached color onto the new target on first state change (symptom: dark/light overlay across the control). Fix: also set `_baseColor` via reflection AND snap the target's color to the new resting value immediately.
- **`Outline` is a `BaseMeshEffect`, not a `Graphic`.** Assigning it to `FocusHighlight._targetGraphic` throws — use a real Graphic (Image) overlay.
- **`Mask` + near-transparent Image clips everything.** Use `RectMask2D` for scroll viewports.
- **Georgia SDF loads via `AssetDatabase` editor-only** (`UICanvasUtil.HeadingFont`, `#if UNITY_EDITOR`). For Steam builds, move Georgia SDF into `Resources/` or wire as a serialized field. **Ship blocker — tracked in TODOS.md.**
- **CanvasRenderer + reflection AddComponent** has bitten us before (see memory/gotchas); prefer explicit component setup.
- Use `Time.timeScale` for pause; UI animations run on unscaled time.
