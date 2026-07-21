# Village Requests
Village requests layer repeatable work over the linear story without replacing it: Marra, Edda, and Theo each offer one deterministic order per game day, while one-shot gatherings can own a canonical quest.
Key scripts: `Assets/_Hollowfen/Scripts/Requests/` — `VillageRequestData`, `VillageRequestDatabase`, `VillageRequests`, `VillageRequestScreen`, and `VillageRequestTrackerHUD` (namespace `Hollowfen.Requests`).
Data: `Data/Requests/` contains 12 recurring requests (four per NPC), three sequential apothecary story deliveries, and `festival_four_dishes`; the build-safe runtime index is `Resources/VillageRequestDatabase.asset`.
Persistence: `SaveSlotMeta.VillageRequests` stores completed one-shots, each NPC's claimed request/day, and the optional tracked request/day; raw ingredients, prepared products, copper, relationships, flags, and quests continue to use their owning stores. Copper rewards enter Wren's saved purse ledger as `Village delivery`; an Occupied Crooked Pintle adds two copper to ordinary daily requests and authored wet-weather work can add its own premium at calculation time.
Priority: active story dialogue wins, except after a story request's kickoff flag makes that request the active quest's owner; one-shot story requests then outrank ordinary work.
Status: dawn rotation, controller request card, prepared-product story/repeat work, wet-weather pricing, tracker expiry, and the four-dish festival handoff are Play Mode verified through 2026-07-19; cross-system one-revision delivery plus final-commit rollback/event/achievement isolation remain verifier-covered.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Runtime selection

`VillageRequests.CurrentForNpc(npcId)` is the only selection boundary:

1. Find an eligible incomplete `OneShot` for that NPC. Story work never expires and always outranks a rotating order.
2. If that NPC already completed any delivery today, return null.
3. Filter eligible recurring requests, sort by stable request ID, and select with a stable NPC hash + game day. The same save/day always gets the same order; with three eligible rows, tomorrow advances to the next.

Eligibility can require any combination of GameScores flags, completed quest IDs, and one active quest ID. Daily work currently unlocks after its story relationship exists: Marra after `firstSale`, Theo after `theo_met`, and Edda after `apprentice_system_unlocked`; knife-gated ingredients additionally require `foraging_knife_unlocked`.

`TimeManager.OnDayChanged` expires a tracked ordinary order. A one-shot tracker persists across dawn. Completing a one-shot also occupies that NPC's delivery rhythm for the current day, preventing an immediate ordinary shopping list after a major scene.

## Interaction and presentation

`NPCInteractable.ResolveInteraction` keeps the story spine authoritative:

- An unrelated active-quest dialogue keeps the normal Talk prompt.
- An ordinary request opens the illustrated parchment card and includes **Talk instead**, which routes to the NPC's normal dialogue/trade fallback.
- A story request tied to the active quest owns the interaction and omits the escape into unrelated trade.

`VillageRequestScreen` is constructed at runtime using the journal palette. It renders the request's StoryCard image, requester line, up to four live basket requirements, and payment. Mouse, keyboard, and controller share normal Unity `Button` navigation. It pauses world time/input while open and restores the previous state on close. Opening a card pins it beneath `QuestHUD`; inventory changes update both surfaces live.

## Completion transaction

`InventoryRuntime.TryRemoveBatch` requires aligned non-empty arrays and positive amounts, rejects integer overflow, aggregates duplicate species, and validates the entire basket before mutation. `ApothecaryRuntime.TryConsumeProductsForTransaction` does the equivalent for prepared shelf stock, but may run only inside the owning delivery transaction. As a standalone operation the raw batch path builds a staged snapshot, requires a loadable active journal, writes exactly one higher save revision, verifies the winning primary/temp/backup payload, and only then publishes live counts plus one wildcard inventory event.

`VillageRequests.Complete` wraps the whole delivery in a broader save transaction. It snapshots inventory, purse/ledger, scores/flags, requests, quest/card state, active quest, and waypoint; targeted autosaves become no-ops; and inventory/coin/score/request/quest/card/waypoint events plus achievement dispatch are queued. Completion then stages all owning stores in memory, asks `SaveCoordinator` for one full player snapshot, and commits only when that exact canonical payload is the higher-revision recovery winner. On any validation, IO, integrity, or injected final-write failure, runtime state is restored while the transaction is still suppressing publications, then the entire callback queue is discarded. The old disk revision/artifacts remain authoritative and the UI receives a specific invalid-data, changed-basket, or `Delivery could not be saved. Nothing was consumed.` result.

After the full-slot commit succeeds, queued publications are released and completion:

- record the one-shot or daily claim;
- apply copper every completion with the `Village delivery` purse-ledger reason, including `RestorationBenefits.DailyRequestBonusCopper` and any authored wet-weather premium for eligible recurring work;
- apply relationship/knowledge only on that authored request's first-ever completion (guarded by `village_request_first_<id>`);
- set consequence flags;
- commits the request snapshot, ingredients, reward ledger, scores/flags, and optional active story quest/card in the same revision;
- advances the active quest/waypoint; and
- publishes the inventory, score, coin, story-card, quest, achievement, and request UI notifications.

Recurring orders cannot farm relationship progression. Their copper remains repeatable so foraging, rest/respawn, trade, and cultivation form a useful loop after the story beats are exhausted.

## Authored content

| NPC / gathering | Live requests | Gate |
|---|---|---|
| Marra | Field basket · Goldfoot stew · Woodland broth | `firstSale`; woodland broth also needs the knife |
| Edda | Brightspore draught · Wood Ear poultice · shelf tonic | apprentice unlocked; shelf tonic also needs the knife |
| Theo | Moss-road crate · inn-cellar crate · rain-market basket | `theo_met`; inn-cellar crate also needs the knife |
| Festival | Goldfoot + Lacewig + Field Cap + Brightspore, one each | `festivalHosted` active + Marra's kickoff flag |
| Apothecary story | Field Ink → Theo · Hearth Broth → Marra · Shelf Tonic → Edda | sequential lesson/delivery flags; each consumes one prepared product |
| Apothecary repeats | one prepared product for Theo, Marra, or Edda | appears in that NPC's dawn rotation after their story delivery |

The festival was split into three safe phases: Marra's original coordination lines set `festival_gathering_active`; the request commits ingredients, `festival_prepared`, `festival_hosted`, and the quest; the original Wren/Edda bowls exchange then plays as the finale. A stable Lacewig wild node was added beside an existing woodland source so the authored requirement is obtainable.

The apothecary arc uses the same request authority rather than a parallel delivery screen. Theo, Marra, and Edda each receive one first product in sequence, publish their completion dialogue after the commit, and unlock a recurring product order. Theo's Rain Market basket and repeat Field Ink order pay four extra copper during Drizzle, Rain, or Storm. `RewardFor` is the sole UI/runtime calculation boundary so displayed and committed payment cannot disagree.

Recurring request rewards are also guarded against their ingredients' best direct-sale opportunity cost. The Wood Ear poultice pays 24c for a 22c maximum-value basket; future importer changes must keep this invariant or `ProductionBalanceVerifier` fails.

## Adding or changing a request

1. Add/edit a `VillageRequestData` asset: stable ID, canonical NPC ID, localized copy IDs, StoryCard image, 1–4 combined raw/prepared requirements, reward, and eligibility.
2. Recurring work needs copper and must not own story quest/dialogue outcomes. A one-shot story request needs a matching `ActiveQuestId`, `CompleteQuest`, and completion dialogue.
3. Add it to `VillageRequestDatabase`. The idempotent `VillageRequestContentImporter.BuildAll()` is the canonical authoring pass for the current sixteen assets.
4. Ensure every required species is physically obtainable under the same or earlier progression flags.
5. Run Data Integrity, isolated `InventoryTransactionVerifier.RunAll()`, `VillageRequestVerifier.RunAll()` in Play Mode with saves backed up, and the general smoke test.

## Gotchas

- Request IDs are save data. Never rename one without migrating completed/claimed/tracked IDs and its `village_request_first_*` score flag.
- Selection changes if the eligible request set changes. That is intentional at a dawn boundary; avoid adding/removing eligibility mid-day unless the one-delivery-per-NPC claim rule makes the transition harmless.
- Story request completion happens before its finale dialogue. Quitting during that dialogue is safe and cannot strand the quest half-complete.
- **Do not consume requirements row-by-row or bypass the delivery transaction.** Standalone batch removal owns a verified inventory revision; `VillageRequests.Complete` instead owns one verified full-slot revision and defers every outward callback until it wins recovery inspection.
- The runtime database lives in `Resources`; an asset under `Data/Requests` but absent from the database is invisible in builds. Data Integrity errors on that drift.
