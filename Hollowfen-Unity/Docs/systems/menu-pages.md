# Menu Pages (Story / Wren / Field Guide)
Three data-driven menu pages reachable from Main Menu, all built programmatically in `OnInitialize` from ScriptableObject databases. Visuals match the web prototype at `/src/` (design source of truth): Georgia serif headings, sage/gold/cream palette, full-bleed heroes, gold-rule act dividers.
Key scripts: `Assets/_Hollowfen/Scripts/UI/` — StoryScreen, StoryDetailScreen, WrenScreen, FieldGuideScreen, MushroomDetailScreen, StoryCardCell, MushroomCardCell.
Data: 30 `StoryCardData` + 17 `MushroomFieldGuideData` + 1 `CharacterProfileData` SOs under `Assets/_Hollowfen/Data/`, registered in ordered database SOs.
Entry points: Main Menu NavRow buttons push `story` / `wren` / `field-guide` screen IDs.
Biggest gotchas: iteration order of database SOs is canonical (matches web JS files); `DataImporter` editor tool regenerates SOs from JSON dumps in `Hollowfen-Unity/Temp/`.
Status: shipped + verified. Screens read raw SO fields — `Localization.Get` wiring is a deferred follow-up.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Screens

| Component | Path | Role |
|---|---|---|
| `StoryScreen` | `_Hollowfen/Scripts/UI/StoryScreen.cs` (`screenId="story"`) | Three-column grid grouped by act. Each act block: gold uppercase label · gold-fading rule · `N CARDS` count badge. Cell = photo + scene eyebrow + serif title + subtitle. Click → StoryDetailScreen. |
| `StoryDetailScreen` | `_Hollowfen/Scripts/UI/StoryDetailScreen.cs` (`screenId="story-detail"`) | Full-bleed hero with bottom-up dark gradient + content scrim. 3-col overlay: heading column · body between vertical separators · italic Wren note (gold left border) + beats list. Top-right `✕`, bottom prev/page-indicator/next walking the database. |
| `WrenScreen` | `_Hollowfen/Scripts/UI/WrenScreen.cs` (`screenId="wren"`) | Single scrolling page. Hero = portrait left + dark-gradient info panel right (eyebrow, serif name, italic tagline, lead paragraph, 2×2 stats grid w/ gold values). 4-tile kit row, two body cards, gold-bordered italic pullquote. |
| `FieldGuideScreen` | `_Hollowfen/Scripts/UI/FieldGuideScreen.cs` (`screenId="field-guide"`) | Four-column grid. Cell = photo + serif common name + italic Latin + edibility dot + tinted label. Click → MushroomDetailScreen. |
| `MushroomDetailScreen` | `_Hollowfen/Scripts/UI/MushroomDetailScreen.cs` (`screenId="mushroom-detail"`) | Hero photo + edibility chip + serif name + italic Latin + description + meta strip (HABITAT / SEASON / LOOK-ALIKES). Bottom row: "Identifying features" bullets + "Forager's note" italic. Bottom-left Back. |

## Cell helpers

- `StoryCardCell` / `MushroomCardCell` — tiny MonoBehaviours on instantiated cells holding the SO ref + an `Action` callback wired by the screen.
- `ScrollFocusFollower` keeps gamepad focus visible (see ui-framework.md).

## ScriptableObject content

| SO | Path | Notes |
|---|---|---|
| `StoryCardData` | `_Hollowfen/Scripts/Data/StoryCardData.cs` | `id, act, scene, title, subtitle, body, wrenNote, beats[], image, unlockAt, questId, displayNameId, descriptionId`. 30 assets at `_Hollowfen/Data/StoryCards/StoryCard_NN_*.asset` — verbatim from `src/data/StoryCards.js`. |
| `MushroomFieldGuideData` | `_Hollowfen/Scripts/Data/MushroomFieldGuideData.cs` | `id, commonName, latinName, edibility (enum), edibilityLabel, description, idFeatures[], habitat, season, lookalikes, notes, photo, photoCredit`. 17 assets at `_Hollowfen/Data/Mushrooms/` (16 web-imported + hand-authored Oyster). |
| `CharacterProfileData` | `_Hollowfen/Scripts/Data/CharacterProfileData.cs` | Holds any cast member; one Wren asset at `_Hollowfen/Data/Characters/Character_WrenTobin.asset`. |
| `StoryCardDatabase` / `MushroomFieldGuideDatabase` | `_Hollowfen/Scripts/Data/` | Registry SOs with ordered arrays. Screens read a `[SerializeField]` reference; **iteration order is canonical** (mushrooms match `src/data/mushroomIndex.js`; cards match JS-file order). |
| `DataImporter` | `_Hollowfen/Scripts/Editor/DataImporter.cs` | One-shot editor utility parsing JSON dumps in `Hollowfen-Unity/Temp/` to recreate the SO assets. When web data changes: re-export JSON, run via MCP `execute_code`, re-import. |

## Image assets

PNGs from `/public` imported under `_Hollowfen/UI/{StoryCards,Mushrooms,Characters}/` (47 files), configured Sprite (2D and UI). Sprite refs wired by `DataImporter`.

## Deferred

- **Localization IDs are stamped on every SO** (`story.<id>.title`, `mushroom.<id>.name`, …) but screens read raw fields. Wiring through `Localization.Get(id)` needs the LUT entries added first (see localization.md + TODOS.md).
