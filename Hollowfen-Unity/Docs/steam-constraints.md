# Steam Release Constraints
Requirements that determine Steam Deck Verified status and launch quality — applied to every build from day one, because retrofitting at month 12 is painful. The Steam Deck cert review persona owns this doc.
Release target: Steam (Mac + Windows), Early Access ~month 12, 1.0 ~month 18–24. Steam Deck Verified is a tier-one goal.
Pillars: controller-first input · Deck display targets · Cloud-safe saves · achievement hooks from day one · localization discipline · 60fps perf floor.
Verification: every UI batch is tested with a gamepad before being declared done; perf profiled via the profiler MCP tooling.
Biggest risk items: hover-dependent UI, hardcoded strings, saves outside persistentDataPath, editor-only font loading.
Status: standing constraints — change only with an explicit product decision.

> Self-healing doc: update when a constraint changes or a new cert requirement is discovered.

---

## Controller-first input

- Every interactive element reachable via gamepad (D-pad / stick / face buttons). Mouse/keyboard equally supported, never required.
- New **Input System** only. Action maps: `UI`, `Player`, `Dialogue` (see systems/input.md).
- Every UI screen: a clear default selected element on open + a focus highlight that reads on a small Deck screen (not Unity's default thin outline).
- All scrolling works with stick/D-pad. Back/Cancel = East button; Confirm = South.
- Test menu navigation with a gamepad before declaring any UI task done.

## Steam Deck display targets

- 1280×800 native must look correct: UI scales, text legible at arm's length, no clipping.
- Tap targets ≥ ~24px equivalent.
- No hover-required tooltips — gamepad has no hover state.

## Save system

- `Application.persistentDataPath` always. Never `Application.dataPath` or absolute paths.
- Steam Cloud-safe: under ~1MB per slot, portable Mac↔Windows (no platform paths inside save data).
- 3 manual slots + 1 autosave.

## Achievements (hooks now, SDK later)

- Every quest completion, story beat, and milestone fires `GameEvents.TriggerAchievement(id)`.
- Stub `AchievementManager` listens + logs; Steamworks SDK wiring is a later session — but hooks exist from the start.
- ID pattern: `ACH_ACT1_ARRIVAL`, `ACH_FORAGE_FIRST`, `ACH_NPC_BRAM_T2`, `ACH_END_INDEPENDENCE`.

## Localization

- All player-facing strings via `Localization.Get(stringId)` (see systems/localization.md).
- EA languages: English, Simplified Chinese.

## Performance floor

- 60fps on Steam Deck for the village scene; Old Wood may target 40fps if needed.
- Profile early + often (profiler MCP tooling). Known debt: 414k-vert Field Mushroom mesh needs decimation before EA.

## Known ship blockers (tracked in TODOS.md)

- Georgia SDF font loads via editor-only `AssetDatabase` — broken in builds until moved to `Resources/` or serialized refs.
- Steamworks SDK not wired.
- Localization LUT English-only and partially wired.
