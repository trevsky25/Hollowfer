# Dialogue System
Dialogue: `DialogueData` = ordered `DialogueLine[]` + a one-shot outcome block + EITHER a linear `_nextDialog` chain OR `_choices` (PLAYER CHOICES, max 4 — added Batch 17: text + branch + optional flag; outcomes fire before choices show; each branch owns its own outcomes). NPC-side branching still happens upstream in `NPCData.PickDialog`. `DialogueScreen` renders cinematic-letterbox with typewriter, frozen timeScale; choices render as numbered pills above the panel (1-4 keys / D-pad+stick / Submit / mouse; choice 1 on top; public `SelectChoice(int)` API).
Key scripts: `Assets/_Hollowfen/Scripts/Dialogue/` — DialogueData, DialogueScreen (namespace `Hollowfen.Dialogue`). Design reference: `Docs/dialog-system.md` (web-era).
Data: `Data/Dialogue/Dialogue_ActN_<Context>_<Variant>.asset` (65 across Acts I–IV; SpeakerColors now includes Aldric); `_id` fields are decorative — asset GUID refs drive everything. Dialogues can also be played by QuestInteractables (`_playsDialogue`) — Wren's riverbed monologue and the cottage arrival are prop-anchored, not NPC-anchored.
Outcomes on finish (in order): grant key item → grant forage → CONSUME forage (`_consumeForage`+count — Marra's tonic ingredient; runs before the basket sale so it isn't also sold) → basket-sale coin math → spend coins → add coins → set flags → score/relationship deltas → unlock story card → complete quest → chain next dialog.
Biggest gotchas: line text + speaker names are RAW STRINGS (localization violation, and speaker doubles as the SpeakerColors dictionary key); screen polls input devices directly (the Dialogue action map is UNUSED); outcomes re-fire on every replay — one-shot semantics must be authored via picker conditions (flags/quest completion), or a coin-granting dialog becomes a money faucet.
Status: verified against code 2026-07-11.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## DialogueData

`ScriptableObject`, CreateAssetMenu `Hollowfen/Dialogue/Dialogue Data`.

- **`DialogueLine`** struct: `speaker` (raw display name — keys into `DialogueScreen.SpeakerColors`), `text` (`[TextArea]`; consecutive same-speaker lines merged with `\n\n` per authoring convention), `isCloseup` (⚠️ authored but UNUSED — reserved for a future cinematic camera pass).
- **Outcome fields**: `_unlockStoryCard` (StoryCardData) · `_completeQuest` (QuestData) · `_giveItemId` (→`KeyItems.Grant`) · `_grantCoinsCopper` / `_spendsCoinsCopper` (12c = 1s; spend is best-effort `CoinPurse.TrySpend`, result ignored) · `_sellsForageBasket` + `_basketCopperPerItem` (Marra's repeatable loop; count read BEFORE `InventoryRuntime.RemoveAll`) · `_grantForage` + `_grantForageCount` (MushroomFieldGuideData — Almy's spawn plugs) · `_setFlagIds[]` (→`GameScores.SetFlag`) · `_villageHopeDelta` / `_knowledgeDelta` · `_relationshipNpcIds[]`+`_relationshipDeltas[]` (parallel, min-length).
- **`_nextDialog`**: linked-list chain; each link fires its own full outcome block; screen stays open, timeScale stays 0 across the chain.
- **`_choices` (`DialogueChoice[]`)**: `text` (Wren's option) + `next` (branch dialogue, null = close) + `setsFlagId`. Shown AFTER the last line's outcomes fire; non-empty choices make `_nextDialog` ignored (integrity warns). `IsChoosing` exposes the state; selection via number keys, D-pad/stick + confirm, or mouse click.

**Two branching layers**: which dialog plays = `NPCData.PickDialog()` (npcs.md); within a dialog, `_choices` fork at the end. First on-disk consumer: Act III's `theoCapitalOffer` (batch-21). ⚠️ **`_choices` graphs must be ACYCLIC** — `DataIntegrity.HasDialogueCycle` errors on any `_nextDialog`/`_choices` loop ("traps the player at timeScale 0"). So a question→hub→question loop is illegal; batch-21 keeps the core+choices in an always-seen Intro, makes each question branch terminal, and lets the player re-ask by re-talking to the NPC (a flag routes re-talks to a lighter Hub). Post-completion, a Repeat dialogue re-offers the question branches as a hub (terminals have no outcomes, so re-entry is safe).

## DialogueScreen

Scene-local singleton (`Instance` in Awake, NOT a UIScreen — same pattern as InspectScreen/MapScreen). Procedural UI on first `Open`.

- **Open**: `Time.timeScale = 0` (previous cached) · `PlayerInteractor.Suspended = true` (this IS the player-input gate — no action-map switch) · HUD hidden via `GameObject.Find("_HUDCanvas")`/`"_MiniMapCanvas"` + CanvasGroup alpha 0 · cursor shown.
- **Close**: reverses everything; no events fired on close.
- **Input**: `Update()` polls devices directly — Space/Enter/E, gamepad South/North, mouse left. One "advance" signal: typing → `SkipTypewriter()` (instant complete); shown → `AdvanceLine()`. ⚠️ Ignores the InputActions asset entirely; rebinding won't affect it; hint text always claims "Space".
- **Typewriter**: coroutine on `WaitForSecondsRealtime` (timeScale is 0), `_typewriterCps` default 32 (matches web prototype).
- **Layout**: 110px letterbox bars · rounded parchment panel 1240×230 bottom-anchored (UICanvasUtil rounded panel + shadow, optional parchment sprite wash) · speaker name plate = ink tab riding the panel's top-left, auto-fit width, fill lerped toward the speaker accent color, gold hairline · body 27pt serif italic InkDeep · hint `"Space · continue"` (⚠️ hardcoded).
- **SpeakerColors** (static dict, hardcoded): Bram `#7a4a1a`, Wren `#5b3a6a`, Marra `#8a3a2a`, Almy `#4a6b3a`, Joren `#4d4338`, Voss `#3e4e63`, Theo `#1f6f62`, Edda `#77704f`, Hollin `#5a4a72`, Pell `#615a48`, Calden `#3d3550`; unknown speakers fall back silently to default ink. New NPC → add an entry.
- **`FinishDialog()`** fires outcomes in the order listed in the header, then chains or closes. Quest advance = `QuestManager.CompleteQuest` direct call.

## Persistence

None owned by dialogue. No seen-bits on assets. Repeat-prevention is authored via the systems dialogs write to: set a flag (e.g. `voss_first_visit_seen`) or complete a quest, and the NPC's picker conditions route to a different dialog next time. All of that persists via GameScores/QuestManager → save slot. **Consequence**: anything reachable repeatedly (repeat/waiting variants) MUST have empty or intentionally-repeatable outcomes.

## Authoring checklist

1. Lines verbatim from the bible's scene spec; merge consecutive same-speaker lines with `\n\n`.
2. Asset named `Dialogue_ActN_<Context>_<Variant>`; `_id` dotted (`act2.voss_first_visit.demand`) for tooling.
3. One-shot? Set a flag in `_setFlagIds` and require it (or quest completion) in the NPC's entry conditions.
4. New speaker? Add to `SpeakerColors` + NPCData (npcs.md) + verify name-plate render.
5. Every conversation needs a repeat/waiting fallback so NPCs never go mute (unless intentionally non-interactable, like Voss outside his window).
6. Play-mode verify with gamepad AND keyboard.

## Gotchas

- **Localization**: speaker, text, and hint all bypass `Localization.Get` — the pre-EA localization pass must restructure this (TODOS). Speaker rename also breaks color lookup silently.
- **E-key overlap**: E both interacts (opens dialogue via PlayerInteractor) and advances — works because `wasPressedThisFrame` is consumed same-frame; fragile if interaction moves to release/hold.
- **Dead code**: `BuildFrame(...)` unused; `isCloseup` unread.
- Chained dialogs stack outcome blocks — intentional, but audit when chaining.
