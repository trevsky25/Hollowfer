# Hollowfen — Claude Code Project Rules

## Project Overview

**Hollowfen: The Failing Village** is a single-player, narrative-driven exploration game built in Unity. The player is **Wren**, a miller's daughter who returns to her dying home village and slowly rebuilds it through mushroom foraging, cultivation, NPC relationships, and quiet rural restoration.

**Tech stack:**
- Unity (third-person, character is Mixamo-rigged Meshy export)
- C# only
- Coplay MCP for in-editor scaffolding
- ScriptableObject-driven content (data over hardcoded strings)
- Localization-ready string IDs from day one

**Release target:** Steam (Mac + Windows builds), Early Access launch ~month 12, full 1.0 ~month 18–24. Steam Deck Verified is a tier-one goal, not an afterthought.

---

## Read This First, Every Session

Before any task, read `Docs/story.md` (the project bible). It is the source of truth for:
- World, characters, story spine
- The four acts and their beats
- NPC voice and cadence
- Mushroom tier system
- Witch's Cottage and other story gates

If a request conflicts with the bible, ask before proceeding.

---

## Current Foundation (as of 2026-05-04)

The full UI framework + cross-scene flow is built and validated. Read this so you understand what exists before extending it.

### Scene flow

```
Scene_MainMenu  ──Continue──►  Medieval Environment - Demo 1  ──Pause→Quit──►  Scene_MainMenu
       │                                  │
   (boot scene)                  (gameplay sandbox; Wren spawns)
       │                                  │
       └─ MainMenu / Settings / Save Slot / Pause / ConfirmModal / Loading
```

- `Scene_MainMenu` (`Assets/_Hollowfen/Scenes/Scene_MainMenu.unity`) is the boot scene. It contains UIManager + ScreenRoot + all menu UIScreens as DDOL'd children.
- `Scene_Hollowfen` (`Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity`) is **the actual Hollowfen village** — already 3D-modeled. Originally a copy of Magic Pig Games' "Medieval Environment - Demo 1" demo scene; we copied it into our owned folder so future package updates don't clobber our work. Contains `PlayerArmature` (Wren) with `ThirdPersonController` + `StarterAssetsInputs` + `PlayerInput` driven by `Assets/Starter Assets/.../StarterAssets.inputactions`. The original Magic Pig demo stays untouched at its package path for reference.
- The legacy prototype `Assets/Scenes/MainMenu.unity` is kept as visual reference only; the production menu lives at `Scene_MainMenu`.
- `Build Settings` order: Scene_MainMenu (0), MainMenu (legacy), Village, Scene_Hollowfen.

### UI framework

| Type | Path | Role |
|---|---|---|
| `UIManager` | `_Hollowfen/Scripts/UI/UIManager.cs` | Singleton, DDOL'd. Owns the screen stack, fade overlay, EventSystem creation, InputSystemUIInputModule auto-wiring, scene-transition orchestration (`LoadSceneAndOpen(sceneName, nextScreenId=null)`), pause-input subscription, modal-aware push/pop, dynamic canvas sortingOrder by stack depth. Subscribes to `SceneManager.sceneLoaded` to clean up duplicate EventSystems. |
| `UIScreen` (base) | `_Hollowfen/Scripts/UI/UIScreen.cs` | Non-abstract base. Inspector fields: `_screenId`, `_defaultSelected`, `_canvasGroup`, `_isModal`. Lifecycle hooks: `OnInitialize` (subclass setup before activation), `OnOpen`, `OnClose`, `OnBack`. `EnsureInitialized` is invoked by `RegisterScreen` so subclass setup runs even if the GameObject was deactivated before its Unity `Awake` could fire. |
| `MainMenuScreen` | `_Hollowfen/Scripts/UI/MainMenuScreen.cs` | 5 buttons: New Game, Continue, Settings, Credits, Quit. Save-aware default focus. Continue routes to gameplay via `LoadSceneAndOpen`. Quit goes through ConfirmModal → editor-stop / `Application.Quit`. |
| `SettingsScreen` | `_Hollowfen/Scripts/UI/SettingsScreen.cs` | 3 tabs (Audio / Graphics / Controls), LB/RB navigation. Audio tab fully wired to `AudioMixer` + PlayerPrefs. Graphics tab has fullscreen toggle + resolution dropdown + quality dropdown. Controls tab is a read-only binding scheme. Active-tab visual state via background tint. |
| `SaveSlotScreen` | `_Hollowfen/Scripts/UI/SaveSlotScreen.cs` | 4 rows (1 autosave + 3 manual). Reads via `SaveManager.GetSlotMeta`, refreshes on open. Submit on row = load (or new game if empty), `UI/Delete` (West/Square/Delete) = ConfirmModal → `SaveManager.DeleteSlot` → refresh. |
| `PauseScreen` | `_Hollowfen/Prefabs/UI/PauseMenu.prefab` (lazy-instantiated by UIManager) | Resume / Settings / Save Game / Quit to Main Menu. `Time.timeScale = 0` on open, `1` on close. Quit-to-MainMenu routes through ConfirmModal → `LoadSceneAndOpen` back to Scene_MainMenu. `IsModal=true` so underlying gameplay stays visible. |
| `ConfirmModal` | `_Hollowfen/Scripts/UI/ConfirmModal.cs` | Reusable. Static `Show(title, message, onConfirm, onCancel)`. Only pops itself in `HandleConfirm`/`HandleCancel` if it's still on top — so callbacks that navigate elsewhere (e.g., `LoadSceneAndOpen`) don't get clobbered. |
| `LoadingScreen` | `_Hollowfen/Scripts/UI/LoadingScreen.cs` | Wren forest hero + animated "Traveling to Hollowfen…" with rolling dots on `WaitForSecondsRealtime`. Used by `LoadSceneAndOpen` during async scene transitions. |
| `FocusHighlight` | `_Hollowfen/Scripts/UI/FocusHighlight.cs` | Per-Selectable focus visual. Color tint + scale + optional glow. `_underlineText` rich-text mode exists but is disabled scene-wide — UI.Text would render the literal `<u>` tags in our setup; left in place for future re-implementation as a child Image bar if a true underline visual is wanted. Routes `OnPointerEnter` through `EventSystem.SetSelectedGameObject` so mouse hover and gamepad focus share state. |

### Story / Wren / Field Guide pages

Three menu pages reachable from the Main Menu — all data-driven, all built programmatically in `OnInitialize` from ScriptableObject databases. Visuals match the web prototype at `/src/`: Georgia serif headings, sage / gold / cream palette, full-bleed hero on detail views, gold-rule act dividers.

| Component | Path | Role |
|---|---|---|
| `StoryScreen` | `_Hollowfen/Scripts/UI/StoryScreen.cs` (`screenId="story"`) | Three-column grid grouped by act. Each act block: gold uppercase label · gold-fading rule · `N CARDS` count badge. Cell shows photo + scene eyebrow + serif title + subtitle. Click → `StoryDetailScreen`. |
| `StoryDetailScreen` | `_Hollowfen/Scripts/UI/StoryDetailScreen.cs` (`screenId="story-detail"`) | Full-bleed hero image with bottom-up dark gradient + dedicated content scrim for legibility. 3-col content overlay: heading column · body between vertical separators · italic Wren note (gold left border) + beats list. Top-right `✕` close, bottom row prev/page-indicator/next walking the database. |
| `WrenScreen` | `_Hollowfen/Scripts/UI/WrenScreen.cs` (`screenId="wren"`) | Single scrolling page. Hero = portrait left + dark-gradient info panel right (eyebrow, big serif name, italic tagline, lead paragraph, 2×2 stats grid w/ gold values). 4-tile kit row, two body cards, gold-bordered italic pullquote at the bottom. |
| `FieldGuideScreen` | `_Hollowfen/Scripts/UI/FieldGuideScreen.cs` (`screenId="field-guide"`) | Four-column grid. Cell shows photo + serif common name + italic Latin + edibility dot + tinted edibility label. Click → `MushroomDetailScreen`. |
| `MushroomDetailScreen` | `_Hollowfen/Scripts/UI/MushroomDetailScreen.cs` (`screenId="mushroom-detail"`) | Hero photo + edibility chip + serif name + italic Latin + description + meta strip (HABITAT / SEASON / LOOK-ALIKES) on the right. Bottom row splits "Identifying features" bullets and "Forager's note" italic. Bottom-left Back button. |

#### Card / cell helpers

| Component | Path | Role |
|---|---|---|
| `StoryCardCell` / `MushroomCardCell` | `_Hollowfen/Scripts/UI/` | Tiny MonoBehaviours on each instantiated cell. Hold the SO reference + an `Action` callback the screen wires when building the cell. |
| `ScrollFocusFollower` | `_Hollowfen/Scripts/UI/ScrollFocusFollower.cs` | Watches `EventSystem.currentSelectedGameObject` and `SmoothDamp`s the parent `ScrollRect` to keep the focused cell inside the viewport. **Math is in content-local space, not world** — using world coords breaks under `CanvasScaler` (the original implementation jumped to the bottom on every D-pad press at non-1× canvas scale). |
| `UICanvasUtil` | `_Hollowfen/Scripts/UI/UICanvasUtil.cs` | Programmatic UI factories. `NewRect / NewImage / NewText / NewHeading / NewEyebrow / NewBody`. `NewHeading` uses Georgia SDF; `NewEyebrow` is bold uppercase with `+24` `characterSpacing` (≈0.32em letter-spacing). Procedural gradient sprite builder (`MakeVerticalGradient` / `MakeHorizontalGradient`) — used for the detail-page scrims and Wren info-panel gradient. |
| `HollowfenPalette` | `_Hollowfen/Scripts/UI/HollowfenPalette.cs` | Single source of truth for cream / parchment / moss / gold / sage tokens **and** the verbatim edibility colors from the handoff (`#7ec38a` edible, `#d36a5b` deadly, `#a47bd0` psychoactive, `#7da7c8` medicinal, `#bbb190` unknown). |

#### ScriptableObject content

| SO | Path | Notes |
|---|---|---|
| `StoryCardData` | `_Hollowfen/Scripts/Data/StoryCardData.cs` | `id, act, scene, title, subtitle, body, wrenNote, beats[], image, unlockAt, questId, displayNameId, descriptionId`. 30 assets at `_Hollowfen/Data/StoryCards/StoryCard_NN_*.asset` — verbatim copy from `src/data/StoryCards.js`. |
| `MushroomFieldGuideData` | `_Hollowfen/Scripts/Data/MushroomFieldGuideData.cs` | `id, commonName, latinName, edibility (enum), edibilityLabel, description, idFeatures[], habitat, season, lookalikes, notes, photo, photoCredit`. 16 assets at `_Hollowfen/Data/Mushrooms/`. |
| `CharacterProfileData` | `_Hollowfen/Scripts/Data/CharacterProfileData.cs` | Designed to hold any cast member; one Wren asset today at `_Hollowfen/Data/Characters/Character_WrenTobin.asset`. |
| `StoryCardDatabase` / `MushroomFieldGuideDatabase` | `_Hollowfen/Scripts/Data/` | Registry SOs holding ordered arrays. Screens read from a `[SerializeField]` reference; iteration order is canonical (matches `src/data/mushroomIndex.js` for mushrooms; JS-file order for cards). |
| `DataImporter` | `_Hollowfen/Scripts/Editor/DataImporter.cs` | One-shot editor utility that parses JSON dumps in `Hollowfen-Unity/Temp/` and recreates the 30 + 16 + 1 SO assets. Useful when web data changes — re-export JSON, run via `execute_code`, re-import. |

#### Image assets

PNGs from `/public` are imported under `_Hollowfen/UI/{StoryCards,Mushrooms,Characters}/` (47 files), all configured Sprite (2D and UI). Sprite refs are wired into the SOs by `DataImporter`.

#### Settings: 4 tabs

`SettingsScreen` now cycles 4 tabs: AUDIO · GRAPHICS · CONTROLS · CREDITS. The Credits panel was cloned from AudioPanel and populated with placeholder copy. `LB`/`RB` cycle through all four (mod 4). Credits is no longer a top-level main-menu button.

#### Main menu reshape

Main menu's `NavRow` now reads `Continue · Story · Wren · Field Guide · Settings` with gold `|` dividers between each, left-aligned with the New Game CTA above. Credits moved into Settings (above). `Quit` is a floating bottom-left button (`Btn_Quit_Floating`), muted red `#A04338` italic on transparent bg, hover/focus glides to brighter red `#E36B5E` with a 6% scale via `FocusHighlight` (Button.transition is `None` so the legacy ColorBlock doesn't fight the FH animation).

#### Conventions / gotchas worth remembering

- **All UI built programmatically.** The screen GameObjects in `Scene_MainMenu` are essentially empty Canvas hosts with the screen script + `[SerializeField]` database refs. `OnInitialize` builds everything. `_built` flag prevents rebuilding on subsequent OnInitialize calls. Means re-running the scene rebuilds from current code — no prefabs to keep in sync.
- **`FocusHighlight._baseColor` caches at Awake.** When you reassign `_targetGraphic` via reflection AFTER AddComponent, the original target's color is already cached and will be painted onto the new target on first state change. Symptom: a dark/light overlay appears across the whole control. Fix: also set `_baseColor` via reflection **and** snap the target's color to the new resting value immediately. Story / Field Guide cards do this for their `FocusGlow` overlay images.
- **`Outline` is a `BaseMeshEffect`, not a `Graphic`.** Reflection-assigning it to `FocusHighlight._targetGraphic` throws `ArgumentException` — use a real Graphic (Image) overlay instead.
- **`Mask` + a near-transparent Image clips everything.** A scroll viewport using `Mask` with `Image.color = (0,0,0,0.001)` produces zero visible mask area → all children clipped. Use `RectMask2D` (rect-based, no Graphic dependency) for scroll viewports.
- **TMP everywhere on these pages, but Georgia loads via `AssetDatabase` editor-only.** `UICanvasUtil.HeadingFont` uses `#if UNITY_EDITOR` AssetDatabase to find Georgia SDF. For Steam builds, either move Georgia SDF into a `Resources/` folder or wire it as a serialized field on the screens.
- **Localization IDs are stamped on every SO** (`story.<id>.title`, `mushroom.<id>.name`, etc.) but the screens currently read raw fields directly. Wiring through `Localization.Get(id)` is a follow-up — needs the LUT entries added first.

### Map system

A mini-map widget plus a toggleable full-screen map in `Scene_Hollowfen`, both rendering the actual 3D world via secondary orthographic cameras → RenderTextures → UI. A Skyrim-style compass strip sits at the top of the gameplay HUD.

| Component | Path | Role |
|---|---|---|
| `MiniMapCamera` | `_Hollowfen/Scripts/Map/MiniMapCamera.cs` | Top-down ortho camera following the Player tag at ~60m. Always active. Renders to `MiniMapRT` (512×512). |
| `MapCamera` | `_Hollowfen/Scripts/Map/MapCamera.cs` | Wider ortho camera (size 150 = 300m frame) following Player at 80m. Always active. Renders to `MapViewRT` (2048×2048). Subscribes to `RenderPipelineManager.beginCameraRendering` to disable scene fog only while it renders — otherwise URP fog washes the view to skybox-blue at altitude. |
| `MapScreen` | `_Hollowfen/Scripts/Map/MapScreen.cs` | Toggles the full-screen canvas, hides the mini-map canvas, freezes `Time.timeScale`. Not a UIScreen subclass — runs independently of UIManager since UIManager is DDOL'd from Scene_MainMenu. |
| `MapInputBridge` | `_Hollowfen/Scripts/Map/MapInputBridge.cs` | Subscribes to `Player/OpenMap` (toggle) and `UI/Cancel` (close) on the project InputActions asset. Bound to `M`, DualSense Touchpad, Xbox View, Steam Deck View. |
| `MiniMapWidget` | `_Hollowfen/Scripts/Map/MiniMapWidget.cs` | Optional rotate-map-with-player mode for the mini-map. |
| `PlayerHeadingArrow` | `_Hollowfen/Scripts/Map/PlayerHeadingArrow.cs` | Drop-on rotator — matches a RectTransform's Z rotation to the player's Y rotation. Used on both heading arrows. |
| `CompassStrip` | `_Hollowfen/Scripts/UI/CompassStrip.cs` | Top-of-screen compass: N/NE/E/SE/S/SW/W/NW marks slide under a center notch as Wren turns. 120° visible at a time, `RectMask2D` clips. |
| `UITriangle` | `_Hollowfen/Scripts/UI/UITriangle.cs` | Custom `MaskableGraphic` drawing a solid upward triangle via `OnPopulateMesh` — no sprite needed. Used for heading arrows and the compass notch. |

**Scene scaffolding** (all in Scene_Hollowfen, all canvases scale with screen size against 1920×1080):
- `_HUDCanvas` — always-on, holds the compass strip
- `_MiniMapCanvas` — always-on corner widget (320×320, sortingOrder 10): frame + RawImage of MiniMapRT + gold `UITriangle` heading arrow + N/S/E/W edge labels
- `_MapCanvas` — deactivated by default (sortingOrder 50): dimmed background + 1020×1020 `MapPanel` with leather frame + HOLLOWFEN title + cardinal labels + centered heading arrow
- `_MiniMapCamera`, `_MapCamera`, `_MapInputBridge`

**Location data infrastructure (parked)**: `LocationData` ScriptableObject + 8 Act I/II POI assets at `_Hollowfen/Data/Locations/` + `LocationMarker` / `RegionTrigger` / `LocationRegistry` / `LocationDebugHUD` scripts. Scene placeholders (`_Locations`, `_Regions`, `_LocationDebugHUD`) are disabled until a real POI placement workflow exists. Compiles cleanly; ready to reactivate when needed.

### Game settings (live, persisted)

`Hollowfen.Settings.GameSettings` (`_Hollowfen/Scripts/Settings/GameSettings.cs`) is the static home for tunable runtime preferences backed by `PlayerPrefs`. Today it owns one preference; future controls go here too.

| Setting | UI | Multiplier range | Notes |
|---|---|---|---|
| Look sensitivity | Settings → Controls slider (1–10) | 0.75× … 1.25× | Two-segment lerp: slider 5 maps exactly to 1.0× (the tested baseline). Tight range chosen because StarterAssets ThirdPersonController feeds mouse-pixels-per-frame straight into yaw — wider multipliers either threshold-gate input at the low end or outrun Cinemachine damping at the high end. PlayerPref key `controls.lookSensitivity`. |

`LookSensitivityHook` (`_Hollowfen/Scripts/Settings/LookSensitivityHook.cs`) lives on `PlayerArmature`. `[DefaultExecutionOrder(-100)]` so its `LateUpdate` runs before `ThirdPersonController.LateUpdate` (default 0); it scales `StarterAssetsInputs.look *= GameSettings.LookSensitivity` in place each frame. No third-party StarterAssets code is touched.

### Inputs / save / audio / localization

- **Project InputActions** at `_Hollowfen/Input/InputActions.inputactions`. Three maps: UI (Navigate, Submit, Cancel, TabLeft, TabRight, Delete), Player (Move, Look, Interact, Jump, OpenJournal, Pause, **OpenMap**), Dialogue (Advance, Skip, Choice1-4). Gamepad bindings follow Steam Deck conventions (Submit=South, Cancel=East, OpenMap=Select/Touchpad). C# wrapper auto-generated as `Hollowfen.Input.InputActions`.
- **Wren's controller still uses `StarterAssets.inputactions`** in its PlayerInput component. The two assets coexist intentionally — we'll consolidate when there's a concrete reason (e.g., gameplay needs to trigger menu actions). UIManager's pause input fires from our project asset regardless, since UIManager runs DDOL.
- **`SaveManager`** at `_Hollowfen/Scripts/Save/`. Static class. JSON to `Application.persistentDataPath/saves/slotN.json`. Methods: `SlotHasData`, `GetSlotMeta`, `DeleteSlot`, `WritePlaceholderToSlot`. Real game-state serialization is still TODO — only `SaveSlotMeta` round-trips today.
- **`GameEvents.TriggerAchievement(id)`** in `_Hollowfen/Scripts/GameEvents.cs`. `AchievementManager` subscribes via `[RuntimeInitializeOnLoadMethod]` and `Debug.Log`s. Steamworks SDK wiring is still a future session.
- **`AudioMixer`** at `_Hollowfen/Audio/MainMixer.mixer` with three exposed parameters: `MasterVolume`, `MusicVolume`, `SFXVolume`. Master / Music / SFX child groups under Master. Settings sliders bound via `mixer.SetFloat`, persisted to `PlayerPrefs`.
- **`Localization.Get(id)`** in `_Hollowfen/Scripts/Localization.cs`. Real dictionary now (no longer a passthrough); add new IDs to its `_table`. Used by Pause's quit-confirm copy and Main Menu's quit-confirm copy.

### Foraging system

First vertical slice of Wren's foraging loop. Walk up to a mushroom → HUD prompt → Inspect screen with rotating 3D model + species info → Forage / Leave. Discovery-gated detail copy: first encounter shows `?` + "unknown" hint; foraging unlocks the field guide entry; subsequent inspections show full info.

| Component | Path | Role |
|---|---|---|
| `IInteractable` | `_Hollowfen/Scripts/Foraging/IInteractable.cs` | Tiny contract — `PromptVerb`, `PromptTarget`, `CanInteract`, `Interact`. Future-proofs NPCs / chests. |
| `MushroomNode` | `_Hollowfen/Scripts/Foraging/MushroomNode.cs` | On the world prefab. References `MushroomFieldGuideData`. `Interact()` opens `InspectScreen`; `Harvest()` is called by the screen — logs, fires `OnAnyHarvested`, marks discovery, fires `ACH_FORAGE_FIRST` once, deactivates self. Optional `_respawnSeconds` field exists but disabled. |
| `PlayerInteractor` | `_Hollowfen/Scripts/Foraging/PlayerInteractor.cs` | On `PlayerArmature`. Each frame `Physics.OverlapSphereNonAlloc` on the Foraging layer from a chest-height offset, picks the closest `IInteractable` inside a forward cone (dot ≥ -0.2), raises `OnFocusChanged`. Subscribes to `Player/Interact` and calls `current.Interact(gameObject)`. Static `Suspended` flag — any screen that takes input ownership flips it true and search/interact go quiet (HUD fades out automatically). |
| `MushroomDiscovery` | `_Hollowfen/Scripts/Foraging/MushroomDiscovery.cs` | Static. PlayerPrefs-backed registry of discovered species IDs (`forage.discoveredIds` as `;`-joined list). Lazy-loaded on first access. Fires `OnDiscovered` event. |
| `MushroomPreviewer` | `_Hollowfen/Scripts/Foraging/MushroomPreviewer.cs` | Singleton scene component at `(0, -1000, 0)`. Builds its rig in `Awake()` — orthographic camera + key/fill directional lights + turntable mount. Renders to a 1024² ARGB32 RT (4× MSAA). RT background is warm `Parchment` cream so the model reads as a specimen on journal paper. `Show(data, silhouette)` instantiates the SO's `WorldPrefab` onto the mount, recursively re-layers it to `MushroomPreview`, and (when `silhouette=true`) replaces every renderer's `sharedMaterials` with a flat URP-Unlit dark walnut material — used for undiscovered mushrooms so first-encounter inspections show shape + scale only, not species detail. `ApplyRotationDelta` (yaw + clamped pitch ±80°) and `ApplyZoomDelta` (clamped ortho size) drive manual control; user input disables `AutoRotate` until the next `Show()`. Zoom range `_minOrthoSize = 0.005 .. _maxOrthoSize = 0.50` gives ~26× zoom-in from default 0.13 — enough to inspect gill structure on the Meshy 4K maps. `SetBackgroundColor(c)` re-applies the camera bg at runtime. |
| `InspectScreen` | `_Hollowfen/Scripts/Foraging/InspectScreen.cs` | Scene-local screen (Map-style, NOT a `UIScreen`). Singleton built programmatically from `UICanvasUtil` + `HollowfenPalette`. Parchment journal layout — 1500×940 panel with a `[SerializeField] Sprite _parchmentSprite` background (falls back to flat `Parchment` color), vertical-gradient vignette overlay, and an outer + inner gold-faint double-rule frame. Top: gold "FIELD JOURNAL · SPECIMEN" eyebrow. Left: 700×700 preview frame with thin gold inset, RawImage of the previewer RT, and a hint pill at the TOP of the frame (controls). Right: text column with edibility eyebrow, Georgia 68px serif title in `InkDeep`, gold underline rule, italic Latin in `Moss`, edibility chip (dot + label inside a low-alpha ink pill), body description in dark ink, three-column stat strip (HABITAT / SEASON / LOOK-ALIKES, height 170 to fit safety copy) under a gold-faint rule, and an italic gold Forager's note pull-quote. Bottom: solid gold Forage button + outlined parchment Leave button, each with a glyph pill on the left that auto-detects the active gamepad (`△`/`○` for DualSense/DualShock, `Y`/`B` for Xbox/XInput, `X`/`A` for Switch, `E`/`Esc` when no pad). `RefreshButtonGlyphs()` runs each frame so swapping pads mid-session updates live. Pauses with `Time.timeScale = 0`, sets `PlayerInteractor.Suspended = true`, releases mouse cursor. `Update()` reads `Mouse.current` + `Gamepad.current` directly (right stick rotate, RT/LT zoom; mouse drag rotate inside preview rect, scroll-wheel zoom anywhere). |
| `InteractionPromptHUD` | `_Hollowfen/Scripts/UI/InteractionPromptHUD.cs` | Bottom-center pill under `_HUDCanvas`. Builds itself programmatically. CanvasGroup-driven 120ms fade on unscaled time. Subscribes to static `PlayerInteractor.OnFocusChanged`. Renders `[E / △]  {verb} {target}` — verb routed through `Localization.Get` (key on `MushroomNode.PromptVerb`), target read raw from `data.CommonName`. |

#### Conventions / gotchas

- **Localization keys, not raw strings, on `IInteractable.PromptVerb`.** `MushroomNode.PromptVerb => "prompt.inspect.verb"`; the HUD calls `Localization.Get` on it. Keeps the HUD a single funnel for new verbs (NPC "Talk", chest "Open", etc.).
- **Two layers added**: `Foraging` (slot 8) hosts only the trigger SphereCollider on the prefab root — the inner `Mesh` child with the convex MeshCollider stays on `Default` so movement physics doesn't collide with the focus-search lane. `MushroomPreview` (slot 9) isolates the previewer's camera + spawned model from the main camera.
- **PlayerArmature was already on layer 8** from the StarterAssets template; coincidentally now named `Foraging`. OverlapSphere returns the player's own collider every frame but `GetComponentInParent<IInteractable>` filters it out — slightly wasteful, not a bug.
- **InspectScreen auto-creates an EventSystem** if none exists (so playing directly into `Scene_Hollowfen` without the MainMenu boot flow still works) and turns off `InputSystemUIInputModule.deselectOnBackgroundClick` so mouse motion doesn't strand gamepad navigation. `Update()` also re-applies the last-focused button if `currentSelectedGameObject` ever goes null.
- **Forage shortcut redundancy**: from inside the inspect screen, `Player/Interact` (Triangle / E) is bound as a Forage shortcut, `UI/Submit` (Cross / Enter / Space) activates the focused button, `UI/Cancel` (Circle / Esc) leaves. Same Triangle button opens the screen and forages — feels continuous.
- **URP material recipe** for Meshy mushroom assets: pack `metallic.rgb + (1 - roughness).a` into a new `*_MaskMap.png` (sRGB off, `alphaSource = FromInput`), feed to URP Lit `_MetallicGlossMap` with `_SmoothnessTextureChannel = 0` (metallic alpha) and `_METALLICSPECGLOSSMAP` keyword. Same recipe applies to Fly Agaric and Oysters when they're modeled.
- **Mushroom prefab pivot + scale**: Meshy FBX exports a ~1cm mesh inside a `localScale=100` transform. Wrap it in a clean parent at root `localScale=0.08` → final ~16cm tall (real-life Agaricus campestris size). Trigger `SphereCollider` lives on the parent: at scale 0.08, local radius 18 = 1.44m world. `MeshCollider(convex)` lives on the inner Mesh child so it inherits the FBX's 100× transform and matches the visual.
- **`MushroomPreviewer.Update()` uses `Time.unscaledDeltaTime`** so the auto-rotate keeps spinning while the inspect screen is paused.
- **Silhouette material is created lazily on first call to `Show(data, silhouette=true)`** — `Shader.Find("Universal Render Pipeline/Unlit")` with `_BaseColor` (and the URP-fallback `_Color`) set to near-black walnut. All `sharedMaterials` arrays on every Renderer in the spawned prefab are replaced with this single material so it reads as a clean shape against the cream RT background. Reverting on subsequent discovered Show() is automatic — that path uses `Instantiate(WorldPrefab)` which gets the prefab's original PBR materials.
- **Inspect-screen layout dimensions** (after the journal redesign): panel 1500×940, preview 700×700 anchored at `(56, 60)` panel-local (UPPER half of the panel — keeps it well above the centered button row at `(0, 36)` from panel bottom), text column starts at `panel-left + 56 + 700 + 56 = 868` from panel left edge. Stat strip is 170 tall on purpose — `Mushroom_07_FieldMushroom`'s `LOOK-ALIKES` value runs ~7 lines and used to bleed into the Forager's note when the strip was 64. Hint pill lives at the TOP of the preview frame (was bottom — that collided with the centered button row at high panel widths).
- **Gamepad glyph detection** uses `Gamepad.current.GetType().Name` plus `pad.description.product.ToLowerInvariant()` to match against PS / Xbox / Switch / generic. Default fallback is PS-style (`△`/`○`) since DualSense is the primary dev pad. Glyphs refresh every frame in `Update()` so the user can swap controllers mid-session.
- **Foraging gating**: every encounter routes through Inspect — there's no quick-pickup. `MushroomNode.Interact` → `InspectScreen.Open(this)`. Foraging happens from the screen (button click or shortcut). Per-species discovery state lives in `MushroomDiscovery`.
- **First-harvest achievement** (`ACH_FORAGE_FIRST`) is `PlayerPrefs`-gated by `forage.firstHarvestSeen`. Persists across sessions — no `SaveManager` integration needed yet.
- **Vertex count is 414k** on Field Mushroom Meshy export. Heavy; flagged for a decimation pass before EA. Acceptable for the slice — only one species modeled, only ~3 in the world right now.

#### Inputs / Gamepad mapping (post-rebind)

| Action | Keyboard | Gamepad |
|---|---|---|
| `Player/Interact` (open Inspect / Forage shortcut in screen) | `E` | **Triangle / Y** (`buttonNorth`) |
| `Player/OpenJournal` (parked, journal not built yet) | `J` | **Square / X** (`buttonWest`) |
| `UI/Submit` (activate focused button) | `Enter` / `Space` | **Cross / South** |
| `UI/Cancel` (Leave from Inspect) | `Esc` | **Circle / East** |
| Inspect rotate | mouse drag inside preview | right stick |
| Inspect zoom | scroll wheel | RT (in) / LT (out) |

### Known deferred work

- **Wren animations** — Mixamo rig + Unity StarterAsset animations don't perfectly match (palms-up running, curled idle fingers). Diagnosed but not fixed. Real fix is either (a) Mixamo-sourced animations matching her rig, (b) Avatar reconfiguration for wrist orientation, or (c) avatar-mask + override layer for hands. Scoped as its own focused animation session.
- **Map POIs and region detection** — script + data infrastructure exists (`LocationData`, `LocationMarker`, `RegionTrigger`, `LocationRegistry`, 8 Act I/II SOs). Scene placeholders are disabled until the POI placement workflow is solved (placing markers blindly was unproductive).
- **Map pan/zoom** — full-screen map is fixed-frame (camera follows Wren, ortho 150). No drag/scroll yet.
- **Map open input** — currently shipping `M` + Gamepad Select + DualSense Touchpad. Final controller mapping decision (reuse `Player/OpenJournal` on `Y/Triangle/J` vs the dedicated `Player/OpenMap` action) still open.
- **Region-enter toast notifications** — events exist on `LocationRegistry`; UI not built.
- **Wren as pink dot in `MapViewRT`** — minor URP missing-material artifact when she's rendered top-down. Covered by the centered heading triangle in normal play; cosmetic cleanup pass for later.
- **Real game-state save** — `SaveSlotMeta` writes/reads work; actual game state (inventory, quests, world state) isn't persisted yet.
- **Steamworks SDK** — achievement events fire correctly but aren't wired to Steam yet.
- **Translation pass** — Localization LUT is English-only; EA target adds Simplified Chinese.

### Third-party console warnings (ignore)

When loading `Medieval Environment - Demo 1`, the console fills with warnings from the Magic Pig Games / NatureManufacture asset packs:
- `BoxCollider does not support negative scale` (~30 of these — third-party scene content)
- `The tree prefab_X couldn't be instanced because bounds could not be determined` — NatureManufacture trees
- `TerrainCollider: MeshCollider is not supported on terrain` — terrain config
- `The referenced script (Unknown) on this Behaviour is missing!` — orphan components in third-party prefabs

These are pre-existing in the asset pack content and not from our code. Don't try to "fix" them by editing third-party prefabs (changes get lost on package update). They're cosmetic.

---

## Communication Style

- Direct, output-first. Skip preamble.
- No "Great question!" or "I'd be happy to help!" intros.
- When you finish a task, end with a 3–7 step test script so I can verify in play mode.
- If you need a decision, state the options and your recommendation. Don't ask open-ended questions.
- Brief is better than verbose. Code comments only where logic is non-obvious.

---

## Code Conventions

- PascalCase class names matching filename, one MonoBehaviour per file
- Private fields: `_camelCase` with `[SerializeField]` when editor-exposed
- Avoid `public` fields; prefer properties or `[SerializeField] private`
- Namespaces: `Hollowfen.Foraging`, `Hollowfen.Dialogue`, `Hollowfen.UI`, etc.
- ScriptableObject classes end with `Data` (e.g. `MushroomData`, `NPCData`, `QuestData`)
- Use `[CreateAssetMenu]` with menu paths like `Hollowfen/Mushrooms/...`
- Use UnityEvents sparingly; prefer C# events between systems
- No singletons except for `GameManager`, `TimeManager`, `SaveManager`

---

## Folder Structure

```
Assets/
├── _Hollowfen/                   (everything I own; underscore sorts to top)
│   ├── Scripts/
│   │   ├── Foraging/
│   │   ├── Dialogue/
│   │   ├── Quests/
│   │   ├── NPCs/
│   │   ├── UI/
│   │   ├── Save/
│   │   ├── Steam/                 (Steamworks integration: achievements, cloud, rich presence)
│   │   └── Time/
│   ├── Data/
│   │   ├── Mushrooms/
│   │   ├── NPCs/
│   │   ├── Quests/
│   │   ├── Dialogue/
│   │   └── StoryCards/
│   ├── Prefabs/
│   │   ├── Foraging/
│   │   ├── NPCs/
│   │   └── UI/
│   ├── Scenes/
│   ├── UI/                        (sprites, fonts, atlases)
│   └── Audio/
├── ImportedAssets/                (Asset Store village, Mixamo/Meshy chars)
└── ThirdParty/                    (third-party packages)
```

---

## Naming

- Mushrooms: `MushroomData_T1_BrightCap` (tier-prefixed)
- NPCs: `NPCData_Bram`, `NPCData_Edda`, etc.
- Quests: `QuestData_Act1_01_GetMillKey`
- Dialogue: `DialogueTree_Bram_Intro`, `DialogueTree_Bram_T2`
- Story Cards: `StoryCard_Act1_01_Arrival`
- Scenes: `Scene_MainMenu`, `Scene_Hollowfen`, `Scene_OldWood`

---

## Cast (Spell Correctly, Don't Improvise)

- **Wren** — protagonist, miller's daughter, returned home after father's death
- **Bram** — gives mill key, runs village trade
- **Marra** — village cook
- **Edda** — quiet observer, granddaughter saved by Brightspore tonics
- **Sister Almy** — teaches cultivation, owns the seedbook
- **Joren** — smith, forges Wren's foraging tools (Tier 2 gate)
- **Voss** — tax collector (antagonist, not villain)
- **Theo** — traveling trader (expands mushroom markets)
- **Hollin** — late-arriving companion (NOT a romance arc)
- **Father Calden** — village priest, softens slowly
- **Lord Aldric** — Act IV endgame fork
- **The Old Wood** — treated as the 12th character (environmental design)

---

## Mushroom Tiers (placeholder — confirm before locking final names)

- **T1** — starter, common, surface foraging, no tools required
- **T2** — requires Joren's tools (Act II gate)
- **T3** — Almy's seedbook required, cultivation-only
- **T4** — rare wild, gates Witch's Cottage
- **T5** — bible-reserved for late game, do not implement yet

---

## Steam Release Constraints (Apply to Every Build, From Day One)

These are the constraints that determine Steam Deck Verified status and a strong launch. Bake them in as you build — retrofitting at month 12 is painful.

### Controller-First Input
- Every interactive element must be reachable via gamepad navigation (D-pad / left stick / face buttons). Mouse and keyboard are equally supported but never required.
- Use Unity's new **Input System** (not legacy Input Manager). Define an `InputActions` asset with action maps: `UI`, `Player`, `Dialogue`.
- Every UI screen needs a clear default selected element on open, with a visible focus highlight (not just Unity's default thin outline — needs to read on a small Steam Deck screen).
- All UI scrolling must work with stick/D-pad, not just mouse wheel.
- Bind Back/Cancel to B (Xbox) / Circle (PS) / East button. Confirm = A / Cross / South.
- Test menu navigation with a gamepad before declaring any UI task done.

### Steam Deck Display Targets
- 1280×800 (Steam Deck native) must look correct. UI scales properly, text legible at arm's length, no clipping.
- Tap targets sized for handheld viewing distance (minimum ~24px equivalent).
- Avoid relying on tooltips that require hover — gamepad has no hover state.

### Save System
- Save files go to `Application.persistentDataPath` always. Never write to `Application.dataPath` or absolute paths.
- Save format must be Steam Cloud-safe: small file size (under ~1MB per slot), portable across Mac and Windows builds (no platform-specific paths inside save data).
- 3 save slots + 1 autosave slot.

### Achievements (Hooks Now, SDK Later)
- Every quest completion, story beat, and milestone fires a `GameEvents.OnAchievementTrigger(string achievementId)` event.
- A stub `AchievementManager.cs` listens and logs the trigger. Real Steamworks SDK wiring happens in a later session — but the hooks must exist from the start.
- Achievement IDs follow pattern: `ACH_ACT1_ARRIVAL`, `ACH_FORAGE_FIRST`, `ACH_NPC_BRAM_T2`, `ACH_END_INDEPENDENCE`.

### Localization
- All player-facing strings flow through a `Localization.Get(stringId)` lookup. Never hardcode display text.
- Placeholder LUT is fine for now — real translation happens before EA launch.
- Target languages at EA launch: English, Simplified Chinese.

### Performance Floor
- Target 60fps on Steam Deck for Hollowfen scene (the village). Old Wood scene can target 40fps if needed.
- Profile early and often using the new `manage_profiler` MCP tool.

---



- Always pin assets with `@` syntax in meaningful prompts (e.g. `@MushroomNode.cs`, `@Scene_MainMenu.unity`)
- Work in small, verifiable batches; never bundle 10 disjoint tasks into one prompt
- If a session output goes wrong, **do not debug conversationally** — start a new thread, re-pin assets
- Re-read bible + this file at the start of any new thread
- When creating prefabs, attach all required components before declaring done
- Never dump files in `Assets/` root; use the folder structure above
- Use `Time.timeScale` for pause; UI animations use unscaled time

---

## Things NOT to Do

- Do not modify Asset Store village assets directly; create prefab variants if changes needed
- Do not hardcode UI strings — use string IDs pointing to a localization table (placeholder LUT is fine)
- Do not write romance content (Hollin is companion, not love interest)
- Do not use emoji in dialogue or UI
- Do not use contemporary slang in NPC dialogue (slight archaism preferred)
- Do not exceed "Lord help me" as the strongest oath
- Do not assume Unity version features without confirming the project version first
- Do not invent NPCs, mushrooms, or locations not in the bible — ask instead
- Do not build mouse-only UI — every screen must be gamepad-navigable
- Do not write to absolute file paths for saves — use `Application.persistentDataPath`
- Do not skip the achievement event hook on quest/beat completion, even before the SDK is wired
- Do not use Unity's legacy Input Manager — new Input System only

---

## Test Output Template

End every task with this block:

```
### Test in Play Mode
1. Open scene `Scene_X`
2. Action...
3. Expected: ...
4. ...
```
