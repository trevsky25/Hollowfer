# Quest System
Linear quest chain: static `QuestManager` holds ONE active quest + completed-id set (no state enum, no fail state); quests auto-chain via `QuestData.NextQuest`; scene components (triggers/interactables/forage objectives) call `CompleteQuest` when conditions met. `GameScores` (static) holds the ending meters (VillageHope, Knowledge, per-NPC relationships, named flags).
Key scripts: `Assets/_Hollowfen/Scripts/Quests/` — QuestManager, QuestData, QuestBootstrap, QuestHUD, QuestTrigger, QuestInteractable, QuestForageObjective, KeyLockedDoor, StoryBeats, TaxDeadline, GameScores, ScoreHooks, ScoreDebugHUD.
Data: `Data/Quests/Quest_ActN_NN_Name.asset` (26: Act1 01–07, Act2 08–15, Act3 16–23, Act4 24–26); quest `_id`s are camelCase story.md ids (`arrive`, `wendlightFound`, `meetAldric`).
Persistence: `CompletedQuestIds`/`UnlockedStoryCardIds` + all scores/flags in the save slot (no PlayerPrefs); active quest is NOT saved — `QuestBootstrap` re-derives it by walking the chain past completed entries.
Biggest gotchas: achievement hook fires ONLY on story-card unlock (`ACH_STORY_<cardId>`), not quest completion — gap vs our non-negotiable; magic quest-id strings synced by hand across TaxDeadline/StoryBeats/ScoreHooks; `QuestCompleted` fires before `_activeQuest` clears.
Status: quests 1–26 play-verified via bridge — Acts I–III complete; **Act IV scenes 1–3 shipped** (2026-07-12). Scene 1 `aldricOfferRead` = the `_MillLetter` prop reading beat (→ `act4_started`). Scene 2 `wendSource` = the upstream clear-cut: a prop-anchored `_ClearCutSite` (QuestInteractable + LocationMarker on one GO) plays the Hollin scene, discovers Aldermark (species #21), completes with Knowledge +15 + evidence flags (`wend_source_visited`/`clearcut_evidence_found`/`aldermark_sample_collected`). Scene 3 `meetAldric` = the negotiation SCAFFOLD (batch-27): NPC_Aldric (flag-gated `_Aldric` FAO → `AldricGroup` capsule at the new `manor` LocationData) plays `Dialogue_Act4_MeetAldric` (bible-verbatim, ends at the fork, NO `_choices`); completes with 0 deltas and ScoreHooks sets `aldric_meeting_started` + `final_choice_available`. **STOP POINT — the 4 endings + ending-selection engine are unbuilt (Trevor's authorship, FABLE-GATED, TODOS item 13).** QuestInteractable can play a DIALOGUE on use (`_playsDialogue`) and unlock field-guide entries (`_discoversSpecies`). Act III B `caldenReconcile` uses a two-step NPC dialogue gated by a DayFlagScheduler "wait one day" pair (`calden_records_requested→calden_records_read`) and reopens the chapel garden via an inverted `_offFlagId` (`calden_garden_unlocked`) on the existing `_ChapelGateLock` FlagActivatedObject.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Lifecycle

Three implicit states: **Active** (`QuestManager.ActiveQuest`, single slot), **Completed** (id in `_completedIds`), **Not started**. No fail state anywhere.

- **`StartQuest(QuestData)`**: guards (null/completed/already-active) → sets active → `ApplyWaypointHint` (matches `quest.WaypointLocation` against `LocationRegistry.Markers`, sets map waypoint) → fires `QuestStarted`.
- **`CompleteQuest(string id)`**: only if id == active quest → add to completed + `Persist()` → apply QuestData score deltas (`AddVillageHope/AddKnowledge/AddRelationship` pairs) → `UnlockStoryCard` if set → fires `QuestCompleted` (⚠️ BEFORE nulling `_activeQuest`) → chain `StartQuest(NextQuest)` or `LocationRegistry.ClearWaypoint()`.
- **`UnlockStoryCard(cardId)`**: adds + persists + fires `StoryCardUnlocked` + `GameEvents.TriggerAchievement("ACH_STORY_" + cardId.ToUpperInvariant())` — **the only achievement path in the quest system**.
- Listeners of `QuestCompleted`: ScoreHooks (flags), StoryBeats (checkpoint save + act-break narration), QuestHUD (refresh).

## QuestData fields

`_id` (camelCase story.md id) · `_displayNameId`/`_objectiveTextId` (localization keys) · `_act` (1–4) · `_order` (⚠️ dead at runtime — chain order comes from `_nextQuest`) · `_unlockStoryCardOnComplete` · `_nextQuest` (auto-chain) · `_waypointLocation` (LocationData) · `_villageHopeDelta`/`_knowledgeDelta` · `_relationshipNpcIds[]`+`_relationshipDeltas[]` (parallel arrays, min-length iterated).

## Objective components (scene → CompleteQuest)

| Component | Mechanism |
|---|---|
| `QuestTrigger` | Collider trigger + `"Player"` tag. `_autoStartIfNoneActive` (starts quest — how Act I begins at the village square) and/or `_completesQuestIfActive`. ⚠️ `_fireOnce` only latches on the complete path. |
| `QuestInteractable` | `IInteractable` examine prop (journal, letters, tonic delivery). Foraging-layer SphereCollider convention. `_requiresActiveQuest`, `_requiresItemId` (KeyItems gate — tonic delivery needs the tonic), `_completesQuestIfActive`, `_grantsItemId` (→`KeyItems.Grant`), `_setsFlagId` (→`GameScores.SetFlag` — lets DayFlagScheduler stage next-day beats), `_discoversSpecies[]` (→`MushroomDiscovery.MarkDiscovered` — field-guide teaching), `_playsDialogue`, `_playsNarrationId` (batch-51 — localized passage split on blank lines → `NarrationOverlay.Show` captions; live Georgia text), `_deactivateOnUse`. Session-local `_used`. **`Journal_FathersJournal`** (findJournal) uses `_discoversSpecies` = [fieldCap, woodEar, pinecrest] (Goldfoot stays an unfinished margin note — NOT taught here) + `_playsNarrationId` = `act1.hidden_journal.tobin_note` (the reveal + Tobin's farewell note). |
| `KeyLockedDoor` | `CanInteract = !_opened && KeyItems.Has(_requiredItemId)` (default `item.mill_key`). Opens via Magic Pig `DemoDoor.Open()` or 110° Y-rotate fallback; disables collider; completes quest. ⚠️ `_opened` not persisted — door re-closes on reload (benign; quest stays complete). |
| `FlagActivatedObject` | Declarative world-state switch: mirrors a GameScores flag onto a target's active state (`_flagId`, `_target`, `_deactivateWhenSet`, `_offFlagId` = override-off flag). Host must stay ACTIVE. Uses: Theo's wagon, Edda, Calden, cottage boards (inverted), chapel gate lock, and Hollin's inn→mill move (`_offFlagId=hollin_at_mill` on the inn host). |
| `QuestForageObjective` | Subscribes `MushroomNode.OnAnyHarvested`; completes when every `_requiredSpecies` is harvested this session OR already in inventory (pre-quest stock counts). Empty list = any harvest. Progress not persisted/displayed. |

## QuestManager / QuestBootstrap / QuestHUD

- **QuestManager**: static class (not a MonoBehaviour). API: `ActiveQuest`, `CompletedQuestIds`, `UnlockedStoryCardIds`, `StartQuest`, `CompleteQuest`, `IsCompleted/IsActive`, `UnlockStoryCard/IsStoryCardUnlocked`, `ResetForSlotSwitch()` (clears state, KEEPS event subscribers), `HydrateFrom(quests, cards)`. Events: `QuestStarted/QuestCompleted/StoryCardUnlocked`. Lazy `EnsureHydrated()` from active slot; `Persist()` → `AutoSaveQuestState` on every change; `ResetOnLoad` (`SubsystemRegistration`) clears state AND event delegates.
- **QuestBootstrap**: scene MonoBehaviour; on `Start()`, if nothing active, walks `_initialQuest`'s NextQuest chain past completed entries (cap 64) and starts the first uncompleted — this is how resume works without saving the active pointer. ⚠️ `_startDelaySeconds` is serialized but dead.
- **QuestHUD**: top-left procedural card (UICanvasUtil rounded panel, 360×80). Eyebrow `"QUEST · ACT <roman>"` (⚠️ hardcoded English), name/objective via `Localization.Get`. Hides via CanvasGroup when no active quest. Event-driven, no polling.

## GameScores / ScoreHooks / StoryBeats / TaxDeadline

- **GameScores** (static): `VillageHope` + `Knowledge` (clamped ≥0), per-NPC relationships (⚠️ NOT clamped, can go negative), named flags (`HasFlag/SetFlag` — SetFlag returns true only when newly set). `OnChanged` event (no payload). Same lifecycle recipe as QuestManager (ResetOnLoad / lazy hydrate / Persist→`AutoSaveScores`). `HydrateFrom(meta)` / `WriteTo(meta)`.
- **ScoreHooks** (on `_Narration`): declarative questId→flags table applied on `QuestCompleted` (e.g. `meetAlmy`→`almy_met`+`act1_complete`, `firstTax`→`wenmar_tax_paid`+`voss_notices_wren`). Act II's `act2_started`/`joren_met`/`voss_first_visit_seen` are set by dialogues instead. Also: `homecoming` card → `homecoming_seen`; `MushroomDiscovery.OnDiscovered` → `AddKnowledge(1)` + species known-flags.
- **StoryBeats**: homecoming intro narration (gated by `HomecomingIntroSeen`, persisted BEFORE showing) + Act I break narration on `meetAlmy` (⚠️ caption strings hardcoded, bypass Localization) + **full checkpoint save (`SaveCoordinator.SaveAllWithPlayer`) on every quest completion**.
- **TaxDeadline**: on `TimeManager.OnSundown`, if `firstTax` active AND `voss_first_visit_seen` AND NOT `wenmar_tax_paid` → `AddVillageHope(-2)`. Recurring pressure, no hard fail, no fixed deadline day; only completing `firstTax` stops the bleed. Paying nets +10 Hope / +3 Voss.
- **ScoreDebugHUD**: F3 toggles dev overlay (Hope/Knowledge/relationships/flags), refreshes every 30 frames.

## Gotchas

- **Achievement gap**: quests without a story card fire NO achievement (e.g. `firstTax`). Non-negotiable says every quest/beat completion → hook. Fix tracked in TODOS.md.
- **Magic strings**: `"firstTax"`/`"arrive"`/`"meetAlmy"`/ScoreHooks tables must match asset `_id`s by hand — renaming an id silently breaks wiring. (EditMode test candidate.)
- **Event ordering**: `QuestCompleted` fires while `ActiveQuest` still points at the completed quest; chained `QuestStarted` corrects HUD a frame later.
- **Write amplification**: one completion → AutoSaveQuestState + N×AutoSaveScores + StoryBeats' full SaveAll ≈ 5+ slot-file writes.
- **Static events + domain-reload-off**: any NEW static event must be added to `ResetOnLoad` by hand (QuestManager/GameScores do this correctly).
