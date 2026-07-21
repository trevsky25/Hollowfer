# Mushroom Learning and Ecology

## Player loop

Mushrooms are no longer pickups that teach themselves. Before Wren finds Tobin's field journal,
an unknown specimen may be rotated and observed but not named or cut. Finding the journal opens
the reference pages for the current story tier. It does not reveal the live specimen's identity:
its prompt, inspect title, details, and 3D preview remain unknown/silhouetted until Wren verifies it.
In the field, **Compare with Journal** starts a three-part identification test:

1. Browse three plausible illustrated spreads and match the live silhouette to the correct page.
2. Confirm it with a recorded physical feature rather than cap colour alone.
3. Apply the safety rule: check every feature and the look-alikes before cutting.

A correct test records the species and awards the existing discovery Knowledge point through
`ScoreHooks`. It does not harvest anything. Identification resolves as a staged field-record
ceremony: Wren circles three observations in ink, signs the page in cursive, the live silhouette
becomes the full-colour 3D specimen, and a rarity seal, collection count, stamp, haptic pulse, and
checkmark confirm the permanent journal change. **Prepare to Cut** stays disabled until the sequence
finishes, then still enforces the species' story-tier flag and launches the two-handed knife
challenge. A wrong answer is consequence-free and quotes a concise feature from the currently
selected candidate so the player knows what to compare without being told the answer; the game
never encourages tasting an unknown mushroom.

Journal knowledge and field permission are separate even when a story scene has already named a
species. Field Mushroom, Wood Ear, Pinecrest, Goldfoot, and all 17 remaining species must each pass
the live test once before any wild or cultivated node can enter the cutting sequence. The persistent
`mushroom_identified_<speciesId>` flag is the only field-verification proof; harvesting cannot set it.
Existing saves retain tests they already passed.

The comparison screen uses high-contrast focus rather than three similar gold buttons: resting
answers are dark, the active answer gains a gold fill, leading focus marker, strong outline, and dark label,
and wrong choices retain a red treatment. All guidance wraps inside a fixed feedback card. Passing
the final safety question completes the authored field-record ceremony before the player can prepare
the cut.

The match step never pins the answer page or prints the target species as an answer choice. It builds
a deterministic book from the target and two readable look-alike references, opens on a non-target
spread when available, and keeps the live silhouette alongside the artwork. Previous/Next turns the
book pages; **Enlarge Page** opens the current 1672×940 spread across nearly the full modal, with the
same page controls and a controller-safe return path. A wrong page stays in Match. Once the correct
spread is chosen, it remains open for Feature and Safety, and the browser no longer permits switching
the evidence underneath those questions.

Page navigation now behaves like paper: a short unscaled horizontal fold carries a moving shadow,
swaps artwork only at the midpoint, unfolds, and restores controls. A procedural rustle and restrained
gamepad pulse accompany the motion. Selecting the live specimen opens an optional observation lens
with a large silhouette, right-stick/mouse rotation, trigger/wheel zoom, mouse middle-drag pan, and
reset/return controls. The lens never names the specimen and is not required to pass.

The Field Guide now distinguishes reference access from field discovery. Father's journal makes
the current tier readable for study, while the counter continues to report how many of the 21
species Wren has personally identified. Higher-tier pages appear only when their data-authored
foraging flag opens. The first successful test also records the in-game day and nearest authored
location (or current region). That context appears as Wren's handwritten slip during the reveal and
remains over the illustrated spread whenever the verified species is reopened. Older verified saves
receive an undated “recorded before this journal entry” note rather than losing access.

## Illustrated field journal

Every one of the 21 species now owns a production-sized 1672x940 hand-drawn book spread under
`Assets/_Hollowfen/UI/MushroomJournal/Pages/<species-id>.png`. Each spread uses the same physical
book, paper, sepia-ink hand, restrained watercolor, anatomical-study layout, habitat vignette, and
ruled safety block established by the approved Fly Agaric prototype. The page is the primary detail
view; **Specimen study** toggles back to the existing rotatable 3D model and localized data sheet.

The page art is bound through `MushroomFieldGuideData.JournalPage`. Run
`Hollowfen > Mushrooms > Build Illustrated Journal Pages` after adding or replacing pages, and
`Hollowfen > Verify > Illustrated Mushroom Journal` to enforce 21 stable bindings, unique ids, and
the production resolution floor.

The journal has four explicit knowledge states:

| State | Player-facing result |
|---|---|
| Locked | The journal has not been found, or the story tier has not exposed the reference. |
| Reference available | The illustrated page can appear among readable references, but the live specimen remains unnamed. |
| Studied | Browsing the available spreads enables field comparison without revealing which one matches. |
| Field verified | A correct three-step comparison records discovery and permits harvesting when its story flag also allows it. |

Inspecting an unknown but readable species offers **Study Journal Page** first. That study opens the
candidate book rather than the target page, so the correct species remains a deduction. The field
comparison cannot start until the book has been studied; naming still does not harvest. This makes
the path explicit: unlock references → browse drawings → compare the live silhouette → verify
identity → reveal the normal details sheet → prepare to cut.

The in-game footer states that the journal is not a real-world identification guide and that wild
mushrooms must not be eaten without expert identification. This is intentional: public-health
guidance stresses whole-specimen and substrate checks, warns that look-alikes can deceive, and
discourages inexperienced foragers from eating wild mushrooms. See the CDC's death-cap case review
and Poison Control's wild-mushroom warning:

- <https://www.cdc.gov/mmwr/volumes/66/wr/mm6621a1.htm>
- <https://www.poison.org/articles/wild-mushroom-warning>

### Image-generation direction

The pages were generated with OpenAI's built-in image-generation tool. The approved Fly Agaric
spread was the canonical book/hand/layout reference; the existing species field image was supplied
as the anatomy/color reference when available. Oyster reused the authored Lacewig reference because
both entries are *Pleurotus ostreatus*; Aldermark was drawn from its authored *Grifola frondosa*
features.

Shared prompt direction: *scientific-educational Hollowfen two-page field-journal spread; exact
same aged stitched book and careful hand as the reference; nearly orthographic 16:9 open spread;
accurate complete specimen plus diagnostic sepia-ink and restrained-watercolor studies on the left;
large common name, italic Latin name, concise handwritten field notes, habitat sketch, and ruled
safety block on the right; botanically readable structure over decoration; no modern typography,
photoreal paste-in, magic, pseudo-text, extra species, logo, or watermark.* Species-specific prompts
were derived from each data asset's identifying features, habitat, season, look-alikes, edibility,
and narrative note. Hollowfen-local duplicates receive distinct lore hands (`SABLE'S WARNING` or
`SABLE'S SEEDBOOK`) while preserving the shared scientific anatomy.

## Whole-map ecology

`MushroomWorldSpawner` adds 41 deterministic wild nodes to the 21 authored specimens. Every node
has a stable `wild.generated.<habitat>.<index>` id and therefore uses the same per-save harvest and
respawn records as hand-placed nodes. Generation is deterministic, terrain-only, slope-limited,
collision-checked, and kept at least 3.5 metres from authored or generated specimens.

| Habitat | Population | Story/ecology role |
|---|---:|---|
| South fields | 5 | The first safe comparison pages near the arrival route. |
| Village lanes | 6 | Common species between the Pintle, well, and market roads. |
| Wend banks | 6 | Transitional riverbank species on the lower bridge circuit. |
| Chapel cottages | 6 | Act II species around Almy's teaching district. |
| Clear-cut | 5 | Disturbed-soil species and the first dangerous comparisons. |
| Old Wood edge | 7 | Deepwood escalation along the northern threshold. |
| Deep Old Wood | 6 | Final-lesson species, lethal look-alikes, and Aldermark. |

The ecology deliberately places locked species in visible habitats before the player can identify
them. They are future questions, not unusable loot: Wren can inspect them, then return after the
relevant lesson. Runtime terrain checks are verified in Play Mode; if a future environment edit
blocks a candidate, the spawner tries another deterministic point rather than clipping a mushroom
through a building.
