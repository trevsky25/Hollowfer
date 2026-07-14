# Audio (music + voice-over)
Batch-29 test pipeline: `MusicManager` (one looping bed, fade-in, Music mixer group) + per-line
dialogue VO (`DialogueLine.voiceClip`, played by DialogueScreen) + caption-synced narration VO
(`NarrationOverlay.Show(captions, clips)`); clips generated locally by `tools/agent/generate_vo.py`
(Kokoro-82M TTS, Apache) into `Assets/_Hollowfen/Audio/VO/<Dialogue>/<idx>_<Speaker>.wav`.
Batch-39: the **full 6-beat homecoming intro is voiced** (`HomecomingIntro/00..05_Narrator`, Narrator=`af_heart`@0.86, index-matched to `StoryBeats._introVoiceClips`) and **Bram revoiced older** (`bm_lewis`@0.86, was `bm_george`). Trevor directed opening VO (QUESTIONS Q10). Voices live in `generate_vo.py` VOICES; re-run the scratchpad Kokoro venv to regenerate. WAVs are git-tracked.
Key scripts: `Scripts/Audio/MusicManager.cs` (on `_Music`), playback in DialogueScreen/NarrationOverlay (both `_voiceOutput`→SFX group for now); StoryBeats `_introVoiceClips`. **Menu ambience (`Scripts/Audio/AmbienceManager.cs`, batch-57)**: a fully procedural, seamless-looping forest bed (low-passed wind + leaf shimmer + sparse synth bird calls; fixed seed → same bed every boot; head/tail equal-power crossfade for a click-free loop), on `_Ambience` in Scene_MainMenu. Routed to the **Master** group; volume = an internal `_ceiling` (0.45) × the player's Ambience setting (`PlayerPrefs "audio.ambience"`, live via `AmbienceManager.Instance.SetUserVolume`, settings slider). **Design note (batch-57):** ambience is trimmed at the SOURCE level with its own settings slider rather than getting a dedicated Ambience mixer *node* — a deliberate choice to avoid risky internal-API `.mixer` surgery on the shipping asset. User-facing behavior (an independent Ambience slider; Music/SFX don't touch it; Master does) is equivalent. A true Ambience group can be added later if DSP ducking (e.g. under VO) is wanted. **UI SFX (`Scripts/UI/UISfx.cs`, batch-56)**: fully procedural cue set — `Move` (nav focus change), `Select` (open/advance), `Back` (close/retreat), `Confirm` (modal affirmative), `Error` (dead-end press); routed to the SFX group via `UIManager._uiSfxOutput`. Hooks live in `UIManager`: `Update` watches `EventSystem` selection for Move (seeded silent in `SetFocusToTop`, gated by a post-transition cooldown), `TransitionRoutine` plays Select on push/replace + Back on pop, `OnSubmitAudio` plays Error on a non-interactable target; `ConfirmModal.HandleConfirm` plays Confirm + `UIManager.SuppressNextTransitionSfx()` so the pop's Back doesn't double it.
Biggest gotchas: audio state is UNMEASURABLE ACROSS bridge calls — editor pauses between execute_code calls hard-stop AudioSources (they read stopped/`isVirtual` with time 0), so assert playback IN THE SAME CALL immediately after Play(), never across calls (batch-30: a whole phantom-bug hunt). **Corollary (batch-56):** under `EditorApplication.Step()` the audio DSP clock does NOT advance, so `AudioSource.isPlaying` stays `true` forever once a one-shot starts and NEVER reports silence — it proves "a cue fired" but is useless for "did it stop / was it suppressed". Verify silence/suppression deterministically instead: inspect the synthesized `AudioClip` sample data (length/peak), the `outputAudioMixerGroup`, and one-shot flag lifecycles via reflection. VO/narration sources use priority 0 and music 16 (the vendored world carries ~50 ambient sources — default-priority speech can be virtualized); the espeakng-loader wheel hard-exits on macOS — `brew install espeak-ng` is the generator's one system prerequisite.
Status: entrance scene verified end-to-end 2026-07-12 (intro narration voiced + hold-extended, Bram chain voiced per speaker, clean cut on advance/close, Misty Forest bed via Music slider). AI-VO shipping decision + Steam AI disclosure = QUESTIONS Q10.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## What exists (batch-29)

| Piece | Where | Notes |
|---|---|---|
| Music bed | `_Music` GameObject → `MusicManager` | Loops the Magic Pig pack's `Misty Forest.wav` (licensed pack asset — ⚠️ lives in the pack's `_Demo Scenes/` folder; a pack update relocating demo content silently kills the bed); 5s fade-in on scene load; source-level ceiling 0.55 UNDER the mixer's Music volume; hard cut on scene exit (no fade-out — Audio pass). Deliberately dumb — region/state music is the Audio-pass backlog item. |
| Dialogue VO | `DialogueLine.voiceClip` (nullable) | DialogueScreen plays on line show via a lazily-added AudioSource (`_voiceOutput` scene-serialized → SFX group); previous clip always stops first; Close() stops. All 70 pre-VO dialogues unaffected (null = silent). |
| Narration VO | `NarrationOverlay.Show(captions, clips, onDone)` | Index-matched clips; caption hold = `max(autoAdvance, clip.length + 0.8s)`; advance/skip cuts audio. StoryBeats passes `_introVoiceClips` for the homecoming intro. |
| Generator | `tools/agent/generate_vo.py` | Parses dialogue .asset YAML (multiline/quoted scalars) → Kokoro WAVs, 24kHz mono. Voice cast in `VOICES` (Wren `af_heart`, Bram `bm_george`, narrator = Wren slowed). Venv: scratchpad, uv + **cpython-3.12 aarch64** (3.14 has no spacy wheels; the default uv python resolved x86_64 → torch dead end). Needs `en_core_web_sm` wheel pre-installed (uv venvs lack pip, misaki's auto-install fails) and brew espeak-ng (wheel loader hard-exits). **`--extras-only`** (batch-42) regenerates just the non-dialogue card utterances (`EXTRAS`, e.g. IntroGuide) without touching the intro narration or dialogue WAVs. |

## Coverage (test scope)

Voiced: HomecomingIntro (2 captions) + Bram Act I chain (Homecoming 5, CrookedPintle key 12, Repeat 3) + the IntroGuide first-steps passage (batch-42: re-read to the new "…find Old Bram at the well" copy, ~11s narrator; regen via `generate_vo.py --extras-only`).
Everything else is silent by design until the VO direction is decided (Q10).

## Design intents (don't "fix" these)

- **Skip-typewriter does NOT cut the voice** — first press completes the text while the read
  finishes (fully-voiced genre convention); the second press advances and cuts. Intentional.
- **The last line's clip keeps reading under choice pills** — the question is still being asked.

## Follow-ups (backlog)

- Dedicated **Voice mixer group + settings slider** — VO currently rides the SFX slider; a player
  who zeroes SFX silently mutes all speech. Acceptable for the test, MUST fix before broad VO.
- **Staleness guard**: emit a per-index text-hash manifest next to the WAVs + a `--check` mode in
  the generator, so edited dialogue text can't silently keep old audio. (Also: the intro captions
  are duplicated verbatim in generate_vo.py vs StoryBeats — two sources of truth until then.)
- If voiced narration ever fires from a quest-completion that chains a dialogue, the two
  AudioSources can overlap — have `NarrationOverlay.Show` stop DialogueScreen's voice then.
- If AI VO ships: full-cast voice map, regeneration sweep over all dialogues, pronunciation
  overrides for names (Hollowfen/Wendlight read acceptably in the test; audit before shipping),
  and the **Steam AI-content disclosure** on the store page.
- `Application.runInBackground` as a real project setting (PC-game norm) instead of test-only.
