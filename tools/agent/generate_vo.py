#!/usr/bin/env python3
"""Hollowfen VO generator — Unity dialogue .asset → Kokoro TTS WAVs (batch-29).

Parses `_lines` (speaker/text) out of Unity dialogue YAML, synthesizes each line with
Kokoro-82M (local, Apache-2.0), and writes 24kHz mono WAVs to
    Hollowfen-Unity/Assets/_Hollowfen/Audio/VO/<AssetName>/<idx>_<Speaker>.wav
Index-matched to the dialogue's line order so the editor-side wiring is deterministic.

Voice cast lives in VOICES below. ``--all-dialogue`` renders every non-Wren line
in the complete dialogue graph; Wren and narration remain owned by the staged,
reference-conditioned Chatterbox pipeline.
Run via the scratchpad venv (Python 3.12 — kokoro's deps don't build on 3.14):
    <scratchpad>/kokoro-env/bin/python tools/agent/generate_vo.py [DialogueAsset ...]
With no args, generates the batch-29 entrance-scene set (intro narration + Bram chain).

NOTE: test-quality AI VO. The shipping decision (incl. Steam AI-content disclosure)
is Trevor's — QUESTIONS.md Q10.

Batch-76 protection: Wren/Narrator are now owned by
``generate_wren_chatterbox.py``. This Kokoro tool skips those speakers unless
``--allow-wren-scratch`` is passed explicitly, so regenerating Bram cannot
silently roll the natural-voice recast back.
"""
import os
import json
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIALOGUE_DIR = os.path.join(REPO, "Hollowfen-Unity/Assets/_Hollowfen/Data/Dialogue")
OUT_ROOT = os.path.join(REPO, "Hollowfen-Unity/Assets/_Hollowfen/Audio/VO")

# Voice cast (Kokoro voice ids; prefix picks the pipeline lang: a=American, b=British).
# Wren/Narrator entries are fallback scratch casting only. The active assets are
# reference-conditioned Chatterbox renders; see generate_wren_chatterbox.py.
VOICES = {
    "Wren":            ("bf_emma", 0.90),
    "Narrator":        ("bf_emma", 0.82),
    "Bram":            ("bm_lewis", 0.86),   # old innkeeper; measured, warm
    "Marra":           ("bf_emma", 0.93),    # mid-fifties; practical, quick-edged
    "Almy":            ("bf_isabella", 0.84), # elder; low and deliberate
    "Edda":            ("bf_lily", 1.04),     # fourteen; direct, alert
    "Hollin":          ("bf_alice", 0.94),    # late twenties; quiet, grounded
    "Joren":           ("bm_george", 0.88),   # smith; broad and unhurried
    "Voss":            ("bm_daniel", 0.93),   # official; clipped and controlled
    "Theo":            ("bm_fable", 1.00),    # trader; nimble, amused
    "Calden":          ("bm_lewis", 0.92),    # priest; sober, restrained
    "Aldric":          ("bm_daniel", 0.84),   # lord; polished and assured
    "Aldric's letter": ("bm_daniel", 0.84),   # read in Aldric's own cast voice
    "Pell":            ("bm_george", 0.97),   # dry village chronicler
}

# The once-per-save homecoming intro (copy verbatim from StoryBeats.IntroCaptions —
# batch-36 restored the bible's fuller 6-beat Scene-1 passage).
INTRO_CAPTIONS = [
    "It had been three years since Wren Tobin walked the east road into Hollowfen.",
    "At first, the valley looked as it always had from the ridge: the low roofs tucked into the hollow, the dark shoulder of the Old Wood behind them, the pale line of the Wend cutting through the fields.",
    "Then the road dipped, and the old picture came apart.",
    "The river was wrong.",
    "Smoke rose from fewer chimneys than Wren remembered. Two cottages near the well had boards nailed over their windows.",
    "The village did not greet her. No children ran the lane, no cart on the Slatemoor road. The only one who stood there was an old friend — Bram, the innkeeper of The Crooked Pintle.",
]

# The hidden-journal reveal (Scene 4), Wren's inner narration voice reading her father's journal
# and note (batch-62). Index-matched to the captions split from Localization act1.hidden_journal.tobin_note.
# TTS-clean text (em-dash signature dropped, intra-caption newline flattened); the on-screen captions
# keep their flourishes. The Narrator role is Wren's British inner voice, since the passage is
# framed as Wren reading; Chatterbox owns the active renders. A distinct Tobin voice is a parked
# casting call (QUESTIONS Q10).
JOURNAL_CAPTIONS = [
    "The first pages were recipes, in her mother's hand.",
    "Then Tobin's writing began. Field Cap. Wood Ear. Pinecrest.",
    "Each a careful sketch, and under one a line pressed so hard the pencil tore the paper: never eat what you cannot name twice.",
    "If you're reading this, I never told you.",
    "The forest was always our family's secret. Your grandmother knew. Your mother knew. I was waiting until you were old enough.",
    "I am sorry I waited too long.",
    "Da.",
]

# Non-dialogue utterances (copy duplicated from Localization.cs — the parked staleness-manifest
# follow-up in Docs/systems/audio.md covers keeping these honest).
EXTRAS = {
    "IntroGuide": [
        ("Narrator", "The square is just down the road, the old well standing at the heart of it. I'll find Old Bram there — he keeps The Crooked Pintle, and he holds my father's mill key."),
    ],
    "MarraKitchen": [
        ("Narrator", "An hour later, the inn smelled like Wren's mother. Not exactly. Nothing was exact after enough years."),
    ],
}

DEFAULT_SET = [
    "Dialogue_Act1_Homecoming_Bram",
    "Dialogue_Act1_CrookedPintle_BramKey",
    "Dialogue_Act1_Bram_Repeat",
]


def dialogue_asset_names():
    """Return every authored DialogueData asset name in a deterministic order."""
    return sorted(
        os.path.splitext(name)[0]
        for name in os.listdir(DIALOGUE_DIR)
        if name.startswith("Dialogue_") and name.endswith(".asset")
    )


def sanitize_speaker(speaker):
    """Make a stable filename token shared with DialogueVoiceoverImporter."""
    token = re.sub(r"[^A-Za-z0-9]+", "_", speaker.strip()).strip("_")
    if not token:
        raise ValueError(f"speaker has no filename-safe characters: {speaker!r}")
    return token


def parse_lines(asset_path):
    """Extract ordered (speaker, text) from a Unity dialogue .asset's _lines block.

    Returns EVERY line in Unity's order (empty text included) — WAV indices must match
    the asset's _lines indices exactly, or wiring by filename plays wrong-line audio.
    """
    with open(asset_path, encoding="utf-8") as f:
        raw = f.readlines()
    lines, in_block, cur = [], False, None
    appended_cur = True
    i = 0
    while i < len(raw):
        line = raw[i]
        if line.startswith("  _lines:"):
            in_block = True
            i += 1
            continue
        if in_block:
            if re.match(r"^  _\w", line):        # next serialized field — block over
                break
            m = re.match(r"^  - speaker: (.*)$", line)
            if m:
                if not appended_cur:
                    lines.append(cur)
                cur = {"speaker": unquote(m.group(1).strip()), "text": ""}
                appended_cur = False
            else:
                m = re.match(r"^    text: (.*)$", line.rstrip("\n"))
                if m and cur is not None:
                    text = m.group(1)
                    joiner = " "
                    # Scalars fold across deeper-indented continuation lines. Unity also
                    # serializes the project's same-speaker "\n\n" merge as a visual blank
                    # line before more indented content. Preserve that paragraph boundary
                    # while folding through continuation lines, or every multi-paragraph
                    # line silently truncates at its first paragraph.
                    while i + 1 < len(raw):
                        nxt = raw[i + 1]
                        if re.match(r"^      \S", nxt):
                            text += joiner + nxt.strip()
                            joiner = " "
                            i += 1
                        elif nxt.strip() == "":
                            j = i + 1
                            while j < len(raw) and raw[j].strip() == "":
                                j += 1
                            if j < len(raw) and re.match(r"^      \S", raw[j]):
                                # Unity preserves each serialized blank line inside a
                                # quoted scalar as one authored newline.
                                text += "\n" * (j - (i + 1))
                                joiner = ""
                                i = j - 1
                            else:
                                break
                        else:
                            break
                    cur["text"] = unquote(text)
        i += 1
    if not appended_cur:
        lines.append(cur)
    return [(l["speaker"], l["text"]) for l in lines]


def unquote(s):
    s = s.strip()
    if len(s) >= 2 and s[0] == "'" and s[-1] == "'":
        return s[1:-1].replace("''", "'")
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        # Unity emits YAML double-quoted scalars with JSON-compatible escapes
        # (\n paragraph breaks, \u2014 punctuation, escaped quotes). Decode the
        # complete scalar or the speech model will literally read formatting tokens.
        return json.loads(s)
    return s


def main():
    from kokoro import KPipeline
    import numpy as np
    import soundfile as sf

    # The espeakng-loader wheel (0.2.4) hard-exits on macOS: its dylib initializes with a
    # baked CI build path before phonemizer's data-path override lands. Re-point the wrapper
    # at Homebrew's espeak-ng (valid compiled-in paths) BEFORE any pipeline constructs the
    # espeak fallback. `brew install espeak-ng` is the one system prerequisite.
    brew_lib = "/opt/homebrew/lib/libespeak-ng.dylib"
    brew_data = "/opt/homebrew/share/espeak-ng-data"
    if os.path.exists(brew_lib):
        from phonemizer.backend.espeak.wrapper import EspeakWrapper
        EspeakWrapper.set_library(brew_lib)
        EspeakWrapper.set_data_path(brew_data)

    args = sys.argv[1:]
    allow_wren_scratch = "--allow-wren-scratch" in args
    if allow_wren_scratch:
        args.remove("--allow-wren-scratch")
    if "--all-dialogue" in args:
        args.remove("--all-dialogue")
        if args:
            raise SystemExit("--all-dialogue cannot be combined with explicit dialogue asset names")
        args = dialogue_asset_names()

    pipelines = {}

    def synth(text, voice, speed):
        lang = voice[0]  # 'a' / 'b'
        if lang not in pipelines:
            pipelines[lang] = KPipeline(lang_code=lang, repo_id="hexgrad/Kokoro-82M")
        chunks = [audio for _, _, audio in pipelines[lang](text, voice=voice, speed=speed)]
        return np.concatenate(chunks) if len(chunks) > 1 else chunks[0]

    def emit(out_dir, idx, speaker, text):
        if speaker in ("Wren", "Narrator") and not allow_wren_scratch:
            print(
                f"  SKIP {idx:02d}_{speaker}.wav — active Wren assets are owned by "
                "generate_wren_chatterbox.py (pass --allow-wren-scratch to override intentionally)"
            )
            return
        if speaker not in VOICES:
            raise RuntimeError(
                f"No cast voice configured for {speaker!r}. Add it to VOICES before rendering."
            )
        voice, speed = VOICES[speaker]
        wav = synth(text, voice, speed)
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, f"{idx:02d}_{sanitize_speaker(speaker)}.wav")
        sf.write(path, wav, 24000)
        print(f"  {os.path.relpath(path, REPO)}  ({len(wav)/24000:.1f}s)  «{text[:60]}»")

    # `--extras-only` regenerates just the non-dialogue utterances (IntroGuide etc.) without
    # touching the intro narration or the dialogue sets — used when a card's copy changes.
    if args and args[0] == "--extras-only":
        for group, utterances in EXTRAS.items():
            print(f"{group}:")
            for i, (speaker, text) in enumerate(utterances):
                emit(os.path.join(OUT_ROOT, group), i, speaker, text)
        return

    # Target one or more extra groups without regenerating unrelated WAVs.
    if args and args[0] == "--extra-only":
        groups = args[1:]
        if not groups:
            raise SystemExit("--extra-only requires at least one EXTRAS group name")
        unknown = [group for group in groups if group not in EXTRAS]
        if unknown:
            raise SystemExit("unknown EXTRAS group(s): " + ", ".join(unknown))
        for group in groups:
            print(f"{group}:")
            for i, (speaker, text) in enumerate(EXTRAS[group]):
                emit(os.path.join(OUT_ROOT, group), i, speaker, text)
        return

    # Recast one speaker without overwriting the other performances in a dialogue set.
    # With no explicit assets, this targets the established voiced entrance-scene set.
    if args and args[0] == "--speaker-only":
        if len(args) < 2:
            raise SystemExit("--speaker-only requires a speaker name")
        speaker_name = args[1]
        targets = args[2:] or DEFAULT_SET
        for name in targets:
            asset = os.path.join(DIALOGUE_DIR, name + ".asset")
            if not os.path.isfile(asset):
                print(f"SKIP (not found): {asset}")
                continue
            pairs = parse_lines(asset)
            print(f"{name}: {speaker_name} only")
            for i, (speaker, text) in enumerate(pairs):
                if speaker == speaker_name and text:
                    emit(os.path.join(OUT_ROOT, name), i, speaker, text)
        return

    # `--journal-only [i ...]` regenerates the hidden-journal reveal narration (batch-62); with indices,
    # only those captions. Writes VO/HiddenJournal/<idx>_Narrator.wav.
    if args and args[0] == "--journal-only":
        want = {int(a) for a in args[1:]} if len(args) > 1 else set(range(len(JOURNAL_CAPTIONS)))
        print("HiddenJournal:")
        for i, caption in enumerate(JOURNAL_CAPTIONS):
            if i in want:
                emit(os.path.join(OUT_ROOT, "HiddenJournal"), i, "Narrator", caption)
        return

    # `--intro-only [i ...]` regenerates the homecoming intro narration; with indices, only those
    # captions (e.g. `--intro-only 5` after rewording the last beat) — no dialogue/extras churn.
    if args and args[0] == "--intro-only":
        want = {int(a) for a in args[1:]} if len(args) > 1 else set(range(len(INTRO_CAPTIONS)))
        print("HomecomingIntro:")
        for i, caption in enumerate(INTRO_CAPTIONS):
            if i in want:
                emit(os.path.join(OUT_ROOT, "HomecomingIntro"), i, "Narrator", caption)
        return

    targets = args or DEFAULT_SET

    # Intro narration + extras (only in the default set).
    if not args:
        print("HomecomingIntro:")
        for i, caption in enumerate(INTRO_CAPTIONS):
            emit(os.path.join(OUT_ROOT, "HomecomingIntro"), i, "Narrator", caption)
        for group, utterances in EXTRAS.items():
            print(f"{group}:")
            for i, (speaker, text) in enumerate(utterances):
                emit(os.path.join(OUT_ROOT, group), i, speaker, text)

    for name in targets:
        asset = os.path.join(DIALOGUE_DIR, name + ".asset")
        if not os.path.isfile(asset):
            print(f"SKIP (not found): {asset}")
            continue
        pairs = parse_lines(asset)
        print(f"{name}: {len(pairs)} lines")
        for i, (speaker, text) in enumerate(pairs):
            if not text:
                print(f"  {i:02d}: empty text — index burned, no WAV")
                continue
            emit(os.path.join(OUT_ROOT, name), i, speaker, text)


if __name__ == "__main__":
    main()
