# Batch 31 — Intro guide visual redo

**Date:** 2026-07-12 · **Status:** IN PROGRESS · tag `batch-31` (pending)
**Directive:** Trevor — "I like this but I want to redo the UI of the welcome screen on first load.
it needs some help."

## Diagnosis of the batch-30 card
Flat untextured cream slab, centered — reads as a system dialog, not a journal page; it also blocks
the road/world it's inviting the player into. Single boxy column, cramped two-line controls lump,
no paper materiality.

## Direction (visual-only — behavior, flow, flags, copy keys untouched except controls restructure)
1. **Offset-left journal page** — card anchored left; the east road + Wren stay visible right.
   Scrim becomes left-weighted (dark under the page, clear over the world) + light global dim.
2. **Paper materiality** — procedural grain sprite (new cached `UICanvasUtil.PaperGrain`, fixed
   seed) over the parchment fill, inset gold hairline frame, stronger shadow.
3. **Ledger typography** — double rule under the title (gold + ink hairlines), task block as an
   inset "ledger entry" panel (soft ink wash, rounded).
4. **Keycap chips** — controls rendered as rounded key chips ([W A S D] [Left Stick] · [E] [Y] …)
   grouped under small eyebrow labels, replacing the composite `guide.controls` string with
   structured `guide.ctl.*` / `guide.key.*` localization keys.
5. Button/hints/VO/focus wiring unchanged.

## Review gate note
No canon copy change (passage untouched), no flow/state change — pure layout/visuals with Trevor
reviewing the screenshot live in-session; the independent-reviewer pass is deliberately skipped
this batch (recorded here for honesty vs the Model-Tiering habit).

## Verification evidence
- Compile clean; integrity 0/0; lint 0/0.
- Fresh-save run (SkipAll fast path — the batch-30 `_pendingOnDone` fix earning its keep): card
  fades in offset-left with Wren + the east road + the wall visible right; grain/sheen/frame/
  double-rule/task-inset/keycap chips all render (screenshot `intro_guide_v2.png`); live quest
  block intact; dismiss → flag set + control restored ✓.
- **Editor gotcha hit:** the in-memory scene went stale after the earlier tangled play sessions —
  `_IntroGuide` existed on disk (committed) but not in the running scene. Force `OpenScene` from
  disk before playing when a scene object "disappears" (added to the session recipe, not a code bug).

## Docs updated
- `systems/ui-framework.md` — IntroGuide line notes the restyle + PaperGrain.
- `Localization.cs` — composite `guide.controls` replaced by structured `guide.ctl.*`/`guide.key.*`
  (18 keys) for the chips.
- `UICanvasUtil.PaperGrain` — new cached procedural primitive (fixed seed), first shared paper
  texture; candidate for the dialogue parchment later.
