# Story and World Alignment Audit — 2026-07-19

## Outcome

Hollowfen's main story is one continuous, recoverable 26-quest chain. Every quest now has:

- the intended predecessor and successor;
- a concrete gameplay or dialogue completion owner;
- a registered compass destination;
- story copy that agrees with the destination;
- a reachable physical site in the production scene.

The route is no longer concentrated around the mill yard. Its principal sites span 235 metres
east-to-west and 367 metres south-to-north, using the village's built districts and the full
Old Wood approach.

## Narrative route

| Act | Player journey | Narrative purpose |
|---|---|---|
| I | East road → village well → Tobin mill → Old Wood forage → Crooked Pintle → Almy's western house | Wren returns, inherits Tobin's knowledge, proves it useful, and learns Almy has withheld part of the truth. |
| II | Mill grow bed → west forge → east market road → Edda's cottage → chapel district | Mushroom knowledge becomes village work; the player sees the competing pressures of care, commerce, tax, and faith. |
| III | Mill → chapel → Old Wood edge → witch's cottage → chapel/cottage/mill circuit | The ecological mystery deepens while Calden, Edda, Almy, and Hollin confront their part in Hollowfen's decline. |
| IV | Empty Crooked Pintle → village festival → mill door → eastern manor → north-west clear-cut → Wend source → manor | Restoration becomes a political choice, forcing the player to cross the whole map before choosing Hollowfen's future. |

This route alternates direction intentionally. Each long journey changes the narrative context,
rather than functioning as travel padding: domestic grief becomes practical knowledge, practical
knowledge becomes village trust, and trust becomes leverage over the land's future.

## Corrected story/world contradictions

- Bram's mill-key objective and compass now point to Bram beside the **village well**, where the
  cinematic handoff actually occurs.
- Voss's first tax scene now occupies the **east market road**. His pressure establishes the empty
  commercial pitch before Theo's dawn arrival turns the same location toward opportunity.
- Theo's actor, wagon schedule, and map marker now share the restored eastern site instead of the
  obsolete central-village anchor.
- Edda's Brightspore request correctly begins at **Edda's cottage**. She later visits the **mill**
  for her apprenticeship request, then returns home afterward.
- Calden gives his warning at the **mill**, then withdraws to the **chapel gate** for the next stage.
- Theo's private Capital offer now occurs inside the **empty Crooked Pintle**, matching its dialogue
  and his authored schedule.
- Voss appears only when the story needs him: at the east market for tax collection and at the
  **mill door** with Aldric's letter. He no longer stands in the village before his entrance.
- The Aldric letter objective now points to the mill door rather than the village well.

## Dynamic objective routing

Eleven authored route stages now update both the compass and objective copy during multi-site
quests:

- Almy's lesson moves from her doorway to the mill cultivation bed.
- Edda's grandfather chain moves through Edda → Almy → Old Wood Brightspore habitat → Marra →
  Edda delivery → next-day recovery.
- Calden's warning moves from the mill confrontation to the chapel gate.

The HUD listens for route changes, so the player is not left following the first location after a
quest advances internally.

## Character distribution

Nine principal NPCs now use authored story-aware schedules. The most important geographical roles
are:

| Character | Primary territory | Story movement |
|---|---|---|
| Bram | Village well / Crooked Pintle | Moves into the Pintle for the first-sale scene. |
| Marra | Crooked Pintle kitchen | Anchors food, sales, and tonic preparation. |
| Almy | Western house / chapel garden | Remains physically separate until her knowledge becomes active. |
| Joren | West forge | Gives Act II a strong western destination. |
| Theo | East market road | Arrives only when the trade story opens; later meets privately in the Pintle. |
| Edda | Western cottage | Comes to the mill for apprenticeship, then returns to her recovered life. |
| Hollin | Mill / restoration sites | Keeps restoration work visibly connected to Wren's home base. |
| Calden | Mill / chapel | His movement externalizes his retreat after the warning. |
| Voss | East market / mill door | Appears only for Aldric's pressure beats. |

The manor map pin is intentionally an approach point about 18 metres east of the large manor
facade, placing Wren on its door side rather than at the building's visual centre.

## Automated evidence

- `Hollowfen/Verify/Story & World Alignment`: **PASS**
  - exact 26-quest canonical order and `NextQuest` links;
  - a concrete completion owner for all 26 quests;
  - registered destinations for all 26 quests;
  - all 11 staged objective routes;
  - all nine scheduled principal characters;
  - 235 m × 367 m route spread.
- `Hollowfen/Verify/NPC Schedules`: **PASS**
  - Voss east-market-to-mill staging;
  - Calden mill-to-chapel staging;
  - Edda cottage-to-mill-to-cottage staging;
  - Theo's restored eastern anchor and Pintle override.
- `Hollowfen/Verify/Data Integrity`: **0 errors, 0 warnings**.
- Verification used an isolated temporary save. The player's active save and backup hashes were
  unchanged after the run.

## Remaining acceptance pass

Automation proves structure, ownership, staging, compass routing, and clean data references. The
remaining production acceptance step is one human-controlled fresh-save playthrough of all 26
quests. That pass should judge traversal pacing, whether each arrival feels visually legible from
the player's camera, and whether any long route needs an ambient beat—without moving the sites
back toward the mill merely to shorten travel.
