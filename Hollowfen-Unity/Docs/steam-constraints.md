# Steam Release Constraints
Requirements that determine Steam Deck Verified status and launch quality — applied to every build from day one, because retrofitting at month 12 is painful. The Steam Deck cert review persona owns this doc.
Release target: Steam (Mac + Windows), Early Access ~month 12, 1.0 ~month 18–24. Steam Deck Verified is a tier-one goal. **EA content floor: a polished Acts I–II playthrough** (bible Act II completion state as the EA ending point; Acts III–IV land during EA) — decided 2026-07-11, QUESTIONS.md Q3.
Pillars: controller-first input · Deck display targets · Cloud-safe saves · achievement hooks from day one · localization discipline · 60fps perf floor.
Verification: every UI batch is tested with a gamepad before being declared done; perf profiled via the profiler MCP tooling.
Biggest risk items: hover-dependent UI, hardcoded strings, saves outside persistentDataPath. (Font build-strip fixed batch-32 — fonts must stay Static/baked; see conventions.md.)
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
- `ProductionPerformancePolicy` (batch-89) enforces the 60fps target in players: hardware VSync at clean
  60Hz multiples, a software 60fps fallback on non-multiple refresh displays, and live re-evaluation after
  display/fullscreen/quality changes. The tagged gameplay camera runs native scale with HDR, SMAA High,
  dithering, and occlusion culling. Its post pipeline remains active for those camera-quality passes while a
  zero volume mask prevents the legacy vendor demo profile from recoloring Hollowfen's day/night palette.
- Profile early + often (profiler MCP tooling). Batch-68 resolved the 414k-vertex Field Mushroom debt: shipping world/journal derivatives are 15.8k/47.4k vertices, and all delivered mushroom models use separate 12k–16k / 60k–75k triangle budgets.

## Known ship blockers

Full evidence and exit criteria: [2026-07-17 production audit](review/production-audit-2026-07-17.md).

- **Font player-boot verification is resolved.** The optimized non-development macOS player completed the
  real main-menu-to-gameplay flow with the Static Georgia/LiberationSans atlases intact and no player-log
  errors. Keep the fonts Static and re-bake them according to `conventions.md`.
- Shipping identity is still placeholder (`DefaultCompany`, `Hollowfen-Unity`, `0.1.0`, Unity template
  identifier); the production build gate intentionally blocks release builds until approved values exist.
- Windows Build Support for Unity 6000.4.4f1 is not installed, so the declared Windows target is unbuilt.
- Steamworks/App ID, SteamPipe depots, partner-branch installation, and any advertised Achievements, Cloud,
  or native Steam Input integration are not complete.
- Simplified Chinese is not implemented: the project has no locale selector/storage, translated table, or
  CJK font fallback. Either complete it or remove it from the EA language commitment until ready.
- Physical Steam Deck performance, controller-only access, suspend/resume, offline, glyph, and readability
  verification remain required for the declared Verified goal.
- The final NPC character-model pass and Aldermark/canonical Maitake visual gap remain content work.
- macOS Developer ID signing/notarization and visual/audio/vendor/AI provenance records must be completed.
