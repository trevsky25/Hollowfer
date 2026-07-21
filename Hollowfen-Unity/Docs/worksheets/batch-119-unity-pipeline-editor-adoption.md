# Batch 119 — Unity Pipeline Editor Adoption

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Adopt Unity Pipeline's live Editor and native MCP surfaces beside Coplay without
allowing either integration layer, Newtonsoft, or Pipeline's Roslyn dependencies
into a Hollowfen player.

## Chosen boundary

- Coplay remains the proven live-Editor fallback.
- Unity Pipeline is pinned at `0.3.1-exp.1`.
- Coplay is upgraded from the locally hardened `9.6.8` snapshot to upstream
  `10.1.0`, which uses Unity's official Newtonsoft package.
- Coplay, Pipeline, their runtime tests, Roslyn plugins, and Newtonsoft are hardened
  as Editor-only.
- Pipeline Player-runtime automation is intentionally unavailable.
- Hollowfen's production gate remains authoritative and gains explicit Pipeline and
  Roslyn denylist entries.

## Disposable-copy evidence

The full project was cloned to an APFS copy-on-write workspace before production
files were changed.

- Coplay `10.1.0` + Pipeline `0.3.1-exp.1` + one official Newtonsoft `3.2.2`
  assembly resolved and compiled together.
- Data integrity passed with 26 quests, 145 dialogues, 11 NPCs, 15 locations,
  21 mushrooms, 30 story cards, 28 story moments, 31 profiles, four endings,
  16 requests, seven restoration projects, zero errors, and zero warnings.
- Pipeline discovered the live Editor on its project-specific port and successfully
  executed `editor_status`, `get_scene_hierarchy`, `get_console_logs`, and
  Roslyn `eval`.
- Coplay simultaneously started its HTTP bridge and Hollowfen reported the bridge
  connected.
- The unmodified upstream packages correctly failed Hollowfen's preflight because
  Coplay runtime and Newtonsoft were player-compatible.
- After the Editor-only hardening, the production preflight passed and a direct
  inspection found no Coplay, Pipeline, Newtonsoft, or Pipeline Roslyn assembly or
  plugin compatible with the macOS player.

## Production verification

- [x] Resolve and compile the embedded packages in the real project.
- [x] Run Hollowfen data integrity.
- [x] Re-run the production preflight and direct player-assembly exclusion check.
- [x] Open the real Editor and verify both Coplay and Pipeline live connections.
- [x] Configure the native Unity MCP entry for Codex, pinned to Hollowfen.
- [x] Run a gated macOS audit build and inspect its contents.
- [x] Commit the reviewed adoption as one rollback point.

## Production result

- Embedded package resolution selected Coplay, Pipeline, and Newtonsoft from the
  source-controlled `Packages/` snapshots.
- A final headless compile and data-integrity run passed with zero errors and zero
  warnings.
- Pipeline returned a ready Editor status on port 7800 and Roslyn evaluation returned
  Hollowfen's product name.
- Coplay's local HTTP server listened on port 8080, and Hollowfen reported its bridge
  connected in the same Editor session.
- The production preflight passed. A separate inspection returned no compatible
  Coplay, Pipeline, Newtonsoft, or Pipeline Roslyn player assembly/plugin.
- The native Unity MCP entry is configured for Codex and pinned to Hollowfen's
  absolute project path. A new Codex task/app reload is required to load a newly
  registered MCP server.
- Gated macOS audit build passed: `StandaloneOSX`, 4,171,093,821 bytes, 189 files,
  3.9 GB on disk, with no denied Coplay/Pipeline/Newtonsoft/Roslyn file names.
- Fast repository lint passed: `ERRORS=0 WARNINGS=0 WAIVED=1`.

## Additional hardening discovered during adoption

- Pipeline's upstream startup wrote `PlayerSettings.runInBackground` just by opening
  the Editor. The embedded fork restricts that assignment to non-Editor Players;
  Hollowfen's setting remains unchanged.
- Pipeline bundled `System.Runtime.CompilerServices.Unsafe` 4.0.4 while Unity
  Collections already supplied 6.0.0. The redundant Pipeline plugin and explicit
  reference are disabled, eliminating the duplicate-assembly warning.
- Coplay 10 uses its Unity package version to select a matching Python server
  distribution. Its version remains exactly `10.1.0`; local fork identity lives in
  `UPSTREAM.md` rather than a version suffix.

## Security note

Unity batch startup logs can include a transient Hub access token in the Editor
command line. Raw logs remain local and must not be published without redacting the
token-bearing line.
