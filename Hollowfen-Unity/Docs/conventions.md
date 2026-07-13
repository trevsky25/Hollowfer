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

## Fonts (TMP) — ship config (do not regress; fixed batch-32, migrated to OFL fonts batch-55)

- **All shipping TMP font assets stay `atlasPopulationMode: Static` with `m_ClearDynamicDataOnBuild: 0`.**
  Dynamic mode strips the baked glyph table at build (empty text in the player) **and** re-serializes the
  `.asset` on every play-mode run (the old churn chore). Static ships the baked atlas and is churn-immune.
- **Ship fonts (batch-55 — Georgia retired, was MS-licensed ship blocker):**
  - **`Assets/UI/Fonts/IMFellEnglish SDF.asset`** — DISPLAY/title role (the "Hollowfen" wordmark, section/act
    headers, StoryCard titles). **201 glyphs / 2 atlas pages** (ASCII + Latin-1 + typographic punctuation).
  - **`Assets/UI/Fonts/EBGaramond SDF.asset`** — BODY + UI role (nav, buttons, paragraphs, labels, eyebrows,
    tooltips). **212 glyphs / 2 atlas pages**. This is the **TMP default font asset** (`TMP Settings.asset`
    `m_defaultFontAsset`), so any TMP text with no explicit font resolves to it.
  - **`…/LiberationSans SDF - Fallback.asset`** — fallback wired on BOTH via each font's own
    `fallbackFontAssetTable` (TMP global list stays empty). It carries → ← ○ and the few dash/prime glyphs
    the old-style serifs lack (IM Fell misses 9, EBG misses only ○). All assets are multi-atlas-enabled.
  - Both are **OFL (SIL Open Font License)** — source `.ttf` + each family's `OFL.txt` are KEPT in
    `Assets/UI/Fonts/` and DO ship (OFL requires the license to travel with the font). **Unlike Georgia, do
    NOT null `m_SourceFontFile`** — OFL permits redistributing the source, and keeping it linked simplifies
    re-bakes. Credit line: `credits.fonts` in `Localization.cs`.
- **Runtime font wiring (batch-55):** code-built screens get their fonts from `UICanvasUtil.HeadingFont`
  (IM Fell) / `BodyFont` (EBG). `UIManager.Awake` injects both via `SetHeadingFont/SetBodyFont` from
  `[SerializeField]` refs on the scene UIManager, so headings resolve **in a player build**, not just via the
  editor-only `AssetDatabase` fallback (the pre-batch-55 latent bug where built headings silently fell back to
  body). If you add a screen that builds before UIManager.Awake, keep the editor `AssetDatabase` fallback path.
- **Adding a new displayed glyph** (a localized string with a new character, a new UI symbol): re-bake,
  don't switch to Dynamic permanently. Fresh-asset recipe (bridge `execute_code`, CodeDom C#6 — no `using`,
  fully-qualify types): `TMP_FontAsset.CreateFontAsset(srcFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
  AtlasPopulationMode.Dynamic, true)` → `AssetDatabase.CreateAsset` → `TryAddCharacters(fullSet, out string
  missing)` → `AddObjectToAsset` each atlas texture + the material → wire `fallbackFontAssetTable` →
  reflection-set `m_AtlasPopulationMode=Static`, `m_ClearDynamicDataOnBuild=false`,
  `m_IsMultiAtlasTexturesEnabled=true` → `SetDirty` asset+atlas+material → `SaveAssets`. `missing` tells you
  which glyphs the source genuinely lacks (those fall to the fallback). Full worked example in
  `Docs/worksheets/batch-55-font-migration-imfell-ebgaramond.md` (batch-32's in-place reflection re-bake of an
  existing asset still applies when you must preserve a GUID). `AssetDatabase.DeleteAsset` is blocked by the
  bridge safety check — delete via the `manage_asset` tool instead.
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
