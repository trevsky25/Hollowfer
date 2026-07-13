# Batch 45 — Cinematic dialogue camera (Cinematic Pass #3)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-45` (pending)
**Directive:** Trevor — "make a cinematic dialog interaction between Wren and Bram. Pan from character to
character, zoom-ins, etc. Visually amazing."

## Design — DialogueCinematics (new, Scripts/Dialogue/DialogueCinematics.cs)
A procedural camera director that owns Camera.main while a dialogue plays (the `isCloseup` field and the
"dialogue gets its own cinematic treatment" note in MushroomFocusCamera have waited for this since batch-17).
Film grammar, all computed from the two speakers' head positions:

- **Anchors:** player (tag Player) = Wren; the NPC/prop transform passed through `DialogueScreen.Open`.
  Head = SkinnedMeshRenderer bounds top − 12% height (fallback +1.6m).
- **180° rule:** one side vector (`cross(up, A→B)`) chosen at Begin; every shot stays on that side of the
  axis, so eyelines never flip.
- **Establishing two-shot** at Begin: lateral side-on frame of both, slow dolly drift.
- **Per line:** speaker != "Wren" → over-Wren's-shoulder framing the NPC; "Wren" → the reverse OTS.
  `isCloseup` → tighter single (FOV 27, nearer, off-axis). Speaker change = eased **pan/dolly glide**
  (~1.1s smoothstep of pos+rot+FOV — a camera move, not a cut); same speaker again = deepen the push.
- **Push-in:** every line slowly dollies toward the speaker for its duration (VO length or text estimate).
- **Handheld breath:** Perlin-noise sway (±12mm, ±0.25°) over everything — kills the tripod deadness.
- **Choices:** pull back to the two-shot while the player decides.
- **Occlusion guard:** linecast lookAt→camera, pull in to 88% of any hit (QueryTriggerInteraction.Ignore).
- **Freeze-proof:** dialogue runs at timeScale 0 — the director animates on unscaled time and flips both
  speakers' Animators to UnscaledTime for the scene (restored after), so Bram keeps breathing in closeups.
- **Camera ownership:** CinemachineBrain disabled at Begin (pose cached), glide-restored + re-enabled at End.

## Integration
- `DialogueScreen.Open(DialogueData, Transform anchor)` overload; NPCInteractable + QuestInteractable pass
  `transform`. ShowCurrentLine → `OnLine(speaker, isCloseup, estDuration)`; BeginChoices → two-shot;
  Close → `End()`. No dialogue *data* changes.

## Verification (play mode, Scene_Hollowfen, Bram homecoming dialogue at the well)
- [x] Compiles clean.
- [x] Establishing two-shot: Wren left / Bram right in profile, FOV 40 (`dlg_01_establishing.png`).
- [x] Bram OTS: Wren's shoulder soft in the left foreground, Bram framed on "Wren?" (`dlg_02_bram_ots.png`).
- [x] Wren reverse: after body-aware offsets, clean single on Wren, face upper-center
      (`dlg_04_wren_reverse_fixed.png`) — first pass had Bram's shoulder eating half the frame
      (`dlg_03`), fixed by scaling OTS offsets with listener ShoulderRadius.
- [x] Closeup: distance-scaled single on Bram, whole face in the letterbox safe area
      (`dlg_06_bram_closeup_fixed.png`) — first pass framed his chest; fixed by scaling closeup distance
      with speaker radius + head-height camera.
- [x] Restore: Close → camera glided back to the exact cached follow-cam pose (287.4, 38.4, 152.7),
      FOV 40, CinemachineBrain re-enabled, timeScale 1, Bram's Animator back to Normal (disabled parent
      Animator untouched — CollectAnimators skips disabled).

## Tuning knobs (constants at the top of DialogueCinematics)
GlideSeconds 1.1 · RestoreSeconds 0.55 · PushInFraction 0.045 · SwayPos 0.012 · SwayDeg 0.25 ·
OTS FOV 34 · closeup FOV 29 · two-shot FOV 40.
