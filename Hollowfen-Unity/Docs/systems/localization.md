# Localization
Player-facing strings resolve through `Localization.Get`; never add literal display text directly to UI scripts. The live LUT is English today, with Simplified Chinese required for EA.
Key script: `Assets/_Hollowfen/Scripts/Localization.cs`; `Get(id)` reports the raw id on a miss, while batch-63 `Get(id, englishFallback)` keeps SO-backed content readable until its translation row exists.
ID conventions: `story.<id>.<field>`, `mushroom.<id>.<field>`, `character.<id>.<field>`, `prompt.<context>.verb`, plus fixed `journal.*` and `ending.*` chrome IDs.
Localized/routed today: quest/objective text, prompt verbs/NPC names, map/location content and chrome, modal copy, loading/save-slot presentation, the journal family (including candidate-page browsing and enlarged-spread controls), mushroom content outside the journal, ending decisions/epilogues/credits, quest HUD chrome, the six-beat homecoming opening, and live text painted onto story-moment pages.
Known unrouted areas: dialogue lines and choices, plus the Act I completion captions in StoryBeats. Dialogue still has no stable per-line or per-choice localization IDs. `CurrentQuestId` is now preferred over the slot row's cached English display string.
Status: infrastructure live; journal routing is complete, the hidden-journal letter resolves as localized live page text in its dedicated cursive role, and voiced story moments cannot hide their matching caption. Simplified Chinese is not yet shippable: there is no language store/selector, translated table, or CJK-capable TMP fallback, and several runtime surfaces still contain English literals.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Resolution rules

- Fixed chrome calls `Localization.Get("journal.…")`; every consumed fixed journal ID is asserted by `DataIntegrity`.
- First-identification browser chrome uses fixed `inspect.browser.*`, `inspect.lens.*`, `inspect.quiz.*`, and `inspect.discovery.*` IDs. Species names remain inside the authored candidate spreads during Match; no separate target-name string may be rendered until `IsFieldIdentified` succeeds. Candidate-specific wrong feedback formats a localized sentence around one localized species feature. Discovery annotation copy formats the localized species/place at display time while its save record retains only stable IDs.
- SO-backed journal copy calls `Localization.Get(derivedId, englishFallback)` through `JournalText`. This is deliberate: the canonical English remains on the SO, while an authored LUT entry overrides it without changing screen code.
- Stable derived examples: `story.homecoming.title`, `mushroom.flyAgaric.name`, `mushroom.flyAgaric.feature.0`, `character.wrenTobin.kit.0.name`.
- Array members use their canonical index in the ID. Reordering beats/features/kit items changes the semantic key and must be treated as a content migration.
- `IInteractable.PromptVerb` returns a localization key, not display text; the HUD resolves it.
- `StoryMomentData.PageText` resolves `_pageTextId` with `_pageTextFallback`; the cinematic overlay draws that result on the authored image rect rather than baking English into the art. Optional paragraph reveal beats keep the localized writing synchronized with its caption and VO, while `_useCursivePageText` changes typography without changing the localized string.
- Target languages at EA: English and Simplified Chinese.

## Journal implementation

`JournalText` is the only presentation adapter for journal SO copy:

- Story: act, scene, title, subtitle, body, Wren note, and each beat.
- Mushroom: common/Latin names, edibility label, description, habitat, season, look-alikes, note, credit, and each identifying feature.
- Wren: role, home, age, work, keepsake, tagline, dossier prose, pull-quote, and kit item names/lines. Display-name/lead IDs already stamped on the character SO continue to resolve directly.

Locked copy, counters, section headings, paging, missing-art/model-pending text, 3D-study/photo labels, controller hints, and plate captions are fixed `journal.*` table entries. Locked Story and mushroom cards never resolve or display their hidden content/model fields.

## Integrity and deferred translation work

`DataIntegrity.RunAll` currently proves every fixed journal ID exists and that required SO text fields are populated. It does not require all derived IDs to exist yet because their canonical English fallback is intentional.

Remaining localization work:

1. Add Simplified Chinese storage/selection to the localization service.
2. Export the derived content-ID inventory and author English/Chinese rows.
3. Move dialogue lines/choices and the remaining Act I completion captions onto stable IDs.
4. Promote translated-language layout checks at 1280×800 into the Play-mode verification pass.
