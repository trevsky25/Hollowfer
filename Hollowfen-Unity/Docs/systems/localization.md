# Localization
All player-facing strings flow through `Localization.Get(stringId)` — never hardcode display text. Real dictionary LUT (no longer a passthrough); English-only today, Simplified Chinese required for EA launch.
Key script: `Assets/_Hollowfen/Scripts/Localization.cs` (static `_table` dictionary; add new IDs there).
ID conventions: `story.<id>.title`, `mushroom.<id>.name`, `prompt.<context>.verb`, quest/dialogue IDs stamped on SOs.
Localized today: quest names/objectives (QuestHUD), prompt verbs + NPC display names (InteractionPromptHUD), grow-bed prompt, map side-card content, location names (`loc.<id>.name/.desc`), confirm-modal copy.
NOT localized (audited 2026-07-11): dialogue lines + speaker names + advance hint (DialogueScreen), QuestHUD eyebrow, StoryBeats narration captions, map chrome (region-name switch, "VILLAGE"/"REGIONAL", bar labels), menu pages (raw SO fields), `SaveSlotMeta.CurrentQuest` (stores localized TEXT at save time — wrong language after switching).
Status: infrastructure live; coverage partial with known gaps above. Translation pass is a pre-EA milestone (TODOS.md).

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Rules

- New player-facing string → add an ID to `Localization._table`, reference the ID. No literal display text in scripts or SOs' display paths.
- `IInteractable.PromptVerb` returns a localization KEY (e.g. `"prompt.inspect.verb"`), the HUD resolves it — single funnel for interaction verbs.
- Localization IDs are stamped on every content SO even where not yet consumed, so the wiring pass is mechanical.
- Target languages at EA: English, Simplified Chinese.

## Deferred

- Wire menu pages through `Localization.Get` (needs LUT entries for the 30+17+1 content SOs).
- Extraction tooling: a future EditMode test should assert every ID referenced in code/SOs exists in the LUT (Phase 2 infra — tests.md).
