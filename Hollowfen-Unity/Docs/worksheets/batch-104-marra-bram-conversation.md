# Batch 104 — Bram in Marra's first conversation

**Date:** 2026-07-18 · **Status:** complete

## Goal

Stage Bram's live character model beside Marra for the Act I First Sale conversation and make the
dialogue camera resolve Bram's authored lines to Bram instead of treating every non-Wren line as Marra.

## Plan

- [x] Trace Marra's first-basket and first-payment dialogue chain, NPC routing, schedule, and camera anchor.
- [x] Add a first-match First Sale schedule override that places Bram at `Bram_Pintle`.
- [x] Resolve nearby named NPC participants per dialogue line and keep all participant animators unscaled.
- [x] Extend focused schedule and Bram character verifiers for the three-character scene.
- [x] Compile, run the focused Play Mode verifiers, and visually inspect the opening Bram shot.
- [x] Run lint/data-integrity checks and finalize the system docs.

## Root cause

`firstSale` routes Marra to `Dialogue_Act1_MarraKitchen_FirstBasket`, whose first line and several later
lines are spoken by Bram. Bram's schedule had no First Sale override, so his only eligible slot was the
village well. Separately, `DialogueCinematics` had only Wren plus the interacted transform and treated
every speaker other than Wren as that one transform. Even if Bram were nearby, a Bram line still framed
Marra.

## Implementation

- Bram's ordered schedule now puts `First sale at the Pintle` ahead of evening and well slots.
- The dialogue camera indexes nearby `NPCInteractable` and `DialogueSpeakerAnimator` identities when a
  conversation begins, then selects the matching live transform on each non-Wren line.
- Every indexed participant animator uses unscaled time during dialogue and restores its prior update mode.
- Focused verifiers cover the quest override, enabled Bram renderer, talking state, and resolved camera anchor.

## Verification

- Unity script compilation and domain reload completed without compiler errors.
- `NPCScheduleVerifier`: PASS, including Bram's `firstSale` placement at `Bram_Pintle`.
- `BramCharacterVerifier`: PASS, including enabled renderer, talking state, and `NPC_Bram` camera resolution.
- Live Play Mode spot-check: the first authored speaker was `Bram`, the resolved anchor was `NPC_Bram`,
  Bram was visible, and his model stood 3.61m from Marra.
- `lint_hollowfen.py`, `run_integrity.py`, and `git diff --check`: PASS.

## Unfinished / handoff

Do not create or run a player build until Trevor explicitly requests one.
