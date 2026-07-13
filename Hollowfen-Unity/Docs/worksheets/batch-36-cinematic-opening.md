# Batch 36 — Cinematic opening sequence (homecoming)

**Date:** 2026-07-12 · **Status:** DONE (play-verified, screenshots) · tag `batch-36` (pending)
**Directive:** Trevor — "the two black screens with text seems short, missing something. Use some of the
images with the characters in a cinematic way that paints a good picture as the VO is happening."

## Diagnosis
The homecoming intro was two bible lines on full black ("It had been three years…" / "The village did not
greet her."). Two problems: (1) it was **truncated** — the bible's Scene 1 opening (`story.md` L75–85) is a
much richer atmospheric passage; (2) it was **flat** — text on black, no imagery.

## What shipped
1. **Restored the fuller opening** — 6 **bible-verbatim** beats (exact sentences from `story.md` Scene 1,
   selected + split at sentence boundaries for pacing; no rewording): arrival → the valley from the ridge →
   "the old picture came apart" → "The river was wrong." → the visible decline (smoke, boarded cottages) →
   "The village did not greet her…". Addresses "short / missing something" with canon, not invention.
2. **Cinematic image backing** — `NarrationOverlay` gained a `ShowCinematic(captions, clips, hero, onDone)`
   mode that paints the captions over a **hero image** with:
   - a slow **Ken Burns journey** (the painting pushes out 1.20→1.06 and drifts from Wren toward the village
     over ~42s — a motivated "arrival reveal" as the decline is described),
   - **letterbox bars** that grow in with the fade (2.35-ish cinematic frame),
   - a **bottom scrim** (procedural vertical-gradient sprite) so lower-third captions read over the art,
   - captions repositioned to the lower third, serif italic.
   The plain `Show(...)` black mode is untouched (the Act-I-complete journal narration still uses it).
3. **Hero = `homecoming.png`** (the StoryCard plate — Wren with pack + basket on the dusk lane, the decaying
   cottages, Bram sweeping by a lit doorway, the Wend's stony bed, sunset over the Old Wood). Wired to
   `StoryBeats._introHeroImage`. It depicts nearly the whole passage in one shot, so one hero + a slow journey
   reads more cohesively than cutting between plates.
4. **VO**: the two existing narrator clips voice the first + last beats (the emotional anchors); the four
   restored middle beats play silent over the moving image, timed to their reading length
   (`SilentHold` scales the hold by word count). **Full-passage VO pends the Q10 audio-direction call.**

## Verification (play-mode, fresh save)
- Fires on a fresh save; letterbox + Ken-Burned homecoming image + lower-third caption all render
  (screenshots `b36_intro1.png` beat 1, `b36_intro2.png` beat 2 — the long descriptive line reads cleanly in
  two lines over the scrim).
- Ken Burns confirmed moving across beats (scale 1.20→1.18→…, pos drifting).
- **Chain intact**: narration → `onDone` → `IntroGuide` shows (verified SHOWING) → player control held by the
  guide (batch-30 flow preserved). `SkipAll` still fires the completion + guide.
- Compiles clean; lint 0/0; integrity 0/0. Console: only pre-existing vendored warnings.

## Review note
Copy is bible-verbatim (checked against `story.md` L75–85). System change is UI-native + additive (plain black
mode preserved). Trevor is the live reviewer on this taste-critical first-impression moment; shipped for his
reaction rather than a subagent gate. Open tuning questions for him: beat count/pacing (6 beats ≈ 40s,
per-beat skippable), and whether to generate VO for the 4 silent beats (reopens Q10).

## Docs updated
`systems/ui-framework.md` — NarrationOverlay cinematic mode (hero + Ken Burns + letterbox + scrim).
