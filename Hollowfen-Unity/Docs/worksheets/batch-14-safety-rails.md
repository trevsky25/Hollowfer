# Batch 14 — Phase 2 Safety Rails: Integrity Checks, Linter, Pre-commit Gate

**Date:** 2026-07-11 · **Status:** committed (tag `batch-14`)

## Goal
The net under autonomous content work (TODOS items 3+4): asset-layer data-integrity checks catching the silent failure classes the doc-backfill audit identified, a gotcha linter for conventions.md's prohibited patterns, a pre-commit gate wiring both into every commit, and the play-mode smoke test promoted to a permanent tool. Prerequisite for night-shift authoring — Act II B triples content volume next.

## Plan
- [x] `DataIntegrity.cs` (editor utility) — 13 check categories; menu item + bridge + batchmode entry points
- [x] Negative test: corrupt a committed quest asset → checker reports both faults → git restore → clean
- [x] `lint_hollowfen.py` + `lint_waivers.txt` — 5 rules; negative-tested (planted emoji + meta-less files caught)
- [x] `.githooks/pre-commit` + `core.hooksPath` — lint always, integrity when bridge up; dry-run passed
- [x] `smoke_play.py` promoted from scratchpad (App Nap activation built in)
- [x] `Docs/tests.md` manifest — what each check proves + explicit NOT-covered list
- [x] TODOS/README updates, dashboard regen

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Test vehicle | Editor utility, NOT Unity Test Framework | Game code compiles into Assembly-CSharp (StarterAssets/Magic Pig have no asmdefs); test asmdefs can't reference it. Same assertions, and bridge/batchmode-runnable is better for night shift anyway |
| Check targeting | Only failures that are SILENT at runtime | Localization.Get returns raw id on miss, PickDialog skips nulls, extra relationship ids ignored — the console never reports these; loud failures don't need tests |
| public-field rule scope | Only MonoBehaviour/ScriptableObject classes | First run produced 101 warnings, all acceptable data structs/DTOs — noise nobody reads is worse than no rule; refined to the actual violation class (0 false positives after) |
| Relationship-id validation | Against a bible cast constant, not NPC assets | GameScores keys by string; edda has deltas but no asset (correct) — asset existence is the wrong invariant, canon membership is the right one |
| Integrity in pre-commit | Skip-with-warning when bridge down | Blocking commits when Unity is closed punishes doc-only commits; agent commits always have Unity open so they get the full gate |
| lint `--fix` | Not implemented v1 | Nothing mechanical to fix safely yet (.meta must be Unity-generated); reserved |

## Verification evidence
- Checker: clean baseline (0E/0W over 10 quests, 19 dialogues, 5 NPCs, 8 locations, 17 mushrooms) → planted corruption in `Quest_Act2_10_FirstTax` (bogus id `brem` + parallel-array mismatch) → **both caught with exact paths** → git restore → clean again. Compile verified via bridge.
- Linter: 0 errors with 1 justified waiver (LocationDebugHUD legacy-input → owned by build-cleanup TODOS item); planted emoji 🍄 + two meta-less files → **all 3 caught** → removed → clean. Warning noise eliminated (101 → 0 false positives).
- Pre-commit dry-run: lint + bridge integrity both ran, exit 0.
- This batch's own commit passed through the live hook (first real gated commit).

## Docs updated
- `Docs/tests.md` — NEW: manifest of all three layers + NOT-covered list + waiver policy
- `tools/agent/README.md` — three new script rows + pre-commit note; wanted-list pruned
- `TODOS.md` — items 1–4 marked done with outcomes

## Unfinished / handoff
- `core.hooksPath` is per-clone config — a fresh clone must re-run `git config core.hooksPath .githooks` (documented in tools README).
- Known gaps deliberately deferred: scene-component validation, quest-flow behavior tests, unlockAt semantics, hardcoded-string detection — all listed with owners in tests.md.

## Feedback to Trevor
- The 101→0 warning-noise fix is the lesson worth keeping: a check's value is signal density, not coverage. Every rule should be tuned until its output is actionable, or it trains everyone to ignore the tool.
- The corrupt→detect→restore drill should become the periodic false-confidence audit (tests.md lists it) — checks rot when the code they reflect on moves.
