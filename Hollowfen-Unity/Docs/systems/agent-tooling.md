# Agent Tooling (Unity CLI / Pipeline / Coplay)
Native Unity CLI/Pipeline is Hollowfen's primary structured Editor-control lane; Coplay remains the independent fallback and cross-check.
Key code: `Assets/_Hollowfen/PipelineEditor/HollowfenPipelineCommands.cs`, isolated in the Editor-only, non-auto-referenced `Hollowfen.Pipeline.Editor` assembly.
Entry points: seven `hollowfen_*` commands for health, production preflight, verifier discovery/execution, owned save isolation, and scene auditing.
Safety boundary: commands wrap the existing integrity, save, UI, and `ProductionBuildGate` policy; they never expose Player-runtime automation or a generic build shortcut.
Biggest gotchas: Pipeline reconnects after Play/compile domain reloads; command arguments keep their advertised underscores; world-audit totals are authored hierarchy cost, not visible-frame performance; disabled colliders are reported separately from active physics findings.
Status: Batch 122 uses the command layer for a production scene-health pass, clears all 36 enabled negative-determinant collider findings through project-owned scene overrides, and independently passes Coplay 10.1.0 integrity and smoke.

> Self-healing doc: if this command surface or either Unity connection changes, update this doc and the test manifest in the same batch.

---

## Why this layer exists

Pipeline gives scripts and agents a discoverable, schema-described, JSON command surface into the
already-open Editor. Hollowfen owns only a thin adapter over that surface. Game and ordinary Editor
code remain independent of the experimental package, and all product rules stay in their existing
authoritative classes.

`Hollowfen.Pipeline.Editor.asmdef` is restricted to `Editor`, has `autoReferenced: false`, and alone
references `Unity.Pipeline` / `Unity.Pipeline.Editor`. Unity's predefined `Assembly-CSharp-Editor`
cannot be referenced from an asmdef, so the adapter reaches existing Hollowfen tools through a
hardcoded type-and-method allowlist. It does not accept arbitrary type names, method names, or source
code. Coplay and the existing bridge scripts remain available for independent checks and workflows
that have not earned a native command.

## Hollowfen command surface

| Command | Contract |
|---|---|
| `hollowfen_health` | Read Editor/play/compile state, active/open/dirty scenes, bounded Pipeline console counts, active save override ownership, package versions, and build target. |
| `hollowfen_preflight` | With the Editor stopped and every open scene clean, run the exact `ProductionBuildGate.ValidateAuditPreflightForAutomation()` and `DataIntegrity.RunAllAsReport()` gates. |
| `hollowfen_verifier_catalog` | List the 24 synchronously reportable allowlisted verifiers and their Play Mode, gameplay-scene, and owned-isolation requirements. |
| `hollowfen_run_verifier` | Validate one catalog name with `dry_run=true`, then require `confirm=true` to execute. Success requires an explicit synchronous PASS report; textual FAILs and missing reports reject the command. |
| `hollowfen_begin_save_isolation` | While stopped/clean, create and arm an owned directory under `Library/HollowfenPipeline/isolated-saves/<id>`. It refuses to replace an override it does not own. |
| `hollowfen_end_save_isolation` | While stopped/clean, clear and delete only the `SessionState`-tracked directory below the owned root. It refuses path escape or a mismatched active override. |
| `hollowfen_world_audit` | Read the loaded active scene's object, renderer, material, light, particle, mesh-triangle, and missing-script totals; report collider components as enabled/disabled and separate enabled negative/zero-scale hazards from disabled negative-scale findings, with capped paths. |

`PresentationSessionVerifier.Run()` is intentionally absent from the catalog: it completes on a
later Editor frame and currently returns no synchronous report. Keep using its menu/Coplay workflow
until it exposes a deterministic completion API; never infer PASS from a void return.

## Native CLI examples

Use the live project descriptor; do not read Unity startup logs to discover credentials or state.
The repo helper defaults to `~/.unity/bin/unity` and adds structured JSON formatting automatically.

```bash
UNITY_CLI="$HOME/.unity/bin/unity"
PROJECT="/absolute/path/to/Hollowfen-Unity"

"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_health
"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_preflight
"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_verifier_catalog
"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_run_verifier --name narrative-copy --dry_run true
"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_run_verifier --name narrative-copy --confirm true
"$UNITY_CLI" --format json --no-banner command --project-path "$PROJECT" hollowfen_world_audit --include_inactive true --max_findings 25
```

Pipeline binds command arguments by their exact `CliArg` name. Use `--dry_run`,
`--include_inactive`, `--max_findings`, and `--interval_ms`, not hyphenated variants. The repo's
`UnityPipeline.command(command_name, **parameters)` helper preserves those names and allows a
command parameter named `name`.

## Safe mutating-verifier sequence

1. Run `hollowfen_preflight` from a stopped Editor with clean scenes.
2. Run `hollowfen_begin_save_isolation --dry_run true`, then repeat with `--confirm true`.
3. Enter Play Mode with `editor_play`. A domain reload temporarily drops Pipeline; poll the
   read-only `editor_status` command until it reconnects and reports `playing`.
4. Focus Unity or use throttled `set_autotick --enable true --interval_ms 16`, then prove that real
   frames have advanced before checking a settled UI or gameplay state.
5. Run the chosen verifier with `--dry_run true`, then `--confirm true` only when ready.
6. Call `editor_stop`, wait for `stopped`, and disable auto-tick if it was enabled.
7. Run `hollowfen_end_save_isolation --dry_run true`, then `--confirm true`. Finish with
   `hollowfen_health` and confirm `overrideActive=false` / `ownedIsolation=false`.

The isolation directory is deliberately under project `Library`, outside `Assets` and source
control. Cleanup is confined to the adapter-owned root. A domain reload preserves ownership through
`SessionState`, while real journals remain untouched.

## Interpretation and production boundary

- `hollowfen_preflight` is the only native production-preflight entry point. It invokes the existing
  gate, whose Player assembly/plugin inspection and Coplay/Pipeline/Newtonsoft/Roslyn denylist remain
  authoritative. Do not add a raw Pipeline build command that bypasses it.
- Pipeline Player-runtime support stays disabled. This adapter is Editor-only and must remain absent
  from Player compilation along with Pipeline, Coplay, Newtonsoft, and Pipeline Roslyn tooling.
- `hollowfen_world_audit` is a one-shot Editor structural scan. Its triangle total counts loaded
  authored mesh instances, including inactive objects when requested; it is not frustum-culling,
  GPU, release-player, or Steam Deck evidence. Use the matched route in `Docs/benchmarks.md` and a
  release-player profile for performance decisions. Collider-transform hazards count only enabled
  physics components; disabled negative-scale components remain visible in a separate diagnostic
  bucket so a repair is auditable without being misreported as live collision.
- Console counts come from Pipeline's bounded observability buffer. Clear it immediately before a
  scoped run when exact deltas matter; Coplay's `read_console` is the independent fallback.
- New authoring/mutation commands must be narrow, dry-runnable, explicitly confirmed, and rooted in
  a Hollowfen-owned path or object scope. They must call existing integrity/production policy rather
  than reproduce or weaken it.
- Never publish raw Unity startup logs. Structured command responses, bounded console queries, and
  project resources provide the required evidence without exposing transient Hub credentials.

## Extending the allowlist

Add a verifier only when its existing method has a synchronous, explicit PASS/FAIL contract and its
state restoration is already trustworthy. Declare the exact type and method in `Verifiers`, set the
minimum Play/gameplay/isolation requirements, negative-test `dry_run` blockers, and prove a real PASS
through the native CLI. If the tool is asynchronous, produces only console output, or requires a new
mutation model, fix that tool's completion contract first instead of weakening the adapter.
