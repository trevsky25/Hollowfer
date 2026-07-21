# Relationship memory and personal arcs

`GameScores` remains the scalar answer to “how close is Wren to this villager?”  `VillagerRelationships` is the durable history behind that score: specific shared memories, symmetric bonds between NPCs, and monotonic stages in personal favor chains.

## Save contract

`SaveSlotMeta.VillagerRelationships` stores three parallel-array tables:

- `(MemoryNpcIds, MemoryIds, MemoryDays)` for idempotent named recollections;
- `(BondNpcAIds, BondNpcBIds, BondValues)` for canonical unordered NPC pairs, clamped to -100…100;
- `(FavorIds, FavorStages)` for monotonic optional-arc progress.

Historical saves deserialize the field as null and hydrate to an empty history. `SaveFileFormat` trims mismatched arrays and bounds every numeric value. New Game, slot load, targeted autosave, and full checkpoint save all include the store.

## Dialogue contract

`NPCDialogueEntry` can require or block a memory, gate a favor-stage range, and gate Wren's relationship range. `NPCData.PickDialog` always gives an active quest-owned entry semantic priority before considering optional content, regardless of importer order.

`DialogueData` may grant memories, NPC bonds, favor stages, and optional-activity time. Outcomes remain idempotent or monotonic, so re-entry cannot duplicate a memory or regress an arc.

## Authored first release

Bram, Almy, Joren, Marra, Edda, and Pell each have:

- two restored-world personal activities (meals, walks, work, care rounds, or quiet conversations);
- a familiar greeting after both moments and sufficient relationship standing;
- a distinct one-time response to each of the four endings;
- a journal history that shows Wren's standing, favor progress, recent memories, and the strongest changed village bond.

Joren, Marra, Edda, and Pell also speak while the final choice is under consideration. Bram and Almy retain their dedicated Act IV consultation dialogues.

The scene carries seven derived NPC schedules. Post-restoration roles keep the six core villagers visiting the Pintle, forge, chapel beds, apothecary, and reopened cottages after construction crews leave.

Four paired living-village encounters now make those roles spatial rather than merely conversational. Bram and Edda keep a wet-weather ledger inside the apothecary, Joren and Pell settle what the forge means in the public record, Almy and Marra trade seed between garden and kitchen, and Bram and Marra set the Pintle's last table. Each scene is available only while both schedules physically overlap at the named restored place; completing it disperses the pair, records a memory for each participant, and changes their mutual bond.

## Verification

- `Hollowfen/Verify/Relationship Memory & Personal Arcs` — isolated Play Mode save round-trip, idempotency, bond canonicalization, favor monotonicity, quest-priority routing, complete VO, and seven live schedules.
- `Hollowfen/Verify/Living Village Encounters` — four exact schedule-gated pairs, weather-aware arrival, 20 voiced beats, spatial leak prevention, eight memories, four bonds, and one-shot dispersal.
- `SaveIntegrityVerifier` — new snapshot survives checksummed disk round-trip.
- `DataIntegrity` — validates memory/bond/favor outcomes and impossible NPC gate ranges.
- `tools/agent/build_voice_manifest.py --check` — every relationship line has current 24 kHz mono VO with text fingerprinting.
