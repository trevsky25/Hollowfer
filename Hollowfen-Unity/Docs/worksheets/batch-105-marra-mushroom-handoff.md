# Batch 105 — Marra mushroom handoff + 4K story paintings

**Date:** 2026-07-18 · **Status:** complete

## Goal

Make Marra and Bram's first-basket scene hold up as a production cinematic: preserve three existing
kitchen compositions as crisp 4K masters, prevent Unity from recompressing them, and stage Wren's
Goldfoot as a real 3D handoff to Marra immediately before Marra identifies it.

## Plan

- [x] Trace the first-basket dialogue, story interstitial, payment outcome, participant anchors, art masters, and importer settings.
- [x] Replace the three 1672×941 paintings with composition-preserving 4K masters and enforce lossless runtime imports.
- [x] Add a reusable, data-authored mushroom-handoff cue to dialogue and author the Goldfoot beat before Marra's identification line.
- [x] Verify the cue, 3D presentation prefab, camera ownership, art dimensions/imports, dialogue data, and Play Mode behavior.
- [x] Update dialogue documentation and record final verification.

## Narrative contract

- The handoff occurs after Wren says she checked Father's journal and before Marra says
  “Goldfoots.” That lets Marra's identification land as the payoff to the live model inspection.
- The handoff is presentation-only. Inventory remains untouched until the authored first-payment
  node completes, preserving the existing atomic quest/payment outcome.
- The cinematic uses the canonical Goldfoot journal-preview prefab, not a lookalike or detached
  duplicate mesh, and destroys the temporary presentation instance on completion/cancel.

## Implementation

- Restored the three existing Marra compositions with the built-in image-edit workflow, then created
  exact 3840×2160 project masters. The original generated outputs remain in Codex's image cache; the
  project consumes the 4K copies under `UI/StoryCards` and `UI/StoryMoments/MarraKitchen`.
- `MarraKitchenStoryMomentImporter` now delegates every texture to
  `StoryArtQualityImporter.Configure`, eliminating its old 2048/CompressedHQ override. The shared
  verifier confirms all 93 story paintings are uncompressed, mip-free HD assets.
- `DialogueMushroomHandoffCue` authors a species, recipient, trigger line, and model height without
  mixing presentation into inventory outcomes. `DataIntegrity` rejects bad indices, recipients,
  missing 3D prefabs, and repeat-dialogue use.
- `DialogueScreen` pauses before the cue's target line, stops VO/talking, hides the parchment panel,
  and resumes only after the live transfer returns.
- `DialogueCinematics` resolves Marra, uses the nearest valid humanoid hands (with body fallbacks),
  normalizes and sanitizes the canonical Goldfoot preview, and carries it through a 28° close insert
  on an eased arc with a small night-readable fill. Close/destroy cancels and cleans up safely.

## Verification

- Unity compile/domain reload: zero compiler errors; standard script validation: zero errors.
- `Marra Kitchen Sequence`: PASS — 3 voiced 4K paintings + authored Goldfoot cue.
- `HD Story Art`: PASS — 93 paintings HD, uncompressed, mip-free.
- Unity `Data Integrity Report`: 0 errors / 0 warnings.
- CLI lint: 0 errors / 0 warnings (1 existing waiver); CLI data integrity: 0 / 0.
- Focused Play Mode verifier: PASS — Bram live speaker resolution, Marra recipient resolution,
  temporary Goldfoot creation, and close/cancel cleanup.
- Runtime visual proof: cue auto-fired at line index 10; panel paused; prop bounds 0.36×0.31×0.33m;
  committed insert reached FOV 28 at 1.76m and presented the gold stems/ridges clearly.
- General smoke: PASS at 241 synchronously stepped frames, 0 pre-play/in-play/post-stop errors.
- `git diff --check` is clean for this batch; unrelated pre-existing Scene_Hollowfen YAML whitespace
  remains outside this batch's files.

## Unfinished / handoff

Do not create or run a player build unless Trevor explicitly requests one.
