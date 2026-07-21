# Batch 121 — Hollowfen native Pipeline command layer

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Add a thin, project-owned Editor-only command layer that fully leverages native Unity CLI/Pipeline
without coupling Hollowfen's game assemblies to the experimental package or bypassing existing
integrity, save-isolation, UI, and production-build policy.

## Plan

- [x] Inventory the live Pipeline surface and confirm the adapter assembly boundary.
- [x] Add first-class health and exact production-preflight commands.
- [x] Add a discoverable allowlist for existing focused verifiers.
- [x] Add owned temporary-save lifecycle commands for mutating Play Mode verification.
- [x] Add a compact read-only world audit for scene cleanup and dressing work.
- [x] Exercise every command through native Unity CLI and cross-check Coplay.
- [x] Update tooling/test docs, queue, dashboard, and this worksheet.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Dependency direction | Isolated, Editor-only, `autoReferenced: false` `Hollowfen.Pipeline.Editor` asmdef references Pipeline; existing Hollowfen assemblies do not. | `Unity.Pipeline` is intentionally not auto-referenced. Keeping the adapter separate prevents experimental tooling from becoming a game/editor architecture dependency. |
| Existing policy access | Reflection through an explicit type/method allowlist. | Unity asmdef assemblies cannot reference predefined `Assembly-CSharp(-Editor)` directly; a narrow allowlist preserves compile isolation without allowing arbitrary method execution. |
| Player boundary | Editor-only asmdef plus existing production denylist/preflight. | The adapter and Pipeline remain unavailable to Player compilation; runtime automation stays disabled. |
| Verifier result contract | Expose only 24 methods that synchronously return an explicit PASS report; reject textual FAIL/missing reports. | A normal method return is not proof. The live run caught the first wrapper incorrectly labelling a UI `FAIL` as passed, so success is now evidence-based. |
| Asynchronous verifier | Keep `PresentationSessionVerifier.Run()` on its existing menu/Coplay path. | It completes on a later frame and returns void; Pipeline cannot truthfully report its final outcome until the verifier gains a completion API. |
| Verifier mutation | `dry_run` state validation plus mandatory `confirm=true`; gameplay verifiers require owned save isolation. | A CLI convenience must not turn a focused verifier into an accidental real-journal mutation. |
| Isolation storage | Project-local `Library/HollowfenPipeline/isolated-saves/<id>`, tracked in `SessionState`. | The directory survives domain reload for a verification session, is outside Assets/git, and cleanup can prove it only deletes command-owned fixtures. Stale ownership must be cleaned before another session. |
| World metrics | Authored hierarchy totals, explicitly not frame-performance evidence. | Scene cleanup needs deterministic structural counts; Batch 120 and release-player profiling retain performance authority. |
| CLI argument binding | Preserve exact advertised `CliArg` names, including underscores. | The CLI silently left C# defaults in place when the helper converted `dry_run`/`interval_ms` to hyphenated forms. |

## Verification evidence

- **Starting state:** branch `codex/gameplay-production-pass` began clean at `e692c25` / tag
  `batch-120`, four commits ahead of origin. Existing user work was not changed or discarded.
- **Native discovery/compile:** forced Pipeline recompilation completed after the new isolated asmdef
  was made non-auto-referenced. The command inventory remained live at 150 commands, including all
  seven `hollowfen_*` endpoints; the verifier catalog contains 24 synchronous entries.
- **Health/preflight:** final native health reported Unity `6000.4.4f1`, Pipeline `0.3.1-exp.1`,
  Coplay `10.1.0`, `StandaloneOSX`, clean `Scene_MainMenu`, stopped/not compiling, zero captured
  errors/warnings, and no active save override. `hollowfen_preflight` returned
  `AUDIT PREFLIGHT — PASS (StandaloneOSX)` plus `DATA INTEGRITY — ERRORS=0 WARNINGS=0` through the
  existing authoritative methods.
- **Verifier contracts:** all 24 catalog entries were dry-run live with zero missing-type,
  missing-method, or non-synchronous-report contract failures; their only blockers were the expected
  Play/gameplay/isolation state. Missing `confirm=true` and an unknown verifier name both returned
  structured 400 rejections. Confirmed
  `narrative-copy` returned its explicit PASS for 145 dialogues, 26 quests, 16 requests, 15
  locations, and 28 cinematic moments. The Python client now preserves underscore args, accepts a
  command parameter named `name`, and surfaces structured command failures.
- **Owned Play Mode lifecycle:** final run dry-ran then armed isolation
  `000de3a849114d4699d8b01eca590e44`, entered Play Mode, tolerated Pipeline's expected domain-reload
  disconnect, advanced to frame 291, and returned `PASS · active UI presentation · 0 critical · 0
  advisory`. It then stopped, disabled auto-tick, dry-ran cleanup, deleted the owned directory, and
  proved `overrideActive=false` / `ownedIsolation=false`.
- **Scene audit:** Main Menu returned 74 objects and no missing scripts/collider findings. The clean
  loaded gameplay scene returned 608,094 objects, 377,161 renderers, 72,617 colliders, 185 materials,
  138,347,536 loaded authored mesh-instance triangles, zero missing scripts, zero zero-scale
  colliders, and 36 negative-determinant collider transforms. Those mirrored third-party prop
  transforms are existing cleanup debt already represented in the build-cleanup backlog; the audit
  made no scene change and explicitly does not claim visible-frame performance.
- **Coplay cross-check:** the single pinned `Hollowfen-Unity@46c2af2073f65405` instance remained
  connected. A harmless Roslyn read returned the Hollowfen product, `Scene_MainMenu`, and stopped
  state; `read_console` returned zero errors. `run_integrity.py` passed with errors=0/warnings=0 and
  `smoke_play.py --min-frames 240` reached frame 368 with zero pre-play/in-play errors before clean
  stop and fixture deletion.
- **Save safety:** the aggregate SHA-256 of real save primaries/backups matched the Batch 120
  baseline before and after all final native/Coplay Play Mode checks:
  `2ad3ece7a596382a97b989b2a990e9a674b0f024f84842ecc593a7c1c0a48443`.
- **Player boundary:** no Player-runtime component or raw build path was added. The unchanged exact
  production preflight passed its Player assembly/plugin inspection and hardened
  Coplay/Pipeline/Newtonsoft/Roslyn denylist. A second audit build was unnecessary for this
  Editor-only adapter after Batch 119's gated macOS audit build.
- **Static gates:** Python byte-compilation passed; gotcha lint returned `ERRORS=0 WARNINGS=0
  WAIVED=1`; `git diff --check` passed; the owned-isolation root was empty; final Coplay console
  remained at zero errors; and the final real-save aggregate matched.
- **Log hygiene:** only structured Pipeline/Coplay responses and bounded console queries were read;
  raw Unity startup logs were never inspected or published.

## Review verdict

**Architecture: PASS.** The experimental dependency points inward to one non-auto-referenced
Editor-only adapter; existing Hollowfen assemblies and all Player code remain unaware of it. The
adapter exposes bounded domain commands, not arbitrary reflection or a second production policy.

**Save integrity: PASS.** No schema, hydrate, persist, or real save path changed. Mutating verifiers
can be gated on an owned project-`Library` fixture; begin/end refuse foreign overrides, enforce root
confinement, retain ownership if partial cleanup fails, and leave real-save hashes unchanged.

**Performance: PASS.** The batch adds no Player assembly or hot-path work. The world scan is a
user-invoked Editor operation, and its response labels hierarchy totals as non-player performance
evidence. Batch 120's matched route and a release-player profile remain the performance authorities.

## Docs updated

- `Docs/systems/agent-tooling.md` — architecture, seven-command contract, safe Play/reconnect/cleanup
  sequence, production boundary, and extension rules.
- `Docs/tests.md` — native invocation layer, explicit PASS contract, isolation recipe, and async
  verifier exclusion.
- `tools/agent/README.md` — helper semantics and command-surface pointer.
- `../TODOS.md` — Batch 121 production-infrastructure completion.
- `Docs/dashboard.html` — regenerated production board.
- This worksheet — decisions, live evidence, reviews, and handoff.

## Unfinished / handoff

No temporary save isolation, Play Mode, auto-tick, dirty scene, or console error remains. Keep the
asynchronous presentation-session verifier on Coplay/menu execution until it returns a deterministic
completion result. The gameplay-scene audit's 36 mirrored collider paths are visibility for the
existing build-cleanup item, not scope added to this infrastructure batch.

## Feedback to Trevor

The native connection is now most valuable as a fast, discoverable control plane: one command can
prove project routing and policy, state blockers are machine-readable before mutation, and focused
checks can run without bespoke eval snippets. The live exercise also found three integration details
worth retaining: Play/compile domain reloads require reconnect polling, command `CliArg` underscores
must remain exact, and success must come from the verifier's explicit report rather than a normal
method return. Coplay remains genuinely useful as the differently implemented independent check; its
direct tools stayed healthy even when its cached editor-state telemetry briefly reported stale.
