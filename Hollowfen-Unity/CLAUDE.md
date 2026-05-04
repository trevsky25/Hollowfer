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
