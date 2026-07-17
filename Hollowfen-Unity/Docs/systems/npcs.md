# NPC System
NPC = `NPCData` SO (id, localized display name, ordered condition→dialog table, repeat fallback) + `NPCInteractable` scene component riding the shared `IInteractable`/Foraging-layer convention. Dialogue routing remains first-match-wins; `NPCInteractable` composes it with the save-backed Village Requests layer, while `NPCSchedule` derives physical placement from the clock, quest milestones, and flags.
Key scripts: `Assets/_Hollowfen/Scripts/NPCs/` — NPCData, NPCInteractable, NPCSchedule (namespace `Hollowfen.NPCs`).
Data: `Data/NPCs/NPC_<Name>.asset` — Bram, Marra, Almy, Joren, Voss, Theo, Edda, Hollin, Pell, Calden, Aldric (11). Ids lowercase (`voss`), matching relationship ids in dialogue/quest score deltas.
Entry conditions (ANDed, unset = skip): `activeQuest` (IsActive) · `requiresQuestCompleted` · `requiresFlagId` (GameScores) · `requiresCoinsCopper` (≥) · `requiresBasketNonEmpty` · `requiresForage` (species SO — basket holds ≥1 of it; Marra's tonic gate).
Biggest gotchas: ENTRY ORDER IS THE DIALOGUE PRIORITY SYSTEM; schedule slots are also first-match-wins. Schedule hosts must remain active even when their actor is hidden. Active story dialogue outranks ordinary requests, while a story-linked request explicitly takes over its own active quest after its kickoff. Null repeat + no dialogue/request means no prompt.
Status: whole cast placed; Marra/Edda/Theo expose three dawn-rotating requests each, Marra owns the persistent four-dish festival gathering, and Theo/Joren/Bram/Pell now follow the first milestone-gated village routines. Remaining cast work is schedule expansion, model-swap, and world-staging polish.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## NPCData

CreateAssetMenu `Hollowfen/NPCs/NPC Data`. Fields: `_id` (lowercase key) · `_displayNameId` (localization key, e.g. `npc.voss.name` — resolved by the prompt HUD) · `_dialogueEntries` (`NPCDialogueEntry[]`, private — consumed only by PickDialog) · `_repeatDialog` (fallback; MAY be null).

**`PickDialog()`**: first entry whose conditions ALL pass → its `dialog`; none → `_repeatDialog` (possibly null). `PickActiveQuestDialog()` evaluates only active-quest rows so the interaction router can preserve story priority before considering ambient village work.

**Worked example — `NPC_Voss.asset`** (most-specific-first ordering):
1. `firstTax` active + flag `voss_first_visit_seen` + ≥144 copper → payment dialog
2. same quest + flag, no coin gate → "still short" dialog
3. quest active, no flag → first-visit demand (which sets the flag itself)
4. quest completed → aftermath dialog
No repeat fallback → Voss is non-interactable outside his window.

## NPCInteractable

`[DisallowMultipleComponent]`, implements `IInteractable`. **Setup**: drop on the NPC GameObject with a trigger SphereCollider on the **Foraging layer** — `PlayerInteractor` discovers it with zero extra wiring (same as MushroomNode).

- `CanInteract`: false if no data or a dialogue/request card is already open; otherwise resolves the highest-priority story dialogue or request.
- `Interact`: opens story dialogue, the illustrated request card, or the NPC fallback supplied to **Talk instead**.
- `PromptVerb`: Talk / View order / Deliver based on the resolved interaction and live basket; `PromptTarget` stays localized NPC display name.
- Editor gizmo: blue wire sphere 1.7m up.

## NPCSchedule

`NPCSchedule` lives on an always-active host separate from the actor it controls. Its ordered `NPCScheduleSlot[]` is first-match-wins. Each slot can require an active quest, a completed quest, a set flag, an absent flag, and either an all-day destination or a start/end hour. A window whose end is earlier than its start wraps through midnight.

Schedules are deliberately derived rather than saved: the active slot is recalculated from `TimeManager`, `QuestManager`, and `GameScores` after load, at dawn/sundown, on quest/score changes, and during a lightweight unscaled poll. The clock, quest, and flag state already belong to the save schema, so there is no second source of truth to migrate or corrupt.

Routine moves are deferred while dialogue/request UI owns the NPC, interaction is suspended, or the player is within 12 metres of the actor or destination. The pending slot applies after the player leaves, avoiding visible teleports without requiring navigation AI. An immediate refresh is reserved for initialization, import/debug tooling, and focused verification.

The initial authored routines live beneath `_NPCSchedules` in `Scene_Hollowfen`:

- Theo is hidden until `theo_wagon_arrived`, trades at the wagon by day, and goes to the Crooked Pintle from 18:30–07:00 after `eddaApprentice`. While `theoCapitalOffer` is active, its higher-priority slot keeps him at the Pintle all day and the quest waypoint points there.
- Joren unlocks Pintle evenings after `forgeKnife`; otherwise he remains at the forge.
- Bram unlocks Pintle evenings after `meetAlmy`; otherwise he remains at the village well.
- Pell unlocks Pintle evenings after `cottagesReopen`; otherwise he remains at the village well.

For a new routine, parent the actor beneath `_NPCSchedules/Actors`, add named destination anchors beneath `_NPCSchedules/Anchors`, and put the `NPCSchedule` on its own active host. Author story overrides before ordinary time windows and ensure the actor is no longer nested under an unrelated `FlagActivatedObject` that can disable it.

## Adding a new NPC (checklist)

1. `NPC_<Name>.asset`: id, `npc.<id>.name` localization entry, dialogue entry table (most-specific first!), repeat dialog.
2. Speaker color entry in `DialogueScreen.SpeakerColors` (see dialogue.md).
3. Scene object: model (Meshy pipeline or placeholder) + trigger SphereCollider on Foraging layer + NPCInteractable with the asset. If scheduled, put the actor beneath `_NPCSchedules/Actors` and keep its schedule on a separate active host.
4. Relationship deltas elsewhere reference the same lowercase id.
5. Verify: prompt shows localized name; each entry's dialog reachable in the right quest state; repeat fallback plays after the arc.

## Gotchas

- Entry order is load-bearing and unvalidated — a general entry above a gated one shadows it forever (Voss only works because the coin-gated entry is first).
- Schedule order is equally load-bearing: put active-quest/story overrides before ordinary time windows and always end with the intended fallback.
- Do not attach `NPCSchedule` to an actor that can be hidden; a disabled host cannot wake itself when a later slot becomes eligible.
- `Interact` calls `PickDialog()` twice (CanInteract + Interact) — cheap, but conditions with side effects would double-fire (don't write those).
- No StoryBeats reference, no priority numbers — array order only.
