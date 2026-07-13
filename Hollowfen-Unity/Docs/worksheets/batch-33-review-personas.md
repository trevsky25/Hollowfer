# Batch 33 — Review personas (Phase 3 infra)

**Date:** 2026-07-12 · **Status:** DONE · tag `batch-33` (pending)
**TODOS item:** #10 — "Phase 3 infra: review personas."

## What shipped
`Docs/review/` — the end-of-batch adversarial gate, formalized. Pairs with night-shift.md Model Tiering:
tiering says WHEN to review at all; the personas say WHICH lens + WHAT to check.
- `README.md` — router: the fan-out routing table (batch surface → persona → model), the invocation recipe,
  and the shared **verdict protocol** (PASS / PASS WITH CHANGES / BLOCK). Encodes the batch-32 precedent.
- `narrative-bible.md` (fable) — canon guardian: no invented NPCs/species/locations, cast voice, no
  romance/emoji/slang, "Lord help me" ceiling, tier names, real-species truth, continuity, ending-engine
  is Trevor's.
- `steam-deck-cert.md` (fable/sonnet) — gamepad-first: default selection, full pad nav, no hover-dependence,
  1280×800 fit, glyph correctness (incl. the batch-32 ✓✕△◉ boxes + baked → ← ○ •), Input System discipline.
- `save-integrity.md` (fable) — round-trip proof, persistentDataPath-only, atomic writes, static-event
  ResetOnLoad, schema compatibility, saves hygiene.
- `localization.md` (sonnet/fable) — no hardcoded strings, id hygiene, glyph coverage (ties to batch-32),
  the U+202F culture-format trap, the dialogue raw-string restructure caution.
- `performance.md` (sonnet) — vert budget (Field Mushroom 414k debt), draw calls, atlas/texture memory,
  URP gotchas, frame-time floor (60fps village / 40fps Old Wood).

Each persona doc opens with a dense ~7-line summary (matches the `head -7 Docs/**/*.md` survey convention),
then Triggers / Checklist / Owned system docs / Verdict. Checklists are repo-specific — grounded in the
non-negotiables (conventions.md, steam-constraints.md), the real hardening-backlog debts, and shipped-batch
catches (batch-24 seal continuity, batch-27 beat integrity, batch-30 focus/binding fixes, batch-32 font).

## Wiring
- `night-shift.md` Model Tiering critical-decision gate → points at `Docs/review/README.md` for lens + checklist.
- `CLAUDE.md` router table → new row "Reviewing a batch before commit → Docs/review/README.md."

## Review gate for THIS batch
Process documentation, no code/content/canon change — the personas encode existing rules, they don't invent
canon or architecture. Self-reviewed against the source docs (conventions.md non-negotiables, steam-constraints
pillars, the systems-backlog debts, the QUESTIONS decisions). No fable gate needed (nothing the lint/integrity
gates or the source docs don't already establish). Recorded here for honesty vs the Model-Tiering habit.

## Verification
- Lint 0/0, integrity 0/0 (docs-only change; gates confirm no regression).
- Ownership cross-checked: every referenced system doc exists under `Docs/systems/`; every cited rule traces to
  conventions.md / steam-constraints.md / QUESTIONS.md.
- Routing table sanity: batch-32 (a font/localization/perf-touching batch) would route to localization +
  performance (+ the fable engineering gate it actually got) — the table reproduces the review that happened.

## Docs updated
`Docs/review/README.md` + 5 persona docs (new) · `night-shift.md` · `CLAUDE.md` · `TODOS.md` (#10 done).
