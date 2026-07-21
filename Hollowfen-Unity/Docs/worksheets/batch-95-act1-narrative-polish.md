# Batch 95 — Act I narrative and voice-over polish

**Date:** 2026-07-17 · **Status:** complete

## Goal
Strengthen Hollowfen's opening story flow: sharpen the homecoming narration, turn Bram's mill-key scene into a shorter and more substantial introduction, keep Bram staged beside the village well, explain the purpose of Tobin's mushroom journal more clearly, replace "Da" with "Dad," and place live localized farewell text on the final blank journal page.

## Plan
- [x] Trace the live welcome, Bram, key-handoff, journal-reveal, VO, and schedule paths.
- [x] Revise the canonical and in-game Act I copy.
- [x] Add data-authored live page text to illustrated story moments.
- [x] Regenerate affected Wren/narrator and Bram VO, rebuild the voice manifest, and rewire clips.
- [x] Run integrity, compile, and targeted Play-mode verification.
- [x] Update system docs and finalize this worksheet.

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Father's form of address | Use "Dad" in Wren's speech, notes, and the farewell signature; Bram says "your father." | Canon gives Tobin no distinct first name, and Trevor explicitly retired "Da." |
| Bram scene shape | Five first-meeting turns across the existing recognition/key dialogue chain; no automatic repeat-dialogue tail. | Preserves the quest/outcome graph while cutting the interaction from twenty advances to five substantial reads. |
| Bram placement | Keep the authored `Bram_Well` anchor, 2m from the village-well marker, as the homecoming fallback. | The existing schedule already matches the requested location and keeps later Pintle routines available after Act I. |
| Journal page copy | Localized TMP text attached to the active painted hero layer. | Text remains crisp/localizable and follows the image's Ken Burns movement instead of being baked into one language's art. |

## Verification evidence
- Unity refresh/compile completed with no C# errors; runtime console remained error-free during the targeted reveal.
- `DialogueVoiceoverImporter.ApplyAll`: `Wired 253 lines across 75 dialogues; updated 5 spoken-word importers.`
- `build_voice_manifest.py --check`: PASS for all 253 current dialogue lines.
- New Bram-chain contract: 2 homecoming clips + 3 key clips + 1 later repeat clip, all 24 kHz mono. Durations are 12.16s / 15.57s, 9.87s / 21.88s / 7.21s, and 7.90s respectively.
- Removed 19 obsolete clips and their `.meta` files only after proving their GUIDs had no remaining asset references; Git can recover them if the old takes are ever wanted.
- Live scene inspection: `Bram_Well=(284.80, 37.03, 158.40)`, `Marker_VillageWell=(286.00, 37.00, 160.00)`, distance `2.00m`.
- Hidden-journal Play-mode inspection: seven resolved captions, final page text active on image 2 from beat 3, 205 localized characters; 3840×2160 capture confirmed the full note and `— Dad` signature are readable on the paper and clear of the hands/page edge.
- Follow-up alignment correction: the page now reveals paragraph 1 on beat 3, paragraph 2 on beat 4, and the apology/signature on beat 5. Start- and end-pose 3840×2160 captures confirmed the handwriting stays legible through the full image-2 Ken Burns move and the active paragraph matches the subtitle/VO.
- `lint_hollowfen.py`: `ERRORS=0 WARNINGS=0` (one existing waiver).
- `run_integrity.py`: `ERRORS=0 WARNINGS=0` across 26 quests, 75 dialogues, 11 NPCs, 30 story cards, two story moments, and the remaining canonical databases.
- `npm run build`: PASS for the synchronized web dialogue/story copy.
- `git diff --check`: PASS; Unity/root story and dialog-system mirrors are byte-identical.

## Docs updated
- Canonical story mirrors: `Hollowfen-Unity/Docs/story.md`, `docs/story.md`.
- System references: audio, dialogue, localization, quests, UI framework, and both dialog-system mirrors.
- Web-era story data/export: `src/data/Dialogs.js`, `src/data/StoryCards.js`, `public/book/index.html`.
- `QUESTIONS.md` and regenerated `Docs/dashboard.html` now use “Dad” in the open voice-casting question.

## Unfinished / handoff
The optional creative decision in QUESTIONS Q15 remains open: the farewell can stay in Wren's reading voice or later receive a distinct father voice. No separate first name for Tobin was invented. The repository and gameplay scene contained extensive unrelated pre-existing changes before this batch; they were preserved.

## Feedback to Trevor
The opening now moves from a more specific view of Hollowfen's failure into an affectionate recognition at the well. Bram introduces himself through remembered behavior, his history with Wren's parents, and the burden of keeping the key. The journal reveal now identifies the object before explaining its entries, then lets the blank final page become the father's actual letter instead of leaving the emotional turn only in subtitles.
