# Overnight production audit — 2026-07-19

Scope: seven-pass review of UI/UX, fresh-save pacing, economy, Living Restoration, story/dialogue, persistence/sequence safety, and performance/visual cohesion. The active player journal and backup were hashed before and after state-mutating checks; every mutation ran behind a temporary save-directory override or a verifier-owned snapshot/restore boundary.

Verdict: **pass with release follow-ups**. The integrated game systems are structurally coherent and the defects found in this pass were repaired. A physical start-to-ending playthrough, standalone-player frame capture, and Steam Deck hardware run remain release evidence—not work that an Editor verifier can honestly replace.

## 1. UI/UX, accessibility, and controller ownership — PASS

Audited gameplay HUD, main menu, save slots, settings, pause, Story, Field Guide, Wren, inventory, map, cultivation, village requests, preparation ledger, and apothecary casework. Settled runtime presentations return `0 critical / 0 advisory`; 115% Interface Size, Reduced Motion, and Caption Backing also pass.

Findings repaired:

- Wren's dossier and standalone apothecary canvases could lose controller focus after mouse input or dynamic-row rebuilds. `UIFocusRecovery` now restores the first valid control without emitting a false navigation sound, and every dynamic non-`UIManager` modal participates.
- Wren's dossier scroller now has a visible controller focus rail/wash.
- The production verifier previously inspected only the `UIManager` stack. It now finds the highest blocking standalone modal, rejects focus outside the active presentation, and validates current focus—not merely the existence of a default.
- The longer first-tax explanation exposed an objective outside the fixed quest card. `QuestHUD` now expands to preferred copy height, up to a bounded 196 reference pixels, and the verifier fails if the objective escapes the surface.
- Runtime screenshot proof: `Assets/Screenshots/overnight-audit-final-ui.png`.

## 2. Fresh-save pacing, quest handoffs, and exploration rhythm — PASS

- All 26 quests form one recoverable chain with one destination each.
- Eleven stage-aware routes move both compass destination and objective copy through Almy's lesson, the Brightspore chain, and Calden's warning.
- Nine scheduled principals occupy the village, market, mill, Old Wood, chapel, apothecary, and late-game sites across a 235m × 367m route.
- Consecutive story legs range from zero-distance handoffs to 318m. Long walks occur at act/region changes; local loops return the player to nearby services before the next long departure.
- Voss's demand could land only minutes before Day 1 sundown despite requiring a 144c payment. The first crossed sundown now records a visible grace warning; only later misses cost 2 Village Hope. This guarantees one complete overnight/dawn forage window without hard-failing the story.

Automated evidence: `StoryWorldAlignmentVerifier`, `NPCScheduleVerifier`, `GameplayFoundationVerifier`, `MillDoorProgressionVerifier`, and `EndingEngineVerifier` all pass.

## 3. Economy and progression — PASS

- All 21 mushroom profiles have bounded respawns and non-negative prices; Theo never undercuts Marra where both buy.
- The common world population is worth 133c to Marra. The first 38c payment less Joren's 12c commission leaves enough combined liquidity to meet Voss's 144c demand in one common-forage cycle.
- All twelve recurring jobs now meet or beat the best direct-sale value of their ingredients. Edda's Wood Ear poultice moved from 18c to 24c because the same basket can sell for 22c.
- Seven restoration projects cost 202c total, within two full common-forage cycles (266c), with individual contribution rows held to 10–24c.

Automated evidence: `ProductionBalanceVerifier` and `VillageRequestVerifier` pass, including prepared-stock delivery, first-only relationship rewards, wet premiums, festival handoff, exact single-revision commits, and rollback.

## 4. Living Restoration, weather, and apothecary world state — PASS

- Cottage migration/round-trip, seven-project catalogue, ten atomic supply lines, bridge reopening, dawn reveals, permanent benefits, and first-use idempotence pass.
- Four restored-place encounters route the correct paired villagers, weather window, 20 voiced lines, eight memories, four bonds, and one-shot dispersal.
- Weather remains deterministic across all six periods and drives fog, rain, shelter, wind, audio, growth, wild flushes, premiums, and the icon-led forecast card.
- Apothecary checks pass for purchased-building ownership, sealed exterior sightline, inward automatic entrance, rear chain gate, player-sized traversal, seven candle controls, level terrain, three four-step recipes, shelf stock, six sequential cases, 24 evidence beats, 18 outcomes, delayed follow-ups, and atomic rollback.

Audit-harness repairs: restoration expansion now expects all nine live schedules; the apothecary traversal probe explicitly opens the player-tracked automatic door before measuring its open threshold.

## 5. Story, dialogue, copy, and character continuity — PASS

- Data Integrity reports `0 errors / 0 warnings` across 26 quests, 145 dialogues, 11 NPCs, 15 locations, 21 mushrooms, 30 story cards, 28 story moments, 31 character profiles, four endings, 16 requests, and seven restorations.
- All 28 cinematic cards are complete; the 24 newly covered dialogue turns have three painted beats; Bram's sequence retains the live key handoff.
- All 93 story paintings are HD, uncompressed, and mip-free.
- Relationship memory passes idempotent save/load, symmetric NPC bonds, six monotonic favor chains, quest-priority routing, 46 voiced relationship conversations, four ending reactions per core villager, and nine recovered schedules.
- New copy lint passes all 145 dialogues, 26 quest cards, 16 requests, 15 location toasts, and 28 moments. It catches blank/dirty text, malformed spacing, common typos, UI-length outliers, excess choices, and accidental unvoiced same-speaker advances while preserving deliberate voiced dramatic pauses.

## 6. Persistence, interrupted sequences, and transaction safety — PASS

Passing coverage includes:

- legacy save upgrade, checksum/semantic corruption, temp/backup revision selection, future-schema refusal, normalization, full round-trip, and recovered rewrite;
- strict inventory batches, duplicate aggregation, damaged-journal isolation, single-revision village commits, and zero-event rollback;
- nested presentation owners, minimum time override, cursor/input/HUD restoration, same-frame shortcut blocking, and zero leaked owners;
- Bram key persistence, two-sided mill-door interaction, one-shot animation, and completed-save open-threshold restoration;
- four ending gates, pending-choice resume, card/achievement isolation, acknowledgement, second-choice refusal, and atomic backup recovery;
- 63 stable wild nodes, identification before all 21 species can be harvested, cultivation, respawn, buyer quote purity, and ordered sundown/dawn events.

The pre/post SHA-256 hashes of the tester's slot and backup are identical.

## 7. Performance and visual cohesion — PASS WITH RELEASE MEASUREMENT REQUIRED

The largest cost is the purchased medieval world, not the new restoration/apothecary layer: `World/Main Town/Buildings` contains 34,903 serialized renderers beneath 46,762 very small LOD groups. At the audited night/forge view, the former PC LOD bias 2.0 held approximately 6.0M visible triangles and 1,446 shadow casters. A 1.25 A/B pass reduced the view to approximately 3.8M triangles and 835 shadow casters while 4K captures retained the same composition and near-field detail.

The production policy and PC quality asset now own 1.25 and reapply it after display/quality changes. The final sampled view reported roughly 3.55M triangles, 933–949 shadow casters, 115 set-pass calls, and one active gameplay camera. Main-thread samples remained around 3.5–3.8ms; Editor GPU timing varied materially (15–36ms) with Game-view capture/focus, so it is recorded as diagnostic rather than claimed as standalone proof.

New systems remain bounded independently: apothecary ≤470 serialized renderers / 1.5M serialized-LOD triangles / four shadowless realtime lights; restoration ≤240 simultaneous stage renderers / 18 materials / seven particles / four shadowless lights; camera-local weather rain ≤720 particles.

## Regression matrix

| Gate | Result |
|---|---|
| Script compile | PASS — 0 errors |
| Active production UI | PASS — 0 critical / 0 advisory |
| Accessibility presentation | PASS |
| Data Integrity | PASS — 0 errors / 0 warnings |
| Narrative copy | PASS |
| HD story art / complete dialogue moments | PASS |
| Story/world alignment / NPC schedules | PASS |
| Economy/progression / village requests | PASS |
| Restoration / bridge / expansion / encounters | PASS |
| Apothecary preparation / casework | PASS |
| Weather | PASS |
| Save / inventory / presentation ownership | PASS |
| Gameplay foundation / mill door / endings | PASS |

## Release follow-ups

1. Run one uninterrupted fresh-slot playthrough through all 26 physical interactions and one complete ending; automated routing proves reachability and state transitions but does not press every prompt in sequence.
2. Build a non-development standalone player and capture fixed-route frame-time/VRAM data. Editor memory and GPU counters include Editor overhead and are not certification evidence.
3. Run controller-only, suspend/resume, offline, 1280×800 readability, thermal, and 40/60fps checks on physical Steam Deck hardware.
4. Finish the two intentional visual-content gaps reported by Data Integrity: Oyster journal sketch and Aldermark sketch/canonical Maitake model.
5. Final human VO casting/localization/legal/store-identity work remains a release-production decision, not a code-verifier pass.
