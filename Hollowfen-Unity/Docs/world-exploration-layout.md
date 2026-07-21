# Hollowfen World Exploration Layout

The production route uses the environment's two complete built districts instead of clustering
quest actors in the mill yard. The village well, Crooked Pintle, and Tobin mill stay exactly where
they were approved. Every reassigned destination points at a real existing building or dressed
site, and the NPC/quest props move with the pin.

| Story site | World role | Position (x, z) | Narrative reason |
|---|---|---:|---|
| Crooked Pintle | South social hub | 275, 88 | Arrival, Marra's kitchen, and the village's familiar hearth. |
| Village well | Central landmark | 286, 160 | Bram's key handoff and the player's first reliable orientation point. |
| Joren's Forge | West foundry | 198, 198 | The commission now crosses the lower bridge and pulls Act II out of the inn district. |
| Theo's Wagon | East market road | 325, 213 | Voss first establishes the tax pressure here; Theo's dawn arrival then turns the same road toward trade and possibility. |
| Almy's Doorway | Western solitary house | 145, 271 | Her distance from the village reflects the knowledge she has withheld. |
| Edda's Cottage | West chapel district | 197, 279 | Close enough to the chapel for aid, but no longer sharing the mill yard. |
| Chapel | North-west high point | 205, 314 | Calden's conversations and Almy's garden lesson gain a distinct sacred/cultivation site. |
| Tobin Mill | North home base | 233, 318 | Approved position; the journal, beds, and Hollin's later work remain here. |
| Clear-cut | Far north-west | 130, 372 | Ecological evidence at the end of a separate trail. |
| Old Wood edge | Northern threshold | 272, 411 | The escalation from village knowledge to Deepwood knowledge. |
| Witch's Cottage | Deep north | 262, 455 | The farthest inward journey and Act III reveal. |
| Aldric's Manor | Far east | 365, 190 | Political power is visibly outside Hollowfen's ordinary village circuit. |

## Progression rhythm

Act I moves south hub → well → north mill → south kitchen → western doorway. Act II then opens a
three-spoke circuit: west forge, east wagon, and north-west chapel/cottage. Act III extends beyond
the settled districts into the Old Wood. Act IV crosses the full map between the north-west
clear-cut, eastern manor, and northern source. The route deliberately alternates direction so a
quest chain cannot be completed by standing in one courtyard.

Quest-critical character staging follows the same geography. Voss appears at the east market for
the Wenmar tax and later walks to Wren's mill door with Aldric's letter. Calden delivers his warning
at the mill before withdrawing to the locked chapel garden. Edda begins her bedside request at her
western cottage and later comes to the mill to ask for apprenticeship. Theo's live actor now shares
the relocated wagon anchor instead of the obsolete central-village pitch.

The layout is reapplied idempotently with `Hollowfen/World/Apply Exploration Layout`; story staging
and staged compass objectives are reapplied with `Hollowfen/Story/Apply Story-World Alignment`.
Verify with `Hollowfen/Verify/Exploration Layout`, `Hollowfen/Verify/NPC Schedules`, and
`Hollowfen/Verify/Story & World Alignment`.
