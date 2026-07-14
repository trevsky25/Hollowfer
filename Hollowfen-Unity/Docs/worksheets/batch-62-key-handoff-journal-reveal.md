# Batch 62 — Key handoff camera + journal reveal (VO, smooth transition, moved to table)

**Date:** 2026-07-13 · **Status:** verified

## Goal
Four play-feedback fixes from Trevor:
1. The Bram mill-key handoff should look better — "amazing camera angles."
2. Add voice-over for Wren discovering the journal in the mill.
3. Smooth the journal discovery zoom-in → painted-images transition.
4. Move the journal off the too-tall clothes wardrobe onto the small table.

## What shipped
| # | Change | Files |
|---|---|---|
| 1 | Mill-key hero shot now approaches on a **sweeping arc** (`arcDegrees 40`) that **settles down onto a low hero angle** (`camHeightOffset -0.07`, `arcRise 0.14`, fov 24, push 1.35s) — the camera curves in and looks up as the key catches the light, vs the old flat level dolly. | `MillKeyHandoff.cs`, `PropFocusCinematic.cs` |
| 2 | 7 Narrator (`af_heart`) VO clips for `act1.hidden_journal.tobin_note` at `VO/HiddenJournal/00..06_Narrator.wav`, wired to `Journal_FathersJournal` QuestInteractable `_narrationVoiceClips`. | `generate_vo.py` (`--journal-only`), scene, `QuestInteractable.cs` (new field) |
| 3 | Reveal no longer glides back to the room then hard-cuts to the paintings. The focus push-in **holds on the book** (`PropFocusCinematic.Play(holdAtEnd:true)`) and the painted spreads **dissolve in over it** (`NarrationOverlay.ShowCinematic(fadeIn:true)`); the camera glides out (`Restore()`) only after narration ends. | `PropFocusCinematic.cs`, `NarrationOverlay.cs`, `QuestInteractable.cs` |
| 4 | `Journal_FathersJournal` moved from atop the tall wardrobe (book at Y≈34.94, ~1.6 m up) to the mill dining table `TableA` (surface 33.788), front-left, clear of the apple/candle/bottle clutter, angled to read. | scene |

## Key implementation notes
- **PropFocusCinematic** gained: optional arc (`arcDegrees`/`arcRise` — swing/height that ease to 0 over the push), and **hold-at-end** (`holdAtEnd` bool + `IsHeld` + public `Restore()`). Start pose is now cached in fields so `Restore()` can glide back after an arbitrary hold. Existing callers (KeyLockedDoor, old QuestInteractable path) unaffected — new params are trailing/defaulted.
- **NarrationOverlay** cinematic mode still snaps opaque by default (cross-fades from the boot loading screen); new trailing `fadeIn` on the multi-image `ShowCinematic` dissolves the whole overlay 0→1 for a mid-game reveal with nothing behind it.
- **QuestInteractable**: when a prop has BOTH a focus target and painted narration, it uses the held handoff + fadeIn + deferred prop retire (book stays visible under the dissolve, retired after `Restore()`). Dialogue props and black-caption passages keep the old restore-then-reveal path.
- **VO voice casting:** whole journal passage in Wren's narration voice (`af_heart`), consistent with the homecoming/act-break narration since it's framed as Wren reading. A distinct Tobin voice for his farewell lines (captions 3–5) is parked in QUESTIONS (Q10 family).

## Verification evidence (play-mode, bridge, `EditorApplication.Step`)
Slot 0 was NOT disturbed — the reveal was driven by invoking the focus+narration path directly (no `Interact`, so no quest completion / save); the key handoff was fired via `MillKeyHandoff.HandleGranted` reflection.
- `b62_journal_table3.png` — journal resting flat on the dining table beside the apple, reading height, clear of clutter.
- `b62_reveal_1hold.png` — tight cinematic close-up of the strapped leather journal, letterbox in (the push-in/hold).
- `b62_reveal_2dissolve.png` — painted spread `journal-01-sketches` dissolved in over the held book, caption "The first pages were recipes, in her mother's hand.", VO clip `00_Narrator` loaded (`pf.IsHeld=True`, `narrationClip=00_Narrator`).
- Tail: `no.SkipAll()` → `pf.Restore()` → `pf.IsPlaying=False, IsHeld=False, PlayerInteractor.Suspended=False` (clean cleanup).
- `b62_keyhandoff_arc.png` / `_hold.png` — mill key on a low hero angle with letterbox, catching light (arc approach). NOTE: the test spot was among trees so a trunk overlaps the bow; in the real flow the handoff fires at the fixed well/square spot after Bram's dialogue.
- All 7 VO clips generated cleanly (4.0/5.0/8.5/3.1/8.6/2.6/1.5 s). Compile clean (only the pre-existing Magic Pig asset-path warning).

## Docs updated
- `Docs/systems/dialogue.md` — new PropFocusCinematic section (arc + hold-at-end/Restore).
- `Docs/systems/audio.md` — journal VO set + regeneration command.
- `Docs/systems/quests.md` — QuestInteractable narration/focus fields + the smooth reveal flow + table move.
- `Docs/systems/ui-framework.md` — NarrationOverlay `fadeIn` param.

## Test script for Trevor (Play mode)
1. Load a save before `speakBram`; talk to Bram at the well and take the key — watch the key hero shot **arc in and settle to a low angle** (letterbox, key catching light).
2. Go to the mill; the journal now sits **on the dining table** (not the tall wardrobe), at reading height by the apple.
3. Examine it: the camera **pushes in and holds on the book**, then the **painted journal spreads dissolve in over it** (no glide back to the room, no hard cut) while **Wren's voice reads** her father's journal and note; it ends and the camera glides back.

## Unfinished / handoff
None dirty. Parked: distinct Tobin voice for the farewell lines (QUESTIONS). The mill-key handoff framing is only as good as the fixed spot Bram's dialogue leaves the player in — worth an eyeball in the real flow; the arc/angle params are serialized on the `MillKeyHandoff` component if they need a tune.
