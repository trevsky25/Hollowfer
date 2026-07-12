# Batch 13 — Production Dashboard + Questions Inbox

**Date:** 2026-07-11 · **Status:** committed (tag `batch-13`)

## Goal
Trevor's "morning after" surface for autonomous runs: a sprint-board-style dashboard showing open questions, shipped batches (with verification evidence), the decision log, and the work queue — generated from repo state so the night shift regenerates it at wrap-up. Plus `QUESTIONS.md`, the decision inbox agents park Trevor-only decisions in instead of guessing.

## Plan
- [x] `QUESTIONS.md` (repo root) — inbox convention + 3 seed questions from today's audits
- [x] `tools/agent/dashboard.py` — stdlib-only generator: parses TODOS.md snapshot/queue, QUESTIONS.md open items, worksheets (goal, decisions tables, verification, feedback), git log/tags → static HTML in the game's field-journal design language (parchment/ink/gold/moss, Georgia serif, eyebrow labels)
- [x] Published as a Claude Artifact (stable private URL, republished on each regen)
- [x] Router workflow updated: step 5 = park decisions in QUESTIONS.md; step 6 = regenerate dashboard at wrap-up
- [x] Verified render in browser (dark theme), fixed charset + markdown-rendering bugs found during verification

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Dashboard tech | Static HTML generator in tools/agent + Claude Artifact hosting | Regenerable by any agent at wrap-up; stable URL Trevor can check from anywhere; no server to run |
| Visual language | The game's own palette + Georgia serif (field-journal look) | Honor the existing design system; the board reads as part of the project |
| Questions home | Dedicated root `QUESTIONS.md`, not worksheet sections | One inbox to check; worksheets keep per-batch feedback, QUESTIONS.md holds blocking decisions with recommendations |
| Data sources | Parse the artifacts we already maintain (TODOS/QUESTIONS/worksheets/git) | No second bookkeeping system to keep in sync — the board is only ever as stale as the repo |

## Verification evidence
- Generator runs clean from repo root; output served locally and inspected in the browser (dark theme screenshot in session log): header/stats/questions/shipped/decision-log/queue/commits all render, chips + collapsibles work.
- Render check caught and fixed: missing `<meta charset>` (mojibake em-dashes), raw markdown in the snapshot line, stale TODOS snapshot text, stale batch-11 worksheet status.
- Artifact published and republished to the same URL (https://claude.ai/code/artifact/cc6edb78-f4cd-4f44-a608-fc51635870c3).

## Docs updated
- `Hollowfen-Unity/CLAUDE.md` — workflow steps 5–6 (questions inbox, dashboard regen at wrap-up)
- `tools/agent/README.md` — dashboard.py row; unitymcp.py marked verified + library usage

## Unfinished / handoff
- Dashboard republish from a *different* conversation requires passing the artifact URL as `url` to the Artifact tool (noted here so night-shift sessions keep the stable link).
- Future: fold screenshot thumbnails (Phase 3 visual regression) into the Shipped cards.

## Feedback to Trevor
- The render check paid for itself immediately — four bugs found by actually looking at the output instead of trusting the generator. This is the visual-verification habit Phase 3 formalizes.
