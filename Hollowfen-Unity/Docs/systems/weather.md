# Dynamic Weather

`WeatherSystem` turns the saved day/hour into a deterministic six-period forecast and projects that state into presentation and gameplay. It is attached at runtime to the gameplay `TimeManager`; no scene-authored weather object or extra save field is required.

Key scripts: `Scripts/Weather/{WeatherSystem,WeatherPresentation,WeatherAudio}.cs`; lighting seam: `Scripts/Time/DayNightLighting.cs`; HUD: `Scripts/UI/ClockHUD.cs`; focused verifier: `Scripts/Editor/WeatherSystemVerifier.cs`.

## Clock domain

Each day has six four-hour periods. `WeatherSystem.Resolve(day, period)` uses a stable versioned hash, so the same journal day always produces the same Clear, Overcast, Morning Mist, Drizzle, Rain, or Storm result. The final 0.55 hour of a period blends into the next profile. Day one downgrades storms to rain, and mist is biased toward dawn/morning.

`WeatherState` owns precipitation, mist, wind, wetness, key/ambient/fog multipliers, exposure, saturation, temperature, and light tint. `DayNightLighting` remains the sole writer of global lighting and composes those values with its normal clock curve. New presentation systems must extend this modifier instead of writing `RenderSettings` independently.

## Presentation and accessibility

`WeatherPresentation` keeps precipitation on a camera-local box rather than filling the 500 m map. It caps live particles at 720 (420 on low quality), disables shadows, reuses one runtime material, and uses world-space fall. An upward ray samples cover every 0.22 seconds; exposure eases between outdoor and sheltered values. Reduced Motion lowers emission while preserving an unmistakable wet-weather state. One directional `WindZone` follows the weather profile.

`WeatherAudio` builds and caches three runtime clips: seamless rain, seamless wind, and thunder, all 48 kHz mono. Sources are routed to `AmbienceManager.Output` even when initialization order changes. Shelter lowers rain/wind volume and closes a rain low-pass filter; lightning supplies a deterministic distance and thunder delay.

The clock pill uses `ForecastLabel` to show current weather plus the next state/period. All player-facing names and formats use `Localization`.

## Gameplay hooks

- `GrowBed` integrates weather-adjusted elapsed hours, with wet periods faster than dry ones.
- `ForageNodeStates` can shorten a multi-day wild cooldown by one day when the intervening forecast is sufficiently wet; minimum cooldown remains one day.
- `VillageRequests.RewardFor` adds an authored wet-weather premium only to recurring requests. Story deliveries never vary their reward.

These are bounded modifiers, not survival penalties: no weather damages inventory, blocks quests, or strands a save.

## Verification

Run `WeatherSystemVerifier.RunAll()` in `Scene_Hollowfen` Play Mode. It samples 40 deterministic days, every weather state and profile bound, day-one safety, live lighting/fog, the camera rig, audio formats, HUD, growth, respawn, and delivery integration. Follow with the normal 240+ frame smoke and a visual rain/shelter pass; console warnings from particle configuration are treated as failures.
