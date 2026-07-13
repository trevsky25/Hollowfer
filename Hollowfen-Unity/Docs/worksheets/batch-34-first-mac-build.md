# Batch 34 — First Mac build (item 15b) + build-scene cleanup

**Date:** 2026-07-12 · **Status:** DONE (build succeeds + boots, verified) · tag `batch-34` (pending)
**TODOS item:** #15b — "First Mac build boot test + build-scene-list cleanup" (ship blocker).
**Directive:** Trevor — "proceed with item 15b build test."

## Build-scene cleanup (done)
`EditorBuildSettings` had 4 scenes including two dead prototype scenes. Verified via code grep that the
real game loads only `Scene_MainMenu` (const `MainMenuSceneName` in PauseScreen) and `Scene_Hollowfen`
(const `GameplaySceneName` in MainMenuScreen/SaveSlotScreen); the legacy `Assets/Scenes/MainMenu.unity`
uses a prototype `MainMenuController` pointing at a nonexistent "Game" scene, and `Assets/Scenes/Village.unity`
is unreferenced. Dropped both → build scene list is now `[Scene_MainMenu, Scene_Hollowfen]` (Scene_MainMenu
at index 0 = entry point). Persisted to `ProjectSettings/EditorBuildSettings.asset`.

## The App-Nap blocker and the fix
The batch-32 build attempt (and a first attempt this batch) stalled: macOS **App Nap** throttled the
backgrounded editor's main thread mid-build (log mtime freezes, ~1% CPU, no bee_backend). A foreground-keeper
loop (re-activating Unity every 2s) got the build further but couldn't win — Claude's own tool calls keep
stealing focus, so App Nap crept back in the quiet player-packaging phase.

**Root-cause fix applied:** `defaults write com.unity3d.UnityEditor5.x NSAppSleepDisabled -bool YES` — but it
MUST be set while Unity is **closed** (writing it for a running app gets clobbered when the app flushes prefs
on exit — observed live: the value read back `1`, then vanished after Unity quit). Procedure that worked:
force-quit Unity → `defaults write … NSAppSleepDisabled -bool YES` → `killall cfprefsd` → verify `= 1` →
relaunch `Unity -projectPath …`. Post-relaunch the editor runs the build at 99% CPU with no stall.
**This is now the standing precondition in night-shift.md (already documented there); the new lesson is the
set-while-closed ordering.**

## Build-only bug caught + fixed (the build test earning its keep)
The first player-assembly compile to actually COMPLETE (earlier attempts stalled before it) surfaced a real
build-only error hidden until now: `Magic Pig/Equipment System/Demo/IPBR_CharacterEquip.cs(63)` — vendor typo,
`child.thisBoneRoot` in the `#else` (build) branch where a `Transform` has no such member; the `#if UNITY_EDITOR`
branch used the class's `static thisBoneRoot` field correctly, so their non-editor path never compiled. That class
is used at runtime by `Scripts/PrefabChildManager.cs:181`, so the demo folder can't be excluded — fixed the one
line to mirror the editor branch. **It's gitignored vendored code → recorded in `Docs/vendored-build-fixes.md`
to re-apply on pack reinstall.**

## Boot test — PASS
- `[MCP Build] Build succeeded: StandaloneOSX → …/Hollowfen.app (4488.35 MB, 1397.0s)` (first build, no cache).
- **`Georgia.ttf` ABSENT from the entire `.app`** (`find … -iname *.ttf` empty) — the batch-32 `m_SourceFontFile`
  null confirmed: only the baked SDF atlas ships, not the Microsoft-licensed source. Licensing exposure closed
  for the font redistribution (the SDF-derives-from-it question remains a Q9/pre-EA legal check).
- `.app` bundle complete (executable `Hollowfen-Unity`, Frameworks, Data/Managed, _CodeSignature).
- **Boots clean:** launched, ran, Player.log shows CodeReloadManager + Input System + Physics initialized, no
  errors/exceptions (only the benign dev-build "LoadedFromMemory is not a mono symbol file" note). Scene_MainMenu
  is build index 0.
- (Visual screenshot of the running player skipped — macOS `screencapture` needs screen-recording permission
  this session; Player.log clean-init is the evidence.)

## App-Nap fix (the real unblock)
Two build attempts stalled on App Nap. Root cause fixed by relaunching Unity with `NSAppSleepDisabled` set —
**and the ordering lesson: set it while Unity is CLOSED** (`defaults write com.unity3d.UnityEditor5.x
NSAppSleepDisabled -bool YES` for a *running* editor is clobbered when it flushes prefs on exit — observed: read
back `1`, then vanished after quit). Sequence that worked: quit Unity → write default → `killall cfprefsd` →
verify `= 1` → relaunch `Unity -projectPath …`. Post-relaunch the build ran at 99% CPU to completion (~23 min,
first-build shader compile is the long pole; cached rebuilds are far faster).

## Verification
Build succeeded + boots + Georgia.ttf absent (above). Editor console clean (0 CS errors) after the vendor fix.
Build scene list = `[Scene_MainMenu, Scene_Hollowfen]`. Fonts survived the relaunch (Georgia Static/201/src=NULL).

## Docs updated
- `Docs/vendored-build-fixes.md` (NEW) — tracked record of the gitignored Magic Pig vendor fix to re-apply on reinstall.
- `night-shift.md` — App-Nap precondition note: set NSAppSleepDisabled while Unity is CLOSED (clobber-on-exit).
- `TODOS.md` — #15b done; first Mac build milestone reached.
- (separate docs commit this session: `QUESTIONS.md` Q8–Q11 answered per Trevor; new
  `Docs/meshy-mixamo-worklist.md`.)
