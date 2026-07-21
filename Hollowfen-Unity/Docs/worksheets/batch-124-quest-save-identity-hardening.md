# Batch 124 — Quest/save identity hardening

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Make save-slot quest presentation authoritative from the stable quest ID, retire localized display
copy from all current write paths, preserve id-less historical journals, and prove the behavior in
the real 1280×800 save-slot UI without changing the save schema or weakening production policy.

## Plan

- [x] Refresh the existing draft integration PR from the production branch to `main`.
- [x] Trace every `CurrentQuest` / `CurrentQuestId` reader and writer.
- [x] Centralize quest identity assignment, write normalization, and display resolution.
- [x] Preserve schema-zero display compatibility while rejecting stale-cache fallback for current IDs.
- [x] Add data-integrity and focused save-verifier coverage.
- [x] Exercise current, legacy, unknown, and empty journals through the live save-slot UI.
- [x] Run compile, production preflight, integrity, lint, Coplay smoke, and save-hash regressions.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Identity authority | `CurrentQuestId` is the only current quest identity; `SaveQuestIdentity.Set` clears `CurrentQuest`. | Localized display copy changes by language and must never control resume/UI logic. |
| Historical compatibility | Keep `CurrentQuest` serialized only as a read-only fallback when an old journal has no ID. | Removing the field would make schema-zero journals lose their chapter label; the first authoritative full save naturally retires it. |
| Current writes | `WriteJsonAtomically` calls `SaveQuestIdentity.PrepareForWrite`; any non-empty ID strips cached text, including targeted autosaves and verifier writes. | Central normalization closes paths that bypass `SaveCoordinator`. |
| Quest label lookup | Resolve canonical IDs through `quest.<id>.name`; Data Integrity requires each `QuestData.DisplayNameId` to match that exact key. | The menu has no gameplay quest registry, so a verified convention is smaller and safer than a duplicate runtime catalogue. |
| Unknown ID behavior | Render localized `Unknown chapter`; never fall back to a stale cache when an ID exists. | Stale text would mask removed, mistyped, or future quest IDs and create false confidence. |
| Schema version | Keep schema 1. | No field shape or meaning required an incompatible payload migration; the legacy field remains readable and older schema-1 files still decode. |

## Verification evidence

- **Starting state / integration:** the branch began clean and remote-synchronized at `31409f7` /
  `batch-123`. Existing draft PR
  [#1](https://github.com/trevsky25/Hollowfer/pull/1) targets `main`, is mergeable, and its title/body
  were refreshed through Batch 123 before implementation. No duplicate PR was created.
- **Compile:** forced native Pipeline recompiles completed with `failed=false` and zero errors.
- **Focused save contract:** native `save-integrity` dry-run reported no blockers and the confirmed
  run returned `SAVE INTEGRITY — PASS: legacy upgrade, authoritative quest identity, semantic
  corruption, checksum, temp/backup revision recovery, future-version barrier, load isolation,
  normalization, full round-trip, recovered rewrite`.
- **Live UI:** a command-owned isolated journal staged four rows: current `arrive` plus deliberately
  stale cache, id-less schema-zero `Legacy chapter title`, unknown current ID plus stale cache, and
  Empty. At Play Mode frame 249 the settled 1280×800 `save-slot` screen rendered `Homecoming`,
  `Legacy chapter title`, `Unknown chapter`, and `New Game` respectively. The active production UI
  verifier returned PASS with zero critical findings and one pre-existing 11.2px footer advisory;
  no layout or footer code changed in this batch.
- **Production boundary:** `hollowfen_preflight` returned `AUDIT PREFLIGHT — PASS (StandaloneOSX)`
  plus `DATA INTEGRITY — ERRORS=0 WARNINGS=0`. ProductionBuildGate, its Player artifact denylist,
  package compatibility, and Pipeline Player-runtime automation were unchanged.
- **Data contract:** the new `quest-save-identity` check covers all 26 canonical quests and requires
  their authored display localization to match the ID-derived key. The full integrity report still
  covers 145 dialogues, 11 NPCs, 15 locations, 21 mushrooms, 30 story cards, 28 story moments, 31
  character profiles, four endings, 16 requests, and seven restoration projects with zero findings.
- **Coplay cross-check:** Coplay `10.1.0` independently returned data integrity errors=0/warnings=0.
  After clearing the expected negative-test refusal from its bounded console, `smoke_play.py
  --min-frames 240` reached frame 241 with zero pre-play or in-play errors and stopped cleanly.
- **Static gates:** gotcha lint returned `ERRORS=0 WARNINGS=0 WAIVED=0`; Python byte-compilation,
  `git diff --check`, and Unity `.meta` coverage passed.
- **Save safety:** all mutation used owned isolation
  `a8dd0562fa6f4333ac0eaef807bd1018`, which was dry-run reviewed then deleted through the native
  cleanup command. The project isolation root is empty, no override remains, and the aggregate
  SHA-256 of real save primaries/backups was unchanged before and after:
  `007f9232d4a4c7853834227def0de955db20a29c186fd33c5ffd1d692df9784e`.
- **Final Editor state:** native health reports Unity `6000.4.4f1`, stopped/not compiling, clean
  `Scene_MainMenu`, zero errors/warnings, no save override, Pipeline `0.3.1-exp.1`, and Coplay
  `10.1.0`; Coplay's direct console query also returns zero errors.

## Review verdict

**Save integrity: PASS.** Current writes carry one stable quest ID and scrub localized cache text;
schema-zero journals remain loadable/displayable; recovery, future-version refusal, normalization,
atomic rewrite, and full round-trip all still pass in isolated storage. No real journal changed.

**Localization: PASS.** Save rows resolve at render time through authored localization. A missing
current mapping is visible as `Unknown chapter` and cannot be hidden by yesterday's cached English.

**Production/performance: PASS.** The change adds a small pure string resolver on save-row refresh
and one normalization call per journal write. It adds no Player automation, scene object, frame-loop
work, renderer, allocation-heavy collection, or production-gate exception.

## Docs updated

- `Docs/systems/save.md` — authoritative ID flow, compatibility cache, write normalization, and UI rules.
- `Docs/systems/localization.md` — ID-derived save labels and integrity-enforced convention.
- `Docs/tests.md` — `quest-save-identity` category and expanded save verifier contract.
- `Docs/review/save-integrity.md` — resolved localized-text debt and retained compatibility boundary.
- `../TODOS.md` — Batch 124 completion and remaining hardening work.
- `Docs/dashboard.html` — regenerated production board.
- This worksheet — decisions, evidence, reviews, and handoff.

## Unfinished / handoff

`CurrentQuest` deliberately remains in `SaveSlotMeta` so id-less schema-zero journals keep their
historical label; it is no longer a current writer or identity source. Physical field removal would
require an explicit legacy parser/schema-support decision and offers no player value today.
Remaining hardening is separate: per-quest achievement hooks, the dead-code sweep, and the seedbook
collection-gating decision.

## Feedback to Trevor

This batch is the intended CLI advantage in miniature: native commands proved the exact save
contract, staged contradictory current/legacy data in owned isolation, inspected the real rendered
rows, and ran the production gate without touching tester journals. Coplay then exercised the same
project through its independent bridge. The connections are now supporting production work rather
than becoming another gameplay dependency.
