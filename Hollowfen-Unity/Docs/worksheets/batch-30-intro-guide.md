# Batch 30 — First-steps intro guide ("The Road into Hollowfen")

**Date:** 2026-07-12 · **Status:** IN PROGRESS · tag `batch-30` (pending)
**Directive:** Trevor — after the entrance transition on a new game, "a pop up with instructions
and a guide to the first mission … engaging so the user is interested in playing. lets nail this."

## Design position
A generic tutorial modal would break the spell the voiced intro just cast. The Hollowfen-native
answer is a **journal page**: parchment card in the pause-menu/dialogue paper style, Wren's voice
framing the road ahead (bible Scene 1 transposed — "Then the road dips, and the old picture comes
apart"), the LIVE first quest pulled from QuestManager, three orientation hints, a compact controls
line reusing the batch-28 `settings.bind.*` strings, and one Georgia-italic **"Set out →"** button
(the main menu's "New Game →" grammar). Voiced by the batch-29 pipeline (narrator read on open).

## Build
- **`IntroGuide`** (new, `Hollowfen.UI`, scene singleton on `_IntroGuide`): code-built canvas at
  sortingOrder 65 (dialogue 60 < guide < narration 70), dim scrim + parchment rounded card +
  shadow; suspends the player while open (NarrationOverlay pattern; no timeScale freeze).
  Dismiss via the button (pad Submit / Enter / Space / click; FocusHighlight; EventSystem default
  selection). Public `Dismiss()` for the test harness.
- **Once per save**: `GameScores` flag `intro_guide_seen` set on dismiss; `ShowOnce()` no-ops when
  the flag is set or `arrive` is already complete.
- **Trigger**: StoryBeats intro `onDone` → `IntroGuide.Instance.ShowOnce()`. Robustness: if the
  intro was already seen but the guide flag is unset and `arrive` still active (quit between the
  two), StoryBeats.Start shows the guide directly.
- **Copy**: passage = bible Scene 1 transposed first-person/present (no invented facts); hints are
  UI chrome verified against reality (quest ribbon top-left; compass waypoint = the Village Well;
  J journal / M map / E interact per the shipped binding table). All via `Localization.Get`.
- **VO**: narrator clip of the passage (generate_vo.py gains an EXTRAS table — note: duplicates the
  passage copy; the parked staleness-manifest follow-up covers it).

## Known-minor (accepted)
Pause (Esc) is never disabled at the UIManager level, so PauseScreen can open over the guide —
same pre-existing class of issue as pause-over-narration (batch-29 review noted it out of scope).

## FABLE REVIEW
**Verdict: SHIP WITH CHANGES.** Canon gate PASSED — the passage verified fragment-by-fragment as a
strict Scene-1 transposition (abridgment only, zero invention, register holds; bonus: no overlap
with the narration captions, so the guide EXTENDS the voiced intro rather than echoing it).
Findings applied:
| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | HIGH | Controls line taught SWAPPED pad buttons — ground truth: Interact = E/**Y**, the J screen = J/**X** (satchel, not the story journal). Root cause is a PRE-EXISTING swap in the batch-28 settings table the guide propagated; contradicted the in-world "[E]/[△]" prompt | guide.controls corrected (Interact E/Y, Satchel J/X, Journal & Pause Esc/Start); settings rows fixed in the same batch (`interact.pad`→Y/Triangle, `journal`→"Satchel", `journal.pad`→X/Square) |
| 2 | HIGH | Mash-through could burn the once-per-save card unseen — the grace only guarded the poll, not EventSystem Submit (button live at alpha 0) | Grace guard moved INTO `Dismiss()` |
| 3 | MED | Journal hint pointed at the wrong surface (J opens the satchel; story/field-guide live in the pause menu) | Hint reworded: "Esc opens your journal — the story so far, and a field guide…" |
| 4 | MED | Esc during guide/narration opened an INVISIBLE pause (sort 10 under overlay 65/70) — focus stolen, and quit-from-hidden-pause leaks `PlayerInteractor.Suspended` across scene load | `UIManager.OnPauseInput` bails while either overlay shows (also covers batch-29's noted narration case); `IntroGuide.OnDestroy` restores the flags |
| 5 | LOW | Dismiss re-entrancy during the fade | `_closing` guard |
| 6 | LOW | `NarrationOverlay.SkipAll()` swallowed `onDone` → a skipped intro deferred the guide to next load | `_pendingOnDone` invoked from SkipAll |
| 7 | NIT | EXTRAS not individually targetable in generate_vo.py | Accepted — adjacent to the parked staleness item |
UX judgment: pass ("diegetic journal card, live quest pull, single focused button, <2s cost to a
second-save player"). Soft note kept: on the direct-ShowOnce path the guide may fade in under the
LoadingScreen — eyeball during a menu-booted run (below).

## Verification evidence
**Play-mode (bridge), fresh saves (backed up + restored), integrity 0/0, lint 0/0:**
- **Full flow:** voiced intro → guide fades in on `onDone`; screenshot `intro_guide_final.png` —
  parchment card over the dimmed east road; live quest block ("Homecoming / Walk the road into
  Hollowfen."), pad focus lands on `SetOutButton`; the HUD corroborates every hint (ribbon top-left,
  compass "Village Well · 168m").
- **Quit-between fallback:** a session that booted intro-seen + flag-unset showed the guide directly
  from StoryBeats.Start ✓ (verified live, accidentally but conclusively).
- **Dismiss:** control restored, `intro_guide_seen` set (persists via AutoSaveScores), ShowOnce
  re-invocation no-ops ✓. Guide voice (18.5s narrator read) verified same-call playing + audible.
- **Measurement gotcha discovered** (now in audio.md): editor pauses between bridge calls hard-stop
  AudioSources — cross-call audio reads show stopped/`isVirtual` phantoms; assert same-call only.
  Also real: default-priority sources CAN be virtualized among the world's ~50 ambient emitters —
  speech now priority 0, music 16.
- **Not bridge-verifiable here:** the pause-bail runtime path needs a menu-booted session (UIManager
  is DDOL from Scene_MainMenu; direct gameplay-scene play has no UIManager). Code-reviewed; covered
  by Trevor's test script below.

## Docs updated
- `systems/ui-framework.md` — IntroGuide in key scripts + the pause-bail gotcha; stale batch-28
  SettingsScreen detail row refreshed.
- `systems/audio.md` — cross-call measurement gotcha, priority policy, IntroGuide clip in coverage.
- `Localization.cs` — `guide.*` block + the settings binding-table pad corrections.
