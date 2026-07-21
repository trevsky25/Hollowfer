# Audio (menu/gameplay score + adaptive ambience + complete cast VO + feedback)
`MusicManager` now carries a nine-composition, full-bag shuffled score from the title screen into the adaptive gameplay mix; `AmbienceManager` keeps four deterministic 48 kHz soundscapes beneath it. Every one of the 410 lines in the 145-asset dialogue graph owns index-matched 24 kHz mono VO, and `GameplaySfx` gives core interactions a restrained tactile cue.
Key scripts: `Scripts/Audio/{MusicManager,AmbienceManager,GameplaySfx}.cs`; playlist verification in `MusicPlaylistVerifier`; playback in DialogueScreen/NarrationOverlay; `DialogueVoiceoverImporter` + the text-hash manifest; `RegionCatalog` + `RegionTrigger`.
Routing: score → Music; UI/gameplay cues → SFX; dialogue/narration → Master with independent `audio.voice` trim; ambience → Master with independent `audio.ambience` trim. The project mix is locked to 48 kHz.
World state: six trigger volumes cover southern/northern village, Wend crossing, clear-cut, Old Wood, and manor. Region/night score filters remain active across playlist changes; quiet gaps retain that state instead of resetting it.
Biggest gotchas: audio DSP time does not advance under stepped-editor verification, so the verifier advances the shuffle bag explicitly. Long companion clips must remain Streaming with preload disabled. Dialogue text changes require a matching regenerated clip and `dialogue_manifest.json` rebuild or integrity intentionally fails.
Status: the title screen opens on `Misty Forest`, while gameplay opens on a companion piece and then shares the full nine-track bag. The homecoming's first VO is gated behind the new-game loading card's visible 100% completion. Four regional day/night profiles, dynamic rain/wind/thunder, 13 gameplay cues, procedural paper/pencil/stamp journal feedback, 410/410 spoken dialogue lines, forced captions for every voiced story moment, and an independent Voice setting remain intact.

Batch-29 established the first music/VO test pipeline. Batch-76 moved Wren and her inner narrator to a reference-conditioned Chatterbox pipeline with staged, validated replacement; batch-79 applied the approved pitch-preserving `1.05×` pacing. Batch-85 extended that same Wren identity across the dialogue graph and cast every other NPC. Batch-95 condenses Bram's first meeting from 20 advances to five substantial turns, leaving 101 Wren dialogue reads and 152 supporting-cast reads while preserving complete coverage.

The full supporting cast is intentionally differentiated: Bram `bm_lewis`@0.86; Marra `bf_emma`@0.93; Almy `bf_isabella`@0.84; Edda `bf_lily`@1.04; Hollin `bf_alice`@0.94; Joren `bm_george`@0.88; Voss `bm_daniel`@0.93; Theo `bm_fable`@1.00; Calden `bm_lewis`@0.92; Aldric/letter `bm_daniel`@0.84; Pell `bm_george`@0.97. These are test/production-direction performances, not a claim that final human casting is complete.

The project audio mix is explicitly locked to **48 kHz** in `ProjectSettings/AudioManager.asset`. Leaving System Sample Rate at platform default allowed the editor to initialize at 24 kHz on the test device while the music bed was 48 kHz, producing a shared 2× playback-rate failure. Source VO remains 24 kHz mono; Unity resamples it into the 48 kHz mix while preserving duration and pitch. **UI SFX (`Scripts/UI/UISfx.cs`, batch-56)** remains a fully procedural cue set routed to SFX.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## What exists

| Piece | Where | Notes |
|---|---|---|
| Adaptive music | Gameplay `_Music` / menu `_MenuAudio` → `MusicManager` | Nine unique 48 kHz stereo compositions route through Music. A private `System.Random` shuffle bag exhausts every clip before refill and rejects immediate repeats; the last cue from the prior scene is deferred to the final available slot in the new scene's bag. The title theme leads the menu but is excluded from gameplay's first selection, including a direct gameplay-scene launch. Sources are non-looping; menu gaps randomize from 20–50 seconds and gameplay gaps from 45–120 seconds. Gameplay retains two runtime banks and all regional/day-night volume and low-pass behavior. Optional dedicated regional clips can still override the selected playlist cue. |
| Companion score library | `Audio/Music/Companion Score`; `Tools/Audio/generate_companion_score.py` | Eight deterministic original modal-fantasy pieces join the unchanged licensed `Misty Forest.wav`. High-quality MP3 source assets are 48 kHz stereo, 154–196 seconds, about -16.6 to -16.8 LUFS, and imported as Streaming with preload disabled. `companion_score_manifest.json` records titles, seeds, musical parameters, authorship, and filenames. |
| Regional ambience | `_WorldFeedback` / menu `_MenuAudio` → `AmbienceManager` | Four lazily synthesized/cached 12-second mono profiles, each with distinct day and night loops at 48 kHz. Two day/night banks use the lighting cycle; two region banks equal-power crossfade over 4 seconds. Fixed seeds, seamless 0.5-second head/tail fold, safety limiter, no imported ambience files. The menu's Old Wood profile is deliberately capped at 0.16 so its wind and birds sit behind the score; gameplay defaults to village until a trigger fires. |
| Dynamic weather | `TimeManager` → `WeatherAudio` | Cached procedural rain/wind loops plus distance-delayed thunder, all 48 kHz mono. Three bounded sources route to `AmbienceManager.Output`; current precipitation/wind, user volume, and shelter exposure control level, while shelter also applies rain low-pass occlusion. |
| Region routing | `RegionTrigger` → `LocationRegistry.RegionChanged` | One event fans out to ambience, adaptive score, map region label, and `RegionArrivalToast`. Null gaps retain the previous audio state; trigger disable unregisters itself. |
| Dialogue VO | `DialogueLine.voiceClip` | All 410 lines are required. DialogueScreen plays on line show via a priority-0 AudioSource configured by `VoiceAudio`; advancing stops the prior read, closing stops speech, and typewriter skip intentionally does not. |
| Narration VO | `NarrationOverlay.Show(captions, clips, onDone)` | Index-matched clips; caption hold = `max(autoAdvance, clip.length + 0.8s)`; advance/skip cuts audio. Any voiced `StoryMomentData` forces its caption visible, even when the authored painting was previously image-only. The seven Hidden Journal reads are now synchronized one-to-one with the opening book, the three canonical species plates, the identification warning, and Dad's letter/final reaction. On new-game entry, `StoryBeats` waits on `UIManager.IsCinematicIntroHeld`, so the first homecoming clip is not assigned or played until scene activation, visible loading completion, and the arrival beat have all finished. |
| Voice level | `VoiceAudio` + Settings Audio tab | Pref `audio.voice`; all dialogue, narration, and intro-guide sources route under Master and receive the independent source trim. SFX can be muted without muting speech. |
| Gameplay feedback | `GameplaySfx` | Thirteen cached 48 kHz mono procedural cues: alternating knife strokes, forage release, delivery, coin earn/spend, item/quest result, key turn, door, rest, planting, and crop maturity. One hidden DDOL source is routed through the same SFX mixer setting as UI feedback. Dialogue outcomes choose one highest-priority cue to avoid reward cacophony. |
| Journal tactile feedback | `Scripts/UI/UISfx.cs` | Cached procedural `PageTurn`, `Pencil`, and `InkStamp` cues give the identification book a dry paper fold, graphite-writing beat, and decisive field-record stamp without imported audio. They share the normal UI source/output, honor the SFX setting, and pair with restrained standard-gamepad pulses owned by `InspectScreen`. |
| Player Foley | `ThirdPersonController.FootstepSource` | The existing ten-clip footstep bank and landing clip now play through a dedicated spatial source routed to `GameplaySfx.Output`; they no longer bypass the SFX setting through `PlayClipAtPoint`. |
| Cast generator | `tools/agent/generate_vo.py --all-dialogue` | Parses Unity YAML and renders the full non-Wren cast through explicit British voice profiles. Unknown speakers are a hard error. Wren/Narrator remain protected unless `--allow-wren-scratch` is deliberate. Output uses the stable `<dialogue>/<index>_<speaker>.wav` contract. |
| Wren recast generator | `tools/agent/generate_wren_chatterbox.py` | Reference-conditioned 24 kHz mono Chatterbox; asserts the current 242-clip Wren/narrator graph, uses grounded dialogue/narration profiles, applies pitch-safe 1.05× tempo, validates before `--apply`, and supports `--missing-only` so approved reads are preserved. |
| VO importer + manifest | `DialogueVoiceoverImporter`; `tools/agent/build_voice_manifest.py` | Applies mono/background/Vorbis spoken-word settings, wires all clips by index/speaker, and refuses missing files. The manifest fingerprints speaker, text, and clip with SHA-256; both the importer and DataIntegrity reject stale dialogue audio. |

## Coverage (test scope)

Dialogue: **410/410 lines**, 145/145 DialogueData assets. Wren owns 150 dialogue reads; the supporting cast owns 260. The same manifest continues to bind exact speaker, text, clip path, and hash for every line.

Soundtrack: **9/9 unique compositions** (26:13 of music before quiet intervals). A full rotation is roughly 31 minutes on the title screen and 37 minutes in gameplay at average gap lengths; exact ordering and gaps vary per run. `MusicPlaylistVerifier` checks clip/import properties, mixer routing, both non-looping banks, full-bag uniqueness, and refill-boundary repeat protection in Play Mode.

## Design intents (don't "fix" these)

- **Skip-typewriter does NOT cut the voice** — first press completes the text while the read
  finishes (fully-voiced genre convention); the second press advances and cuts. Intentional.
- **The last line's clip keeps reading under choice pills** — the question is still being asked.

## Follow-ups (backlog)

- A dedicated **Voice mixer group** remains optional if mixer-side processing or ducking is wanted.
  The shipped Voice slider is source-trimmed under Master, so it is already independent of SFX.
- Dedicated regional override compositions remain optional. The default shuffled library already
  inherits every region's volume/filter state; assigning an override uses the existing crossfade banks.
- A true Ambience mixer group remains optional if VO ducking is wanted. Source-level Ambience trim
  currently gives the settings slider independent behavior without risky mixer-asset surgery.
- Intro captions are still duplicated in `generate_vo.py` and the localization table; dialogue itself
  is protected by the manifest, but non-dialogue narration remains a two-source authoring risk.
- If voiced narration ever fires from a quest-completion that chains a dialogue, the two
  AudioSources can overlap — have `NarrationOverlay.Show` stop DialogueScreen's voice then.
- If AI VO ships: do a final pronunciation/listening audit and complete the **Steam AI-content
  disclosure** on the store page. The full cast map and regeneration sweep are now implemented.
- `Application.runInBackground` as a real project setting (PC-game norm) instead of test-only.
