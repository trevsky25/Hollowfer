# Batch 122 — Gameplay collider and scene health

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Use the Hollowfen-native Pipeline command layer on a real production task: classify the gameplay
scene's negative-scale collider findings, repair only Hollowfen-owned scene/prefab state when a
behavior-preserving correction is justified, and leave a repeatable collision/traversal regression
check without editing third-party source assets merely to silence known vendor warnings.

## Plan

- [x] Capture the exact native world-audit and gameplay warning baseline.
- [x] Trace every finding to collider type, transform, prefab instance root, and source asset.
- [x] Inspect representative geometry and determine whether findings are cosmetic vendor noise or
  real Hollowfen collision defects.
- [x] Apply only project-owned, behavior-preserving remediation; preserve vendor source assets.
- [x] Verify collision/traversal, gameplay Play Mode health, production preflight, and Coplay fallback.
- [x] Update docs, queue, dashboard, and this worksheet; commit and tag `batch-122`.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Third-party boundary | Diagnose first; never edit an imported source prefab in place. | `Docs/conventions.md` explicitly classifies these asset-pack warnings as known-harmless and requires prefab variants/external project-owned hooks for justified corrections. |
| Exit criterion | Prefer proved collision correctness over a cosmetically empty warning counter. | Replacing valid vendor collision only to silence Unity would add ownership and regression risk without improving the game. |
| Repair scope | Disable only the 36 exact scene instances after proving they are enabled, zero-bounds, invisible proxy renderers with separate visible LOD art. | The components were structurally invalid and supplied no collision; a scene override removes active physics debt without forking four nested vendor prefab families. |
| Audit semantics | Count collider components, enabled colliders, and disabled colliders separately; only enabled components can be active negative/zero-scale hazards. | The old report would keep calling a safely disabled component a live defect or hide it entirely depending on `include_inactive`; the new split preserves both operational truth and audit history. |

## Verification evidence

- **Starting state:** the repository began clean on `codex/gameplay-production-pass` at `71ce634` /
  tag `batch-121`, five commits ahead of origin. Existing user work was not changed or discarded.
- **Baseline:** the clean loaded `Scene_Hollowfen` contained 608,094 GameObjects, 377,161 renderers,
  72,617 collider components, 185 materials, 138,347,536 loaded authored mesh-instance triangles,
  zero missing scripts, zero zero-scale collider transforms, and 36 enabled negative-determinant
  collider transforms. Opening/auditing the scene produced zero current warnings or errors, so the
  older warning language was treated as historical rather than assumed current truth.
- **Classification:** a guarded read-only Pipeline inspection proved all 36 were enabled non-trigger
  `BoxCollider`s at exact `.../Wood Support 3/BrokenWood2 (2|4)/Cube` paths beneath 11 outer rampart
  instances. Every collider had `(0,0,0)` world bounds, a disabled proxy `MeshRenderer`, separate
  visible `LODGroup` art, and the same source asset:
  `Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Wood/BrokenWood2.prefab`.
  The source prefab was inspected read-only and was not modified.
- **Narrow scene repair:** one guarded Editor operation required the exact source, zero bounds,
  disabled proxy renderer, visible LOD art, clean gameplay scene, and exactly 36 matches before
  disabling them. The saved scene diff is 144 added YAML lines: 36 prefab-instance
  `m_Enabled: 0` overrides, with no transform, renderer, material, or vendor-asset change. Scene hash
  changed from `d208f324ec74af9263fc68335f6972ae7671ab5c701e4980090d62c2430fcee3` to
  `6ae93595531e2fe81643ac82e483963b205d87d76699c0075b1ed3f6cfa9969d`.
- **Collision/traversal structure:** post-repair inspection found zero enabled negative-scale
  colliders, all 36 reviewed components in the disabled diagnostic bucket, and 376 enabled,
  non-trigger, nonzero-bounds colliders retained across the 11 affected rampart roots. Every root
  retains 21–51 functional colliders; no wall root was left without collision.
- **Native gameplay regression:** Main Menu → owned isolated new game slot 3 →
  `Scene_Hollowfen` reached frame 846 with Wren and `TimeManager` present, zero enabled negative-scale
  or zero-scale collider findings, and zero warnings/errors. Stop restored the clean Main Menu,
  cleared the owned fixture, and reported no active save override. Prefab-source identity is not
  available on the runtime-instantiated objects, so the exact 36/source/11-root assertions remain an
  Edit Mode structural proof rather than a misleading Play Mode claim.
- **Authoritative gates:** native `hollowfen_preflight` returned
  `AUDIT PREFLIGHT — PASS (StandaloneOSX)` and `DATA INTEGRITY — ERRORS=0 WARNINGS=0`.
  Gotcha lint returned `ERRORS=0 WARNINGS=0 WAIVED=1`; Python tooling byte-compilation and
  `git diff --check` passed.
- **Coplay cross-check:** Coplay 10.1.0 independently returned product `Hollowfen - The Failing
  Village`, clean `Scene_MainMenu`, stopped state, and zero warning/error entries. `run_integrity.py`
  returned errors=0/warnings=0; `smoke_play.py --min-frames 240` reached frame 365 with zero pre-play
  and in-play errors before stopping and deleting its temporary journal.
- **Save and production safety:** the real-save aggregate stayed at
  `2ad3ece7a596382a97b989b2a990e9a674b0f024f84842ecc593a7c1c0a48443`. Pipeline, Coplay,
  Newtonsoft, and Roslyn remain Editor-only; Player-runtime Pipeline automation, the production gate,
  and the player-artifact denylist were not changed. Only structured responses and bounded console
  queries were read; raw Unity startup logs were never inspected or published.

## Review verdict

**Performance: PASS.** The change disables 36 invalid, zero-bounds physics components and adds no
Player code or hot-path work. No mesh, renderer, material, LOD, or transform changes were made. The
376 retained functional wall colliders are the traversal backstop; authored scene totals remain
diagnostic rather than a Steam Deck framerate claim.

**Production boundary: PASS.** All implementation lives in a scene override plus the existing
Editor-only Pipeline adapter. Imported assets and Player dependency boundaries remain intact, and
the authoritative production preflight still passes.

## Docs updated

- `Docs/systems/agent-tooling.md` — active/disabled collider-audit contract and Batch 122 status.
- `Docs/tests.md` — exact world-audit semantics and the bounded cleanup evidence.
- `Docs/conventions.md` — current third-party diagnostic policy and scene-override rationale.
- `../TODOS.md` — Batch 122 completion and the remaining build-cleanup scope.
- `Docs/dashboard.html` — regenerated production board.
- This worksheet — classification, repair, regression evidence, and review.

## Unfinished / handoff

No Play Mode, save override, owned fixture, dirty scene, console error, or third-party source edit
remains. Future negative-scale or terrain-collider reports should be reclassified from current
evidence rather than assumed equivalent to these 36 rampart proxies.

## Feedback to Trevor

This was a strong first gameplay use of the CLI connection: the native audit reduced a vague legacy
warning category to 36 exact objects, guarded the mutation against the evidence, and re-ran the
authoritative production gate without a bespoke manual checklist. The useful leverage is not broad
Editor automation for its own sake; it is fast, repeatable proof around narrowly owned changes, with
Coplay retained as an independent implementation path.
