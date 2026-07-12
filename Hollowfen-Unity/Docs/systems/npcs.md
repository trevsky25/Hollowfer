# NPC System
NPC = `NPCData` SO (id, localized display name, ordered condition→dialog table, repeat fallback) + `NPCInteractable` scene component riding the shared `IInteractable`/Foraging-layer convention. ALL dialogue "branching" lives here: `PickDialog()` walks the entry table in author order, first full match wins, else `_repeatDialog`.
Key scripts: `Assets/_Hollowfen/Scripts/NPCs/` — NPCData, NPCInteractable (namespace `Hollowfen.NPCs`).
Data: `Data/NPCs/NPC_<Name>.asset` — Bram, Marra, Almy, Joren, Voss, Theo, Edda, Hollin, Pell, Calden, Aldric (11). Ids lowercase (`voss`), matching relationship ids in dialogue/quest score deltas.
Entry conditions (ANDed, unset = skip): `activeQuest` (IsActive) · `requiresQuestCompleted` · `requiresFlagId` (GameScores) · `requiresCoinsCopper` (≥) · `requiresBasketNonEmpty` · `requiresForage` (species SO — basket holds ≥1 of it; Marra's tonic gate).
Biggest gotchas: ENTRY ORDER IS THE PRIORITY SYSTEM (specific/gated entries must be authored above general ones — no validation); null `_repeatDialog` + no match = NPC shows NO prompt at all (intentional for Voss outside his quest window, easy to misread as a bug). ⚠️ A broad `requiresQuestCompleted` idle/repeat entry must sit BELOW a trade NPC's basket-sale entry, or it permanently shadows the sale loop (batch-21 fable catch: Theo's Capital-offer repeat was starving his `requiresBasketNonEmpty` sale until reordered).
Status: Theo/Edda/Hollin/Calden staged with flag-gated presence, Pell always-on at the well. Hollin has TWO placements sharing one NPCData (inn + mill; the inn host's `_offFlagId=hollin_at_mill` yields to the mill on Act III start) — PickDialog is data-level, so both speak identically. Act III B (batch-19): Calden routes records→reconcile via a `calden_records_read` flag gate (set overnight by DayFlagScheduler) then a post-arc repeat gated on `requiresQuestCompleted: caldenReconcile`; Edda gains an apprentice-acceptance entry + post-arc repeat. Act IV (batch-25): Bram/Almy/Hollin gained a "consult about Aldric's offer" entry at the TOP, gated on `npc_consultations_unlocked` (inert until Act IV) — ⚠️ future Act IV active-quest entries for these NPCs must sit ABOVE the consult entry. Act IV scene 3 (batch-27): **Lord Aldric now exists** — the first physical antagonist, a placeholder capsule using the dual-placement pattern: an always-active `_Aldric` FlagActivatedObject host toggles the (initially inactive) `AldricGroup` on `wend_source_visited`, so Aldric appears at the new `manor` LocationData (365, 34.22, 190) exactly when `meetAldric` becomes active. NPC_Aldric has a single entry (active `meetAldric` → `Dialogue_Act4_MeetAldric`) and null `_repeatDialog` — silent after the meeting by design (the ending sequence supersedes him). Whole cast now placed; remaining is model-swap polish (placeholder capsule → Meshy).

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## NPCData

CreateAssetMenu `Hollowfen/NPCs/NPC Data`. Fields: `_id` (lowercase key) · `_displayNameId` (localization key, e.g. `npc.voss.name` — resolved by the prompt HUD) · `_dialogueEntries` (`NPCDialogueEntry[]`, private — consumed only by PickDialog) · `_repeatDialog` (fallback; MAY be null).

**`PickDialog()`**: first entry whose conditions ALL pass → its `dialog`; none → `_repeatDialog` (possibly null). Entries with null `dialog` are skipped. Mirrors the web prototype's per-NPC picker functions.

**Worked example — `NPC_Voss.asset`** (most-specific-first ordering):
1. `firstTax` active + flag `voss_first_visit_seen` + ≥144 copper → payment dialog
2. same quest + flag, no coin gate → "still short" dialog
3. quest active, no flag → first-visit demand (which sets the flag itself)
4. quest completed → aftermath dialog
No repeat fallback → Voss is non-interactable outside his window.

## NPCInteractable

`[DisallowMultipleComponent]`, implements `IInteractable`. **Setup**: drop on the NPC GameObject with a trigger SphereCollider on the **Foraging layer** — `PlayerInteractor` discovers it with zero extra wiring (same as MushroomNode).

- `CanInteract`: false if no data, false if `DialogueScreen.Instance.IsOpen` (no dialog-over-dialog), else `PickDialog() != null`.
- `Interact`: re-picks and `DialogueScreen.Instance.Open(dlg)`; warns if no DialogueScreen in scene.
- `PromptVerb => "prompt.npc.talk"` (localization id); `PromptTarget` = `Localization.Get(DisplayNameId)`.
- Editor gizmo: blue wire sphere 1.7m up.

## Adding a new NPC (checklist)

1. `NPC_<Name>.asset`: id, `npc.<id>.name` localization entry, dialogue entry table (most-specific first!), repeat dialog.
2. Speaker color entry in `DialogueScreen.SpeakerColors` (see dialogue.md).
3. Scene object: model (Meshy pipeline or placeholder) + trigger SphereCollider on Foraging layer + NPCInteractable with the asset.
4. Relationship deltas elsewhere reference the same lowercase id.
5. Verify: prompt shows localized name; each entry's dialog reachable in the right quest state; repeat fallback plays after the arc.

## Gotchas

- Entry order is load-bearing and unvalidated — a general entry above a gated one shadows it forever (Voss only works because the coin-gated entry is first).
- `Interact` calls `PickDialog()` twice (CanInteract + Interact) — cheap, but conditions with side effects would double-fire (don't write those).
- No StoryBeats reference, no priority numbers — array order only.
