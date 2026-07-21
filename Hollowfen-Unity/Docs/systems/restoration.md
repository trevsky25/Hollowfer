# Living Restoration System
Data-authored, save-backed village projects turn story outcomes into visible multi-stage world change; project stages are monotonic and can be reconciled upward from canonical quest/flag facts.
Key scripts: `Assets/_Hollowfen/Scripts/Restoration/` — RestorationProjectData/Database, RestorationProjects, RestorationSite/Board/UseTrigger/RevealDirector, and RestorationBenefits; UI: `Scripts/UI/RestorationLedgerScreen.cs`; authoring/verifiers: `Scripts/Editor/RestorationContentImporter.cs`, `BridgeRestorationImporter.cs`, `VillageRestorationExpansionImporter.cs`, and their focused verifiers.
Data/runtime: seven projects (`cottages`, `wend_bridge`, `jorens_forge`, `chapel_garden`, `crooked_pintle`, `witch_cottage`, `tobin_workshop`) are indexed by `Resources/RestorationProjectDatabase.asset`; each owns a `_LivingRestoration_*` root in `Scene_Hollowfen`.
Persistence: `SaveSlotMeta.RestorationProjects` stores project id, stage, started day, and changed day in aligned arrays; full saves and targeted restoration autosaves capture it, and old saves migrate from quest/flag rules.
Biggest gotchas: a project never regresses; contribution coins/flags/stage must commit atomically; first-use rewards and consequence flags share that transaction; sites toggle separate presentation roots rather than themselves; day rules are dependency-ordered to prevent same-dawn cascading; reveal cameras must be checked against real walls/trees at their authored world positions.
Status: the complete seven-project village arc is live through 2026-07-18: 14 atomic supply rows, staged world geometry, six story-gated work crews, seven dawn reveals, five permanent gameplay benefits, first-use completion, controller ledger, legacy migration, performance bounds, and isolated rollback/save verification.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Domain model

`RestorationStage` is a shared six-value progression:

1. `Unavailable`
2. `Surveyed`
3. `SuppliesCommitted`
4. `WorkUnderway`
5. `Restored`
6. `Occupied`

The vocabulary is generic enough for cottages, bridges, gardens, commercial interiors, and public works. A project may skip a stage when several story facts settle in one interaction; stage numbers are presentation/progression facts, not a requirement that every project expose five separate transactions.

`RestorationProjectData` owns stable project/localization ids, optional story quests, condition-to-stage reconciliation rules, title/body/short copy for each stage, localized ledger milestones, and optional contribution rows. Each contribution carries localized label/detail copy, positive copper cost, funded flag, and earliest stage; a project-level completion flag is set when every authored row is funded.

`RestorationProjects` is the static runtime store. It hydrates lazily, subscribes to score/quest/day changes, advances only upward, publishes `OnStageChanged(projectId, stage)`, and writes a targeted snapshot when the durable stage changes. `Contribute` validates availability and balance, stages purse + score flags + project state inside `SaveManager`'s atomic transaction, writes one full snapshot, and restores all runtime stores with zero publication if the final commit fails. `Advance` remains the authored work/first-use seam; story-backed rules remain the compatibility and narrative-authority layer.

## Cottage onboarding project

The project id is `cottages`; its story owner remains `cottagesReopen`.

| Canon fact | Minimum stage | World result |
|---|---:|---|
| `cottagesReopen` active | Surveyed | Both sites gain measured timber and repair marks. |
| `shutters_funded` | SuppliesCommitted | Pell's 24-copper record is inked; no dialogue ownership moves to the board. |
| `cottages_reopened_1` | WorkUnderway | Wenmar is usable; north lane shows timber, saw-horse, hand saw, repair ladder, tool crate, sawing dust, and scheduled villagers at work. |
| `cottages_reopened_2` | Restored | Both fronts are open; Pell's final conversation is available. |
| `cottagesReopen` complete | Occupied | Smoke, evening light, move-in supplies, lived-in doorstep props, and the permanent village board appear. |

The funding dialogue sets both `shutters_funded` and `cottages_reopened_1`, so the player normally moves directly from Surveyed to WorkUnderway. `SuppliesCommitted` remains a real shared stage for future projects and tools/debugging, but the cottage story does not manufacture an extra pause merely to display it.

## Wend bridge catalogue project

`wend_bridge` is the first post-tutorial project and unlocks at Surveyed when `cottagesReopen` completes. The existing large framed crossing at `(218.21, 29.54, 224.97)` is story-critical before restoration, so the old deck is never removed.

| Canon fact | Minimum stage | World result |
|---|---:|---|
| `cottagesReopen` complete | Surveyed | Warning posts, survey board, condemned outer deck, and a 2.9m protected central foot lane appear. |
| `wend_bridge_timber_funded` + `wend_bridge_iron_funded` | SuppliesCommitted | Theo's 24-copper oak and Joren's 12-copper fittings gather at the south bank. |
| first rollover after `wend_bridge_supplies_ready` | WorkUnderway | Joren, Theo, and Pell take distinct daytime work anchors; fresh outer decking and work props appear while the center remains open. |
| next rollover | Restored | A textured six-meter cart deck, complete rails, and four night lamps appear with the authored bridge reveal. |
| Wren crosses the reopened center trigger | Occupied / localized “In Use” | The project is inked complete and trade-cart/delivery dressing marks the west road as active. |

The bridge scheduler arrays are deliberately authored in reverse dependency order: `work_started → restored` is checked before `supplies_ready → work_started`. That prevents a single day rollover from setting both flags. The reveal listens only to the real `wend_bridge_restored` promotion; hydration cannot replay it.

## Full village expansion

The five post-bridge projects use the same vertical slice rather than a parallel quest system: a canon quest exposes Surveyed, two ledger contributions fund SuppliesCommitted, the next two dawns promote WorkUnderway and Restored, and the location's first real use atomically grants Occupied plus its lasting benefit.

| Project | Story gate | Supply cost | First use / permanent result |
|---|---|---:|---|
| Joren's Forge | `forgeKnife` complete | 16 + 14 copper | The lit hearth and tool rack reopen; mushroom cutting drops from six to five strokes. |
| Chapel Garden | `caldenReconcile` complete | 12 + 10 copper | Two cultivation beds open; planted crops mature in 75% of their normal time. |
| Crooked Pintle | `festivalHosted` complete | 18 + 20 copper | The east roadside common area reopens; completed daily requests pay +2 copper. |
| Sable's Cottage | `wendlightFound` complete | 22 + 16 copper | The nursery and spring return; wild-node cooldowns shorten by one day and the two established Witch's Path ending flags settle. |
| Tobin's Apothecary | `almyTeach` complete | 20 + 18 copper | The purchased vaulted workshop becomes usable; preparations and visible shelf stock open, and cultivated harvests yield +1 mushroom. |

Each reveal targets the visible finished work—not an abstract transform—and was surveyed from the final cinematic position. The Pintle's furniture/sign sit outside the real east-wall collider; Tobin's project retains the complete purchased Alchemy and Magic Lab building on the level mill terrace, with open authored thresholds and no replacement low-poly architecture; forge/garden/cottage framing avoids the roofs that obscured the first visual pass.

`RestorationBenefits` derives its values from Occupied stages, so no extra save schema or duplicate booleans exist. Foraging, cultivation, and village requests ask this single projection at the point they calculate strokes, respawn day, growth, yield, or copper. Rehydrating a save therefore recreates all benefits from the canonical restoration snapshot.

## Scene presentation

`RestorationSite` is an `IInteractable` on the Foraging layer. It lives on an always-active trigger host and toggles serialized `RestorationPresentation` roots by inclusive stage range. This prevents the inactive-listener bug and lets two sites interpret one project stage differently.

- Wenmar: survey dressing → open-home dressing during WorkUnderway/Restored → occupied dressing.
- North lane: survey dressing → active repair site during WorkUnderway → restored front → occupied dressing. The work stage adds a hand saw and restrained sawing-dust particles to the timber, saw-horse, ladder, and tool crate.
- Both restored fronts own a geometry-free evening `NightLight` and low-cost billboard smoke, avoiding a floating emissive pane when the site anchor does not exactly match the cottage wall.
- Move-in and occupied details instance scaled props from the existing Medieval Environment Pack—bucket, firewood basket, crate, bench, broom, pot, and ladder—beneath project-owned stage roots. The importer changes no third-party asset or prefab.

The derived NPC schedule adds visible labor without a second persistence model. Cottage WorkUnderway uses Joren/Pell from 07:00–18:30 and Bram's 11:00–13:30 meal visit. Bridge WorkUnderway uses Joren/Theo/Pell at distinct banks/deck anchors. Six later crews reuse Joren, Pell, Bram, and Almy at the actual forge, garden, Pintle, cottage, and mill sites. First-match rows sit above ordinary routines, story gates prevent premature travel, and each restored flag blocks its work rows immediately.

`RestorationBoard` is a separate always-active host beside Pell. Its visual root and interaction collider enable at Occupied. Later projects can be added to the database and will appear as additional ledger rows without rebuilding the screen.

The foundation command is `Hollowfen > Restoration > Build Cottage Foundation`; the bridge pass is `Build Wend Bridge Project`; the idempotent current-world command is `Build Full Village Expansion`. The full command heals the first two projects, upserts the five later data assets, rebuilds only owned `_LivingRestoration_*` roots, reapplies NPC schedules/catalogue order, and saves `Scene_Hollowfen`. No importer modifies a third-party source prefab.

## Dawn promotion and reveal

`DayFlagScheduler.FlagPromoted(day, whenFlag, thenFlag)` publishes only when a real day rule sets a new flag. It is cleared on subsystem registration and is deliberately not raised by `GameScores.HydrateFrom`, so loading a restored save never replays yesterday's presentation.

Each `RestorationRevealDirector` listens only for its project's real restored-flag promotion, records its day, and queues exactly once. It waits for the rest transition, screen stack, narration, story moments, and prop-focus lane to clear before taking ownership. `PropFocusCinematic` frames the authored visible work, hides the gameplay HUD, adds letterbox bars, plays the completion chime, and fades a non-interactive localized lower third. Reduced Motion uses the same final composition as a stable cut, with no orbital sweep. Camera, input, HUD, and caption are released through the existing presentation lease.

## Ledger UI

`RestorationLedgerScreen` is a runtime-registered `UIScreen` (`screenId=restoration-ledger`). It follows the 1920×1080 Canvas contract and centralized presentation lease. Worksite signs and the village board open it on the selected project.

The screen contains a controller-selectable seven-project catalogue, project-localized five-step timeline, current-stage interpretation, Pell-style Pencil/Ink milestones, permanent-benefit copy, location/day record, and Close. Projects with contribution rows add two clear funding actions inside the current-state card; paid rows become `✓ … FUNDED`, partial funding remains legible, and confirmation uses the shared parchment modal. The compact rail fits all seven rows at 1280×800; selected project uses forest fill while controller focus uses a separate gold outline. All player copy resolves through `Localization.Get`. Its gameplay modal lease hides the HUD, preventing compass/minimap overlap.

## Save compatibility

`RestorationSnapshot` uses four parallel arrays: `ProjectIds`, `Stages`, `StartedDays`, and `ChangedDays`. `SaveFileFormat` truncates them to shared length, clamps stages to `[0,5]`, and clamps days non-negative. Old schema-1 payloads deserialize the new object as null, so no envelope-version change is required.

New Game null-hydrates the store. Slot load hydrates after `GameScores`, so legacy flag reconciliation sees canonical facts. Hydration merges the stored stage upward with those facts without writing during the load, then rebroadcasts the final stage so world roots and board visibility update with the data. `SaveAll` captures the snapshot, while later durable stage changes call `AutoSaveRestorationProjects`. Neither an old flag nor a malformed earlier record can close a restored home.

Contribution publication is deferred through `SaveManager.PublishAfterAtomicCommit`, including hydration notifications used during rollback. A rejected final write therefore cannot leak a purse event, score flag event, restoration stage event, or UI refresh; disk revision, balance, flags, and stage all remain unchanged.

## Verification

`RestorationVerifier.RunAll()` must run in Play Mode with `SaveManager.EditorSaveDirectoryOverride` set to an isolated temporary directory. It proves exactly two cottage sites, one board, and one correctly framed reveal; interaction triggers, smoke, and night lights; monotonic advancement and regression refusal; visible WorkUnderway switching; a real next-day flag promotion queues exactly one deferred reveal; hydration does not replay it; legacy flag migration; full `SaveCoordinator` round-trip; rehydrated WorkUnderway/Occupied roots and board unlock; and aligned-array normalization.

`NPCScheduleVerifier.RunAll()` separately proves Joren, Pell, and Bram occupy distinct North Lane anchors at noon, Bram leaves after the meal window, the repair pair remains until 18:30, all three disperse afterward, and the restored flag permanently blocks the work rows.

`BridgeRestorationVerifier.RunAll()` owns its own unique temporary save directory. It proves cottages/bridge remain registered in the shared catalogue, bridge/deck y alignment, one bridge site/use trigger/scheduler/reveal, 2.9m pre-restoration lane, six-meter restored collider, three NPC work rows, exact 24 + 12 copper transactions, duplicate rejection, one-revision commits, distinct two-dawn promotion, reveal queue, first-crossing final state, stage-root switching, and injected final-commit rollback with zero state/event leakage.

`VillageRestorationExpansionVerifier.RunAll()` proves the seven-project catalogue and all five later world roots; ten new atomic contribution rows; distinct work/restored dawns; first-use idempotence and rejected-commit rollback; exact hope/knowledge rewards; all five gameplay benefits; Witch's Path consequences; six crew routes; stage-root collider safety; exactly three grow beds; four shadowless lights; at most seven particle systems, 18 shared materials, 430 authored renderers, and 240 renderers in the worst simultaneous stage combination.

`AccessibilityPresentationVerifier` separately proves 115% UI scaling across live canvases, Reduced Motion focus without scale animation, retained non-motion focus feedback, and caption-backing persistence while restoring the tester's device preferences afterward.

The Data Integrity report additionally checks project/database uniqueness and completeness, valid quest references, stage-copy coverage, and every fixed/project localization id.
