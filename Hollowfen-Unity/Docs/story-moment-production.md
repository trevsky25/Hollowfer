# Illustrated Story Moment Production

Hollowfen now gives every story-critical dialogue turn an illustrated, voiced transition. The
production set contains 28 `StoryMomentData` assets: the three established sequences (Tobin's
hidden journal, Marra's first basket, and Almy's garden lesson) plus 25 newly completed dialogue
sequences spanning Acts 1–4 and all four endings.

The mill-key handoff remains the interaction model around these moments: authored dialogue and an
in-world focus shot lead cleanly into the painting sequence, then control returns to the quest.
`StoryMomentDirector` and `NarrationOverlay` provide one queue, skip path, modal gameplay boundary,
caption path, voice path, and completion callback for the full set.

## Coverage

The new sequences cover Homecoming, the Crooked Pintle key handoff, Almy's doorway, Joren's forge,
Voss's first payment, Theo's first trade, Edda's grandfather, Hollin's arrival, the reopened
cottages, Calden's doubt, Hollin's inheritance, the witch cottage, Wend's truth, the chapel garden,
Edda's apprenticeship, Theo's capital offer, the first festival, Voss's sealed letter, Aldric's
offer, the Wend source, meeting Aldric, and the Free Hollow, Lordly Patronage, Capital, and Witch's
Path endings.

Every newly covered sequence has:

- three 16:9 story paintings: the existing Story Card art plus two new continuation images;
- three localized narrator beats with English fallbacks;
- three validated 24 kHz mono narration clips;
- a deterministic dialogue-transition trigger;
- quest ownership, or explicit ending-resolution ownership for the four ending branches.

This adds 50 new paintings and 75 new narration clips. The asset and trigger matrix is maintained
in `Assets/_Hollowfen/Data/StoryMoments/story_dialogue_sequences.json`; that manifest is the
authoritative list for importing and verifying the set.

Story Cards 3 (`fathers_mill`) and 5 (`first_forage`) are intentionally compact, non-dialogue
discoveries. They still unlock and read as Story Cards, but are not dialogue cinematics. Ambient
greetings, repeat conversations, waiting-state lines, shop transactions, and incidental barks also
remain ordinary dialogue so routine play is not interrupted by repeated full-screen sequences.

## Art assets and generation direction

New art lives under:

`Assets/_Hollowfen/UI/StoryMoments/Complete/<story-card-id>/02-middle.png`

`Assets/_Hollowfen/UI/StoryMoments/Complete/<story-card-id>/03-final.png`

The images were generated with OpenAI's built-in image-generation tool. The existing Story Card
painting for each sequence was supplied as the identity and visual-style reference. Each generation
used a stacked diptych so the middle and final continuation shared lighting, characters, wardrobe,
and setting; the panels were then center-cropped to 1672x940 16:9 production sprites.

Shared prompt direction: *Hollowfen in-game cinematic continuation; grounded photorealistic
dark-fantasy historical drama; natural faces, weathered fabrics, earthy palette; preserve the
reference character's identity, age, hair, face, wardrobe, and the village's material culture;
center the action for a 16:9 crop; no modern objects, fantasy armor, text, captions, logo, border,
or watermark.* The per-sequence upper- and lower-panel scene directions are preserved as
`middleVisual` and `finalVisual` in the sequence manifest.

The three earlier benchmark sequences retain their established art:

- `Assets/_Hollowfen/UI/StoryMoments/HiddenJournal/` — three painted journal-discovery images;
- `Assets/_Hollowfen/UI/StoryMoments/MarraKitchen/` — basket, washing, and communal supper;
- `Assets/_Hollowfen/UI/StoryMoments/AlmyLesson/` — observation, comparison, and clean harvest.

## Voice assets

New narration lives under:

`Assets/_Hollowfen/Audio/VO/StoryMoments/<story-card-id>/00_Narrator.wav`

`Assets/_Hollowfen/Audio/VO/StoryMoments/<story-card-id>/01_Narrator.wav`

`Assets/_Hollowfen/Audio/VO/StoryMoments/<story-card-id>/02_Narrator.wav`

The clips were rendered through the project's reference-conditioned Chatterbox staging pipeline,
using the project-owned Hidden Journal narrator performance as the conditioning reference. All 75
renders passed duration, channel, sample-rate, finite-sample, peak, and RMS validation before atomic
application. `tools/agent/generate_vo.py` derives their narration text from the Story Card beats and
sequence manifest, keeping prose and audio generation aligned.

## Rebuilding and verification

In Unity, run `Hollowfen > Story > Build Complete Dialogue Story Moments` to import the sequence
manifest and wire its assets. Run `Hollowfen > Verify > Complete Dialogue Story Moments` to confirm
the 25 manifest entries, the complete 28-moment cinematic set, all image/caption/voice triplets,
dialogue transitions, quest owners, and ending-resolution owners.

The general data-integrity pass also enforces that a dialogue story moment has exactly one valid
owner: a quest, or a matching ending resolution. Runtime smoke coverage verifies that story moments
queue, present, skip, restore control, and complete without console errors.
