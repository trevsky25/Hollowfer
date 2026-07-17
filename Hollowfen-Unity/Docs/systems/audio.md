# Audio (adaptive music + ambience + complete cast voice-over + gameplay feedback)
`MusicManager` keeps the licensed score phase-continuous across regional/day-night states; `AmbienceManager` crossfades four deterministic 48 kHz soundscapes. Every one of the 267 lines in the 75-asset dialogue graph now owns index-matched 24 kHz mono VO, and `GameplaySfx` gives core interactions a restrained tactile cue.
Key scripts: `Scripts/Audio/{MusicManager,AmbienceManager,GameplaySfx}.cs`; playback in DialogueScreen/NarrationOverlay; `DialogueVoiceoverImporter` + the text-hash manifest; `RegionCatalog` + `RegionTrigger`.
Routing: score → Music; dialogue/narration/UI/gameplay cues → SFX; ambience → Master with independent `audio.ambience` trim. The project mix is locked to 48 kHz.
World state: six trigger volumes cover southern/northern village, Wend crossing, clear-cut, Old Wood, and manor. Small gaps retain the last atmosphere rather than cutting to silence.
Biggest gotchas: audio DSP time does not advance under stepped-editor verification; assert dispatch/sample data/routing in one bridge call. Dialogue text changes require a matching regenerated clip and `dialogue_manifest.json` rebuild or integrity intentionally fails.
Status: adaptive score, four regional day/night profiles, 13 gameplay cues, and 267/267 spoken dialogue lines verified 2026-07-16. Shipping AI disclosure remains QUESTIONS Q10.

Batch-29 established the first music/VO test pipeline. Batch-76 moved Wren and her inner narrator to a reference-conditioned Chatterbox pipeline with staged, validated replacement; batch-79 applied the approved pitch-preserving `1.05×` pacing. Batch-85 extends that same Wren identity to all 107 of her dialogue lines, casts every other NPC, and makes complete coverage a project invariant rather than a nullable experiment.

The full supporting cast is intentionally differentiated: Bram `bm_lewis`@0.86; Marra `bf_emma`@0.93; Almy `bf_isabella`@0.84; Edda `bf_lily`@1.04; Hollin `bf_alice`@0.94; Joren `bm_george`@0.88; Voss `bm_daniel`@0.93; Theo `bm_fable`@1.00; Calden `bm_lewis`@0.92; Aldric/letter `bm_daniel`@0.84; Pell `bm_george`@0.97. These are test/production-direction performances, not a claim that final human casting is complete.

The project audio mix is explicitly locked to **48 kHz** in `ProjectSettings/AudioManager.asset`. Leaving System Sample Rate at platform default allowed the editor to initialize at 24 kHz on the test device while the music bed was 48 kHz, producing a shared 2× playback-rate failure. Source VO remains 24 kHz mono; Unity resamples it into the 48 kHz mix while preserving duration and pitch. **UI SFX (`Scripts/UI/UISfx.cs`, batch-56)** remains a fully procedural cue set routed to SFX.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## What exists

| Piece | Where | Notes |
|---|---|---|
| Adaptive music | `_Music` → `MusicManager` | Loops the Magic Pig pack's `Misty Forest.wav` through Music. Two runtime banks support equal-power regional track crossfades; today's four states inherit the same track to preserve phase while volume and low-pass glide. Village is most open; Wend, Old Wood, and manor are progressively restrained; all soften further at night. Optional regional clips can be assigned later without changing the state engine. |
| Regional ambience | `_WorldFeedback` / menu `_Ambience` → `AmbienceManager` | Four lazily synthesized/cached 12-second mono profiles, each with distinct day and night loops at 48 kHz. Two day/night banks use the lighting cycle; two region banks equal-power crossfade over 4 seconds. Fixed seeds, seamless 0.5-second head/tail fold, safety limiter, no imported ambience files. Menu defaults to Old Wood; gameplay defaults to village until a trigger fires. |
| Region routing | `RegionTrigger` → `LocationRegistry.RegionChanged` | One event fans out to ambience, adaptive score, map region label, and `RegionArrivalToast`. Null gaps retain the previous audio state; trigger disable unregisters itself. |
| Dialogue VO | `DialogueLine.voiceClip` | All 267 lines are required. DialogueScreen plays on line show via a priority-0 AudioSource routed to SFX; advancing stops the prior read, closing stops speech, and typewriter skip intentionally does not. |
| Narration VO | `NarrationOverlay.Show(captions, clips, onDone)` | Index-matched clips; caption hold = `max(autoAdvance, clip.length + 0.8s)`; advance/skip cuts audio. StoryBeats passes `_introVoiceClips` for the homecoming intro. |
| Gameplay feedback | `GameplaySfx` | Thirteen cached 48 kHz mono procedural cues: alternating knife strokes, forage release, delivery, coin earn/spend, item/quest result, key turn, door, rest, planting, and crop maturity. One hidden DDOL source is routed through the same SFX mixer setting as UI feedback. Dialogue outcomes choose one highest-priority cue to avoid reward cacophony. |
| Player Foley | `ThirdPersonController.FootstepSource` | The existing ten-clip footstep bank and landing clip now play through a dedicated spatial source routed to `GameplaySfx.Output`; they no longer bypass the SFX setting through `PlayClipAtPoint`. |
| Cast generator | `tools/agent/generate_vo.py --all-dialogue` | Parses Unity YAML and renders the full non-Wren cast through explicit British voice profiles. Unknown speakers are a hard error. Wren/Narrator remain protected unless `--allow-wren-scratch` is deliberate. Output uses the stable `<dialogue>/<index>_<speaker>.wav` contract. |
| Wren recast generator | `tools/agent/generate_wren_chatterbox.py` | Reference-conditioned 24 kHz mono Chatterbox; asserts 122 Wren/narrator clips (107 dialogue + 15 narration), uses grounded dialogue/narration profiles, applies pitch-safe 1.05× tempo, validates before `--apply`, and supports `--missing-only` so approved reads are preserved. |
| VO importer + manifest | `DialogueVoiceoverImporter`; `tools/agent/build_voice_manifest.py` | Applies mono/background/Vorbis spoken-word settings, wires all clips by index/speaker, and refuses missing files. The manifest fingerprints speaker, text, and clip with SHA-256; both the importer and DataIntegrity reject stale dialogue audio. |

## Coverage (test scope)

Dialogue: **267/267 lines**, 75/75 DialogueData assets. Wren owns 107 dialogue reads; the supporting cast owns 160. Separate narration remains 6 HomecomingIntro + 7 HiddenJournal + IntroGuide + MarraKitchen.

## Design intents (don't "fix" these)

- **Skip-typewriter does NOT cut the voice** — first press completes the text while the read
  finishes (fully-voiced genre convention); the second press advances and cuts. Intentional.
- **The last line's clip keeps reading under choice pills** — the question is still being asked.

## Follow-ups (backlog)

- Dedicated **Voice mixer group + settings slider** — VO currently rides the SFX slider; a player
  who zeroes SFX silently mutes all speech. Acceptable for the test, MUST fix before broad VO.
- Replace inherited regional score states with dedicated compositions only when final music is
  authored; the crossfade engine and state slots already exist.
- A true Ambience mixer group remains optional if VO ducking is wanted. Source-level Ambience trim
  currently gives the settings slider independent behavior without risky mixer-asset surgery.
- Intro captions are still duplicated verbatim in `generate_vo.py` vs StoryBeats; dialogue itself is
  protected by the new manifest, but non-dialogue narration remains a two-source authoring risk.
- If voiced narration ever fires from a quest-completion that chains a dialogue, the two
  AudioSources can overlap — have `NarrationOverlay.Show` stop DialogueScreen's voice then.
- If AI VO ships: do a final pronunciation/listening audit and complete the **Steam AI-content
  disclosure** on the store page. The full cast map and regeneration sweep are now implemented.
- `Application.runInBackground` as a real project setting (PC-game norm) instead of test-only.
