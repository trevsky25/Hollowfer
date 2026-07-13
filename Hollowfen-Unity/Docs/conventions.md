# Coding & Content Conventions
Hard conventions for all Hollowfen code and content. Review personas and future pre-commit linters check against this doc — if a rule graduates to a linter, note it here.
Scope: C# style, naming, folder structure, content-authoring rules, prohibited patterns, test-output template.
Sibling docs: `Docs/steam-constraints.md` (release requirements), `Docs/systems/*.md` (per-system architecture + gotchas).
Golden rules: data over hardcoded strings; localization IDs for all display text; gamepad-first UI; small verifiable batches.
Environment noise: third-party asset packs spew known-harmless console warnings — list at the bottom; don't "fix" third-party prefabs.
Status: living doc — add every new convention or gotcha class here as it's discovered.

> Self-healing doc: update alongside the rules you establish; keep the header accurate.

---

## C# style

- PascalCase class names matching filename, one MonoBehaviour per file.
- Private fields: `_camelCase` with `[SerializeField]` when editor-exposed.
- Avoid `public` fields; prefer properties or `[SerializeField] private`.
- Namespaces: `Hollowfen.Foraging`, `Hollowfen.Dialogue`, `Hollowfen.UI`, `Hollowfen.Settings`, etc.
- ScriptableObject classes end with `Data` (e.g. `MushroomData`, `NPCData`, `QuestData`).
- `[CreateAssetMenu]` menu paths like `Hollowfen/Mushrooms/...`.
- UnityEvents sparingly; prefer C# events between systems.
- No singletons except `GameManager`, `TimeManager`, `SaveManager`.
- `Time.timeScale` for pause; UI animations on unscaled time.
- Code comments only where logic is non-obvious.
- Never modify third-party code (StarterAssets, Magic Pig, NatureManufacture) — hook from outside (see `LookSensitivityHook` pattern in systems/settings.md) or create prefab variants.

## Asset naming

- Mushrooms: `MushroomData_T1_BrightCap` (tier-prefixed) / field-guide assets `Mushroom_NN_Name`
- NPCs: `NPC_Bram`, `NPC_Edda`
- Quests: `Quest_Act1_01_GetMillKey`
- Dialogue: `Dialogue_ActN_<Location/Beat>_<Speaker>`, idle variants `_Repeat` / `_Waiting`
- Story Cards: `StoryCard_Act1_01_Arrival`
- Scenes: `Scene_MainMenu`, `Scene_Hollowfen`, `Scene_OldWood`
- Locations: `LocationData_<Place>`
- Achievements: `ACH_ACT1_ARRIVAL`, `ACH_FORAGE_FIRST`, `ACH_NPC_BRAM_T2`, `ACH_END_INDEPENDENCE`

## Folder structure

```
Assets/
├── _Hollowfen/                   (everything we own; underscore sorts to top)
│   ├── Scripts/{Foraging,Dialogue,Quests,NPCs,Cultivation,Time,UI,Map,Save,Settings,Steam,Data,Editor}
│   ├── Data/{Mushrooms,NPCs,Quests,Dialogue,StoryCards,Locations,Characters}
│   ├── Prefabs/{Foraging,NPCs,UI}
│   ├── Scenes/
│   ├── UI/                        (sprites, fonts, atlases)
│   └── Audio/
├── ImportedAssets/                (Asset Store village, Mixamo/Meshy chars)
└── ThirdParty/
```

Never dump files in `Assets/` root.

## Content rules (narrative canon)

- The bible (`Docs/story.md`) is the source of truth — never invent NPCs, mushrooms, or locations not in it; ask instead.
- No romance content (Hollin is a companion, not a love interest).
- No emoji in dialogue or UI. No contemporary slang (slight archaism preferred). Nothing stronger than "Lord help me".
- Mushroom tiers — internal ids stay T1–T5 forever; **player-facing display names locked 2026-07-11** (QUESTIONS.md Q1, folk/trade-ledger register; Trevor may veto before they first render): T1 **"Basket Common"** (everyday, no tools) · T2 **"Knifework"** (needs Joren's knife) · T3 **"Yard-Grown"** (cultivation-only) · T4 **"Deepwood"** (rare wild, gates Witch's Cottage) · T5 bible-reserved, unnamed — **do not implement T5 yet**. When tier names first appear in UI/dialogue, use these via localization ids `tier.t1.name` … `tier.t4.name`.

## Prohibited patterns (linter candidates)

- Hardcoded player-facing strings (must route `Localization.Get`) — see systems/localization.md.
- Legacy Input Manager APIs (`UnityEngine.Input.`) — new Input System only.
- Saves outside `Application.persistentDataPath`.
- Mouse-only UI (every screen gamepad-navigable; default selection on open).
- Skipping the achievement event hook on quest/beat completion.
- Assuming Unity version features without checking the project version (Unity 6000.4.4f1).
- Editing Asset Store content in place (changes lost on package update).

## Fonts (TMP) — ship config (do not regress; fixed batch-32)

- **All shipping TMP font assets stay `atlasPopulationMode: Static` with `m_ClearDynamicDataOnBuild: 0`.**
  Dynamic mode strips the baked glyph table at build (empty text in the player) **and** re-serializes the
  `.asset` on every play-mode run (the old "git checkout the Georgia SDF churn" chore). Static ships the
  baked atlas and is churn-immune.
- Ship fonts: **`Assets/UI/Fonts/Georgia SDF.asset`** (primary serif — prose/dialogue/titles, **201 glyphs
  / 2 atlas pages**: ASCII + full Latin-1 Supplement + typographic punctuation) and **`Assets/TextMesh
  Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`** (fallback, **204 glyphs**, incl.
  → ← ○ that Georgia's serif source lacks). Georgia lists the fallback in its own `fallbackFontAssetTable`
  (TMP global fallback list is empty). All are multi-atlas-enabled so an over-1024 set spills to a new page.
- **`m_SourceFontFile` is nulled (`{fileID: 0}`) on both** so the source `.ttf` (Georgia is Microsoft-licensed)
  is NOT redistributed in the player — the baked SDF atlas ships instead. `m_SourceFontFileGUID` is kept for
  re-bakes. Do not re-link the source in a shipping asset.
- **Adding a new displayed glyph** (a localized string with a new character, a new UI symbol): re-bake,
  don't switch back to Dynamic. Recipe (bridge `execute_code`): (1) reflection-set `m_SourceFontFile` back
  to `AssetDatabase.LoadAssetAtPath<Font>(GUIDToAssetPath(m_SourceFontFileGUID))` (it's nulled for ship);
  (2) reflection-set `m_AtlasPopulationMode = Dynamic` and `m_IsMultiAtlasTexturesEnabled = true`;
  (3) `ClearFontAssetData(true)` → `TryAddCharacters(fullSet, out missing)`; (4) reflection-set
  `m_AtlasPopulationMode = Static`, `m_ClearDynamicDataOnBuild = false`, `m_SourceFontFile = null` again;
  (5) `SetDirty` asset + atlas textures + material → `SaveAssets`. `missing` tells you which glyphs the
  source .ttf genuinely lacks (those need the fallback or an icon set). After any reflection mode-flip,
  diff the asset against TMP's stock static `LiberationSans SDF.asset` for structural parity. Full worked
  example in `Docs/worksheets/batch-32-georgia-sdf-ship-fix.md`.
- Symbols no project font provides (✓ ✕ △ ◉) render as boxes — that's the controller-glyph/icon pass's job
  (QUESTIONS Q11), not a font-mode fix.
- Editor/build **parity** is the point: with Static, an unbaked glyph boxes during authoring instead of
  working in-editor and failing on Deck. One watch-out — culture-sensitive numeric formats (`"N0"` etc. on
  fr/ru locales) emit NBSP (U+00A0, baked) or NARROW NBSP (U+202F, **not** baked) as group separators; when
  localization lands, either force `InvariantCulture` for displayed numbers or bake U+202F.

## Test output template

End every implementation batch with:

```
### Test in Play Mode
1. Open scene `Scene_X`
2. Action...
3. Expected: ...
```

## Third-party console warnings (known-harmless, ignore)

Loading the village scene prints warnings from Magic Pig / NatureManufacture content: `BoxCollider does not support negative scale` (~30×), `The tree prefab_X couldn't be instanced because bounds could not be determined`, `TerrainCollider: MeshCollider is not supported on terrain`, `The referenced script (Unknown) on this Behaviour is missing!`. Pre-existing in the packs; cosmetic; don't edit third-party prefabs to silence them.
