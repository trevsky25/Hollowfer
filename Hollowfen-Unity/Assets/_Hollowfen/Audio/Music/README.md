# Hollowfen music library

`Misty Forest.wav` remains the reference/title composition and is licensed with the imported Magic Pig environment pack.

`Companion Score/` contains eight original, deterministic instrumental compositions written for Hollowfen's quiet medieval-fantasy palette. The high-quality MP3 masters are 48 kHz stereo; Unity imports them as Streaming with preload disabled. Source parameters and authorship metadata live in `companion_score_manifest.json`; regenerate them from the Unity project root with:

```bash
/Users/TrevorKist/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 \
  Tools/Audio/generate_companion_score.py --force
```

The runtime uses these as a shuffle bag: every composition plays once before the bag refills, the same track cannot play twice in a row, and a randomized quiet interval separates cues.
