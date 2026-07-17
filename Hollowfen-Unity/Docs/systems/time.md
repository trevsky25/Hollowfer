# Time System
Real-time game clock plus a continuous visual cycle: `TimeManager` advances `Hour` from `Time.deltaTime` (default 20 real minutes per game day). Natural midnight and the mill-hearth `RestSpot` cross the same sundown/dawn boundaries, `DayNightLighting` renders that shared hour, and `NPCSchedule` consumes it for village routines.
Key scripts: `Assets/_Hollowfen/Scripts/Time/` — TimeManager, DayNightLighting, NightLight, DayFlagScheduler, RestSpot.
Clock: `int Day` (1-based) + `float Hour` (0–24); `IsNight` = before 5.5h or after 20.5h; new game starts Day 1 @ 14h; sundown is 19h.
Rest: after `findJournal`, interacting at the mill hearth advances daytime to dusk, or evening to the next dawn, behind a short unscaled fade; the destination is a full-save checkpoint.
Lighting: the clock continuously blends key light, trilight ambient, procedural sky, fog, reflections, exposure/color grade, and six warm shadowless village practicals; late night is deliberately dark but remains navigable under cool moonlight.
Biggest gotchas: gameplay skips must use `AdvanceTo`, not `SetTime`; `DayNightLighting` is the sole writer for global lighting; edit the static art-direction states together rather than stacking another global exposure volume.
Status: repeatable clock/rest loop, ordered boundary events, five-phase lighting, and the first four time-aware NPC routines play-mode and visually verified 2026-07-16.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## TimeManager

- **Natural advancement**: `Update()` adds `Time.deltaTime * (24 / (_minutesPerGameDay * 60))`. `timeScale = 0` freezes it for menus/dialogue. Crossing 19h fires `OnSundown`; crossing midnight increments Day, fires `OnDayChanged(Day)`, then performs `SaveCoordinator.SaveAllWithPlayer()`.
- **`AdvanceTo(day, hour, saveCheckpoint)`**: forward-only gameplay jump. It walks the absolute timeline in order, emitting every crossed sundown and dawn exactly once before landing on the target. Rest uses this path. A final full save is optional and defaults on.
- **`SetTime(day, hour)`**: direct test/debug setter. It wraps the hour and emits one `OnDayChanged` when the numeric day changes, but deliberately does not reconstruct sundowns or intermediate days. Do not use for authored sleep/wait mechanics.
- **Hydration**: `Awake` reads the active slot. `GameDay > 0` restores; legacy `GameDay == 0` starts Day 1 @ `_newGameHour` (14h).
- **Lifecycle safety**: SubsystemRegistration clears `Instance`, `OnDayChanged`, and `OnSundown`, preventing disabled-domain-reload subscribers from leaking between plays.
- **Lighting handoff**: every ordinary tick and explicit clock jump calls `DayNightLighting.Apply(Hour)`. The clock owns time and boundary events; it no longer contains rendering policy.
- **Routine handoff**: `NPCSchedule` reacts to day/sundown events and polls the continuous hour at low frequency so non-boundary starts such as 18:30 still resolve. Placement policy stays in the NPC system.

## DayNightLighting

`DayNightLighting` is authored on `_TimeManager` and is the single global-lighting owner. Ten smooth keyframes cover deep night → dawn → full day → golden hour → dusk → deep night without a binary pop at sunrise or sundown.

- **Key light**: the scene directional light becomes a warm sun by day and a low-intensity blue moon at night. Rotation, color, and intensity all interpolate.
- **Environment**: `RenderSettings` uses art-directed Trilight sky/equator/ground colors. Fog color/density and reflection intensity deepen with the night.
- **Sky**: a runtime-only clone of the vendor procedural skybox changes exposure, tint, ground color, and atmosphere thickness. The source material is never modified.
- **Post processing**: a runtime-only priority-100 URP `VolumeProfile` owns exposure, contrast, saturation, white balance, color filter, and vignette. This intentionally overrides the environment pack's fixed `+1` exposure, which previously made night look like dim daytime. Bloom and unrelated vendor effects remain untouched.
- **Lifecycle**: runtime skybox/profile objects use `HideAndDontSave`; destruction restores the scene's original RenderSettings and material references.
- **Debug/test readback**: `NightBlend`, `CurrentExposure`, `CurrentSkyExposure`, and `CurrentFogDensity` expose the evaluated state without making the keyframe table mutable.

## NightLight

Six broad point lights under `_NightLighting` create warm pools at the Crooked Pintle, Joren's forge, village well, Edda's cottage, chapel, and father's mill. They fade from `DayNightLighting.NightBlend`, use restrained per-light Perlin flicker, cast no shadows, and disable below a 1% blend. They are lighting rigs for windows/hearths, not final visible lantern props; the later world-dressing pass may replace their fixtures without changing the cycle.

## RestSpot

`RestSpot` implements `IInteractable` and is authored once as `_RestSpot_MillHearth`, colocated with the journal/hearth interaction area. It is unavailable until `findJournal` completes.

- Before 7h → rest to 7h that day.
- From 7h to before 19h → rest to 19h that day.
- From 19h onward → rest to 7h the next day.
- A `ConfirmModal` asks before resting when the shared UI exists; direct gameplay-scene testing falls back cleanly without logging a missing-modal error.
- The transition pauses scaled time, disables player/interactor input, fades through black in unscaled time, calls `AdvanceTo`, saves, and restores the exact previous time scale.

## DayFlagScheduler

Inspector-authored `_whenFlags[]`/`_thenFlags[]`. On each real `OnDayChanged`, every satisfied pair sets its destination flag. Current pairs include knife completion, Theo's wagon arrival, Edda's follow-up, and cottage reopening.

- A rule means **next crossed dawn**, not “24 hours after the flag.”
- Both flags persist through `GameScores`, so repeating later dawns is idempotent.
- `AdvanceTo` can cross multiple dawns and therefore advances multi-link schedules naturally; `SetTime` cannot.

## Gotchas

- Loading after 19h does not retroactively fire that day's sundown.
- Day rollover/full rest writes synchronously; this is intentional checkpoint behavior but could hitch on a slow disk.
- Parallel DayFlagScheduler arrays silently truncate to their shorter length; typoed flags remain an authoring risk.
- Any other system writing the sun, ambient, fog, skybox, reflection intensity, or time-of-day color grade every frame will fight `DayNightLighting`.
- The six village practicals are intentionally shadowless and spatially separated: adding overlapping shadowed lanterns later needs a Steam Deck frame-time check.
- Direct `SetTime` calls update the global cycle immediately; practical lights fade on ordinary play and can use `NightLight.RefreshImmediate()` in tests/cinematic cuts.
