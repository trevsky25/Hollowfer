# Batch 17 — Dialogue Choice UI + Night-Shift Orchestration

**Date:** 2026-07-11 · **Status:** committed (tag `batch-17`)

## Goal
The hard gate before Act III: player choices in dialogue (`theoCapitalOffer` needs branching). Plus the night-shift orchestration doc — the last artifact between Trevor and unattended overnight runs.

## What was built
- **`DialogueChoice[]` on DialogueData** — `text` (Wren's option) + `next` (branch dialogue, null = close) + `setsFlagId`. Semantics: the parent dialog's outcomes fire FIRST ("this conversation happened"), then choices show; each branch owns its own outcome block. Non-empty choices supersede `_nextDialog`.
- **Choice UI in DialogueScreen** — four reusable numbered pills stacked above the panel's right edge (choice 1 on top), journal register (ink pill, gold hairline, cream text, gold selection glow). Input: number keys 1–4 select instantly; arrows/WASD/D-pad/left-stick (latched) move; Space/Enter/South confirms; mouse clicks the pill. Hint line switches to choose/confirm copy. `IsChoosing` + public `SelectChoice(int)` — one door for mouse Buttons, future controllers, and the test harness.
- **Integrity checks extended** — `dialogue-choices` (≤4, no empty text, WARN on NextDialog+Choices both set) and `dialogue-chain` upgraded from linear NextDialog walk to full DFS cycle detection over the NextDialog + choice-branch graph.
- **`Docs/night-shift.md`** — the loop around batches: 3-step setup (Unity open, `caffeinate -dims`, kickoff prompt), per-batch flow, decision parking (QUESTIONS.md + skip), stop conditions (budget default 3 / red gate after one fix attempt / throttling / bridge death), hard rules (no push, no third-party, saves hygiene), end-of-shift wrap-up, gotchas list, and Trevor's copy-paste kickoff prompt. Router points to it.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Outcome ordering | Parent outcomes fire BEFORE choices | The conversation happened regardless of the pick; branches carry pick-specific outcomes — keeps authoring composable |
| Verification data | In-memory `ScriptableObject.CreateInstance` + `SerializedObject` | Zero canon pollution — no test assets in Data/ |
| Selection API | Public `SelectChoice(int)` | Mouse, pads, and tests all drive one code path; no reflection needed for the harness |
| Input | Direct device polling (matches screen's existing pattern) | The action-map consolidation is a separate owned TODOS item; consistency now beats a half-migration |
| Pill order | Choice 1 on top | First render had 1 at the bottom — caught by LOOKING at the screenshot, fixed, re-verified |

## Verification evidence
- **5-step in-play run, ALL PASSED, 0 console errors** (twice — before and after the ordering fix): open → advance → `IsChoosing=true` → `SelectChoice(0)` sets `test_picked_a` + opens branch → branch finish fires `test_branch_a_done` + closes → reopen, `SelectChoice(1)` (no branch) sets flag + closes immediately. Saves backed up/restored.
- **Screenshot proof** (`/tmp/choice_ui.png`, session log): letterboxed scene, THEO plate, "The Capital pays triple. Decide.", two pills with choice 1 selected/gold on top, updated hint line.
- Integrity 0 errors incl. the new choice checks over all 42 dialogues.

## Docs updated
- `systems/dialogue.md` (choice model, header), `tests.md` (dialogue-choices row, graph-cycle wording), `night-shift.md` (NEW), router CLAUDE.md (Step() driver note + night-shift pointer), TODOS (item 7 done; Act III A now top of queue).

## Unfinished / handoff
- Choice UI uses direct device polling like the rest of the screen — the action-map consolidation TODOS item now includes Choice1-4 bindings.
- First canon consumer of choices is Act III (`theoCapitalOffer`); Act III A (top of queue) can use light choices earlier if the bible's dialogue tables imply them — check scene specs before assuming linear.

## Feedback to Trevor
- Night shifts are live from tonight: Unity open, `caffeinate -dims`, paste the kickoff prompt from night-shift.md. Start with budget 3 and check `/usage` in the morning to calibrate.
- The pill-ordering bug is the recurring lesson: logic verification passed everything; only the screenshot caught a UX inversion. Visual regression (queue item 10) keeps earning its place.
