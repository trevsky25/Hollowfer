# Batch 115 — Dynamic Hollowfen Weather

**Date:** 2026-07-19 · **Status:** complete

## Goal

Make weather a legible, deterministic part of exploration and village work—not a cosmetic random effect. Rain, fog, cloud, wind, shelter, sound, field growth, wild flushes, and market demand must agree with one saved-world forecast while preserving day/night lighting ownership and bounded performance.

## Delivered

- Six four-hour forecast states: Clear, Overcast, Morning Mist, Drizzle, Rain, and Storm.
- Stable day/period resolution derived from the saved clock; old saves require no schema migration and reload the same forecast.
- Smooth final-33-minute transitions rather than hard sky cuts; onboarding day never rolls a storm.
- `DayNightLighting` remains the sole sun/sky/fog/post owner and receives a composable weather modifier.
- Camera-local rain with a 720-particle ceiling (420 on low quality), directional wind, upward shelter tests, indoor rain suppression, and reduced-motion emission scaling.
- Procedural 48 kHz mono rain, wind, and distance-delayed thunder routed through the ambience mixer; shelter applies volume and low-pass occlusion.
- The clock HUD carries current weather and the next named period.
- Wetness speeds cultivated growth, can shorten wild flush cooldowns, and adds four copper to authored rain-sensitive Theo orders.

## Verification evidence

- `WeatherSystemVerifier.RunAll()` — PASS across a deterministic 40-day domain, every weather type, no day-one storm, live clear/rain lighting, particle/audio bounds, shelter rig, growth, respawn, delivery, forecast, and accessibility hooks.
- Rain was forced in the live gameplay camera for visual inspection; streak width/opacity/trail length were refined after the first capture.
- 360-frame gameplay smoke — zero new console errors.
- Post-verifier console audit — zero errors.

## Manual QA route

Play through one wet forecast outside, enter the mill and apothecary, then return outside. Confirm rain/audio fall away under cover without a hard pop, the forecast remains readable, and no particle emitter follows the player into an interior view. Rest across a four-hour boundary to judge the transition and try Theo's rain-sensitive request during both wet and dry periods.
