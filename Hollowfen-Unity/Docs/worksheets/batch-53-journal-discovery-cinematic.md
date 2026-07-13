# Batch 53 — Journal discovery cinematic (Meshy journal + push-in → painted finale)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-53` (pending)
**Directive (Trevor):** after the mill key — "move onto discovering the journal. Again, want great visual
interactions here." (He dropped the Meshy journal model in `Resources/Mushroom Journal`.)

## Assets
- Imported `Resources/Mushroom Journal/…texture.fbx` (429k verts — **heavy, wants decimation before EA**,
  consistent with the other Meshy exports) + PBR set.
- `Materials/Mat_MushroomJournal.mat` (URP/Lit: albedo/normal[typed]/metallic; non-metal, low gloss —
  leather + paper). `Prefabs/MushroomJournal.prefab` — scale 10 → ~0.19m book.
- Scene: placed `MushroomJournal` as a child of `Journal_FathersJournal` at the placeholder book's pose
  (lying flat on the mill furniture); disabled the old `Prop_FathersJournal` renderers (kept `CandleGlow`).

## Integration
- **QuestInteractable.cs** — new optional focus push-in: `_focusTarget` (+ distance/height/fov/push/hold/
  restore). When set, `Interact` runs `PropFocusCinematic.Play(_focusTarget, …, onDone: PlayReveal)` before
  the reveal; the dialogue/narration payload + self-deactivate moved into `PlayReveal()` so it fires either
  immediately (batch-51 behaviour, unchanged when `_focusTarget` null) or after the push-in.
- `Journal_FathersJournal._focusTarget` = the MushroomJournal model → the camera pushes in on the 3D book,
  holds, then hands to the batch-51b `ShowCinematic` painted-spread finale (crossfade to the paintings +
  live Georgia captions). Reuses the batch-52 `PropFocusCinematic` (HUD-hide + letterbox + bounds-centre).

## Verification (play mode, fresh save — cleared, restored pristine after)
- [x] Compiles clean.
- [x] `findJournal` active → interact: push-in frames the beautiful leather journal (mushroom-emblem cover,
      strap, thick pages) with letterbox + HUD hidden (`b53_book.png`) → crossfades to the painted finale
      (`b53_finale.png`, "The first pages were recipes…") → fieldCap/woodEar/pinecrest discovered, findJournal
      completed → chained to firstForage. Ends clean (narration off, player un-suspended, brain re-enabled).
- Saves restored pristine. (Scene-save prefab-rotation churn gotcha still applies — logged batch-52.)

## Follow-ups
- Decimate the journal (429k) + key meshes before EA (graphics-pipeline todo).
- The push-in → finale is sequential (short 0.2s restore between); a future PropFocus "hand-off" mode could
  fade the finale up directly from the pushed-in book (avoids the brief camera settle).
