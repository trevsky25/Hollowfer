#!/usr/bin/env python3
"""Render Hollowfen's original ambient companion score.

The compositions are deterministic, synthesis-based, and written specifically for Hollowfen.
They intentionally share a restrained medieval-fantasy palette: slow modal harmony, bowed pads,
soft plucks, felt-piano figures, breathy lead phrases, and long natural tails.

Requires numpy and ffmpeg. Codex's bundled artifact runtime supplies numpy:
  /Users/TrevorKist/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 \
      Tools/Audio/generate_companion_score.py
"""

from __future__ import annotations

import argparse
import json
import math
import shutil
import subprocess
import tempfile
import wave
from dataclasses import asdict, dataclass
from pathlib import Path

try:
    import numpy as np
except ImportError as exc:  # pragma: no cover - authoring-time guard
    raise SystemExit("numpy is required; run with the Codex bundled Python runtime") from exc


SAMPLE_RATE = 48_000
GENERATOR_VERSION = 1
ALBUM = "Hollowfen: The Failing Village — Companion Score"


@dataclass(frozen=True)
class Track:
    number: int
    slug: str
    title: str
    duration: float
    bpm: float
    key: str
    tonic: int
    scale: tuple[int, ...]
    progression: tuple[int, ...]
    motif: tuple[int, ...]
    seed: int
    warmth: float
    motion: float
    lead: str

    @property
    def filename(self) -> str:
        return f"Hollowfen - {self.number:02d} {self.title}.mp3"


DORIAN = (0, 2, 3, 5, 7, 9, 10)
AEOLIAN = (0, 2, 3, 5, 7, 8, 10)
MAJOR = (0, 2, 4, 5, 7, 9, 11)
LYDIAN = (0, 2, 4, 6, 7, 9, 11)

TRACKS = (
    Track(1, "lanterns-in-the-fen", "Lanterns in the Fen", 174, 62, "D Dorian", 50,
          DORIAN, (0, 5, 3, 4), (0, 2, 4, 3, 1, 2, 0, -1), 1701, .76, .56, "flute"),
    Track(2, "rain-on-the-mill-roof", "Rain on the Mill Roof", 162, 68, "G Aeolian", 55,
          AEOLIAN, (0, 3, 5, 4), (4, 3, 1, 2, 0, 1, -1, 0), 1702, .66, .64, "piano"),
    Track(3, "the-old-wood-remembers", "The Old Wood Remembers", 188, 56, "A Aeolian", 45,
          AEOLIAN, (0, 5, 3, 6), (0, 1, 3, 2, 0, -2, -1, 0), 1703, .42, .38, "flute"),
    Track(4, "path-through-alder", "Path Through Alder", 154, 70, "C Lydian", 48,
          LYDIAN, (0, 1, 4, 3), (0, 2, 4, 5, 4, 2, 1, 0), 1704, .82, .72, "pluck"),
    Track(5, "hearthlight-at-dusk", "Hearthlight at Dusk", 168, 64, "F Major", 53,
          MAJOR, (0, 4, 5, 3), (2, 1, 0, 2, 4, 3, 1, 0), 1705, .94, .48, "piano"),
    Track(6, "fields-after-rain", "Fields After Rain", 180, 60, "E Dorian", 52,
          DORIAN, (0, 3, 5, 4), (0, 1, 3, 4, 3, 1, 2, 0), 1706, .72, .58, "flute"),
    Track(7, "bells-beyond-the-wend", "Bells Beyond the Wend", 158, 66, "B Aeolian", 47,
          AEOLIAN, (0, 6, 3, 5), (0, 3, 2, 4, 3, 1, 0, -1), 1707, .50, .52, "bell"),
    Track(8, "homeward-by-lantern", "Homeward by Lantern", 196, 58, "D Major", 50,
          MAJOR, (0, 5, 3, 4), (4, 3, 1, 2, 0, 1, 2, 0), 1708, .88, .44, "flute"),
)


def midi_frequency(note: float) -> float:
    return 440.0 * (2.0 ** ((note - 69.0) / 12.0))


def smooth_envelope(length: int, attack: int, release: int) -> np.ndarray:
    env = np.ones(length, dtype=np.float32)
    attack = min(max(1, attack), length)
    release = min(max(1, release), length)
    x = np.linspace(0.0, 1.0, attack, dtype=np.float32)
    env[:attack] = x * x * (3.0 - 2.0 * x)
    x = np.linspace(1.0, 0.0, release, dtype=np.float32)
    env[-release:] *= x * x * (3.0 - 2.0 * x)
    return env


def synth_note(kind: str, frequency: float, duration: float, rng: np.random.Generator) -> np.ndarray:
    count = max(8, int(duration * SAMPLE_RATE))
    t = np.arange(count, dtype=np.float32) / SAMPLE_RATE
    phase = 2.0 * np.pi * frequency * t

    if kind == "pad":
        vibrato = .012 * np.sin(2.0 * np.pi * .19 * t + rng.uniform(0, math.tau))
        body = (.68 * np.sin(phase + vibrato) + .19 * np.sin(2.0 * phase + .3) +
                .09 * np.sin(3.0 * phase + 1.1) + .04 * np.sin(.5 * phase))
        env = smooth_envelope(count, int(min(2.8, duration * .28) * SAMPLE_RATE),
                              int(min(4.0, duration * .34) * SAMPLE_RATE))
        return (body * env).astype(np.float32)

    if kind == "bass":
        body = .82 * np.sin(phase) + .18 * np.sin(2.0 * phase + .4)
        env = smooth_envelope(count, int(.45 * SAMPLE_RATE),
                              int(min(2.2, duration * .4) * SAMPLE_RATE))
        return (body * env).astype(np.float32)

    if kind == "piano":
        body = (.70 * np.sin(phase) + .20 * np.sin(2.01 * phase + .2) +
                .08 * np.sin(3.98 * phase + .7) + .03 * np.sin(7.95 * phase))
        env = (1.0 - np.exp(-t * 35.0)) * np.exp(-t * (1.15 + frequency / 1800.0))
        hammer_count = min(count, int(.045 * SAMPLE_RATE))
        if hammer_count:
            body[:hammer_count] += rng.normal(0.0, .025, hammer_count).astype(np.float32) * np.linspace(
                1.0, 0.0, hammer_count, dtype=np.float32)
        return (body * env).astype(np.float32)

    if kind == "pluck":
        body = (.72 * np.sin(phase) + .19 * np.sin(2.0 * phase + .4) +
                .08 * np.sin(3.0 * phase + .8) + .03 * np.sin(5.0 * phase))
        env = (1.0 - np.exp(-t * 90.0)) * np.exp(-t * (2.15 + frequency / 1200.0))
        return (body * env).astype(np.float32)

    if kind == "bell":
        body = (.54 * np.sin(phase) + .22 * np.sin(2.71 * phase + .4) +
                .15 * np.sin(4.08 * phase + 1.2) + .09 * np.sin(5.43 * phase + .2))
        env = (1.0 - np.exp(-t * 75.0)) * np.exp(-t * .78)
        return (body * env).astype(np.float32)

    # Breathy wooden flute: slow onset, restrained overtones, human-width vibrato.
    vibrato = .026 * np.sin(2.0 * np.pi * (4.7 + .08 * np.sin(t * .31)) * t)
    body = (.84 * np.sin(phase + vibrato) + .12 * np.sin(2.0 * phase + .25) +
            .04 * np.sin(3.0 * phase + .8))
    env = smooth_envelope(count, int(min(.7, duration * .28) * SAMPLE_RATE),
                          int(min(1.1, duration * .35) * SAMPLE_RATE))
    breath = rng.normal(0.0, .012, count).astype(np.float32)
    return ((body + breath) * env).astype(np.float32)


def add_note(mix: np.ndarray, start: float, duration: float, midi: float, kind: str,
             amplitude: float, pan: float, rng: np.random.Generator) -> None:
    start_sample = max(0, int(start * SAMPLE_RATE))
    if start_sample >= len(mix) or duration <= 0.0:
        return
    wave_data = synth_note(kind, midi_frequency(midi), duration, rng)
    end_sample = min(len(mix), start_sample + len(wave_data))
    wave_data = wave_data[:end_sample - start_sample] * amplitude
    pan = max(-1.0, min(1.0, pan))
    left = math.sqrt((1.0 - pan) * .5)
    right = math.sqrt((1.0 + pan) * .5)
    mix[start_sample:end_sample, 0] += wave_data * left
    mix[start_sample:end_sample, 1] += wave_data * right


def scale_note(track: Track, degree: int, octave: int = 0) -> int:
    scale_size = len(track.scale)
    octave_shift, index = divmod(degree, scale_size)
    return track.tonic + track.scale[index] + 12 * (octave + octave_shift)


def chord_notes(track: Track, degree: int) -> tuple[int, int, int, int]:
    return tuple(scale_note(track, degree + interval) for interval in (0, 2, 4, 6))


def section_energy(position: float) -> float:
    if position < .08:
        return .38 + position / .08 * .34
    if position < .42:
        return .72
    if position < .55:
        return .46
    if position < .84:
        return .82
    return max(.25, .82 * (1.0 - (position - .84) / .16))


def render_track(track: Track) -> np.ndarray:
    rng = np.random.default_rng(track.seed)
    sample_count = int(track.duration * SAMPLE_RATE)
    mix = np.zeros((sample_count, 2), dtype=np.float32)
    beat = 60.0 / track.bpm
    chord_beats = 8
    chord_duration = chord_beats * beat
    intro = 4.0

    # Long modal harmony and a quiet root movement form the emotional bed.
    chord_index = 0
    chord_starts: list[tuple[float, tuple[int, ...], float]] = []
    start = intro
    while start < track.duration - 8.0:
        degree = track.progression[chord_index % len(track.progression)]
        notes = chord_notes(track, degree)
        position = start / track.duration
        energy = section_energy(position)
        chord_starts.append((start, notes, energy))
        for voice, note in enumerate(notes):
            pan = (-.58, -.18, .22, .58)[voice]
            add_note(mix, start - .3, chord_duration + 1.8, note + 12, "pad",
                     (.022 + .014 * track.warmth) * energy, pan, rng)
        add_note(mix, start, chord_duration + .8, notes[0] - 12, "bass",
                 (.028 + .010 * track.warmth) * energy, -.08, rng)
        chord_index += 1
        start += chord_duration

    # Sparse harp/felt-piano figures. Density recedes in the middle breathing section.
    arp_pattern = (0, 2, 1, 3, 2, 1, 0, 2)
    for chord_start, notes, energy in chord_starts:
        density = (.36 + .42 * track.motion) * energy
        for step, voice in enumerate(arp_pattern):
            if rng.random() > density:
                continue
            when = chord_start + step * beat
            kind = "piano" if track.lead == "piano" or rng.random() < .28 else "pluck"
            register = 24 if kind == "pluck" else 12
            add_note(mix, when, beat * (2.8 if kind == "piano" else 1.7),
                     notes[voice] + register, kind, .025 * energy,
                     rng.uniform(-.48, .48), rng)

    # A memorable eight-note phrase returns with rests, octave shifts, and gentle variation.
    phrase_beats = 16
    phrase = intro + chord_duration
    phrase_number = 0
    while phrase < track.duration - 18.0:
        position = phrase / track.duration
        energy = section_energy(position)
        if energy > .4:
            direction = -1 if phrase_number % 3 == 2 else 1
            cursor = phrase
            for index, degree in enumerate(track.motif):
                duration_beats = 1.5 if index in (0, 4, 7) else 1.0
                if rng.random() < .18:
                    cursor += duration_beats * beat
                    continue
                varied = degree
                if phrase_number % 4 == 3 and index in (3, 6):
                    varied += direction
                octave = 2 + (1 if phrase_number % 5 == 4 and index >= 4 else 0)
                lead_kind = track.lead
                if lead_kind == "piano":
                    lead_kind = "piano"
                elif lead_kind == "pluck":
                    lead_kind = "pluck"
                elif lead_kind == "bell":
                    lead_kind = "bell"
                else:
                    lead_kind = "flute"
                add_note(mix, cursor, duration_beats * beat * 1.12,
                         scale_note(track, varied, octave), lead_kind,
                         (.030 + .012 * track.warmth) * energy,
                         -.18 + .36 * math.sin(phrase_number * 1.7 + index * .4), rng)
                cursor += duration_beats * beat
        phrase += phrase_beats * beat
        phrase_number += 1

    # A few high, distant bells create landmarks without turning the score into a constant melody.
    for chord_number, (chord_start, notes, energy) in enumerate(chord_starts):
        if chord_number % (2 if track.lead == "bell" else 4) != 1 or energy < .5:
            continue
        add_note(mix, chord_start + chord_duration * .72, 4.8,
                 notes[2] + 24, "bell", .016 * energy,
                 .52 if chord_number % 2 else -.52, rng)

    # Slowly changing air/room tone, deliberately below the musical foreground.
    control_rate = 8
    control_count = int(track.duration * control_rate) + 2
    for channel in range(2):
        controls = rng.normal(0.0, 1.0, control_count).astype(np.float32)
        controls = np.cumsum(controls)
        controls -= controls.mean()
        controls /= max(.001, float(np.max(np.abs(controls))))
        air = np.interp(np.arange(sample_count) / SAMPLE_RATE * control_rate,
                        np.arange(control_count), controls).astype(np.float32)
        mix[:, channel] += air * (.0018 + .0015 * (1.0 - track.warmth))

    # Cross-channel multi-tap room. Using the dry signal avoids unstable feedback and stays exact.
    dry = mix.copy()
    for delay_seconds, gain, cross in ((.19, .16, True), (.37, .115, False),
                                       (.61, .082, True), (1.03, .050, False)):
        delay = int(delay_seconds * SAMPLE_RATE)
        if cross:
            mix[delay:, 0] += dry[:-delay, 1] * gain
            mix[delay:, 1] += dry[:-delay, 0] * gain
        else:
            mix[delay:] += dry[:-delay] * gain
    del dry

    # Fade naturally and normalize conservatively before the final EBU pass in ffmpeg.
    fade_in = int(4.0 * SAMPLE_RATE)
    fade_out = int(9.0 * SAMPLE_RATE)
    mix[:fade_in] *= np.linspace(0.0, 1.0, fade_in, dtype=np.float32)[:, None]
    mix[-fade_out:] *= np.linspace(1.0, 0.0, fade_out, dtype=np.float32)[:, None]
    mix -= mix.mean(axis=0, keepdims=True)
    rms = float(np.sqrt(np.mean(mix * mix)))
    if rms > 1e-7:
        mix *= .085 / rms
    peak = float(np.max(np.abs(mix)))
    if peak > .88:
        mix *= .88 / peak
    return mix


def write_wav(path: Path, audio: np.ndarray) -> None:
    pcm = np.clip(audio * 32767.0, -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        output.writeframes(pcm.tobytes())


def encode_mp3(wav_path: Path, output_path: Path, track: Track) -> None:
    command = [
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-i", str(wav_path),
        "-af", "loudnorm=I=-17:LRA=9:TP=-1.5",
        "-ar", str(SAMPLE_RATE), "-c:a", "libmp3lame", "-b:a", "224k",
        "-id3v2_version", "3",
        "-metadata", f"title={track.title}",
        "-metadata", f"album={ALBUM}",
        "-metadata", "artist=Hollowfen Original Score",
        "-metadata", f"track={track.number}",
        "-metadata", "comment=Original deterministic composition rendered for Hollowfen",
        str(output_path),
    ]
    subprocess.run(command, check=True)


def write_manifest(output_dir: Path, tracks: list[Track]) -> None:
    payload = {
        "generatorVersion": GENERATOR_VERSION,
        "sampleRate": SAMPLE_RATE,
        "album": ALBUM,
        "authorship": "Original deterministic compositions rendered for Hollowfen",
        "tracks": [asdict(track) | {"filename": track.filename} for track in tracks],
    }
    (output_dir / "companion_score_manifest.json").write_text(
        json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path,
                        default=Path("Assets/_Hollowfen/Audio/Music/Companion Score"))
    parser.add_argument("--only", help="Render one track slug")
    parser.add_argument("--force", action="store_true", help="Replace existing MP3 files")
    args = parser.parse_args()

    if shutil.which("ffmpeg") is None:
        raise SystemExit("ffmpeg is required")
    chosen = [track for track in TRACKS if args.only in (None, track.slug)]
    if not chosen:
        raise SystemExit(f"unknown track slug: {args.only}")
    args.output.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="hollowfen-score-") as temp_dir:
        temp_root = Path(temp_dir)
        for track in chosen:
            output_path = args.output / track.filename
            if output_path.exists() and not args.force:
                print(f"skip {output_path.name}")
                continue
            print(f"render {track.number:02d} · {track.title} · {track.duration:.0f}s")
            audio = render_track(track)
            wav_path = temp_root / f"{track.slug}.wav"
            write_wav(wav_path, audio)
            del audio
            encode_mp3(wav_path, output_path, track)
            print(f"wrote {output_path}")
    write_manifest(args.output, chosen if args.only else list(TRACKS))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
