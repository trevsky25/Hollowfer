# Review Personas — the end-of-batch adversarial gate

Specialized reviewer specs a subagent adopts to gate a batch BEFORE commit — the "what to check" that pairs with night-shift.md's "when to review at all." Each persona owns a lens and a concrete checklist; you run only the ones the batch actually touches. This is a ROUTER: match the batch's surface to the persona(s) below, spawn each as an independent subagent (model per the table), apply its required findings, record its verdict in the worksheet's Decisions table, THEN commit. A persona that returns BLOCK stops the commit. Personas are self-healing docs: when a review catches a class of bug the checklist missed, add that check.

> Precedent: the batch-32 Georgia SDF fix was fable-reviewed and came back PASS WITH CHANGES — it caught a dropped Latin-1 set, a live font-source ref that would have shipped a licensed .ttf, and build shrapnel in the tree. Gated reviews have caught shipped bugs in 5 of 5 gated batches. Don't skip the gate to save a subagent.

## When a gate is required (from night-shift.md Model Tiering)

Spawn a review BEFORE committing a batch that involves any of:
- a new system or a change to an existing system's architecture (not just new content on existing rails);
- save-schema changes (`SaveSlotMeta` fields, hydrate/persist paths);
- canon-sensitive authoring beyond the bible's literal text (implied mechanics, ending-engine logic);
- anything the integrity/lint gates can't validate (design judgment, not correctness).

Pure content on existing rails, verified by the lint + integrity + play-mode gates, does not need a persona — but if in doubt, run the cheapest relevant persona.

## Fan-out routing — what the batch touches → who reviews (model)

| The batch touches… | Run persona | Model |
|---|---|---|
| dialogue text, NPC voice, quest beats, story cards, any authored prose, tier/species/location names | **narrative-bible** | `fable` |
| a new screen/menu, focus/nav, input maps, gamepad flow, 1280×800 layout, glyphs, settings | **steam-deck-cert** | `fable` (canon-adjacent taste) or `sonnet` (pure mechanics) |
| `SaveCoordinator`, save schema, hydrate/persist, day-flag/quest persistence, `ResetOnLoad` | **save-integrity** | `fable` |
| `Localization.Get`, the LUT, string ids, dialogue-string restructure, font/glyph coverage | **localization** | `sonnet` (or `fable` if it restructures the string architecture) |
| mesh/vert counts, draw calls, atlas/texture memory, frame-time, URP settings, new world props | **performance** | `sonnet` |

Multiple can apply — e.g. a new dialogue screen runs narrative-bible + steam-deck-cert. Run them in parallel (independent subagents, one message).

## Invocation recipe

Spawn each persona as a subagent (`model` per the table). Prompt it with, in this order:
1. **The persona doc** (path — tell it to adopt that role and use that checklist verbatim).
2. **The worksheet-so-far** (the batch's intent + what changed).
3. **The diff summary** (files touched; for content, the actual added text).
4. **Specific questions** you want pressure-tested (the sharper, the better — see the batch-32 worksheet for a model prompt).
5. The verdict protocol below.

The reviewer does NOT need to drive Unity — give it the empirical evidence you already gathered (gate output, play-mode results, hashes, screenshots). It judges approach + completeness + canon, and reads the changed files itself.

## Verdict protocol (every persona returns this)

- **PASS** — ship as-is.
- **PASS WITH CHANGES** — ship after applying an itemized list of concrete required changes. Apply them, re-verify, note each as applied in the worksheet.
- **BLOCK** — a defect that must not ship; do not commit until resolved (or, if it's a Trevor-only call, park in QUESTIONS.md and pull a different item).

Be adversarial; don't rubber-stamp. Cite file:line. Distinguish REQUIRED changes from optional nits. Keep scope to the lens — a performance reviewer doesn't relitigate canon.

## The personas

- [narrative-bible.md](narrative-bible.md) — canon, NPC voice, no invented content, no romance/emoji/slang.
- [steam-deck-cert.md](steam-deck-cert.md) — gamepad-first, 1280×800, glyphs, focus/default-selection.
- [save-integrity.md](save-integrity.md) — round-trip, atomicity, schema, static-event reset.
- [localization.md](localization.md) — no hardcoded strings, LUT discipline, glyph coverage.
- [performance.md](performance.md) — verts, draw calls, atlas memory, frame-time floor.
