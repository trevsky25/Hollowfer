# Hollowfen Restoration Expansion & Production Audit

Audit date: 2026-07-18  
Scope: complete Living Restoration roadmap, UI/UX, accessibility, copy/story, persistence, performance, and regression health  
Runtime: Unity 6000.4.4f1, `Scene_MainMenu` boot into `Scene_Hollowfen`

## Executive verdict

The complete Living Restoration roadmap is implemented and editor-verified. Hollowfen now has seven save-backed projects spanning the full story: cottages, Wend bridge, Joren's Forge, Chapel Garden, Crooked Pintle, Sable's Cottage, and Tobin's Workshop. The five later projects each have two atomic supply decisions, two distinct dawn promotions, visible Surveyed/Supplies/Work/Restored/Occupied states, story-gated NPC labor, a framed world reveal, first-use completion, score rewards, and a permanent gameplay benefit.

The implementation is a strong production-engineering candidate in Unity. The final content/data lint is `0 errors / 0 warnings`; the project lint is `0 errors / 0 warnings / 1 documented waiver`; active UI is `0 critical / 0 advisory`; every focused regression listed below passes. The new world presentation stays inside explicit renderer/material/particle/light budgets and produced no measurable SetPass increase from the audited camera.

This does not change the existing Steam-release verdict. Shipping identity, Windows build support, Steamworks/depot configuration, signing/notarization, Simplified Chinese, source/provenance closure, and physical Steam Deck profiling still require external inputs or target hardware. No standalone candidate was produced during this Unity-focused expansion pass.

## What shipped in this pass

### Seven-project restoration arc

| Project | Story unlock | Funding | Occupied benefit |
|---|---|---:|---|
| North Lane cottages | `cottagesReopen` | existing story funding | Two families return and relight North Lane/the square. |
| Wend bridge | cottages complete | 24 + 12 copper | A safe cart-wide village/mill/Old Wood route. |
| Joren's Forge | `forgeKnife` complete | 16 + 14 copper | Careful forage cutting drops from 6 strokes to 5. |
| Chapel Garden | `caldenReconcile` complete | 12 + 10 copper | Two beds; cultivation matures in 75% of normal time. |
| Crooked Pintle | `festivalHosted` complete | 18 + 20 copper | Ordinary daily village orders pay +2 copper. |
| Sable's Cottage | `wendlightFound` complete | 22 + 16 copper | Wild mushroom cooldowns shorten by one day; Witch's Path knowledge flags settle. |
| Tobin's Workshop | `wendSource` complete | 20 + 18 copper | Cultivated harvests yield +1 mushroom. |

The five new projects contribute 23 Village Hope and 15 Knowledge in total. `RestorationBenefits` projects all mechanical values from the canonical Occupied stages; it adds no duplicate save flags or schema state. Existing saves migrate upward through quest/flag rules and immediately recover their valid benefits.

### World and narrative integration

- Five owned `_LivingRestoration_*` roots add the complete stage vocabulary without modifying third-party prefabs.
- Six post-tutorial crew beats reuse Joren, Pell, Bram, Theo, and Almy at the actual authored locations. The relocated forge route now points to `(198.4, 32.65, 195.7)` rather than the pre-relocation placeholder.
- Ten later supply contributions and five first-use transitions commit purse, flags, scores, project stages, and the full save snapshot atomically. Injected final-write failure rolls back every store and publishes no UI event.
- The Witch's Cottage completion closes the two existing `witch_cottage_restored` / `old_knowledge_restored` ending prerequisites instead of inventing a parallel ending gate.
- Tobin's Workshop copy consistently preserves the story truth: the Wend and mill wheel do not return; Wren restores a home/workroom without falsifying the river.
- Existing dialogue presentation remains complete: 28 cinematic story cards, 24 newly covered three-painting dialogue turns, Bram's live key handoff, Almy's four voiced beats, and Marra's voiced 4K paintings/live Goldfoot handoff all re-verified.

## Audit findings fixed

### UI/UX and accessibility

- The seven-row Restoration Ledger rail was too tall for its footer and had ink-on-dark unselected copy. Rows are now compact and readable, long stage copy has dedicated vertical space, and the selected project (forest fill) is visually distinct from controller focus (gold outline/glow).
- Settings now has five explicitly linked tabs. Accessibility adds Interface Size (100/108/115%), Reduced Motion, and Caption Backing with localized explanation.
- `AccessibilityPresentationPolicy` reaches scene-authored and late code-built canvases immediately and again after one frame. The live menu-booted verification reached 17 runtime canvases at 115%.
- Reduced Motion removes menu motes/mist/Ken Burns, focus scaling, prop-camera arcs, toast bounce/slide, and long narration movement while preserving the stable final shot, focus color/glow, VO timing, input ownership, and callbacks.
- Caption Backing adds a rounded high-opacity plate to cinematic captions and remains absent for image-only beats.
- Standard and 115% Settings layouts plus the seven-project Ledger were inspected live. Production UI lint returns `PASS · 0 critical · 0 advisory` for gameplay and the largest Settings presentation.

### World reveal composition

The first visual sweep found logic-clean reveals aimed through roofs, walls, or canopy. All five cameras were surveyed from their actual final positions:

- Forge now frames the rebuilt hearth/anvil straight from the east instead of looking through its awning.
- Chapel Garden uses a closer north-side frame that reads both beds and boundary rather than a distant wall/tree composition.
- Pintle furniture/sign/firewood were moved beyond the real east-wall collider (raycast face `x=282.16`); the old objects were physically inside the vendor shell.
- Sable's Cottage frames the repaired door/shutters from the clear southeast lane.
- Tobin's five floating roof-patch cubes were removed. The reveal now reads an attached eye-level awning, posts, braces, nameboard, work furniture, and materials.

### Copy, plot, and test truth

- A full typo/placeholder sweep found no player-facing Hollowfin/journal/mushroom/identification misspellings or unresolved placeholder copy.
- Mill benefit copy said “new planting” while the mechanic awards at harvest; it now says “one mushroom to every cultivated harvest.”
- Bridge verification no longer claims the runtime catalogue has only two projects; it verifies shared catalogue registration.
- The audio audit had an obsolete 267-line assertion. The current 75-dialogue graph and its SHA-bound voice manifest contain 249/249 clips (100 Wren, 149 supporting cast), all 24 kHz mono.
- The day/night audit had an obsolete six-light assertion. The authored scene now correctly contains 18 practicals: six village, four cottages, four bridge, and four expansion lights. Every one enables at night and disables at noon.
- The final 240-frame direct-gameplay smoke exposed teardown-only errors: the playlist owner could update after its child AudioSource was destroyed, and an NPC talking driver could clear a bool after its Animator stopped initializing. Both paths now guard teardown state; the repeated smoke reached 388 frames and exited with zero errors.

## Performance audit

The expansion was tested with all five later projects forced to Occupied for worst-case presentation inspection.

| Measure | Result |
|---|---:|
| Full authored mesh triangles across every later stage object | about 89,600 |
| Forge / Garden / Pintle / Cottage / Workshop authored triangles | 4,622 / 17,312 / 24,189 / 12,884 / 30,581 |
| Worst simultaneous stage renderers | <= 240 (verified hard cap) |
| All authored expansion renderer components | <= 430 (verified hard cap) |
| Unique shared materials | <= 18 |
| Particle systems / lights / grow beds | <= 7 / 4 / exactly 3 |
| Restoration light shadows | 0 (all `LightShadows.None`) |
| Live main-thread Editor sample | about 3.36–4.55 ms |
| Settled SetPass with projects active vs disabled | 129 vs 129 |
| Camera-visible triangle delta at the audited location | about +136 triangles |

The initial large one-frame triangle sample occurred during transition/settling and did not persist. GPU frame timing varied from roughly 11–28 ms in the Editor regardless of project-root state, so it is not treated as target-hardware evidence. The existing high Editor footprint (about 2.2 million objects and multi-gigabyte texture/mesh residency) remains a whole-scene/vendor-content concern; Windows and physical Steam Deck captures are still required before a 60 FPS shipping claim.

## Final verification matrix

| Verification | Result |
|---|---|
| Project lint | Pass — 0 errors, 0 warnings, 1 documented waiver |
| Data Integrity | Pass — 0 errors, 0 warnings |
| Full village restoration expansion | Pass — 7 projects, 10 new supply lines, five benefits, bounded world cost |
| Cottage restoration / Wend bridge focused verifiers | Pass |
| Gameplay foundation | Pass — 63 stable nodes, 21 identification gates, 4 cultivation recipes |
| Inventory transactions / save integrity | Pass |
| Village requests | Pass — 9 rotating orders + festival handoff |
| Four-ending engine | Pass |
| NPC schedules | Pass — 5 derived routines and all priority/teleport boundaries |
| Mill door / Bram + Marra cinematic | Pass |
| Mushroom ecology / illustrated journal | Pass — 41 nodes / 21 of 21 spreads |
| Story coverage / HD art | Pass — 28 cards / 93 HD mip-free paintings |
| NPC character models / exploration layout | Pass — 12 actors / 12 destinations across 235m × 367m |
| Gameplay audio | Pass — 13 cues / 249 of 249 voiced lines |
| Day/night / world feedback / music playlist | Pass |
| Presentation ownership | Pass — nested leases and zero-leak cleanup |
| Accessibility presentation | Pass — 115% across 17 canvases, stable reduced-motion focus, caption preference |
| Active gameplay + largest Settings UI | Pass — 0 critical / 0 advisory |
| Final direct-gameplay smoke | Pass — 388 frames observed, clean exit, 0 console errors |

All save-mutating focused tests used unique system-temporary directories and restored runtime stores, the active slot, device preferences, and directory overrides in `finally`. Temporary visual-QA screenshots were removed after inspection.

## Remaining production work

These are not regressions introduced by the expansion:

1. Approve final company/product/version/bundle identity.
2. Install Windows Build Support and produce/test Windows x64.
3. Integrate and privately test the real Steam App ID, depots, Cloud/Input/Achievements actually advertised.
4. Sign and notarize macOS release candidates.
5. Profile a near-final build on physical Steam Deck, including suspend/resume, offline launch, controller-only completion, 1280×800 readability, thermals, memory, and worst-frame time.
6. Finish Simplified Chinese or remove it from the release promise until implemented/reviewed.
7. Close outstanding art/voice/third-party provenance and license records.
8. Add high-contrast/color-vision options, input remapping/invert axes, and haptics controls; run pseudolocale, ultrawide, and OS-scale sweeps.
9. Perform a listening pass for VO pace and a complete start-to-finish human playthrough on every advertised platform.
