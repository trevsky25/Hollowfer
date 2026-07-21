# Dialogue System
Dialogue: `DialogueData` = ordered `DialogueLine[]` + optional live `_mushroomHandoff` + a one-shot outcome block + optional `_transitionMoment` + EITHER `_nextDialog` or `_choices` (max 4). A `DialogueChoice` may branch to another dialogue, set a flag, or reference a terminal `EndingData`. Ending choices are eligibility-aware and disabled with authored reasons. `DialogueScreen` renders cinematic letterbox/typewriter at frozen time; choices use keyboard, controller, or mouse.
Key scripts: `Assets/_Hollowfen/Scripts/Dialogue/` — DialogueData, DialogueScreen, DialogueCinematics (batch-45 procedural camera director; namespace `Hollowfen.Dialogue`). Design reference: `Docs/dialog-system.md` (web-era).
Data: `Data/Dialogue/Dialogue_ActN_<Context>_<Variant>.asset` plus four `Dialogue_Ending_*` resolutions (75 total); `_id` fields are decorative — asset GUID refs drive everything.
Outcomes on finish (in order): grant key item → grant forage → CONSUME forage (`_consumeForage`+count — Marra's tonic ingredient; runs before the basket sale so it isn't also sold) → buyer-aware basket sale → spend coins → add coins → set flags → score/relationship deltas → unlock story card → complete quest → chain next dialog.
Biggest gotchas: line text + speaker names are RAW STRINGS (localization violation, and speaker doubles as the SpeakerColors dictionary key); dialogue keeps E/Y/mouse aliases outside the action asset; outcomes re-fire on every replay — one-shot semantics must be authored via picker conditions (flags/quest completion), or a coin-granting dialog becomes a money faucet. Live handoffs are presentation-only and must never mutate inventory.
Status: choice branching, recoverable terminal ending handoff, illustrated narrative presentation, nearby multi-speaker live-model framing, data-authored 3D mushroom transfer, species-aware Marra/Theo sale routing, purse-ledger outcome reasons, and Bram's Act I scenes are verified through 2026-07-18.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## DialogueData

`ScriptableObject`, CreateAssetMenu `Hollowfen/Dialogue/Dialogue Data`.

- **`DialogueLine`** struct: `speaker` (raw display name — keys into `DialogueScreen.SpeakerColors`), `text` (`[TextArea]`; consecutive same-speaker lines merged with `\n\n` per authoring convention), `isCloseup` (batch-45: LIVE — a true value gives that line a tight single via DialogueCinematics; mark emotional climax beats).
- **`_mushroomHandoff` (`DialogueMushroomHandoffCue`)**: optional `beforeLineIndex` + named `recipientSpeaker` + canonical `MushroomFieldGuideData` + presentation height. `DialogueScreen` pauses before that line, hides only the parchment panel (letterbox stays), and asks `DialogueCinematics` to transfer the species' journal-preview model from Wren's live hand to the recipient. This cue never adds/removes forage; authoritative inventory stays in the outcome block. First authored use: First Basket index 10, Goldfoot → Marra, immediately before Marra says “Goldfoots.” Repeat dialogues and missing/out-of-range cue data are integrity errors.
- **Outcome fields**: `_unlockStoryCard` (StoryCardData) · `_completeQuest` (QuestData) · `_giveItemId` (→`KeyItems.Grant`) · `_grantCoinsCopper` / `_spendsCoinsCopper` (12c = 1s; spend is best-effort `CoinPurse.TrySpend`, result ignored) · `_sellsForageBasket` + `_basketBuyer` + legacy `_basketCopperPerItem` · `_grantForage` + `_grantForageCount` (MushroomFieldGuideData — Almy's spawn plugs) · `_setFlagIds[]` (→`GameScores.SetFlag`) · `_villageHopeDelta` / `_knowledgeDelta` · `_relationshipNpcIds[]`+`_relationshipDeltas[]` (parallel, min-length).
  - When `_basketBuyer` is Marra or Theo, `InventoryRuntime.SellTo` reads each species' buyer value, removes only accepted stock, preserves refused items, and returns the copper total. Canonical repeat-sale dialogues use this path.
  - `MushroomBuyer.None` preserves the old flat `_basketCopperPerItem × TotalCount` behavior and empties the basket. Keep it only for authored one-off beats such as Marra's first pot, whose story payment must remain stable.
  - Merchant sales enter `CoinPurse` as Marra/Theo sale ledger rows; fixed grants use `A promise paid`; spends use `Village purchase`. The outcome block still emits only one highest-priority feedback cue.
- **`_nextDialog`**: linked-list chain; each link fires its own full outcome block; screen stays open, timeScale stays 0 across the chain.
- **Act I Bram opening**: `Homecoming_Bram` has two recognition turns and chains into the three-turn key exchange. The key node has no `_nextDialog`, so its one-line direction repeat appears only on a later interaction; the player is no longer forced through a 20-advance exchange.
- **`_transitionMoment`**: optional `StoryMomentData` played after this node's outcomes and before choices/`_nextDialog`. Input and time remain dialogue-owned while the painted overlay nests safely; repeat dialogue is forbidden from referencing one.
- **`_choices` (`DialogueChoice[]`)**: `text` + `next` + `setsFlagId` + optional terminal `ending`. `next` and `ending` are mutually exclusive (integrity errors); ending consequences live on `EndingData`, never on the loose choice flag. `EndingResolver.Evaluate` disables unavailable pills, skips them during navigation, and supplies the explanation in the dialogue panel.

**Two branching layers**: which dialog plays = `NPCData.PickDialog()` (npcs.md); within a dialog, `_choices` fork at the end. First on-disk consumer: Act III's `theoCapitalOffer` (batch-21). ⚠️ **`_choices` graphs must be ACYCLIC** — `DataIntegrity.HasDialogueCycle` errors on any `_nextDialog`/`_choices` loop ("traps the player at timeScale 0"). So a question→hub→question loop is illegal; batch-21 keeps the core+choices in an always-seen Intro, makes each question branch terminal, and lets the player re-ask by re-talking to the NPC (a flag routes re-talks to a lighter Hub). Post-completion, a Repeat dialogue re-offers the question branches as a hub (terminals have no outcomes, so re-entry is safe).

## DialogueScreen

Scene-local singleton (`Instance` in Awake, NOT a UIScreen — same pattern as InspectScreen/MapScreen). Procedural UI on first `Open`.

- **Open**: acquires `NarrativePresentationSession.Modal + HideGameplayHud`, which reference-counts frozen time, gameplay-input suspension, shortcut blocking, free cursor, and exact HUD state across nested presentation owners. A duplicate `Open` while dialogue is already active is ignored.
- **Close**: disposes that lease. Resources restore only when their final owner releases. `Open(dialog, anchor, onCompleted)` invokes its callback only after natural completion; manual close/cancel does not. `EndingDirector` uses that contract to sequence final dialogue → epilogue → credits.
- **Input**: enables the project `Dialogue` action map while open. `Advance` handles Space/Enter/South; E, North, and mouse-left remain interaction-friendly aliases. An opening-frame plus 0.18s unscaled grace prevents the interaction press that opened dialogue from also advancing its first line. `Skip` (Esc/East) walks the remaining linear chain while applying every node's authored outcomes and invoking natural-completion callbacks; it stops at any required choice. Choice1–4 use `1–4` / D-Pad up-right-down-left directly, with W/S or left stick + Space/Enter/South as a sequential fallback. Hints follow the last meaningful keyboard/mouse or gamepad input.
- **Live handoff pause**: when the current node's cue targets the next line, line/VO input pauses, `SpeakerChanged(null)` settles talking animation, and `DialoguePanel` hides while the existing modal lease remains authoritative. Completion restores the panel and shows the promised line; close/cancel destroys the temporary prop without invoking the continuation.
- **Typewriter**: coroutine on `WaitForSecondsRealtime` (timeScale is 0), `_typewriterCps` default 32 (matches web prototype).
- **Layout**: 110px letterbox bars · rounded parchment panel 1240×230 bottom-anchored (UICanvasUtil rounded panel + shadow, optional parchment sprite wash) · speaker name plate = ink tab riding the panel's top-left, auto-fit width, fill lerped toward the speaker accent color, gold hairline · body 27pt serif italic InkDeep · last-used-device continue/skip/choice hint (still English-only pending the localization restructure).
- **SpeakerColors** (static dict, hardcoded): Bram `#7a4a1a`, Wren `#5b3a6a`, Marra `#8a3a2a`, Almy `#4a6b3a`, Joren `#4d4338`, Voss `#3e4e63`, Theo `#1f6f62`, Edda `#77704f`, Hollin `#5a4a72`, Pell `#615a48`, Calden `#3d3550`; unknown speakers fall back silently to default ink. New NPC → add an entry.
- **`FinishDialog()`** fires outcomes in the order listed in the header, then chains or closes. Quest advance = `QuestManager.CompleteQuest` direct call.

## Persistence

None owned by dialogue. No seen-bits on assets. Repeat-prevention is authored via the systems dialogs write to: set a flag (e.g. `voss_first_visit_seen`) or complete a quest, and the NPC's picker conditions route to a different dialog next time. All of that persists via GameScores/QuestManager → save slot. **Consequence**: anything reachable repeatedly (repeat/waiting variants) MUST have empty or intentionally-repeatable outcomes. A quest-completing dialogue that presents choices after its outcome autosave must also have a persisted NPC recovery route; Aldric requires completed `meetAldric` + `final_choice_available` and is blocked by `game_complete`.

## Authoring checklist

1. Lines verbatim from the bible's scene spec; merge consecutive same-speaker lines with `\n\n`.
2. Asset named `Dialogue_ActN_<Context>_<Variant>`; `_id` dotted (`act2.voss_first_visit.demand`) for tooling.
3. One-shot? Set a flag in `_setFlagIds` and require it (or quest completion) in the NPC's entry conditions.
4. New speaker? Add to `SpeakerColors` + NPCData (npcs.md) + verify name-plate render.
5. Every conversation needs a repeat/waiting fallback so NPCs never go mute (unless intentionally non-interactable, like Voss outside his window).
6. Play-mode verify with gamepad AND keyboard.

## Gotchas

- **Localization**: speaker, text, and dynamic input hints bypass `Localization.Get` — the pre-EA localization pass must restructure this (TODOS). Speaker rename also breaks color lookup silently.
- **E-key overlap**: E both interacts and advances. Dialogue's opening-frame/time grace deliberately absorbs the initiating press; preserve that guard if interaction moves to release/hold.
- **Dead code**: `BuildFrame(...)` unused. (`isCloseup` went live in batch-45.)
- Chained dialogs stack outcome blocks — intentional, but audit when chaining.

## DialogueCinematics (batch-45; coverage tuned batch-49)

Procedural camera director — `Scripts/Dialogue/DialogueCinematics.cs`, lazy scene-local singleton
(`Ensure()`), owns Camera.main while a dialogue plays. It indexes active NPC interactables and
speaker animators staged within 18m of the interaction anchor, then resolves every authored speaker
name to that live transform. Framing is computed from Wren and the current speaker's head positions
(SMR bounds), with the interaction anchor as fallback — zero per-dialogue transform authoring.

- **Entry**: `DialogueScreen.Open(dialog, Transform anchor)` — NPCInteractable and QuestInteractable
  pass `transform`. The anchorless `Open(dialog)` keeps the old static camera (no direction).
- **Multi-speaker staging**: a non-Wren line selects the matching nearby NPC by localized NPC name or
  `DialogueSpeakerAnimator.SpeakerName`. Marra's First Sale quest places Bram at `Bram_Pintle`, so its
  opening Bram line frames and animates Bram instead of silently reusing Marra's anchor.
- **Mushroom handoff insert (batch-105)**: `PlayMushroomHandoff` remains inside this camera owner so no
  second director can fight it. It resolves the named recipient, chooses each humanoid hand nearest the
  other participant (body-aware fallback when no valid avatar exists), normalizes the canonical preview
  model to an authored world height, disables gameplay physics/harvest scripts, and moves it on a 1.22s
  eased arc. The shot glides to a 28° chest-and-hands insert at 1.9m, adds a prop-local warm fill for
  night readability, holds in Marra's hand, destroys the temporary instance, then lets the next line
  choose its normal coverage. `End`/destroy cancels the coroutine and prop without firing its callback.
- **Grammar (coverage mode, batch-49)**: CUT to an establishing side-on two-shot at open → each line
  picks a shot by LENGTH, not by speaker. `wantSingle = isCloseup || estSeconds >= LongLineSeconds(4.2)`.
  - **Short/rapid line → favor two-shot** (`FavorShot`): the wide frame held, LOOK panned ~34% toward
    the talker, FOV 37. Both stay in frame. Within two-shot mode a speaker change is a gentle 0.7s favor
    pan; the SAME speaker holds (push-drift only). This is what kills the batch-45 back-and-forth whip —
    a rapid Bram↔Wren exchange no longer re-glides an over-the-shoulder single on every line.
  - **Long line / `isCloseup` → tight single** (OTS FOV 34 / closeup FOV 29): the only committed camera
    move, a ~1.1s smoothstep glide. Same-speaker singles deepen the push instead of re-gliding.
  - `_shotMode` (Two/Single) tracks which we're in so a mode change is the trigger for a real glide.
  - choices settle back to the two-shot → Close glides to the cached gameplay pose and re-enables the
    CinemachineBrain.
- **180° rule**: one side vector (`cross(up, A→B)`, flipped toward the camera's starting side) fixed at
  Begin; every shot stays on it, so reverse angles never cross the line.
- **Body-aware framing**: OTS and closeup offsets scale with `ShoulderRadius` (SMR bounds) — Bram's bulk
  doesn't swallow the frame, Wren's doesn't leave it empty.
- **Life**: per-line push-in paced by VO length (or a cps estimate), Perlin handheld sway (±8mm/±0.15°,
  damped in batch-49) over everything; both speakers' Animators flipped to UnscaledTime for the scene
  (restored on end) so idles keep breathing through the timeScale-0 freeze.
- **Occlusion**: linecast subject→camera (QueryTriggerInteraction.Ignore, speakers' own colliders
  excluded) pulls the camera to 88% of any hit.
- Gotcha: the director runs in LateUpdate on unscaled time; anything else that writes Camera.main during
  a dialogue will fight it. MushroomFocusCamera can't engage (dialogue suspends PlayerInteractor focus).

## PropFocusCinematic (batch-52; hold + arc batch-62)

Reusable "hero item" push-in — `Scripts/Cinematics/PropFocusCinematic.cs`, self-created persistent
singleton (survives the interacted prop deactivating). Takes over Camera.main (disables the
CinemachineBrain, caches the gameplay pose), pushes in on a target's renderer-bounds centre, holds
while the prop does its own hero animation, glides back, re-enables the brain. Dressing every focus
shares: hides `_HUDCanvas`/`_MiniMapCanvas` and slides in 12%-height letterbox bars. Used by
MillKeyHandoff (the floating mill key), KeyLockedDoor (keyhole), and QuestInteractable (journal reveal).
- `Play(target, distance, height, fov, push, hold, restore, onPeak, onDone, frameDir, arcDegrees, arcRise, holdAtEnd)`.
- **Arc (batch-62)**: `arcDegrees` swings the approach direction out and eases it to 0 over the push;
  `arcRise` starts the approach raised and settles it down — a curved dolly onto the framing instead of a
  flat one. The mill-key handoff uses arcDegrees 40 / arcRise 0.14 with a negative height (low hero angle
  looking up as the key catches the light).
- **Hold-at-end (batch-62)**: `holdAtEnd:true` leaves the camera PARKED on the prop after the hold (letterbox
  up, HUD hidden, input suspended, brain still off) and fires `onDone` instead of gliding back; a later
  `Restore()` runs the glide-out. This lets the journal reveal dissolve its painted spreads in over the held
  book close-up (see menu-pages.md / QuestInteractable) rather than snapping in after a pointless glide back
  to the room. `IsHeld` reports the parked state; `Restore(onDone)` is a no-op unless held.
- Gotcha: like DialogueCinematics it owns Camera.main; don't run both at once. MillKeyHandoff waits on
  `DialogueCinematics.IsActive` before starting so the two takeovers never fight.
