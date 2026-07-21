# Batch 111 — Wend Bridge Restoration

**Date:** 2026-07-18 · **Status:** verified

## Goal

Prove the Living Restoration foundation beyond cottages with a project that materially changes exploration: add the large lower Wend crossing to Pell's catalogue, let Wren fund two understandable supply lines, preserve the story-critical pre-restoration route, show an actual crew over a full work day, reopen a genuinely wider deck at dawn, and finish when Wren uses it.

## Completed slice

- [x] Added generic data-authored `RestorationContribution` rows and project-localized stage short labels.
- [x] Added an atomic purse + flags + restoration transaction with full runtime rollback and deferred publication.
- [x] Added the second catalogue project, timber/iron costs, five milestones, all stage copy, and localization.
- [x] Authored survey, supplies, work, restored, and in-use scene presentations at the large framed crossing.
- [x] Preserved a verified 2.9m central lane before/during work and added a six-meter restored deck collider.
- [x] Added Joren, Theo, and Pell bridge schedules and a reverse-ordered two-dawn work progression.
- [x] Added the dawn bridge reveal, night lamps, first-crossing finalization, and in-use road dressing.
- [x] Extended the ledger with partial-funding actions, confirmation, funded state, project-local timeline labels, and HUD suppression.
- [x] Added isolated Play Mode verification, data-integrity coverage, idempotent authoring, and documentation.

## Key decisions

| Decision | Choice | Reason |
|---|---|---|
| Which bridge | Large framed lower Wend crossing at `(218.21, 29.54, 224.97)` | It serves the western route and creates the clearest exploration payoff. |
| Pre-restoration traversal | Keep the existing center boards and condemn only the outer deck | Act II already uses this route before cottages complete; removing it would strand valid progression. |
| Funding | 24 copper for Theo's oak, 12 for Joren's iron | Two named supply lines make the economy and physical result legible. |
| Work cadence | Supplies at funding, crew next dawn, reopening the following dawn | A full visible labor day prevents the project from feeling like an instant coin-operated swap. |
| Final stage | First restored crossing localizes Occupied as “In Use” | Public works become complete through use, not habitation. |
| Geometry | Six-meter owned deck collider over the old span | The reward changes actual navigable width instead of only changing props. |
| Save safety | One verified full-slot contribution revision with rollback | Coin can never disappear without its flag and stage becoming durable. |

## Verification evidence

- Unity compile/console after the final pass: 0 errors.
- `BridgeRestorationVerifier.RunAll()`: `WEND BRIDGE RESTORATION — PASS: 2-line atomic funding, protected foot lane, cart-width reopened collider, three-person crew, two distinct dawn beats, reveal, first-crossing completion, save-failure rollback, and 2-project catalogue`.
- The verifier injects a rejected final atomic commit and proves unchanged coin, flags, stage, disk revision, and score publications.
- Geometry sampling found the old deck at y `32.67`; the authored root is aligned at y `32.63`, with the restored visual/collider surface directly above it.
- Full-screen UI QA verified two catalogue rows, partial-funding state, correct costs, five milestones, project-local timeline copy, and no HUD/minimap overlap.
- Restored bridge camera QA verified textured fresh timber, complete rails, bank lamps, cart-width presentation, and the retained framed-bridge silhouette.
- All QA saves lived under unique `/tmp` directories and all temporary screenshots/saves were removed afterward.

## Handoff

This chunk completes the second Living Restoration vertical slice. The next best project is the forge: it can reuse contributions/schedules/reveal while adding service tiers and a daily production benefit, which will prove that restoration can change an economy rather than only a route.
