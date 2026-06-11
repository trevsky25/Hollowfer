# Dialog System Reference

Use this as the canonical reference for building new character conversations in Hollowfen. The system was tuned to match the parchment-journal aesthetic and the cinematic feel established with Bram's Act I scenes — keep new dialogs consistent with those rules.

## TL;DR

A dialog is a **data file entry** in [src/data/Dialogs.js](../src/data/Dialogs.js). When the player presses E near an NPC, [main.js](../src/main.js) picks the right dialog ID for that NPC's current state and calls `dialog.start(id)`. The [DialogController](../src/ui/Dialog.js) renders the parchment overlay, runs the typewriter, fires animation triggers at the right beats, and signals the cinematic camera to reframe between speakers.

Files involved:
- **Data**: [src/data/Dialogs.js](../src/data/Dialogs.js) — every dialog tree, plus per-NPC pickers
- **UI**: [src/ui/Dialog.js](../src/ui/Dialog.js) — parchment panel, letterbox, vignette, typewriter
- **Wiring + camera**: [src/main.js](../src/main.js) — `setCinematicShotForSpeaker`, `cinematicCam`, `pickDialogForBram`, the keydown handler that opens dialog on E

## Authoring rules — match Bram's Act I as the reference

1. **One speaker per turn.** Consecutive lines from the same speaker in `docs/story.md` get **merged into a single entry** with `\n\n` between sentences. Never make the player press Space twice for one speaker.
2. **Use the lines verbatim from `docs/story.md`.** No paraphrasing. The dialog ID matches the `Dialogue ID:` marker in the story doc.
3. **Tag 1–2 emotional climax lines per dialog with `shot: 'closeup'`.** Reserve close-ups for genuine emotional beats (apologies, recognitions, hard truths). The default wide shot is for normal beats.
4. **First line of every dialog should fire the speaker's `talking` animation** via a trigger at `atLine: 0`.
5. **Item handoffs / physical actions** get a one-shot animation trigger at the exact narrative beat (e.g. Bram's `handoff` fires on *"Your da left this with me when he took poorly."*).
6. **Chain dialogs via `outcome.nextDialog`** when one scene flows directly into another — DialogController keeps the letterbox up across linked dialogs so it reads as one continuous take.
7. **A "repeat" dialog with 2–4 lines** for when the player talks to an NPC after a story beat is finished. Light, no triggers, no closeups.

## Data shape

```js
'act1.crooked_pintle.bram.key': {
  speaker: 'Bram',                 // primary speaker (used as fallback)
  title: 'Old Bram',               // shown in NPC labels (not currently rendered)
  lines: [
    { speaker: 'Bram', text: '...' },
    { speaker: 'Wren', text: '...' },
    { speaker: 'Bram', text: '...', shot: 'closeup' },  // emotional beat
    // ... merge consecutive same-speaker lines with '\n\n'
  ],
  triggers: [
    { atLine: 0, action: 'talking', target: 'bram' },   // start animation
    { atLine: 2, action: 'handoff', target: 'bram' }    // one-shot at narrative beat
  ],
  outcome: {
    unlockCard: 'crooked_pintle',           // story card to unlock
    giveItem: 'item.mill_key',              // inventory grant
    completeQuest: 'speakBram',             // quest log update
    nextDialog: 'act1.crooked_pintle.bram.repeat'  // auto-chain into the next dialog
  }
}
```

### Per-line fields

- `speaker` — `'Bram' | 'Wren' | 'Marra' | ...` Drives speaker label color and which character the cinematic shot biases toward.
- `text` — the line. Use `\n\n` between merged sentences from the same speaker.
- `shot` — `'wide'` (default) | `'closeup'`. Use closeup sparingly: 1–2 per dialog, on emotional climaxes.

### Trigger fields

- `atLine` — 0-based index into the *merged* `lines` array.
- `action` — must be a clip name on the target villager: `idle`, `talking`, `walking`, `handoff`. Looping clips (`idle`, `talking`, `walking`) call `playVillagerAction`; `handoff` is currently the only one-shot. Add new one-shots in main.js's `onTrigger` handler if needed.
- `target` — `'bram' | 'marra'` (whichever villager has animations loaded).

### Outcome fields (all optional)

- `unlockCard` — story card ID (matches entries in [src/data/StoryCards.js](../src/data/StoryCards.js))
- `giveItem` — item ID, e.g. `'item.mill_key'`
- `completeQuest` — quest ID
- `nextDialog` — ID of the next dialog to chain into. Letterbox + vignette stay up; only the panel content cross-fades. The chain handler is inside DialogController, not main.js.

## Adding a new NPC dialog

1. **Write or copy lines from `docs/story.md`** into a new entry in `DIALOGS`. Keep the dialog ID matching the `Dialogue ID:` marker.
2. **Merge consecutive same-speaker lines** with `\n\n`. Recompute trigger `atLine` indices against the merged array.
3. **Tag emotional climaxes** with `shot: 'closeup'` (max 1–2 per dialog).
4. **Add triggers** for animation cues — at minimum a `talking` trigger at `atLine: 0`.
5. **Add an outcome** if the conversation grants an item, unlocks a card, completes a quest, or chains.
6. **Add a picker function** if the NPC doesn't have one yet. Pattern matches `pickDialogForBram` in [Dialogs.js](../src/data/Dialogs.js):

   ```js
   export function pickDialogForMarra(state) {
     if (!state.marraIntroDone) return 'act1.marra.intro';
     if (!state.marraFirstSale) return 'act1.marra.first_sale';
     return 'act1.marra.repeat';
   }
   ```

7. **Wire the NPC** in [main.js](../src/main.js):
   - The `interactState` flag (e.g. `marraInRange`) — added in `updateVillagers`
   - The keydown handler branch that calls `dialog.start(pickDialogForMarra(npcState))`
   - The `npcState` flags read by the picker (set in the dialog's `onOutcome` callback)

## Camera shot types

### `'wide'` (default)
Side-angle two-shot framing both characters. Camera sits on the **CW perpendicular** of the Bram→speaker axis (the open square side at the Crooked Pintle — change this if a future scene needs the other side). Camera glides ~0.55m along the action axis as the speaker changes, lookAt biases 60/40 toward the speaker. Reads as a smooth slow pan, not a cut.

### `'closeup'`
Tight face shot. Camera ~0.95m from the speaker, on the same perpendicular side as the wide shot (so the cut into closeup doesn't cross the 180° line). Eye level, slight 3/4 angle. Used for emotional climaxes only.

## Speaker accent color palette

Set in [Dialog.js](../src/ui/Dialog.js) — ink-tone colors that read against the parchment:

| Speaker | Color | Hex |
|---------|-------|-----|
| Bram | walnut ink | `#7a4a1a` |
| Wren | dark plum | `#5b3a6a` |
| Marra | brick red | `#8a3a2a` |
| (default) | dark brown | `#3a2810` |

Add new speakers to the `SPEAKER_COLOR` map. Stay in the ink-on-parchment register — saturated enough to punch, dark enough to read.

## Timing knobs (in case future scenes need different pacing)

| Knob | Where | Default | Effect |
|------|-------|---------|--------|
| Typewriter speed | `TYPEWRITER_CPS` in Dialog.js | `32` (chars/sec) | Lower = slower reveal |
| Punctuation pauses | `PAUSE_AFTER` in Dialog.js | `.` 240ms / `,` 90ms / etc | Natural reading rhythm |
| Cinematic camera lerp | `dt * 1.7` in `updateCamera` (main.js) | `1.7` | Lower = slower pan; ~1.2s settle |
| Letterbox slide-in | `transition: height 600ms` | `600ms` | CSS on `dialog-bar-top/bottom` |
| Panel fade between chained dialogs | `setTimeout(start, 360)` in `_finishDialog` | `360ms` | Cross-fade beat |
| Wren's auto-rotate-toward-Bram | `dt * 5` in updatePlayer dialog branch | `5` | Higher = snappier turn |

## Cinematic features (already wired — don't remove)

- **Letterbox bars** (12vh top + bottom) slide in on dialog open, retract on final close
- **Vignette** radial darkening pulls focus to center
- **Parchment panel** matches the journal-paper.webp + ivory gradient + Walter Turncoat font
- **Typewriter reveal** with first-press = complete line, second-press = advance
- **Player movement frozen** during dialog (in `updatePlayer`)
- **Mouse-look frozen** during dialog (in the document mousemove listener)
- **Wren auto-rotates** to face Bram (`player.dialogTargetFacing` set in `onLineChange`)
- **NPC auto-faces Wren** when in range (existing facing logic in `updateVillagerFacing`)
- **Pause menu integration** — `dialog.suspend()` / `dialog.resume()` are called from the Esc/tab-key/world-map handlers so the menu doesn't get covered

## Gotchas / lessons learned

1. **Never use `Box3.setFromObject` to ground a SkinnedMesh.** It uses rest-pose geometry bounds, which can be wildly off from the actual skinned position. Walk bones and find the lowest foot/toe bone instead — see `loadVillagerFBX` in main.js.
2. **Disable `frustumCulled` on SkinnedMeshes** loaded for villagers. Three.js culls based on rest-pose bounds; aggressive cinematic angles can falsely cull the character.
3. **Don't cross the 180° line** between shots. Closeups must use the same perpendicular side as the wide shot. Crossing reads as disorienting.
4. **Camera lerp must be slow enough** to feel like a dolly, not a cut. `dt * 1.7` was tuned over several iterations — much faster feels jarring.
5. **Mixamo "FBX with skin"** files are huge (~35MB each). For new characters, prefer "FBX without skin, In Place" for animation files; only the base T-Pose needs the mesh. Total per-character bundle drops from ~175MB to ~36MB.
6. **Idle clips translate the hips** above the rest pose by a few cm. The foot-bone grounding pass corrects for this — don't try to compensate elsewhere.
7. **Letterbox + vignette stay up across chained dialogs.** That's why chaining must be handled inside DialogController, not in main.js's `onOutcome` (an earlier version had a race condition where the close timeout fired after the chain started).

## Existing dialogs to reference

- [`act1.homecoming.bram.recognition`](../src/data/Dialogs.js) — 5 lines, opener pattern, 1 closeup
- [`act1.crooked_pintle.bram.key`](../src/data/Dialogs.js) — 12 lines, item handoff pattern, 2 closeups at the climax, chains into repeat
- [`act1.crooked_pintle.bram.repeat`](../src/data/Dialogs.js) — 3 lines, post-quest casual pattern, no closeups, no triggers beyond `talking`

When in doubt, copy one of these and adapt.
