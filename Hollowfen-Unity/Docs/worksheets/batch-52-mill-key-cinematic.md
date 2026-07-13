# Batch 52 — Cinematic mill-key handoff (Meshy key model)

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-52` (pending)
**Directive (Trevor):** dropped 3D models for the mill key + mushroom journal in `Resources/`. "Bram should
hand it off to Wren, Wren should use it to unlock the door… great visuals for these mill key interactions."
(This is the 51d cinematic-pickup work, now unblocked by the meshes.)

## Assets
- `Resources/Mill Key/Meshy_AI_Rusted_Skeleton_Key_..._texture.fbx` (27k verts, ~2cm mesh) + PBR set.
- Built `Materials/Mat_MillKey.mat` (URP/Lit: albedo `_BaseMap`, `_BumpMap` normal — normal texture set to
  NormalMap type, `_MetallicGlossMap`, metallic 0.85 / smoothness 0.32 — rusted iron).
- `Prefabs/MillKey.prefab` — FBX instance, Mat_MillKey, uniform scale 6.8 → ~0.13m long (hand-key sized).

## New reusable engine
- **`Cinematics/PropFocusCinematic.cs`** (namespace Hollowfen.Cinematics) — self-created persistent
  singleton (survives the interacted prop deactivating). `Play(target, distance, heightOffset, fov,
  pushSeconds, holdSeconds, restoreSeconds, onPeak, onDone)`: takes over Camera.main (disable
  CinemachineBrain, cache the gameplay pose), pushes in on the target from the camera's current side,
  holds (re-aiming each frame so a spinning prop stays framed), glides back + re-enables the brain.
  Suspends player input for the move. Unscaled time. Mirrors DialogueCinematics' takeover. Reused by the
  journal reveal next (batch-53).
- `DialogueCinematics.IsActive` getter added so PropFocus waits out the dialogue camera's restore.

## Mill-key handoff
- **`Quests/MillKeyHandoff.cs`** — subscribes to `KeyItems.OnGranted`; on `item.mill_key` (end of Bram's
  key dialogue) it waits for the dialogue camera to finish, spawns the key floating in front of Wren at
  chest height, spins/bobs it (hero item-get), and runs PropFocusCinematic push-in. KeyItemToast auto-fires
  on the grant. Tunable framing/rotation fields.
- Scene: a `_MillKeyHandoff` object with the component + MillKey.prefab ref.

## Verification (play mode, fresh save — cleared, restored pristine after)
- [x] Compiles clean.
- [x] Granting `item.mill_key` (as Bram's dialogue does) fires the handoff: key spawns in front of Wren,
      PropFocusCinematic pushes in, KeyItemToast slides in ("Mill Key"). Restores clean: `IsPlaying`
      false, key despawned, `PlayerInteractor.Suspended` false, `_HUDCanvas` alpha back to 1,
      CinemachineBrain re-enabled.
- [x] Visual pass (tuned across 4 screenshots): first pass had the HUD visible + key shaft-vertical
      (`b52_key_hero2.png`); added HUD-hide + letterbox + face-on rock → `b52_key_hero3.png`; the Meshy
      pivot framed the key off-centre → aim at renderer-bounds centre → `b52_key_hero4.png` (well-centred
      rusted skeleton key, bit/shank/bow all reading, letterbox, HUD hidden). "Video-game-quality."
- Note: verified via the KeyItems.Grant path (identical to the dialogue's grant) + the explicit
  wait-for-`DialogueCinematics.IsActive`-false guard; the real dialogue→handoff hand-back rides that guard.

## 52b — Key-in-lock door unlock (DONE this session, tag `batch-52b`)
`KeyLockedDoor` gains an optional cinematic unlock (falls back to the instant unlock when no key prefab is
set). On unlock: the MillKey spawns at a fixed `KeyholeAnchor` (child of the lock, so it stays put while
the door swings), PropFocusCinematic frames it **straight-on along the door's face normal** (new `frameDir`
override — the studded mill door has no keyhole, so an angled shot read badly), the key turns 95° (the
unlock twist), then the door swings open (key parented to the door leaf, swings away with it) revealing the
dim mill interior — a "crossing the threshold" reveal that fits the bible's empty-mill scene. Then despawn
+ complete searchMill + restore.
- PropFocusCinematic: added the optional `frameDir` framing-direction override + a `_keyScale` bump so the
  key reads on a keyhole-less door.
- Play-verified end-to-end: key turns, door opens (collider dropped → passable), searchMill completes, HUD
  back, player resumed. Screenshot `b52b_lock2.png` (threshold reveal into the mill interior).
- Honest note: the handoff (52) is the hero "great visual"; the door is a moodier threshold reveal — the
  key-turn beat is brief and the keyhole-less studded door limits a tight lock closeup. A future polish
  pass could add a bespoke lock plate + a two-beat (turn → threshold) cut.

## Gotcha logged
Saving `Scene_Hollowfen` re-serializes ~3 prefab-instance rotation quaternions by a few degrees each save
(Unity re-normalization; grows slightly per save). Small + imperceptible but cumulative — worth a dedicated
"scene-save churn" investigation (identify the 3 instances; likely a denormalized authored rotation).

## Next
- **Batch 53** — journal discovery cinematic (reuse PropFocusCinematic for the push-in into the book → the
  batch-51b painted-spread finale).
