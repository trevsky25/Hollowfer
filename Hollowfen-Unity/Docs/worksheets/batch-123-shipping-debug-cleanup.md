# Batch 123 — Shipping debug cleanup

**Date:** 2026-07-21 · **Status:** verified and ready to commit

## Goal

Remove the first bounded set of Hollowfen-owned build-cleanup debt: empty duplicate script folders,
the obsolete unreferenced UI coroutine driver, and the inactive gameplay-scene location debug overlay
that still compiles legacy Input/IMGUI/discovery-mutation code into Player assemblies. Preserve the
native production gate and independently verify the result through Coplay.

## Plan

- [x] Capture the clean Batch 122 baseline and exact references for each candidate.
- [x] Remove only assets and scene components proved obsolete or unreferenced.
- [x] Remove the resolved legacy-input lint waiver and update affected documentation.
- [x] Verify compilation, scene integrity, Player assembly/plugin policy, data integrity, and Play Mode.
- [x] Regenerate the dashboard, commit the batch, and tag `batch-123`.

## Decisions made

| Decision | Choice | Why |
|---|---|---|
| Empty duplicate folders | Remove `Save 1` and `Steam 1` plus their metas. | Both directories are empty, their GUIDs have no references, and the canonical save/Steam systems live elsewhere. |
| UI test driver | Retire the script and now-empty `UI/Tests` folder. | The driver GUID has no scene or asset references, its 2026 coroutine only targets `test-a`/`test-b`, and the non-build `Scene_UITest` no longer attaches it. Current focused verifiers supersede it. |
| Location debug HUD | Remove its inactive scene root and delete the script instead of migrating its `F` key to the Input System. | It is an inactive development overlay that polls every frame, renders unlocalized IMGUI, and can mutate location discovery. Keeping a shipping component for obsolete diagnostics adds more risk than value. |

## Verification evidence

- **Baseline and ownership:** the repository was clean at `230392e` / `batch-122`. `Save 1` and
  `Steam 1` were empty directories whose meta GUIDs had no references. `UITestDriver` had no asset or
  scene references; `Scene_UITest` is excluded from Build Settings and contains `test-a`/`test-b` but
  no driver component. The only live `LocationDebugHUD` GUID reference was one inactive, root-level,
  two-component GameObject in the clean gameplay scene.
- **Bounded removal:** a guarded native Pipeline operation required the exact clean gameplay scene,
  exactly one inactive `_LocationDebugHUD`, exactly two components, and the reviewed component type
  before deleting the root and saving. The scene diff is only that 47-line GameObject/component/
  transform block plus its one `SceneRoots` entry. The script, metas, empty folders, unreferenced UI
  driver, and now-empty `UI/Tests` folder were then removed; no vendor asset changed.
- **Scene health:** the post-compile gameplay world audit reports 608,093 GameObjects (exactly one
  fewer), 377,161 renderers, 72,617 collider components, 185 materials, zero missing scripts, zero
  enabled negative/zero-scale collider findings, and the same 36 disabled Batch 122 findings. The
  scene hash changed from `6ae93595531e2fe81643ac82e483963b205d87d76699c0075b1ed3f6cfa9969d`
  to `d694f1987f4bddeb51da7b9e199ec768700536c6299cc3eb72ada29c61ec9659`.
- **Player boundary:** native `hollowfen_preflight` returned
  `AUDIT PREFLIGHT — PASS (StandaloneOSX)` and `DATA INTEGRITY — ERRORS=0 WARNINGS=0` through the
  unchanged `ProductionBuildGate`. A separate live `AssembliesType.Player` inspection found 33
  Player assemblies, 216 `Assembly-CSharp` sources, and zero source paths matching
  `LocationDebugHUD` or `UITestDriver`. The Pipeline/Coplay/Newtonsoft/Roslyn denylist and Player
  runtime automation setting were not changed.
- **Compile classification:** the forced Unity recompile produced zero errors. Its 51 warnings were
  41 imported Magic Pig warnings, nine pre-existing Hollowfen **Editor-tool-only** Unity 6.4
  deprecations, and one interactive-Pipeline advisory. None came from this batch or a Player source;
  vendor files were left untouched and the nine project deprecations are explicitly queued as a
  separate focused cleanup instead of being bundled here.
- **Static and Coplay gates:** gotcha lint returned `ERRORS=0 WARNINGS=0 WAIVED=0`; Python tooling
  byte-compilation and `git diff --check` passed. Coplay 10.1.0 independently reported the correct
  product/Main Menu, 33 Player assemblies, zero retired debug sources, and zero errors.
  `run_integrity.py` returned errors=0/warnings=0; `smoke_play.py --min-frames 240` reached frame 365
  with zero pre-play/in-play errors before stopping and deleting its temporary journal.
- **Save safety:** the real-save aggregate was
  `007f9232d4a4c7853834227def0de955db20a29c186fd33c5ffd1d692df9784e` before and after automated
  Play Mode. This is the Batch 123 baseline after Trevor's intervening manual Batch 122 play test;
  the user-owned change from the older aggregate was preserved.
- **Recovery safety:** the ignored Unity recovery snapshot `Assets/_Recovery/0 (8).unity` contains an
  old copy of the retired GUID. It is not tracked, not in Build Settings, not loaded, and was
  deliberately preserved as user-recoverable history rather than deleted or rewritten.

## Review verdict

**Steam Deck/input: PASS.** The batch removes the only waived legacy Input call and introduces no
new action, glyph, screen, or controller flow. Existing player UI/input behavior is unchanged.

**Performance: PASS.** One inactive component is removed from the scene and two obsolete scripts
leave Player compilation. The retired HUD's per-frame `Update`, `OnGUI`, player lookup, and discovery
query can no longer enter a Player; no new hot-path, renderer, material, mesh, or memory cost exists.

**Production boundary: PASS.** The exact production preflight remains authoritative and green. No
denylist, plugin compatibility, build option, package assembly, or Player-runtime automation setting
was weakened.

## Docs updated

- `Docs/systems/input.md` — zero-waiver Input System baseline and retired debug shortcut.
- `Docs/systems/map.md` — production discovery ownership after removing the debug route.
- `Docs/systems/ui-framework.md` — current verifier ownership after retiring `UITestDriver`.
- `Docs/tests.md` — zero-waiver lint expectation.
- `../TODOS.md` — completed subtask and remaining build-cleanup slices.
- `Docs/dashboard.html` — regenerated production board.
- This worksheet — ownership, removal, production evidence, and review.

## Unfinished / handoff

Unity is stopped on the clean Main Menu with no active save override. The ignored recovery snapshot
is intentionally retained. Remaining build-cleanup work is explicitly split in `TODOS.md`; none is
required for this bounded removal to be safe.

## Feedback to Trevor

This slice demonstrates a useful cleanup pattern for the CLI connection: prove references and live
scene state first, guard the exact mutation against that evidence, then ask Unity's real Player
compilation—not a filename guess—whether the retired code can still ship. Coplay supplies the
independent implementation path, while the production gate remains the policy owner.
