# Batch 29 — VO + music pipeline (entrance-scene test)

**Date:** 2026-07-12 · **Status:** IN PROGRESS · tag `batch-29` (pending)
**Directive:** Trevor — "how hard would it be to get audio for the voiceovers … test it out with the
entrance scene … Some music would be awesome too." Choices (AskUserQuestion): **Kokoro local TTS**
(free/offline/Apache) + scope = **intro narration + Bram's Act I chain**.

## Goal
Prove the audio feel end-to-end on the first 3 minutes of a new game: the 2 homecoming narration
captions (Wren narrator voice) + Bram's 3-dialogue chain (two distinct voices), with music fading
in under the intro. Build the pipeline as REUSABLE infrastructure (dialogue asset → WAVs script),
not a one-off.

## Plan
1. **TTS pipeline** — `tools/agent/generate_vo.py`: parses Unity dialogue .asset YAML (`_lines`
   speaker/text incl. multiline + quoted scalars) → Kokoro-82M WAVs (24kHz mono).
   Voice cast (test): Wren = `af_heart`, Bram = `bm_george` (older British male — village register),
   narrator = Wren's voice at slower speed. Venv in scratchpad (not committed); script committed.
2. **Data model** — `DialogueLine.voiceClip` (AudioClip, defaults null — back-compatible with all
   70 dialogues); StoryBeats gains serialized `_introVoiceClips[]` parallel to IntroCaptions.
3. **Playback** — DialogueScreen: dedicated AudioSource (SFX mixer group for the test), play clip
   on line show, stop on advance/skip. NarrationOverlay: `Show(captions, clips, onDone)` overload;
   caption hold = max(text time, clip length + beat).
4. **Music** — `MusicManager` on a scene host: loops the Magic Pig pack's `Misty Forest.wav`
   (already licensed + imported; pack is gitignored but GUID refs survive — established precedent),
   fade-in on scene load, routed to the Music mixer group (existing slider controls it).
5. Wire clips to the 3 Bram dialogue assets + StoryBeats via bridge; verify in play mode with a
   fresh save (intro fires once per save — back up + restore saves).
6. **Voice mixer group deferred**: VO routes through SFX for the test; a proper Voice group +
   settings slider is a follow-up line item (settings screen makes the slider trivial post-28).

## Out of scope / parked
- Shipping decision on AI-generated VO (incl. **Steam's AI-content disclosure**) → QUESTIONS Q10.
- Full VO coverage of all 70 dialogues, ambience beds, SFX pass (Audio pass backlog item),
  Voice mixer group + slider.

## Pipeline battle scars (recorded for reuse)
1. **Python 3.14 can't run Kokoro** (no spacy wheels) → uv-managed 3.12.
2. **uv's default 3.12 resolved x86_64** on this Mac (`macosx_26_0_x86_64` tag) → torch stuck at
   2.2.2 (last Intel-mac wheel) → transformers refused. Fix: `uv python install
   cpython-3.12-macos-aarch64-none` explicitly.
3. **espeakng-loader wheel hard-exits** (C-level `exit(1)`, uncatchable) — its dylib initializes
   with a baked CI path before phonemizer's override lands. Fix: `brew install espeak-ng` +
   re-point `EspeakWrapper` at the brew lib/data in the generator (self-contained).
4. **misaki auto-installs spaCy's model via uv** and fails in pip-less uv venvs → pre-install the
   `en_core_web_sm` wheel.
5. **AudioSources read `isPlaying=false` while the editor is paused** — `EditorApplication.Step()`
   leaves it paused, so audio asserts need unpaused real-time windows; and the player loop freezes
   when Unity is unfocused (music thread keeps going, coroutines don't) → `Application.runInBackground
   = true` in the test session. Both recorded in audio.md gotchas.

## Verification evidence
**Play-mode (bridge), fresh save (slots backed up + restored), all green** — integrity 0/0,
lint 0/0, no new console errors:
- 22 WAVs generated (2 intro + 20 dialogue lines), all parsed verbatim from the assets
  (multiline + quoted YAML scalars handled; "'Aye" dialect apostrophes preserved).
- **Intro:** narration overlay showed with `00_Narrator` clip; the read played to completion
  (t = 5.1s = full clip length) with the caption held; music bed `Misty Forest` playing at
  vol 0.55 through the Music group (t advancing across checks — audibly live).
- **Bram chain:** fresh save → completed `arrive` → `PickDialog = act1.homecoming.bram.recognition`;
  line 1 loaded `00_Bram` routed to SFX; advance → clip swapped to `01_Wren`, `isPlaying=True`;
  `Close()` → voice stopped.
- 20/20 dialogue lines wired by index/speaker convention; StoryBeats `_introVoiceClips` wired;
  `_Music` + `_voiceOutput` refs scene-serialized (survived an earlier half-applied bridge drop —
  re-verified field-by-field). Scene diff +60/−6 lines; font churn reverted.

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES** (independent fable reviewer; verified the scene/mixer wiring
directly and RAN the parser against the shipped assets — would have been BLOCK uncommitted).
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | HIGH | Parser truncated multi-paragraph lines at Unity's `\n\n` blank-line serialization — **3 shipped WAVs were wrong-content audio**, incl. the key-handoff line ("Aye. I know. ~~I've got the key inside.~~"); the "'Aye" apostrophe was the unterminated-quote artifact, not dialect | Fold generalized through blank-line runs whose next non-blank is continuation; all 22 WAVs regenerated (truncated 3: 1.9→3.3s, 1.8→10.6s, 2.5→4.2s); wired refs re-verified at full length |
| 2 | MED | Empty-text filter re-indexed lines → wrong-line audio landmine as coverage scales | parse returns ALL lines in Unity order; empty text burns its index, no WAV |
| 3 | MED | No staleness guard (edited text keeps old audio silently); intro captions duplicated in two places | Parked to audio.md follow-ups (manifest + `--check` mode) — Q10 gates further generation anyway |
| 4 | LOW | Dead `pass` block in parser; trailing-entry append relied on field order | Real append via `appended_cur` sentinel |
| 5 | LOW | Superseded `Show()` let the old run's clip bleed ~0.8s into the new run | `Show()` stops `_voiceSource` before restarting |
| 6 | LOW | VO on the SFX slider = zeroed SFX silently mutes speech | Documented as MUST-fix before broad VO (audio.md) |
| 8 | NIT | Music track lives in the pack's `_Demo Scenes/` (relocation-fragile); audio.md said "vendored" | Wording fixed + fragility warning in audio.md |
| 9 | NIT | Quoted speaker names skipped `unquote` → silent wrong-voice fallback | Speaker unquoted at parse |
Confirmed-correct by review (design intents recorded in audio.md): skip-typewriter NOT cutting
audio; last-line clip under choice pills; chain/replay clip swap; struct field addition safe for
the other 67 dialogues (name-based serialization, no reserialization stampede — git confirmed
only the 3 wired assets rewrote); MusicManager has no duplicate-instance/DDOL risk.

## Docs updated
- **`systems/audio.md` — NEW** (7-line header; what exists, generator prerequisites + battle scars,
  follow-ups); routed from `CLAUDE.md` (Settings row split).
- `systems/dialogue.md` — header notes `DialogueLine.voiceClip`.
- `QUESTIONS.md` — **Q10** (AI-VO direction + Steam AI-content disclosure + voice-casting veto).
- `TODOS.md` — snapshot + Audio-pass backlog + pre-EA Steam-disclosure checklist item.
