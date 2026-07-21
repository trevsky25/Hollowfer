# Batch 72 — Ending engine

## Goal

Finish the playable story spine after `meetAldric`: four canonical, score-gated outcomes; exactly one durable ending; authored final dialogue and illustrated epilogue; credits; and a safe choice to return to the menu or remain in Hollowfen. Cast models and world dressing are explicitly outside this slice.

## Implementation

- Added `EndingData` assets for Free Hollow, Lordly Patronage, Capital, and Witch's Path. Eligibility is data-authored: Hope/Knowledge thresholds, required flags, and relationship minima.
- Extended `DialogueChoice` with an optional terminal ending. Aldric's last conversation now renders four pills, disables unavailable futures, skips them during controller navigation, and explains each lock.
- Added `EndingResolver` plus `GameScores.TryCompleteEnding`. The commit is one-way: one canonical `ending_*`, consequence flags, `game_complete`, one story card, and one ending achievement.
- Added four bible-authored resolution dialogues and six-beat epilogues over the existing ending paintings.
- Added `EndingDirector` and runtime-built `EndingCreditsScreen`: final dialogue → cinematic narration → full-bleed ending card. Return saves and loads Main Menu; Remain restores time/input and leaves the completed village playable.
- Changed ending cards 27–30 from shared `_unlockAt: 26` to choice-only `-1`.
- Hardened every save writer with temp + flushed atomic replacement + `.bak` recovery. Added stable `CurrentQuestId`; completed saves identify `game_complete` / Act 4.
- Extended localization and integrity coverage for ending chrome, assets, wiring, and choice invariants.

## Canonical gates

| Ending | Gate |
|---|---|
| Free Hollow | Hope 50; final decision; tax paid; clear-cut evidence; Aldermark leverage |
| Lordly Patronage | Final decision; always-available pragmatic fallback |
| Capital | Theo 18; Theo's Capital offer received; final decision |
| Witch's Path | Knowledge 35; Hollin 15; Sable's cottage + seedbook; final decision |

## Verification

- Compile: clean, zero C# errors.
- Lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Integrity: `ERRORS=0 WARNINGS=0`; coverage = 26 quests, 74 dialogues, 4 endings.
- `EndingEngineVerifier.RunAll`: PASS for fallback state, exact threshold boundaries, all four exclusive commits, one-time achievements, card isolation, rejected second ending, disk reload, and corrupt-primary backup recovery.
- Save hygiene: backed up all four real save slots before mutation, restored afterward, and verified every SHA-1 matched byte-for-byte.
- Runtime sequence: Free Hollow choice committed; resolution dialogue handed off to illustrated epilogue; epilogue handed off to credits. Remain restored `timeScale=1` and `PlayerInteractor.Suspended=false`.
- Isolated credits open/close: zero console errors.
- Full smoke: stable at 4,690 frames, zero new console errors.
- Visual evidence at 1280×800: `Docs/screenshots/batch-72/free-hollow-epilogue-1280x800.png` and `free-hollow-credits-1280x800.png`.

## Follow-on

Trevor's requested order is now unblocked: cast model pass, then the Act III–IV world-dressing pass (Theo at the inn, festival square, Voss/Hollin staging, clear-cut/Aldermark, manor/Aldric).
