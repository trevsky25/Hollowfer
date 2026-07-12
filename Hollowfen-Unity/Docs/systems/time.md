# Time System
Real-time game clock: `TimeManager` (scene-local singleton, `Hollowfen.GameTime`) advances `Hour` from `Time.deltaTime` — default 20 real minutes = 1 game day; `timeScale = 0` (menus/dialogue/screens) freezes it for free. Day rollover fires `OnDayChanged` AND a full `SaveCoordinator.SaveAllWithPlayer()` ("a new dawn is a checkpoint"). `DayFlagScheduler` converts flags on day rollover (`whenFlag` present → set `thenFlag`).
Key scripts: `Assets/_Hollowfen/Scripts/Time/` — TimeManager, DayFlagScheduler.
Clock: `int Day` (1-based) + `float Hour` (0–24); `IsNight` = before 5.5h or after 20.5h; new game starts Day 1 @ 14h; `OnSundown` fires crossing 19h (edge-triggered in Update ONLY).
TimeManager also OWNS the sun: drives the `RenderSettings.sun` light arc + color + `ambientIntensity` every frame — any other lighting script will fight it.
Biggest gotchas: `SetTime()` fires `OnDayChanged` once (even for multi-day skips, even backwards) and NEVER fires `OnSundown` — time-jumping tests can miss sundown content; TimeManager's static events have NO domain-reload reset (unlike QuestManager/GameScores) — subscribers must pair OnEnable/OnDisable.
Status: verified against code 2026-07-11.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## TimeManager

- **Advancement**: `Update()` adds `Time.deltaTime * (24 / (_minutesPerGameDay * 60))` to Hour. `_minutesPerGameDay` serialized (default 20) — inspector-shorten to fast-forward sessions. No sleep mechanic yet.
- **Day boundary**: `Hour >= 24` → `Hour -= 24; Day++` → `OnDayChanged(Day)` → **full save** (`SaveCoordinator.SaveAllWithPlayer()` — synchronous file IO hitch every ~20 real minutes; load-bearing checkpoint behavior).
- **API**: `Instance` (scene-local, NOT DDOL — cross-scene caches go stale), `Day`, `Hour`, `IsNight`, `static event OnDayChanged(int)`, `static event OnSundown`, `SetTime(day, hour)` (clamps/wraps; fires OnDayChanged once if day differs — even backwards; no OnSundown, no save), `WriteTo(meta)` (→ `GameDay`/`GameHour`).
- **Hydration**: `Awake` reads active slot meta; `GameDay > 0` restores, else Day 1 @ `_newGameHour` (14h). `GameDay == 0` = legacy-save sentinel. NOT hydrated by SaveCoordinator.LoadSlot — self-hydrates on scene load (asymmetric with SaveAll; see save.md).
- **Sun/lighting side-job**: `ApplySun()` every frame — sine arc rising 6h / peak ~60° noon / set 18h, warm color lerp, night = fixed dim blue moon (0.14 intensity, yaw+180°), writes `RenderSettings.ambientIntensity` directly.

## DayFlagScheduler

Pure inspector config, no code API: parallel `_whenFlags[]`/`_thenFlags[]`. On `OnDayChanged`: each pair where `GameScores.HasFlag(when)` → `SetFlag(then)`. Subscribes OnEnable/unsubscribes OnDisable (its own protection against the missing static-event reset).

- ⚠️ **"Next rollover", not "after N days"**: a flag set at 23:50 fires 10 game-minutes later. Multi-day delays = chained flag pairs, and `SetTime` multi-day skips advance only ONE link per call.
- Stateless — both flags persist via GameScores (save slot). Idempotent across days (`SetFlag` no-ops when already set).
- Current pairs (one instance in `Scene_Hollowfen`): `knife_commissioned → knife_ready` (Joren's overnight forging) · `wenmar_tax_paid → theo_wagon_arrived` (Theo's wagon comes with the dawn) · `tonic_delivered → edda_check_due` (Brightspore return-next-day beat) · `shutters_funded → cottages_reopened_2` (the Veyrwick cousin arrives the dawn after Pell's shutters are funded).

## Dev affordances

- `TimeManager.SetTime(day, hour)` — the only skip path (used dynamically by the play-mode harness; no in-repo callers). Remember: no OnSundown, no save, one OnDayChanged.
- Shorten `_minutesPerGameDay` in inspector for real-time fast-forward (fires everything naturally, including sundown).

## Gotchas

- **Sundown edge-trigger**: loading a save already past 19h never fires `OnSundown` that day — TaxDeadline pressure resumes next sundown.
- **No static-event reset**: `OnDayChanged`/`OnSundown` are never nulled by a `ResetOnLoad` — with domain-reload off, stale subscribers leak across plays. New subscribers MUST pair OnEnable/OnDisable. (Hardening candidate: add the reset — TODOS.)
- **Parallel arrays** in DayFlagScheduler: length mismatch silently truncates; typo'd flag ids fail silently. (EditMode test candidate.)
- Hour drift between saves is lost on load (day/hour snap to last save).
