# Batch 39 — Full intro VO, Bram model + voice, lively welcome

**Date:** 2026-07-12 · **Status:** DONE (play-verified) · tag `batch-39` (pending)
**Directive:** Trevor — "the intro after the first light is missing VO — I want audio for the entire beginning
scene. Make the 'first light' welcome more lively/animated. Add my Bram Meshy model as the Bram NPC (no
character loads now). Make Bram's voice more natural and old."

## 1. Full-intro VO (Trevor called the Q10 direction for the opening)
Generated the **6-beat homecoming intro** as VO (Narrator = `af_heart` @0.86) via the existing Kokoro env —
`HomecomingIntro/00..05_Narrator.wav` (5.2 / 13.0 / 3.8 / 2.1 / 8.5 / 7.2 s). `generate_vo.py` `INTRO_CAPTIONS`
was stale (2 beats) → updated to the bible's 6. `StoryBeats._introVoiceClips` rewired to all 6, index-matched;
`ShowCinematic` passes them directly (dropped the 0/last-only mapping). The whole opening narration is now
voiced (both the direct-load and the batch-38 welcome→intro flows use this path). Play-verified: `00_Narrator`
assigned + playing (t advanced).

## 2. Bram voice — older + more natural
`VOICES["Bram"]` `bm_george`@0.95 → **`bm_lewis`@0.86** (deeper/gravellier + slower for age). Regenerated all
three Bram dialogues (Homecoming, CrookedPintle key, Repeat) — same filenames, so the existing wiring holds.

## 3. Bram model — the NPC now has a character
`Assets/Characters/Bram/Bram-TPose.fbx` (Meshy, Humanoid, already tracked): instantiated as `BramModel` under
`NPC_Bram`, scale 1.0 (~2 m, correct height), seated on the ground; **textured** (Material.001 has a real
albedo — not magenta). Removed the old broken placeholder (`Mesh_0` skinned mesh + `mixamorig` rig). Gave him
a **breathing idle** so he doesn't T-pose: `Bram_Idle.controller` (Wren's `Wren_BreathingIdle` Humanoid clip
retargeted) + Animator avatar = `Bram-TPoseAvatar`. Play-verified: renders (screenshot of a preview copy — a
hatted innkeeper in coat + boots) and animates (hand drops well below head, not T-pose). Follow-ups: decimate
the 34 MB mesh before EA; a bespoke Bram idle/sweep anim would beat the retargeted Wren idle.

## 4. Lively "first light" welcome card
Added **26 drifting golden spore motes** (twinkle + drift + wrap) to the cinematic welcome — reuses the menu's
mote idiom, keeps the hero image static at the Ken-Burns A-state so the seamless handoff (batch-38) still
holds. Also rewrote `LoadingScreen` cleanly: a self-contained **CineRoot** (own hero/scrim/letterbox/motes/
welcome/loading-line) toggled only for new game via **`LoadingScreen.NextIsCinematic`** (set by SaveSlotScreen)
— fixes a latent bug where Continue/Load would have shown the "Chapter One" welcome. Fixed the welcome
title/eyebrow centering (were anchored to the right half). Play-verified: motes drift, "CHAPTER ONE /
Homecoming" centered.

## Verification
Compiles clean; lint 0/0; integrity 0/0. VO WAVs are tracked (audio is version-controlled, per precedent).

## Q10 note
Trevor has now **directed AI-VO for the opening scene** (intro + Bram). This partially answers Q10: AI VO is
the direction for the opening at least. The **Steam AI-content disclosure remains required** at EA if any AI VO
ships (pre-EA checklist). Broad remaining-cast VO still pends a full call, but the opening is now voiced.

## Docs updated
`systems/audio.md` (intro 6-beat VO + Bram `bm_lewis`) · `systems/ui-framework.md` (welcome motes + NextIsCinematic)
· `QUESTIONS.md` (Q10 opening-VO direction) · graphics-pipeline memory (Bram model shipped).
