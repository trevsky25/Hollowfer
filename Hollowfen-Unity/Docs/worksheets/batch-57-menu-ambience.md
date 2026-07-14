# Batch 57 — Menu ambience bed + Ambience slider (menu-ui-audit fix #2)

**Date:** 2026-07-13 · **Status:** DONE (play-verified) · tag `batch-57` (pending)
**Audit items:** A2 (P0, no menu ambience — Cinematic-Pass #2), A3/A4 (mixer group + slider). Trevor
signed off: "all four, suggested order" + ambience clip = **synthesize procedurally**.

## What shipped
- **`AmbienceManager.cs`** — a fully procedural, seamless-looping forest bed (no audio assets):
  low-passed wind with slow periodic gusts + high-passed leaf shimmer + 3 sparse synth bird calls,
  fixed seed (same bed every boot), 14 s loop with a 0.5 s equal-power head/tail crossfade so it
  loops click-free. Volume = internal ceiling `0.45` × player Ambience setting, with a 3.5 s fade-in.
- Placed on **`_Ambience`** in `Scene_MainMenu`, routed to the **Master** mixer group.
- **Ambience settings slider** (Audio tab, 4th row) — pref `audio.ambience` (default 0.8), live via
  `AmbienceManager.Instance.SetUserVolume`. Localization `settings.audio.ambience` = "Ambience".

## Design decision — source-level trim, not a mixer node (A3)
The audit proposed a dedicated **Ambience mixer group**. Adding one means internal-API `.mixer`
surgery (`AudioMixerController.CreateNewGroup` + exposed-param GUID plumbing) on the **shipping**
mixer used by ALL game audio — irreversible if corrupted, no public API. I chose the equivalent,
safe outcome instead: route ambience to **Master** and trim it at the **source** via its own settings
slider. User-facing behavior is identical — an independent Ambience slider; Music/SFX don't affect it;
Master does. A true Ambience node (for DSP ducking under VO) can be added later if wanted. **Flagged
for Trevor.**

## Verification (Play mode, deterministic — isPlaying is a phantom under Step; see audio.md)
- Ambience source: `output=Master`, `loop=True`, clip **14.0 s / 617,400 samples**, peak **0.117**
  (tasteful, no clipping), buffer full of signal. Re-Play in-call → `playing=True, isVirtual=False`
  (not virtualized; 1 active source on the menu). Fade reached target `vol=0.360` (0.8 × 0.45).
- Settings Ambience slider: exists (default 0.80); setting it to 0.20 drove `_userVolume=0.20` and
  `source.volume=0.090` (= 0.20 × 0.45). 4-slider Audio tab lays out cleanly, no footer clipping
  (screenshot `b57_settings_ambience.png`). Compile clean, no errors (only harmless URP memoryless
  noise).

## Test script for Trevor
1. Play from `Scene_MainMenu`, headphones on — a soft forest bed fades in over ~3.5 s (wind + leaf
   rustle + the odd distant bird), sitting well behind everything.
2. Settings → Audio → the new **Ambience** slider. Lower it → the bed quietens live; raise it → louder.
3. Lower **Master** → ambience drops with it. Lower **Music**/**SFX** → ambience is unaffected.
4. Leave the menu open a while — the loop should not click at the seam.

## Next (audit order): batch-58 = TMP migration (SaveSlotScreen + LoadingScreen._label) → kills the
last legacy-sans surfaces + the New-Game font-pop.
