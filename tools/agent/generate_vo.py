#!/usr/bin/env python3
"""Hollowfen VO generator — Unity dialogue .asset → Kokoro TTS WAVs (batch-29).

Parses `_lines` (speaker/text) out of Unity dialogue YAML, synthesizes each line with
Kokoro-82M (local, Apache-2.0), and writes 24kHz mono WAVs to
    Hollowfen-Unity/Assets/_Hollowfen/Audio/VO/<AssetName>/<idx>_<Speaker>.wav
Index-matched to the dialogue's line order so the editor-side wiring is deterministic.

Voice cast lives in VOICES below — extend it as more of the cast gets test VO.
Run via the scratchpad venv (Python 3.12 — kokoro's deps don't build on 3.14):
    <scratchpad>/kokoro-env/bin/python tools/agent/generate_vo.py [DialogueAsset ...]
With no args, generates the batch-29 entrance-scene set (intro narration + Bram chain).

NOTE: test-quality AI VO. The shipping decision (incl. Steam AI-content disclosure)
is Trevor's — QUESTIONS.md Q10.
"""
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DIALOGUE_DIR = os.path.join(REPO, "Hollowfen-Unity/Assets/_Hollowfen/Data/Dialogue")
OUT_ROOT = os.path.join(REPO, "Hollowfen-Unity/Assets/_Hollowfen/Audio/VO")

# Voice cast (Kokoro voice ids; prefix picks the pipeline lang: a=American, b=British).
VOICES = {
    "Wren":     ("af_heart", 1.0),
    "Bram":     ("bm_george", 0.95),   # older British male — village register
    "Narrator": ("af_heart", 0.88),    # Wren's voice, slower — the journal read
}

# The once-per-save homecoming intro (copy verbatim from StoryBeats.IntroCaptions).
INTRO_CAPTIONS = [
    "It had been three years since Wren Tobin walked the east road into Hollowfen.",
    "The village did not greet her.",
]

DEFAULT_SET = [
    "Dialogue_Act1_Homecoming_Bram",
    "Dialogue_Act1_CrookedPintle_BramKey",
    "Dialogue_Act1_Bram_Repeat",
]


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
                    # Scalars fold across deeper-indented continuation lines. Unity also
                    # serializes the project's same-speaker "\n\n" merge as a run of TWO
                    # blank lines before more indented content (YAML: empty line = literal
                    # \n) — fold through any blank run whose next non-blank line is still
                    # continuation, or every multi-paragraph line silently truncates at its
                    # first paragraph (batch-29 fable catch: key-handoff line shipped cut).
                    while i + 1 < len(raw):
                        nxt = raw[i + 1]
                        if re.match(r"^      \S", nxt):
                            text += " " + nxt.strip()
                            i += 1
                        elif nxt.strip() == "":
                            j = i + 1
                            while j < len(raw) and raw[j].strip() == "":
                                j += 1
                            if j < len(raw) and re.match(r"^      \S", raw[j]):
                                i = j - 1   # next iteration folds the content line; sentence punctuation carries the TTS pause
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
        return s[1:-1].replace('\\"', '"')
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

    pipelines = {}

    def synth(text, voice, speed):
        lang = voice[0]  # 'a' / 'b'
        if lang not in pipelines:
            pipelines[lang] = KPipeline(lang_code=lang, repo_id="hexgrad/Kokoro-82M")
        chunks = [audio for _, _, audio in pipelines[lang](text, voice=voice, speed=speed)]
        return np.concatenate(chunks) if len(chunks) > 1 else chunks[0]

    def emit(out_dir, idx, speaker, text):
        voice, speed = VOICES.get(speaker, VOICES["Wren"])
        wav = synth(text, voice, speed)
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, f"{idx:02d}_{speaker}.wav")
        sf.write(path, wav, 24000)
        print(f"  {os.path.relpath(path, REPO)}  ({len(wav)/24000:.1f}s)  «{text[:60]}»")

    targets = sys.argv[1:] or DEFAULT_SET

    # Intro narration (only in the default set).
    if not sys.argv[1:]:
        print("HomecomingIntro:")
        for i, caption in enumerate(INTRO_CAPTIONS):
            emit(os.path.join(OUT_ROOT, "HomecomingIntro"), i, "Narrator", caption)

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
