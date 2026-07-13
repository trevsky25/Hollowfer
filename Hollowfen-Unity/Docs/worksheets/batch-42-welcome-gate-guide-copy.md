# Batch 42 — Interactive welcome gate + actionable IntroGuide copy

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-42` (pending)
**Directive:** Trevor — (1) the first-steps guide card re-narrates the intro; make it say what to DO
(go to the well in the centre of town, then find Bram). (2) A new load freezes on a static screen that
"feels like the game froze" — need an interactive loading screen before the intro sequence begins.

## 1. Interactive welcome gate (the "frozen screen" fix)
Root cause: the scene load was already `LoadSceneAsync`, but `Scene_Hollowfen` is a heavy vendored world —
its final activation (scene integration) lands in a single frame and stalls the welcome card, which reads
as a freeze because nothing invites the player to act.

Fix (`UIManager.LoadSceneRoutine` + `LoadingScreen`): on the new-game cinematic path, load with
`allowSceneActivation = false`, wait for `op.progress >= 0.89`, then flip the welcome's loading line from
"gathering the last light…" to **"Press any key to begin"** and HOLD until the player presses
(`AnyBeginPressed` = any key / south / start / click). On press → `allowSceneActivation = true`, so the
heavy integration stall now lands *after* the player's intent (reads as "entering," not a freeze), and the
welcome card is a live, interactive beat throughout (motes drift, title holds). Non-cinematic
Continue/Load loads are unchanged. Downstream seamless handoff (hold-until-narration → cross-fade) is the
unchanged batch-38/41 path.
- Timing guard: `loading.Cinematic` resolves in `OnOpen` (after the push fade), so the routine now waits
  for the loading screen to be active before branching — otherwise it would skip both the gate AND the
  seamless handoff.
- `LoadingScreen._welcomeState`: 0 loading (pulsing dots) · 1 ready (brighter breathing "Press any key")
  · 2 activating ("entering Hollowfen"). `ShowReadyPrompt()` / `BeginActivation()` drive it.

## 2. IntroGuide copy → directive, not repetitive
`Localization.cs` guide.* + quest.arrive.objective rewritten to tell the player what to do, matching the
real quest arc (arrive → waypoint = Village Well → next quest speakBram → "Speak with Bram outside the inn"):
- title: "The Road into Hollowfen" → **"Into the Village"**
- passage: no longer re-narrates the ridge; now "…make for the well first, then find him at the inn" (names
  the well at the heart of the square, Bram at The Crooked Pintle, the mill key).
- hints trimmed to one line each (well via road/compass · Bram past the well, Interact · Esc journal).
- quest.arrive.objective: "Walk the road into Hollowfen." → "Follow the road to the well in the village square."

## 3. IntroGuide voice-over regenerated to match the new passage
The card's Narrator VO still read the old ridge passage. Updated `generate_vo.py`
`EXTRAS["IntroGuide"]` to the final passage and regenerated `Audio/VO/IntroGuide/00_Narrator.wav`
(same path/GUID → `IntroGuide._voiceClip` ref unchanged). Added a **`--extras-only`** mode to the tool so
non-dialogue card VO can be regenerated without churning the intro narration or dialogue WAVs. Ran via the
existing scratchpad Kokoro venv (Narrator = af_heart @0.86, 10.9s).

## 4. Bram: placement fix (he "doesn't show" → move him to the well)
Runtime diagnosis (play mode): BramModel SMR renders fine — bounds `(1.94, 2.12, 1.64)` at his position,
enabled, valid mesh + URP/Lit material. So he was never a render bug — he stood at (269,35,88.5) by the
Crooked Pintle, ~72 units from the Village Well the quest sends the player to, so the player never saw him.
Moved `NPC_Bram` → **(287, 37.03, 164.5)** (ground raycast = Terrain y=37.03), 4.6m from `Marker_VillageWell`,
rotY=180 facing the player's south approach — the well between them, faithful to the bible reunion. Copy made
consistent: passage "…I'll find Old Bram there", hint "Old Bram is by the well", quest.speakBram.objective
"Speak with Old Bram at the well." Did NOT touch the Animator setup (parent disabled / BramModel Animator
owns Bram_Idle — the batch-40 arrangement, verified still rendering).

## Verification (play mode)
- [x] Interactive welcome: reaches `_welcomeState=1`, line = "Press any key to begin", scene held at 0.89
      (activeScene still Scene_MainMenu). On injected keypress → Scene_Hollowfen activates, loading closes.
      Screenshot `welcome_press_to_begin.png`.
- [x] IntroGuide new copy renders (title/passage/hints) — screenshot `introguide_new_copy.png`. (Task panel
      showed Joren's Forge in the test scene because ActiveQuest was mid-game there; real intro shows the
      live `arrive` quest = Homecoming / the new well objective, verified via Localization.Get.)
- [x] Hint one-line fit after trim: confirmed in-game — HUD quest tracker showed the new objective
      "Homecoming / Follow the road to the well in the village square" + compass "Village Well · 90m".
- [x] IntroGuide VO regenerated (11.0s) at `00_Narrator.wav`; `_voiceClip` GUID `9244f33a…` still in
      Scene_Hollowfen → ref intact, new audio plays. Final passage matches card + `guide.passage`.
- [x] Bram moved to (287, 37.03, 164.5), 4.6m from Marker_VillageWell, facing the approach; scene saved.
      Renders fine (runtime SMR bounds healthy pre-move; only position changed, to open terrain ground).
- Both edited scripts validate clean (0 errors). Trevor verifying final visuals.
