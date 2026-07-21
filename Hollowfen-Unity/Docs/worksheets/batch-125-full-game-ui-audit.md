# Batch 125 — Full-game UI readability audit

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Use the native Unity CLI/Pipeline connection to audit Hollowfen's entire current UI catalogue at
the 1280×800 production target, correct unreadable or undersized text—especially the dense
apothecary casework—and retain repeatable evidence for both standard and maximum accessible text.

## Audited matrix

- 43 distinct routes per profile: main/save/loading/settings, confirmation, journal indexes and
  details, people/purse/restoration/credits, HUD/pause, inventory/inspect/identification, map,
  dialogue, village requests, cultivation, six apothecary states, intro, four notification states,
  narration, cinematic narration, and cutting HUD.
- 100% Interface Size at 1280×800.
- 115% Interface Size with Reduced Motion and Caption Backing at 1280×800.
- 86 gate-checked PNGs plus a machine-readable JSON report.
- Per-route checks: active presentation ownership, focus, accessible canvas contract, rendered text
  and paragraph pixels, TMP clipping, screen-bound intersections, PNG dimensions, and production
  UI verifier result.

## Findings and fixes

| Area | Finding | Resolution |
|---|---|---|
| Apothecary | Dense preparation/casework functional copy and section labels rendered around 11.2–12.6 px in the original standard pass. | Raised case evidence, interview, decision, outcome, recipe, ingredient, method, and safety copy to a consistent readable hierarchy. Every apothecary-owned line is now at least 14 px in standard and all text is at least 14 px in the accessible profile. |
| Mushroom safety/inspection | The Field Guide safety disclaimer reached 8.8 px; inspect habitat/season/look-alike rows reached 9.8 px. | Promoted safety and identification copy to body-text sizes and raised the supporting journal chrome. |
| Identification quiz | Reference headings, page controls, live-specimen label, progress trail, and hint copy were undersized. | Raised reference/control copy, fixed the page hint at a readable size, kept progress on one line at a paragraph-safe size, and preserved full controller navigation. |
| Journal/settings/economy | Multiple labels across Story detail, Settings, Purse, restoration, endings, and shared journal chrome fell below the rendered production floor. | Raised authored sizes locally without changing the shared scaler; long-form paragraphs remain un-clipped. |
| HUD/map/inventory/request | Microcopy in quest/clock/coin HUD, map chrome/marker chips, inventory cells, and village-request actions was too small. | Raised functional labels, marker chips, cell names/counts, request headings/actions, and objective copy while preserving their existing layouts. |
| Dialogue focus | The first audit found both dialogue profiles without an EventSystem-selected choice. | Dialogue now assigns the first active choice as the live controller selection. |
| Main-menu reveal | Deterministic capture exposed an interrupted alpha-zero reveal/input state; an early audit fix then cached pre-layout positions. | Reveal completion now restores input and only caches authored positions after Unity layout resolves. Normal presentation and audit capture both match. |
| Verifier precision | Disabled canvases and non-clipping TMP overflow modes produced 71 false overflow/visibility findings in the first instrumentation pass. | The Editor verifier now requires an enabled canvas and only classifies actual TMP clipping modes as clipped. No production threshold was weakened. |
| Audit frame driver | Repeated `EditorApplication.Step()` calls inside one Pipeline eval generated Unity JobTemp lifetime diagnostics. | The runner now waits on real Pipeline autotick frames; transient notices use a shorter visible-hold sample. Final console evidence is clean. |

## Final results

- **86 / 86 captures passed** `ProductionUIVerifier` with **0 critical and 0 advisory findings**.
- **0 routes contain clipped text**.
- **0 routes contain paragraphs below 14 rendered pixels**.
- The entire **115% accessible profile has 0 text below 14 rendered pixels** across all 43 routes.
- Standard profile body/paragraph copy is at least 14 px. Forty routes retain one or more deliberate
  12.3–13.7 px compact controls/HUD labels that meet the production 12 px control floor.
- Eight profile/routes report off-screen text, all from intentionally masked scroll content in the
  Story index, Field Guide, People archive, and Inventory. Main menu and non-scroll presentations
  have no off-screen findings.
- All six accessible apothecary routes have no sub-14 text; all six standard routes have no
  apothecary-owned sub-14 text. Their seven raw sub-14 entries are the shared compact HUD controls
  behind the modal and remain above the production control floor.

## Verification and safety

- Native Pipeline: Unity `6000.4.4f1`, stopped/ready/not compiling, `Scene_MainMenu` restored and
  clean; `hollowfen_preflight` returned `AUDIT PREFLIGHT — PASS (StandaloneOSX)` and
  `DATA INTEGRITY — ERRORS=0 WARNINGS=0` across 26 quests, 145 dialogues, 11 NPCs, 15 locations,
  21 mushrooms, 30 story cards, 28 story moments, 31 profiles, four endings, 16 requests, and seven
  restorations.
- Coplay `10.1.0`: independently returned the clean active main-menu scene and zero console errors
  after audit-owned diagnostics were cleared.
- Static checks: gotcha lint `ERRORS=0 WARNINGS=0 WAIVED=0`, Python byte-compilation, Unity compile,
  and `git diff --check` passed.
- Save isolation `507bb0bcf6b5497e8e639b297a1aa393` was dry-run reviewed, armed only under
  `Library/HollowfenPipeline/isolated-saves/`, and deleted in cleanup. Native cleanup now reports
  `already_clear`; no real `saves/slot0.json` existed before or after.
- The harness remains under `Assets/_Hollowfen/Scripts/Editor`; no player automation, package
  compatibility, ProductionBuildGate rule, or player-artifact denylist changed. Raw Unity startup
  logs were never read or published.

## Evidence

- `Docs/screenshots/batch-125-ui-audit/full-ui-audit-report.json`
- `Docs/screenshots/batch-125-ui-audit/100-standard/` — 43 PNGs
- `Docs/screenshots/batch-125-ui-audit/115-accessible/` — 43 PNGs
- `tools/agent/capture_full_ui_audit.py` — repeatable native Pipeline runner
- `Assets/_Hollowfen/Scripts/Editor/FullUIAuditHarness.cs` — Editor-only route staging and inspection

## Review verdict

**Readability: PASS.** Hollowfen's long-form and functional copy is readable at 1280×800, with the
dense apothecary and identification flows receiving the largest improvement. The maximum accessible
profile now keeps every visible text element at or above 14 rendered pixels.

**Layout/focus: PASS.** No TMP clipping remains, all production verifier checks pass, choice focus is
owned correctly, and the only off-screen measurements are expected scroll-view content.

**Production boundary: PASS.** The CLI connection is functioning as an Editor-side audit and
evidence layer around Hollowfen's existing integrity, isolation, and production gates; nothing was
added to or enabled for the Player runtime.

## Remaining manual certification

This audit proves the current English UI catalogue at 1280×800 in the Editor. It does not replace a
physical Steam Deck controller/resume/offline/thermal pass, translated-language expansion testing,
or tolerant visual pixel-diff review. Those remain release-certification work rather than blockers
for this UI readability batch.
