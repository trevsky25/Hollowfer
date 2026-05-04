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
- `Medieval Environment - Demo 1` is the third-party Magic Pig Games sample scene. It contains `PlayerArmature` (Wren) with `ThirdPersonController` + `StarterAssetsInputs` + `PlayerInput` driven by `Assets/Starter Assets/.../StarterAssets.inputactions`. Used as the playable gameplay sandbox until a real Hollowfen village scene exists.
- The legacy prototype `Assets/Scenes/MainMenu.unity` is kept as visual reference only; the production menu lives at `Scene_MainMenu`.
- `Build Settings` order: Scene_MainMenu (0), MainMenu (legacy), Village, Medieval Environment - Demo 1.

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
| `FocusHighlight` | `_Hollowfen/Scripts/UI/FocusHighlight.cs` | Per-Selectable focus visual. Color tint + scale + optional `<u>underline</u>` rich text + optional glow. `_underlineText` mode auto-enables `supportRichText` on the target. Routes `OnPointerEnter` through `EventSystem.SetSelectedGameObject` so mouse hover and gamepad focus share state. |

### Inputs / save / audio / localization

- **Project InputActions** at `_Hollowfen/Input/InputActions.inputactions`. Three maps: UI (Navigate, Submit, Cancel, TabLeft, TabRight, Delete), Player (Move, Look, Interact, Jump, OpenJournal, Pause), Dialogue (Advance, Skip, Choice1-4). Gamepad bindings follow Steam Deck conventions (Submit=South, Cancel=East). C# wrapper auto-generated as `Hollowfen.Input.InputActions`.
- **Wren's controller still uses `StarterAssets.inputactions`** in its PlayerInput component. The two assets coexist intentionally — we'll consolidate when there's a concrete reason (e.g., gameplay needs to trigger menu actions). UIManager's pause input fires from our project asset regardless, since UIManager runs DDOL.
- **`SaveManager`** at `_Hollowfen/Scripts/Save/`. Static class. JSON to `Application.persistentDataPath/saves/slotN.json`. Methods: `SlotHasData`, `GetSlotMeta`, `DeleteSlot`, `WritePlaceholderToSlot`. Real game-state serialization is still TODO — only `SaveSlotMeta` round-trips today.
- **`GameEvents.TriggerAchievement(id)`** in `_Hollowfen/Scripts/GameEvents.cs`. `AchievementManager` subscribes via `[RuntimeInitializeOnLoadMethod]` and `Debug.Log`s. Steamworks SDK wiring is still a future session.
- **`AudioMixer`** at `_Hollowfen/Audio/MainMixer.mixer` with three exposed parameters: `MasterVolume`, `MusicVolume`, `SFXVolume`. Master / Music / SFX child groups under Master. Settings sliders bound via `mixer.SetFloat`, persisted to `PlayerPrefs`.
- **`Localization.Get(id)`** in `_Hollowfen/Scripts/Localization.cs`. Real dictionary now (no longer a passthrough); add new IDs to its `_table`. Used by Pause's quit-confirm copy and Main Menu's quit-confirm copy.

### Known deferred work

- **Wren animations** — Mixamo rig + Unity StarterAsset animations don't perfectly match (palms-up running, curled idle fingers). Diagnosed but not fixed. Real fix is either (a) Mixamo-sourced animations matching her rig, (b) Avatar reconfiguration for wrist orientation, or (c) avatar-mask + override layer for hands. Scoped as its own focused animation session.
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
