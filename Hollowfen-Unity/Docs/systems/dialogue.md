# Dialogue System
Strictly LINEAR dialogue: `DialogueData` = ordered `DialogueLine[]` + a one-shot outcome block + optional `_nextDialog` chain link — no choices, no branching (branching happens upstream in `NPCData.PickDialog` selection). `DialogueScreen` renders it cinematic-letterbox style with a typewriter, frozen timeScale, and fires all outcomes on finish via direct static calls.
Key scripts: `Assets/_Hollowfen/Scripts/Dialogue/` — DialogueData, DialogueScreen (namespace `Hollowfen.Dialogue`). Design reference: `Docs/dialog-system.md` (web-era).
Data: `Data/Dialogue/Dialogue_ActN_<Context>_<Variant>.asset` (19 across Acts I–II); `_id` fields are decorative — asset GUID refs drive everything.
Outcomes on finish (in order): grant key item → grant forage → basket-sale coin math → spend coins → add coins → set flags → score/relationship deltas → unlock story card → complete quest → chain next dialog.
Biggest gotchas: line text + speaker names are RAW STRINGS (localization violation, and speaker doubles as the SpeakerColors dictionary key); screen polls input devices directly (the Dialogue action map is UNUSED); outcomes re-fire on every replay — one-shot semantics must be authored via picker conditions (flags/quest completion), or a coin-granting dialog becomes a money faucet.
Status: verified against code 2026-07-11.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## DialogueData

`ScriptableObject`, CreateAssetMenu `Hollowfen/Dialogue/Dialogue Data`.

- **`DialogueLine`** struct: `speaker` (raw display name — keys into `DialogueScreen.SpeakerColors`), `text` (`[TextArea]`; consecutive same-speaker lines merged with `\n\n` per authoring convention), `isCloseup` (⚠️ authored but UNUSED — reserved for a future cinematic camera pass).
- **Outcome fields**: `_unlockStoryCard` (StoryCardData) · `_completeQuest` (QuestData) · `_giveItemId` (→`KeyItems.Grant`) · `_grantCoinsCopper` / `_spendsCoinsCopper` (12c = 1s; spend is best-effort `CoinPurse.TrySpend`, result ignored) · `_sellsForageBasket` + `_basketCopperPerItem` (Marra's repeatable loop; count read BEFORE `InventoryRuntime.RemoveAll`) · `_grantForage` + `_grantForageCount` (MushroomFieldGuideData — Almy's spawn plugs) · `_setFlagIds[]` (→`GameScores.SetFlag`) · `_villageHopeDelta` / `_knowledgeDelta` · `_relationshipNpcIds[]`+`_relationshipDeltas[]` (parallel, min-length).
- **`_nextDialog`**: linked-list chain; each link fires its own full outcome block; screen stays open, timeScale stays 0 across the chain.

**No branching model exists.** Which dialog plays is decided by `NPCData.PickDialog()` (see npcs.md); once playing, it runs start-to-finish.

## DialogueScreen

Scene-local singleton (`Instance` in Awake, NOT a UIScreen — same pattern as InspectScreen/MapScreen). Procedural UI on first `Open`.

- **Open**: `Time.timeScale = 0` (previous cached) · `PlayerInteractor.Suspended = true` (this IS the player-input gate — no action-map switch) · HUD hidden via `GameObject.Find("_HUDCanvas")`/`"_MiniMapCanvas"` + CanvasGroup alpha 0 · cursor shown.
- **Close**: reverses everything; no events fired on close.
- **Input**: `Update()` polls devices directly — Space/Enter/E, gamepad South/North, mouse left. One "advance" signal: typing → `SkipTypewriter()` (instant complete); shown → `AdvanceLine()`. ⚠️ Ignores the InputActions asset entirely; rebinding won't affect it; hint text always claims "Space".
- **Typewriter**: coroutine on `WaitForSecondsRealtime` (timeScale is 0), `_typewriterCps` default 32 (matches web prototype).
- **Layout**: 110px letterbox bars · rounded parchment panel 1240×230 bottom-anchored (UICanvasUtil rounded panel + shadow, optional parchment sprite wash) · speaker name plate = ink tab riding the panel's top-left, auto-fit width, fill lerped toward the speaker accent color, gold hairline · body 27pt serif italic InkDeep · hint `"Space · continue"` (⚠️ hardcoded).
- **SpeakerColors** (static dict, hardcoded): Bram `#7a4a1a`, Wren `#5b3a6a`, Marra `#8a3a2a`, Almy `#4a6b3a`, Joren `#4d4338`, Voss `#3e4e63`; unknown speakers fall back silently to default ink. New NPC → add an entry.
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
