# Batch 118 — Unity CLI Adoption Pilot

**Date:** 2026-07-21 · **Status:** verified partial adoption; Pipeline compatibility-blocked

## Goal
Evaluate Unity's native CLI and experimental Pipeline package beside the existing Coplay MCP bridge, adopting only the parts that can be introduced without destabilizing Hollowfen. This is a reversible pilot: Coplay remains installed, no Player-runtime Pipeline component is added, and incompatible package changes are rolled back.

## Plan
- [x] Record the pre-install CLI, package, Editor, and bridge baseline.
- [x] Install and pin Unity CLI `1.0.0-beta.2` on the development Mac.
- [x] Prove that Unity CLI can generate a project-pinned Codex MCP configuration.
- [x] Trial `com.unity.pipeline` `0.3.1-exp.1` alongside Coplay.
- [x] Diagnose the compile incompatibility and roll Pipeline plus its transitive package changes back.
- [x] Verify the restored compile state, lint, data integrity, and Play-mode smoke.
- [x] Leave release behavior unchanged: no Pipeline package, custom command, or Player component remains.
- [x] Docs updated + worksheet finalized.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Adoption strategy | Side-by-side pilot; do not remove Coplay | Preserves the known-good fallback until native reload, Play-mode, and release behavior are proven. |
| Versions | Pin CLI `1.0.0-beta.2` and Pipeline `0.3.1-exp.1` | Both surfaces are experimental and changing quickly; pinning makes failures reproducible. |
| Player runtime | Do not add `RuntimePipelineManager` in this batch | The first batch is Editor-only and must prove release-package exclusion before adding a QA Player surface. |
| Mutations | Initial Hollowfen commands are read-only | Durable mutations should be added only after the transport and safety model are proven. |
| Existing worktree | Preserve existing package/doc/code changes; do not create a mixed commit | The tree contains extensive user-owned production work predating this pilot, including edits to `manifest.json` and `packages-lock.json`. |
| Compatibility result | Keep the standalone CLI; roll back Pipeline and its Codex MCP registration | Pipeline cannot currently compile beside Hollowfen's embedded Coplay package without patching or weakening a third-party package. A dormant MCP registration would expose unusable tools in future Codex tasks. |
| Hollowfen adapter | Deferred | Custom `[CliCommand]` endpoints require Pipeline, so none were added after the compatibility gate failed. |

## Verification evidence

Pre-install baseline:

- Project Editor: Unity `6000.4.4f1`; satisfies Pipeline's Unity 6.0+ requirement.
- Unity Editor and embedded Coplay MCP bridge are running and reachable.
- Native `unity` command is not installed on the normal `PATH`.
- `com.unity.pipeline` is not installed; a temporary CLI probe reported zero Pipeline instances.
- Current native beta advertises `unity mcp configure codex` and project-pinned MCP configuration.
- Existing Hollowfen automation surface: 63 Editor C# files, 59 menu commands, and 24 `RunAll()` verifier entry points.

Pilot result:

- Installed Unity CLI `1.0.0-beta.2` at `/Users/TrevorKist/.unity/bin/unity` with Unity's checksum-verifying beta installer.
- Confirmed the CLI is authenticated through Unity Hub and advertises native OpenAI Codex MCP configuration.
- Generated and inspected the project-pinned Codex MCP entry, then removed it after Pipeline rollback so future tasks do not advertise an unavailable connection.
- Installed `com.unity.pipeline` `0.3.1-exp.1` temporarily. Unity resolved its dependency on `com.unity.nuget.newtonsoft-json` `3.2.2`.
- Compilation failed because the embedded Coplay package supplies an explicit, Editor-only `Newtonsoft.Json.dll` with the same assembly identity. Pipeline's runtime assembly selected that DLL while Pipeline's Editor assembly received no usable Newtonsoft reference, producing missing `Newtonsoft`, `JsonProperty`, `JArray`, and `NullValueHandling` errors.
- Older advertised Pipeline releases use the same dependency pattern, so a version downgrade is not a credible side-by-side fix.
- Pipeline and its transitive Newtonsoft package were removed from `manifest.json` and `packages-lock.json`; the Editor returned to zero compile/console errors.
- No Coplay files, DLL metadata, release gates, scenes, custom CLI commands, or Player components were modified.

Post-rollback gates:

- `python3 tools/agent/lint_hollowfen.py` — PASS (`ERRORS=0 WARNINGS=0 WAIVED=1`).
- `python3 tools/agent/run_integrity.py` — PASS (`ERRORS=0 WARNINGS=0`).
- `python3 tools/agent/smoke_play.py --min-frames 240` — PASS at 242 frames with zero pre-play and in-play console errors.

## Docs updated

- `Docs/worksheets/batch-118-unity-cli-adoption-pilot.md` — live pilot record and handoff.

## Unfinished / handoff

The safe adoption boundary is now explicit:

1. Use the standalone Unity CLI for supported project/Editor lifecycle, build, and test workflows where it does not require Pipeline.
2. Keep Coplay as Hollowfen's live Editor bridge.
3. Re-test native Pipeline when Unity removes the Newtonsoft assembly collision or documents a supported coexistence mechanism.
4. If earlier evaluation is valuable, use a separate disposable Coplay-free project or copy; do not turn Hollowfen production into the experiment.

The working tree was already extensively dirty before this batch. This worksheet is the only new project file from the pilot; do not stage or commit unrelated files.

## Feedback to Trevor

Unity CLI remains strategically promising and is now installed, but the current experimental Pipeline package is not side-by-side compatible with Hollowfen's hardened embedded Coplay package. The pilot protected the game: it found the failure at the compile gate, preserved Coplay, and restored all project checks before any Hollowfen adapter or Player-runtime surface was introduced.
