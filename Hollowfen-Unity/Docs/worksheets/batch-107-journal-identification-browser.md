# Batch 107 — Journal Identification Browser

**Date:** 2026-07-18 · **Status:** verified

## Goal
Redesign first-time mushroom identification so Wren compares an unknown live silhouette against several browsable illustrated journal spreads without the interface revealing or pinning the correct species. Candidate spreads can expand into a large gallery view, successful verification becomes the identity reveal boundary, repeat inspections return to the known-species details sheet, and the square vignette artifact is removed from the rounded cream inspect modal.

## Plan
- [x] Trace the current inspect, comparison, knowledge, preview, and modal-layer paths
- [x] Make field verification—not story discovery—the identity reveal boundary
- [x] Replace the pinned correct page with a deterministic three-page candidate browser and live silhouette
- [x] Add an enlarged journal-page gallery with mouse, keyboard, and controller navigation
- [x] Remove the square vignette layer from the rounded inspect modal
- [x] Compile, lint, and run first-discovery/repeat-inspection Play-mode verification
- [x] Docs updated + worksheet finalized

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Identity reveal boundary | Persisted per-species field verification | Story discovery can make a reference readable, but it must not answer the field-identification question. |
| First identification reference | Three plausible readable journal spreads in deterministic order | The player must inspect and compare pages instead of being handed the target page. Stable ordering keeps the experience reproducible. |
| Comparison subject | Live silhouette remains visible during the page-match step | This gives the player the exact visual question the journal pages are meant to answer without leaking the species name. |
| Enlarged art | Modal-filling gallery with page turning and close controls | It preserves the illustrated spread as the visual payoff while retaining a clear, controller-first route back to the test. |
| Repeat inspection | Normal identified model and details sheet | Once verified, recognition is learned knowledge rather than a repeated quiz. |
| Modal cleanup | Remove the full-rectangle vignette child; retain the behind-modal scrim | The vignette ignores the rounded cream silhouette and creates the reported blurred square edge. |

## Verification evidence

- Unity domain reload compiled the modified assemblies with zero console errors; `validate_script`
  reported zero errors for both `InspectScreen.cs` and `MushroomNode.cs` (one existing generic
  Update-allocation heuristic warning on Inspect).
- Live first-discovery seam using an unverified Wendlight:
  - world prompt resolved to **Unknown mushroom** and the inspect title to `?`;
  - comparison created exactly three pages and opened on Hollowheart rather than target Wendlight;
  - the live silhouette was active beside the candidate spread;
  - choosing the wrong page kept stage 0 and displayed the bounded mismatch guidance;
  - turning to Wendlight and choosing it advanced to Feature, held the target spread, hid page-turning,
    hid the silhouette, and restored all three feature choices.
- Enlarged-gallery seam:
  - candidate mode opened the illustrated 1672×940 spread across the comparison modal with Previous,
    Return to Test, and Next controls;
  - confirmed feature mode hid Previous/Next, focused Return to Test, and restored focus to Enlarge Page
    on close;
  - the gallery backdrop is fully opaque, so none of the underlying question/buttons ghost through.
- Repeat-inspection seam with the in-memory identification proof present: prompt/title resolved to
  **Wendlight**, CTA resolved to **Prepare to Cut**, the preview renderer no longer used
  `M_Silhouette`, and the comparison panel remained closed.
- Runtime screenshots at 2048×1152 confirmed the cream outer modal has a clean rounded silhouette with
  no square vignette edge; temporary QA captures were removed after inspection.
- CLI lint: 0 errors / 0 warnings (1 existing waiver).
- CLI data integrity: 0 errors / 0 warnings across 21 mushrooms and the broader project content set.
- `git diff --check`: clean for the batch source, localization, docs, and worksheet.

## Docs updated
- `Docs/systems/foraging.md` — documented the verification-only identity reveal boundary, candidate
  browser, enlarged gallery, controller focus, and vignette removal.
- `Docs/systems/mushroom-learning-and-ecology.md` — revised the learning loop and four knowledge-state
  presentation outcomes around non-spoiling multi-page deduction.
- `Docs/systems/localization.md` — documented the fixed browser chrome IDs and prohibition on separate
  target-name presentation before verification.

## Unfinished / handoff

Implementation and verification are complete. No player build was requested or run. The repository
already contains a substantial pre-existing dirty worktree, including changes overlapping these
source files, so this batch was deliberately not staged, committed, or tagged in isolation.

## Feedback to Trevor

The requested change exposes an important progression distinction: “the journal contains a readable
reference” and “Wren has personally verified this specimen” need separate presentation states. This
batch makes that distinction explicit at the world prompt, inspect sheet, 3D preview, comparison book,
and harvest action rather than only at the final authorization check.
