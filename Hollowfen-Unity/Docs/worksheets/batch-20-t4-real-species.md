# Batch 20 — Q4 resolution: the T4 trio become real species

**Date:** 2026-07-12 · **Status:** VERIFIED + committed (tag `batch-20`)

## Goal
Resolve QUESTIONS.md Q4 per Trevor's decision: Hollowfen is an educational mushroom game, so every field-guide species must be a REAL fungus with a real Latin binomial and a real photo. The 3 remaining fictional entries (the T4 trio: Moonring/Hollowheart/Wendlight, `_latinName: Unrecorded`, no photo) become Sable's seedbook folk-names for real deadly/psychoactive species, reusing the photos already in the project.

## Mapping (Trevor picked "folk-names for real killers")
| Folk name (kept as entry title) | Real species | Latin | Edibility | Photo reused |
|---|---|---|---|---|
| Moonring | Destroying Angel | *Amanita virosa* | Deadly (1) | `destroying_angel.png` |
| Hollowheart | Death Cap | *Amanita phalloides* | Deadly (1) | `death_cap.png` |
| Wendlight | Liberty Cap | *Psilocybe semilanceata* | Psychoactive (2) | `liberty_cap.png` |

## What was built
- Rewrote the `_latinName`/`_edibility`/`_edibilityLabel`/`_description`/`_idFeatures`/`_habitat`/`_season`/`_lookalikes`/`_notes`/`_photo`/`_photoCredit` region of the 3 T4 mushroom assets (via a byte-level Python script — the Edit tool couldn't match the file's mixed em-dash encoding). Each entry keeps its folk `_commonName` and `_id` (so no rewiring of seedbook discovery / Wendlight world node), leads the description with the real-species reveal, and carries accurate real ID features, lookalikes, and safety notes adapted from the real entries (02/06/05).
- `_worldPrefab` on Wendlight left intact (still forageable in the Old Wend).

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Entry title | Keep folk name (Moonring…), reveal real species in description line 1 | Avoids two literally-identical "Death Cap" titles while still teaching the real ID; matches Trevor's "also known as" framing |
| ID/lookalike content | Real, accurate (adapted from entries 02/06/05) | Educational-game rule — the entry must actually teach identification + the deadly lookalikes |
| Rewrite method | Byte-level Python region-replace | Edit tool can't match Unity's mixed `—`/literal-em-dash YAML |

## Verification evidence
- Refresh clean (data-only, no recompile; **no App-Nap stall** — the `NSAppSleepDisabled` fix held).
- Asset load check (bridge): `Moonring → Amanita virosa / destroying_angel`, `Hollowheart → Amanita phalloides / death_cap`, `Wendlight → Psilocybe semilanceata / liberty_cap` — all photos resolve non-null, folk names preserved.
- `run_integrity.py` — **0 errors / 0 warnings**, coverage unchanged (20 mushrooms).

## Docs updated
- `systems/foraging.md` — header: all 20 species now real; T4 mapping + the Wendlight world-model follow-up.
- `QUESTIONS.md` — Q4 moved to Answered with the decision.

## Unfinished / handoff
- **Wendlight world prefab** still reads as a mystical glowing mushroom; it now represents a real Liberty Cap and should be remodeled/retinted in the Meshy/model pass. Logged in foraging.md + the graphics follow-ups.
- No other T4 loose ends; all 20 field-guide species are now real.

## Feedback to Trevor
- Unity's YAML mixes `—` (inside double-quoted scalars) and literal `—` (plain scalars) in the same file, which defeats exact-string Edits — a small `tools/agent/` helper for "replace a serialized field region in an .asset" would remove this friction for future content edits.
