# Batch 51 — The Hidden Journal: first lessons + Tobin's note (mill-mission slice)

**Date:** 2026-07-13 · **Status:** IN PROGRESS · tag `batch-51` (pending)
**Directive (Trevor):** The mill mission (Act I Scenes 2–4) — the big one. HYBRID: the 3D world does the
interactions; bespoke Codex PAINTINGS only for the journal interior close-ups.

## Scoping the mill mission (discovery — the spine is already built)
A full-repo map (subagent) + scene inspection found the **spine already wired and working**:
- Bram's `Dialogue_Act1_CrookedPintle_BramKey` grants `item.mill_key` + completes `speakBram` → auto-chains
  to `searchMill`. VO recorded.
- `MillDoor_Lock` (KeyLockedDoor, `_requiredItemId: item.mill_key`) completes `searchMill` → chains to
  `findJournal`. Real mill building exists (`BasicBuilding3`: floor/inside-walls/ceiling/roof/door at ~233,33,318).
- `Journal_FathersJournal` (QuestInteractable) grants `item.fathers_journal` + completes `findJournal` →
  chains to `firstForage` + unlocks `StoryCard_04_HiddenJournal`.

**Real gaps found:**
1. The journal **teaches nothing** — `_discoversSpecies` empty. Bible: findJournal opens the Field Guide on
   Field Cap / Wood Ear / Pinecrest. ← FIX THIS BATCH (data).
2. The journal **shows no note** — `_playsDialogue` empty. Bible: Tobin's farewell note, live Georgia text.
   ← FIX THIS BATCH (small QuestInteractable narration hook + localized passage).
3. **BLOCKED:** the journal painted spreads (`journal-01-sketches.png` …) are NOT in the repo yet — Trevor is
   generating them in Codex. The `ShowCinematic` painted-spread finale + the focus push-in are deferred.
4. Interior "search the house" inspect props (coat/kettle/ledgers/wheel/drawer) + key/journal cinematic
   pickups need meshes the kitbash packs don't have (no coat/kettle/key/book models). Deferred → TODOS/QUESTIONS.

## This batch (unblocked, bible-faithful, mesh-free, data + tiny generic hook)
- **QuestInteractable.cs**: new optional `_playsNarrationId` — on use, localized passage split on blank lines
  → `NarrationOverlay.Instance.Show(captions)` (live Georgia captions, the SAME overlay as the intro; no
  painted art). Generic ("examine → read a passage"), reusable.
- **Localization**: `act1.hidden_journal.tobin_note` — the journal reveal + Tobin's farewell note, bible verbatim
  where possible (recipes → Tobin's writing → Field Cap/Wood Ear/Pinecrest → "never eat what you cannot name
  twice" → the note → "— Da" → Wren: "Da.").
- **Journal_FathersJournal**: `_discoversSpecies` = [FieldCap, WoodEar, Pinecrest] (teaches the first three;
  Goldfoot stays an unfinished margin note per bible — NOT discovered here); `_playsNarrationId` =
  `act1.hidden_journal.tobin_note`.

## Verification (play mode — fresh save: backed up + cleared Trevor's slots, restored after)
- [x] Compiles clean (0 errors).
- [x] Fresh state (activeQuest `arrive`, nothing discovered). Forced `findJournal` active → `qi.Interact(player)`:
  - fieldCap **discovered**, woodEar **discovered**, pinecrest **discovered**; **goldfoot NOT discovered**
    (unfinished margin note per bible ✓).
  - `item.fathers_journal` granted; `findJournal` completed → auto-chained to `firstForage`;
    `StoryCard_04_HiddenJournal` unlocked; `NarrationOverlay.IsShowing == true`.
- [x] Tobin's-note narration renders in **Georgia italic on black** — "The first pages were recipes, in her
      mother's hand." + "Press Space to continue" (`b51_journal_note.png`). Passage splits into the intended
      **7 captions** (recipes → Field Cap/Wood Ear/Pinecrest → "never eat what you cannot name twice" →
      the 3 note beats → "Da."), verified via the localized-string split.
- Saves restored (byte-for-byte). Scene diff clean: journal `_discoversSpecies`+`_playsNarrationId` only
  (+ harmless new-field serialization + a stale `_gamepadGlyph` auto-cleanup); reverted play-mode rotation
  churn before committing. No SDF/font churn.

## 51b — journal painted-spread finale (DONE this session — Trevor dropped the PNGs)
Trevor delivered `journal-01-sketches.png` / `journal-02-spores.png` / `journal-03-note.png` (1672×940,
imported as Sprites). Upgraded the journal's narration from plain black `NarrationOverlay.Show` to the
multi-image `ShowCinematic` (crossfade + Ken Burns over the paintings, live Georgia captions on top).
- **QuestInteractable.cs**: new `_narrationHeroes` (Sprite[]) + `_narrationBeatImages` (int[]). When
  `_narrationHeroes` is set, the passage plays via `ShowCinematic(captions, null, heroes, beatImage, null)`;
  else the black-caption `Show` path (back-compat).
- **Journal_FathersJournal**: `_narrationHeroes` = [sketches, spores, note]; `_narrationBeatImages` =
  [0,0,1,2,2,2,2] → opens on the pencil sketches (recipes / "Tobin's writing began" / the 3 names),
  dissolves to the full ink spread ("careful sketch… never eat what you cannot name twice"), dissolves to
  the near-blank note page for Tobin's farewell note + "Da."
- The 3D **camera push-in into the book** stays with 51d (needs the reusable prop-focus cinematic; the
  journal deactivates on use, so a push-in coroutine must run off a persistent object). The ShowCinematic
  fade-in over the 3D journal already provides the dissolve-to-paintings transition.

### 51b verification (play mode, fresh save — backed up + cleared, restored from the pristine session-start copy)
- [x] Imported the 3 PNGs as Sprites (1672×940). Compiles clean.
- [x] Journal interact → `ShowCinematic` plays the painted finale, crossfading + Ken-Burning the paintings
      under the letterbox with the live Georgia captions:
  - **img0** journal-01-sketches — "The first pages were recipes, in her mother's hand." (`b51b_img0.png`)
  - **img1** journal-02-spores — dissolve landed on "Each a careful sketch… never eat what you cannot name
    twice." (`b51b_img1b.png`)
  - **img2** journal-03-note — "If you're reading this, I never told you." on the near-blank page, Wren's
    hand visible bottom-right (`b51b_img2.png`) — the live note text sits on the unpainted page exactly as
    Trevor intended.
- [x] Ends clean: `IsShowing==false`, player un-suspended, quest chained to `firstForage`, findJournal done.
- Saves restored to the pristine session-start copy (a fresh-test autosave had written slot2; caught via
  size compare and rolled back). Scene diff = journal sprite refs + beat array only (+ harmless empty
  new-field defaults on the other props). No SDF/font/rotation churn.

### Deferred (split into TODOS; blockers in QUESTIONS)
- **51c — mill interior "search the house" inspect props** (coat/kettle/ledgers/window-wheel/drawer +
  `mill.*`/`act1.fathers_mill.wren.inspect` barks, anchored for the cinematic camera): needs meshes the
  kitbash packs lack (no coat/kettle/book/key models — graphics-pipeline). Interior exists (`BasicBuilding3`).
- **51d — cinematic key/journal pickups** (focus push-in + KeyItemToast): needs a reusable prop-focus
  cinematic (MushroomFocusCamera only auto-frames MushroomNode) + a key mesh.
