# Quest System
Linear quest chain: static `QuestManager` holds ONE active quest + completed-id set (no state enum, no fail state); quests auto-chain via `QuestData.NextQuest`; scene components call `CompleteQuest`. `GameScores` owns the ending meters/flags, while `EndingResolver` evaluates and atomically commits exactly one data-authored ending.
Key scripts: quest progression in `Assets/_Hollowfen/Scripts/Quests/`; authored objective presentation in `Scripts/Data/StoryMomentData`, `Scripts/UI/StoryMomentDirector`, `NarrationOverlay`, and `Scripts/Cinematics/NarrativePresentationSession`. Ending assets live in `Data/Endings/`; richer objective beats live in `Data/StoryMoments/`.
Data: `Data/Quests/Quest_ActN_NN_Name.asset` (26: Act1 01–07, Act2 08–15, Act3 16–23, Act4 24–26); quest `_id`s are camelCase story.md ids (`arrive`, `wendlightFound`, `meetAldric`).
Persistence: `CompletedQuestIds`/`UnlockedStoryCardIds` + all scores/flags in the save slot (no PlayerPrefs); active quest is NOT saved — `QuestBootstrap` re-derives it by walking the chain past completed entries.
Biggest gotchas: ordinary quest achievements still fire ONLY through story-card unlock (`ACH_STORY_<cardId>`); ending achievements are explicit on `EndingData`; magic quest-id strings remain hand-synced across TaxDeadline/StoryBeats/ScoreHooks; `QuestCompleted` fires before `_activeQuest` clears.
Status: quests 1–26, story-card presentation coverage, four-outcome ending engine, and the festival's gather-before-completion handoff are bridge-verified through 2026-07-16.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Lifecycle

Three implicit states: **Active** (`QuestManager.ActiveQuest`, single slot), **Completed** (id in `_completedIds`), **Not started**. No fail state anywhere.

- **`StartQuest(QuestData)`**: guards (null/completed/already-active) → sets active → `ApplyWaypointHint` (matches `quest.WaypointLocation` against `LocationRegistry.Markers`, sets map waypoint) → fires `QuestStarted`.
- **`CompleteQuest(string id)`**: only if id == active quest → add to completed + `Persist()` → apply QuestData score deltas (`AddVillageHope/AddKnowledge/AddRelationship` pairs) → `UnlockStoryCard` if set → fires `QuestCompleted` (⚠️ BEFORE nulling `_activeQuest`) → chain `StartQuest(NextQuest)` or `LocationRegistry.ClearWaypoint()`.
- **`UnlockStoryCard(cardId)`**: adds + persists + fires `StoryCardUnlocked` + `GameEvents.TriggerAchievement("ACH_STORY_" + cardId.ToUpperInvariant())` — **the only achievement path in the quest system**.
- Listeners of `QuestCompleted`: ScoreHooks (flags), StoryBeats (checkpoint save + act-break narration), QuestHUD (refresh).

## QuestData fields

`_id` (camelCase story.md id) · `_displayNameId`/`_objectiveTextId` (localization keys) · `_act` (1–4) · `_order` (⚠️ dead at runtime — chain order comes from `_nextQuest`) · `_unlockStoryCardOnComplete` · optional `_storyMoment` (null = compact completion card) · `_nextQuest` (auto-chain) · `_waypointLocation` (LocationData) · `_villageHopeDelta`/`_knowledgeDelta` · `_relationshipNpcIds[]`+`_relationshipDeltas[]` (parallel arrays, min-length iterated).

## Objective components (scene → CompleteQuest)

| Component | Mechanism |
|---|---|
| `QuestTrigger` | Collider trigger + `"Player"` tag. `_autoStartIfNoneActive` (starts quest — how Act I begins at the village square) and/or `_completesQuestIfActive`. ⚠️ `_fireOnce` only latches on the complete path. |
| `QuestInteractable` | `IInteractable` examine prop (journal, letters, tonic delivery). Foraging-layer SphereCollider convention. Core outcome fields are unchanged. `_storyMoment` routes a rich reveal through `StoryMomentDirector`; `_storyMomentContext` supplies optional runtime scene context while images, caption IDs, VO, mapping, timing, and lens live on the asset. The old per-component journal narration fields were removed after migration, leaving one cinematic path. **`Journal_FathersJournal`** now references `StoryMoment_Act1_HiddenJournal`: director pushes into the real book, holds the camera, dissolves through three painted spreads + seven voiced beats, restores, then retires the prop. |
| `KeyLockedDoor` | `CanInteract = !_opened && KeyItems.Has(_requiredItemId)` (default `item.mill_key`). The cinematic unlock preserves the key prefab's authored pose, aims its +X shaft into the door, measures the rendered tip, guides that tip to the keyhole, inserts straight along the door normal, then turns around the shaft. Its motion pivot stays fixed to the frame until seated and is reparented to the leaf before `DemoDoor.Open()`, so the key follows the final swing. Instant fallback remains when cinematic references are absent. Disables the collider and completes the quest. ⚠️ `_opened` not persisted — door re-closes on reload (benign; quest stays complete). |
| `FlagActivatedObject` | Declarative world-state switch: mirrors a GameScores flag onto a target's active state (`_flagId`, `_target`, `_deactivateWhenSet`, `_offFlagId` = override-off flag). Host must stay ACTIVE. Uses: Theo's wagon, Edda, Calden, cottage boards (inverted), chapel gate lock, and Hollin's inn→mill move (`_offFlagId=hollin_at_mill` on the inn host). |
| `QuestForageObjective` | Subscribes `MushroomNode.OnAnyHarvested`; completes when every `_requiredSpecies` is harvested this session OR already in inventory (pre-quest stock counts). Empty list = any harvest. Progress not persisted/displayed. |
| `VillageRequests` | Can own a story delivery through `ActiveQuestId` + `CompleteQuest`. It commits required inventory, flags, request state, and the active quest before presenting its follow-up dialogue. `festivalHosted` is the first consumer. |

## QuestManager / QuestBootstrap / QuestHUD

- **QuestManager**: static class (not a MonoBehaviour). API: `ActiveQuest`, `CompletedQuestIds`, `UnlockedStoryCardIds`, `StartQuest`, `CompleteQuest`, `IsCompleted/IsActive`, `UnlockStoryCard/IsStoryCardUnlocked`, `ResetForSlotSwitch()` (clears state, KEEPS event subscribers), `HydrateFrom(quests, cards)`. Events: `QuestStarted/QuestCompleted/StoryCardUnlocked`. Lazy `EnsureHydrated()` from active slot; `Persist()` → `AutoSaveQuestState` on every change; `ResetOnLoad` (`SubsystemRegistration`) clears state AND event delegates.
- **QuestBootstrap**: scene MonoBehaviour; on `Start()`, if nothing active, walks `_initialQuest`'s NextQuest chain past completed entries (cap 64) and starts the first uncompleted — this is how resume works without saving the active pointer. ⚠️ `_startDelaySeconds` is serialized but dead.
- **QuestHUD**: top-left procedural card (UICanvasUtil rounded panel, 360×80). Eyebrow `"QUEST · ACT <roman>"` (⚠️ hardcoded English), name/objective via `Localization.Get`. Hides via CanvasGroup when no active quest. Event-driven, no polling.

## GameScores / ScoreHooks / StoryBeats / TaxDeadline

- **GameScores** (static): `VillageHope` + `Knowledge` (clamped ≥0), per-NPC relationships (⚠️ NOT clamped, can go negative), named flags (`HasFlag/SetFlag` — SetFlag returns true only when newly set). `OnChanged` event (no payload). Same lifecycle recipe as QuestManager (ResetOnLoad / lazy hydrate / Persist→`AutoSaveScores`). `HydrateFrom(meta)` / `WriteTo(meta)`.
- **EndingResolver / EndingData**: pure `Evaluate(ending)` checks Hope, Knowledge, required flags, and relationship minima; `TryResolve` calls `GameScores.TryCompleteEnding`, which refuses an existing `game_complete` or any prior canonical `ending_*`. Only then does it unlock the selected story card, fire the ending achievement, and take a full save. Canonical gates: Free Hollow = Hope 50 + tax/evidence/Aldermark; Patronage = final-choice fallback; Capital = Theo 18 + Capital offer; Witch's Path = Knowledge 35 + Hollin 15 + cottage/seedbook.
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
