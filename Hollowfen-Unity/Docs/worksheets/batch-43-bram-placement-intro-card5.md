# Batch 43 — Bram at the well, intro beat 5 introduces Bram, VO; texture blocked

**Date:** 2026-07-13 · **Status:** DONE (code/scene verified; final visual + texture are Trevor's) · tag `batch-43` (pending)
**Directive:** Trevor — (1) Bram now shows but is untextured. (2) 4th intro card should end introducing
Bram as the first character ("…the only one who stood there was an old friend, Bram the innkeeper of the
Crooked…"). (3) Move Bram out from the wall onto the gray placeholder pill next to the well — that exact spot.

## 1. Bram texture — BLOCKED on Trevor's asset (diagnosed)
His material `Material.001` (URP/Lit, embedded InPrefab in `Bram-TPose.fbx`) has `_BaseMap` →
**`Assets/Characters/Wren/Image_0.jpg`** — Wren's 2048² albedo mapped onto Bram's UVs = the dark "statue"
look. The Bram FBX has **0 embedded textures** and there is **no Bram texture anywhere in the project**
(only the FBX + controller under `Assets/Characters/Bram/`). So his Meshy albedo/normal/metallic were never
imported. Cannot fix without the asset — Trevor to drop Bram's texture set into `Assets/Characters/Bram/`,
then wire `_BaseMap` (+ normal/metallic) on the material (extract materials from the FBX first, since it's
InPrefab). Parked in QUESTIONS.

## 2. Intro beat 5 introduces Bram
`StoryBeats.IntroCaptions[5]` + `generate_vo.py INTRO_CAPTIONS[5]`:
old "…No cart rattled down from the Slatemoor road." →
"The village did not greet her. No children ran the lane, no cart on the Slatemoor road. **The only one who
stood there was an old friend — Bram, the innkeeper of The Crooked Pintle.**" Beat 5 maps to image 3
(`intro-04-square.png` — the square with Bram at the inn), so text + painting introduce him together.
VO regenerated: `HomecomingIntro/05_Narrator.wav` (12.4s) via new `generate_vo.py --intro-only 5`
(regenerates just the named intro captions — no dialogue/extras churn).

## 3. Bram moved onto the pill next to the well
The gray pill Trevor pointed at is **NPC_Voss's placeholder** capsule (geometry: SW of Bram, foreground-left
= the screenshot). Moved `NPC_Bram` → **(283.8, 37.03, 157.0)** (terrain height; the first raycast hit
Voss's own capsule collider at 38.7 and floated him — corrected via `Terrain.SampleHeight`), 4.1m from the
Village Well, facing the player's south approach. Parked `NPC_Voss` at Bram's old wall spot (287,37,164.5)
so the two don't overlap. Scene saved.
- FLAG: Voss (and NPC_Pell, still a pill at 288,161) standing in the opening square contradicts the bible's
  "the village did not greet her" emptiness — ask Trevor whether Pell/Voss placeholders should be cleared
  from the homecoming square.

## Verification
- [x] Compiles clean (no StoryBeats errors); `HomecomingIntro/05_Narrator.wav` reimported at 12.4s (new
      Bram-introducing line); beatImage map unchanged ({0,0,1,1,2,3}, 6 captions).
- [x] Bram at (283.8, 37.03, 157.0) on terrain ground, 4.1m from the well, facing the approach; Voss parked
      at (287,37.03,164.5); scene saved. Renders (batch-42 runtime bounds healthy) — texture wrong pending
      Trevor's asset (Q12). Trevor doing the final visual pass.
