# Hollowfen Production Audit

Audit date: 2026-07-17  
Project: Hollowfen — The Failing Village  
Unity: 6000.4.4f1, Universal Render Pipeline  
Audit branch: `codex/gameplay-production-pass`

## Executive verdict

Hollowfen's implemented game systems are in substantially better production condition and the full final regression pass is clean. The audited macOS player boots from the real main menu, creates a new save, plays and skips the opening cinematic, enters the village, navigates the pause/field-guide/purse flows, renders day and night correctly, and records no runtime exceptions or player warnings in the clean reproduction run.

The project is **not yet ready to submit to Steam**. The remaining blockers are release inputs or content work that cannot be safely invented during a technical audit: final product identity, Windows build support, Steam App ID/Steamworks/SteamPipe configuration, macOS signing credentials, the promised Simplified Chinese implementation or a scope change, physical Steam Deck validation, the remaining character models, and unresolved rights/provenance records.

| Area | Verdict | Basis |
| --- | --- | --- |
| Gameplay and progression | Pass | Final runtime verification passed quests, requests, endings, schedules, inventory, save recovery, mill progression, physics, and world feedback. |
| Data integrity | Pass | 0 errors, 0 warnings across 26 quests, 75 dialogues, 11 NPCs, 14 locations, 21 mushrooms, 30 story cards, 2 story moments, 1 profile, 4 endings, and 10 village requests. |
| UI and presentation | Pass with follow-up | Active presentation has 0 critical and 0 advisory findings; contrast, minimum text, modal ownership, controller focus, map, toast, and narration issues were repaired. Deck readability and accessibility options remain. |
| Audio and captions | Pass with listening QA | 267/267 spoken dialogue lines have text and unique VO; all 13 gameplay cues resolve. Ten clips exceed 220 WPM and two exceed 250 WPM. |
| Save integrity | Pass | Atomic/recoverable save format, transaction tests, corruption recovery, and slot flow passed. Original user saves are outside the audit changes and are restored after testing. |
| macOS audit build | Pass | Non-development universal player, correct scene order, clean launch-to-gameplay smoke test, no denied editor/vendor assemblies. |
| Performance | Improved; target certification pending | App bundle reduced from 4.3 GiB to 2.3 GiB and Editor mesh duplication reduced by about 2.13 GiB. A physical Steam Deck and Windows target still need profiling. |
| Windows release | Blocked | Windows Build Support for Unity 6000.4.4f1 is not installed. |
| Steam release plumbing | Blocked | No shipping Steamworks SDK, App ID, SteamPipe depot scripts, Cloud configuration, or partner-branch test exists in the repository. |
| Localization | English pass; Chinese blocked | English routing is internally consistent. Simplified Chinese currently has no locale implementation, CJK fallback, translated table, or selector. |
| Content and legal | Blocked on known records | Additional NPC models remain expected; 55 visual assets declare generative-AI provenance and six shipped images still lack adequate source records. |

## Visual evidence

### Main menu

![Audited main menu](production-audit-2026-07-17/main-menu.jpg)

### Day gameplay

![Audited village in daylight](production-audit-2026-07-17/day-world.jpg)

### Night readability

![Audited village at night](production-audit-2026-07-17/night-world.jpg)

### Baked world map

![Audited static world map](production-audit-2026-07-17/world-map.jpg)

## Repairs completed during the audit

### Progression, persistence, and runtime safety

- Hardened ending resolution and recovery so the four authored endings resolve through valid, recoverable state.
- Hardened save serialization, transactions, corruption recovery, and slot-state handling.
- Repaired mill key/door progression, quest handoff behavior, inventory transactions, and shipping physics validation.
- Repaired nested presentation/modal leases, cursor and HUD restoration, minimum display timing, same-frame reuse, and zero-leak cleanup.
- Contained editor/debug helpers so they cannot surface in a production player.
- Corrected controller focus and input ownership across dialogue, map, inventory, guide, purse, pause, story, settings, and confirmations.
- Corrected mushroom preview and focus-camera lifecycle issues.
- Added cold-start display-setting application, including resolution migration by width/height rather than a fragile display-mode index alone.

### Visuals and UI

- Replaced the live full-map/minimap rendering cost with a baked 4096 px overhead map and disabled the redundant map cameras.
- Removed stale third-party demo lighting data: 415 obsolete lightmaps, roughly 128,000 stale probes, and a 75.7 MiB demo lighting asset no longer drive the village.
- Retuned night ambient and practical lighting for readable navigation without flattening the day/night mood.
- Added/validated occlusion data and reflection probes used by the shipping scene.
- Repaired the lingering region-arrival shadow rectangle.
- Repaired candle presentation state.
- Raised the smallest code-authored UI text from 10–11 to at least 12 reference pixels and corrected low-contrast dialogue, narration, pause, loading, map, request, inventory, inspect, purse, and cultivation copy.
- Corrected map, clock, compass, loading, save-slot, story, ending, request, currency, and mushroom text routing.

### Audio and presentation

- Separated voice routing from general sound effects so the Voice setting controls all spoken content consistently.
- Verified 267 unique dialogue clips for 267 spoken lines and all 13 authored gameplay cues.
- Verified every spoken presentation has visible text.
- Audited every dialogue/story raster reference. No genuine missing dialogue or cinematic image was found, so no speculative generated art was added.

### Package and build hygiene

- Removed unused glTFast, Visual Scripting, Multiplayer Center, OpenUPM, and player-facing Newtonsoft package content.
- Embedded the Unity MCP tooling at exact upstream commit `b92c05a25820cfc9f59ce4094eb46aaec8632ea2`, with source/license records, and constrained it and its Newtonsoft dependency to Editor-only compilation.
- Repaired two broken third-party tree FBX metadata records.
- Verified the player denylist contains none of `MCPForUnity`, `glTFast`, `Unity.VisualScripting`, `Unity.Multiplayer.Center`, or `Newtonsoft.Json`.
- Added a production build gate that rejects placeholder identity, the wrong scene list, development/debug/profiler flags, unsupported targets, denied assemblies/plugins, and overwriting an existing output.

## Final verification matrix

All entries below were rerun after the lighting, map, package, performance, settings, UI, and persistence changes.

| Verification | Result |
| --- | --- |
| Bram character/progression verification | Pass |
| Day/night system | Pass |
| Gameplay audio, 13 cues and 267/267 voice lines | Pass |
| Gameplay foundation, including 22 authored nodes | Pass |
| Mill key and door progression | Pass |
| NPC schedules | Pass |
| Village requests | Pass |
| World feedback | Pass |
| Four-ending engine | Pass |
| Inventory transactions | Pass |
| Save integrity and recovery | Pass |
| Shipping physics | Pass |
| Candle repair scan | Pass; 0 unresolved of 6 |
| Project data integrity | Pass; 0 errors, 0 warnings |
| Production UI presentation | Pass; 0 critical, 0 advisory |
| Nested presentation-session lifecycle | Pass; 0 leaked leases |
| Voice mixer route and persisted volume | Pass; audit preference restored |
| Project script compilation | Pass; no project errors or warnings |
| Player assembly denylist | Pass; 0 denied assemblies |
| macOS player log, clean reproduction | Pass; 0 errors/warnings |

The standalone smoke flow was:

`Main menu -> Save slots -> New game -> Opening cinematic -> Skip -> Intro guide -> Dismiss -> Live village gameplay -> Pause -> Field guide -> Purse`

One earlier automation run appeared to hold movement after the cinematic. A clean relaunch and focused reproduction did not reproduce it, including more than six seconds of stationary gameplay. It is recorded as a non-reproducible automation/input artifact; no speculative gameplay patch was made.

## Performance results

These numbers establish a useful Apple Silicon baseline, not Steam Deck certification.

| Metric | Before | After |
| --- | ---: | ---: |
| macOS audit app bundle on disk | 4.3 GiB | 2.3 GiB |
| Build-report output | about 4.40 GB | about 2.377 GB |
| Build duration | about 56.3 s | about 37.7 s |
| Editor mesh memory | about 2,654.5 MiB | about 522.6 MiB |
| Editor graphics-driver memory | about 4,014 MiB | about 2,949–2,960 MiB |
| Editor total tracked memory | about 6,880.8 MiB | about 5,808 MiB |
| Editor draw calls | about 5,734 | about 6,705–8,849 |
| Editor SetPass calls | about 179 | about 177–178 |

The app-size reduction is about 46%, and removing legacy static batching eliminated roughly 2.13 GiB of duplicated Editor mesh memory. The draw-call increase is an intentional tradeoff; SRP batching remains enabled and SetPass count stayed essentially flat.

The clean standalone player selected the production 60 FPS policy and used hardware VSync every two refreshes on a 120 Hz display. On the audit Mac, live-gameplay resident memory was approximately 4.25 GB and the measured physical footprint was approximately 4.6 GB, with much of the resident allocation attributed to Metal/graphics mappings. The player loaded about 2.18 million objects and logged an approximately 114 ms asset-unload spike during scene transition. These figures are high enough that memory and transition-frame-time remain priority measurements on target hardware.

The largest remaining packed content groups in the optimized build are approximately:

- Magic Pig Games content: 1.036 GiB
- Hollowfen-authored content: 0.443 GiB
- Nature content: 0.389 GiB
- Character content: 0.079 GiB

The player is a universal `x86_64` + `arm64` macOS application with a macOS 12 minimum. It currently has only an ad-hoc signature and fails Gatekeeper assessment, which is expected for an audit artifact.

## Localization and accessibility

English content integrity is strong:

- 75 dialogue assets and 267/267 spoken lines have captions and unique voice clips.
- 550 unique English lookup-table rows were found.
- 273 literal localization references have no missing or duplicate keys.
- All known Story Moment, Story Beat intro, and Intro Guide speech has matching visible text.

Simplified Chinese is not presently shippable:

- about 610 fallback/derived rows still require authored translation;
- 267 dialogue lines/speaker labels and 18 choices remain ID-less;
- 10 Story Beat captions remain ID-less;
- no locale selector, locale persistence, secondary table, or CJK font fallback exists.

Accessibility follow-up remains important for Steam Deck and general release quality:

- global UI/caption scale and caption-background controls;
- reduced-motion option for camera motion and transitions;
- high-contrast or color-vision preset;
- invert X/Y, input remapping, and haptics toggle;
- runtime disconnect/reconnect and haptics-stop test;
- pseudolocale test with 30–50% expansion;
- full 1280x800, 1080p, ultrawide, and 200% OS-scale layout sweep;
- listening review for 10 voice clips above 220 WPM, especially the two above 250 WPM.

At the current 1280x800 reference scaler, 12 reference pixels become roughly 8.4 physical pixels and 16 become roughly 11.2. The UI is functional in the audited 1280x800 player, but a user text-scale control is the appropriate production fix for broad readability rather than another blanket font-size increase.

## Image and rights audit

No image-generation action was justified during this sweep:

- all 30 Story Card image references resolve;
- all four opening images and all three hidden-journal images resolve;
- the 75 dialogue assets intentionally present the live 3D camera and have no missing portrait field;
- the one authored dialogue transition correctly falls back to its resolved Story Card image;
- Oyster and Aldermark deliberately have no field-guide sketch rather than a broken reference.

Adding images to those interactions would be new creative scope rather than a repair.

The visual provenance registers identify 55 raster assets with embedded C2PA/JUMBF fields declaring `gpt-image`/OpenAI generative origin: 39 presentation images and 16 mushroom images. Six other shipped images—the app icon and five Wren study plates—still lack adequate creator, generation, or license records. See:

- [`../legal/presentation-image-provenance.md`](../legal/presentation-image-provenance.md)
- [`../legal/mushroom-image-provenance.md`](../legal/mushroom-image-provenance.md)

Before release, retain account/job/receipt evidence and applicable terms for generated assets, resolve the six unknown source records, consolidate third-party model/audio/music/font licenses, and confirm the origin of any AI-assisted voice, narrative, or localization content. Steam's Content Survey requires truthful disclosure of pre-generated AI content that ships in the game, including artwork, sound, narrative, and localization ([official Steamworks Content Survey documentation](https://partner.steamgames.com/doc/gettingstarted/contentsurvey?language=english)).

## Release blockers

### P0 — required before a Steam candidate can be produced

1. **Set the final shipping identity.** Replace `DefaultCompany`, `Hollowfen-Unity`, version `0.1.0`, and the Unity template application identifier with the approved company, product name, semantic version, and bundle identifier. The release build gate intentionally refuses to guess these values.
2. **Install Windows Build Support and test Windows x64.** The project declares Mac + Windows as its Early Access target, but this Unity installation cannot build Windows. Run the full smoke/regression suite on a representative Windows machine after installation.
3. **Configure the Steam application.** Obtain/use the real Steam App ID, integrate only the Steamworks APIs the product will claim, define depots and launch options, add SteamPipe `app_build`/`depot_build` VDFs outside public source control as appropriate, upload to a private branch, install from Steam, and run the complete flow there. Valve recommends testing the uploaded build from a private branch, not only the local executable ([official SteamPipe upload guide](https://partner.steamgames.com/doc/sdk/uploading)).
4. **Decide and implement advertised Steam features.** The repository contains achievement event hooks but only a logging stub. Steam Achievements, Steam Cloud, and Steam Input are not implemented merely because hooks or Unity controller support exist. Do not select store features until they work in a Steam-installed build. Steam Auto-Cloud can be configured without game code, but its quotas, cross-platform root overrides, exclusion of machine-specific settings, and Mac/Windows synchronization must be published and tested ([official Steam Cloud guide](https://partner.steamgames.com/doc/features/cloud?language=english)).
5. **Sign and notarize the macOS candidate.** The audit app is ad-hoc signed and fails Gatekeeper assessment. Use a Developer ID certificate, Hardened Runtime, secure timestamp, Apple notarization, and a stapled ticket, then test the exact Steam-delivered bundle. Apple documents these distribution requirements and the `notarytool` workflow in its [official notarization guide](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution).
6. **Finish the known character-art work.** Add the remaining NPC models planned by the developer and close the Aldermark/canonical Maitake visual gap, then repeat schedule, collision, LOD, lighting, and performance validation.
7. **Resolve release provenance.** Close the six unresolved image source records, consolidate all third-party receipts/licenses, and complete Steam's AI Content Survey accurately.
8. **Resolve the Simplified Chinese commitment.** Either implement and professionally review the locale, CJK fonts, selector, persistence, and layouts, or formally remove Simplified Chinese from the Early Access promise and Steam store metadata until it is ready.
9. **Validate the declared Steam Deck goal on hardware.** Test controller-only access, resume/suspend, offline launch, text at 1280x800, keyboard invocation, device-specific prompts, power/performance, thermals, memory, and worst-case frame time. Valve's compatibility criteria require all content to be reachable with physical controls and prompts to match the active input ([official Deck compatibility criteria](https://partner.steamgames.com/doc/steamhardware/compat)).

### P1 — release-quality follow-up

1. Implement the accessibility controls listed above and repeat the UI sweep.
2. Listen through every VO line and revise the speed outliers.
3. Reduce the scene-load asset-unload spike and profile the 2.18 million loaded-object footprint on Windows and Deck.
4. Audit Magic Pig Games and Nature packed content for additional safe LOD, texture, material, and unused-asset reductions after the final NPC models land.
5. If native Steam Input is advertised, publish action manifests and an official configuration, query action origins, and display the correct controller-specific glyphs. Valve's definition of full support includes correct glyphs, published configurations, and remappable actions ([official Steam Input guidance](https://partner.steamgames.com/doc/features/steam_controller/getting_started_for_devs?language=english)).
6. Complete a near-final start-to-finish playthrough on every advertised operating system. Steam reviews both the store page and a near-final build, and Valve says only implemented features should be advertised; allow at least seven business days for the review window ([official Steam review-process guide](https://partner.steamgames.com/doc/store/review_process?language=english)).

## Production build gate

Use `Hollowfen -> Production -> Validate Release Build` in the Unity Editor before producing a candidate. The gate currently and correctly fails on the four placeholder identity values. It also rejects:

- a build scene list other than `Scene_MainMenu`, then `Scene_Hollowfen`;
- development, debugger, or profiler-connect flags;
- targets other than macOS or Windows x64;
- unavailable platform support modules;
- denied editor/vendor assemblies or compatible plugins;
- an existing output path that would be overwritten.

Batch release entry point:

```text
-executeMethod Hollowfen.EditorTools.ProductionBuildGate.BuildReleaseFromCommandLine
-hollowfenOutput <absolute-output-path>
-hollowfenTarget StandaloneOSX|StandaloneWindows64
```

`BuildAuditFromCommandLine` exists for non-development technical builds and skips only the unresolved identity check; it does not relax the technical, package, scene, option, or output checks.

## Release-candidate exit criteria

A candidate is ready for Steam review only when all of the following are true:

- the production gate passes with approved identity and version;
- clean Mac and Windows x64 release builds complete with zero errors;
- both exact uploaded builds install and launch through the Steam client;
- all advertised store features are implemented and exercised in the Steam build;
- Steam Cloud, if enabled, survives cross-platform upload/download and conflict testing;
- a signed/notarized Mac candidate passes Gatekeeper and Steam-client launch;
- a controller-only complete playthrough passes on physical Steam Deck hardware;
- the final NPC/art pass is integrated and reprofiled;
- all P0 provenance/licensing/AI-survey records are complete;
- each advertised language has a native review and layout pass;
- the full regression matrix remains clean after release-only integrations.

Until then, this audited branch should be treated as a strong **production-engineering candidate**, not a content-complete or final Steam submission package.
