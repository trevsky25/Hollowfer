# Menu Pages (Story / Wren / Field Guide)
Three ScriptableObject-driven menu pages form one controller-first field-journal family. Every screen is built programmatically in `OnInitialize` and targets Steam Deck at 1280×800 first.
Key scripts: `Assets/_Hollowfen/Scripts/UI/` — `StoryScreen`, `StoryDetailScreen`, `WrenScreen`, `FieldGuideScreen`, `MushroomDetailScreen`, plus the shared `Journal*` helpers.
Data: 30 `StoryCardData`, 21 `MushroomFieldGuideData`, and one `CharacterProfileData`, registered in ordered database SOs under `Assets/_Hollowfen/`.
Entry points: Main Menu and Pause buttons push `story`, `wren`, or `field-guide` through `UIManager`; detail screens push over their index and pop back to it.
Biggest gotchas: database order is canonical; locked content must use the same availability predicate for rendering, focus, opening, paging, and counting; sprite art goes through `JournalArtPresenter`; 3D journal art must use a dedicated visual-only preview asset, never a gameplay controller prefab.
Status: batch-71 turns Wren's page into an interactive living character study with an optimized animated model, studio lighting, and pointer/gamepad orbit and zoom; the existing dossier and five plates remain intact.

> Self-healing doc: if you change this system, update this doc (including the 7-line header) in the same batch, and note the change in the batch worksheet.

---

## Shared journal foundation

| Component | Role |
|---|---|
| `JournalChrome` | Shared close control, journal eyebrow/title/summary, bottom controller hint, flexible TMP sizing, restrained structural borders, and the common focus rail/wash treatment. |
| `JournalArtPresenter` | One aspect-aware sprite presenter. `cover=true` uses `AspectRatioFitter.EnvelopeParent` inside a `RectMask2D`; contained plates/photos use `FitInParent`. It also owns missing-art and tint state. |
| `JournalMushroomModelPresenter` | An isolated off-screen model/camera/light rig per visible modeled specimen. It owns an alpha-capable square RenderTexture, four short-range studio lights, and a short-lived preview material clone whose manifest-driven exposure balances dark and pale source albedos without touching gameplay materials. It disables gameplay behavior/colliders, auto-rotates on unscaled time, releases preview materials when recycled, and supports optional orbit/zoom interaction. |
| `JournalWrenModelPresenter` | Wren's visual-only off-screen character stage. It owns a landscape alpha RenderTexture, four-light portrait setup, preview-only material clone, and a Playables graph for the breathing idle. The rig has no player input, movement, camera-follow, collision, or gameplay scripts. Pointer/right-stick orbit, wheel/triggers zoom, and reset all use unscaled time and release their runtime resources with the screen. |
| `JournalNavigation` | Generic availability-aware lookup, previous/next search, available count, and page position. Detail paging cannot land on locked content. |
| `JournalText` | Resolves Story, mushroom, and character SO fields through derived localization IDs and `Localization.Get(id, englishFallback)`. Array fields receive stable indexed IDs. |
| `ScrollFocusFollower` | Keeps an explicitly navigated card or reading waypoint visible and restores the originating index position after Back. |

`FocusHighlight.Configure(...)` is the supported runtime setup API for new controls. Do not mutate its private cached color/graphic fields through reflection.

## Screens

| Component | Role and behavior |
|---|---|
| `StoryScreen` (`screenId="story"`) | Three-column memory index grouped by act. Cards are borderless at rest and use the shared rail/wash focus state. Locked cards are inert, non-selectable, darkened, and reveal neither title nor art. Focus begins on the first unlocked card, skips locked cards, and returns to the originating card/scroll position after detail. |
| `StoryDetailScreen` (`screenId="story-detail"`) | Fullscreen cinematic cover art with one readable right-side journal leaf. Previous/next walks unlocked cards only and reports available position (`N of M`). Annotations expand inside the leaf; Back hides annotations first, then pops the reader. |
| `FieldGuideScreen` (`screenId="field-guide"`) | Three-column square specimen index at Deck resolution. Undiscovered entries are inert `?` cards. A discovered species with dedicated journal 3D art shows an auto-rotating, transparently composited model and unboxed `3D STUDY` label; other species retain their realistic photo card. Model presenters are hydrated only while their card intersects the masked viewport (six rigs at the Deck target instead of twenty). Resting cards have no outline; focus uses the shared rail/wash state. |
| `MushroomDetailScreen` (`screenId="mushroom-detail"`) | Two-leaf specimen spread: the left leaf is a large 3D study on a soft halo (or an explicit pending state), with no nested RenderTexture rectangle. Detail framing fits rotation-safe model bounds directly to the camera plane with a 2% margin, making clicked specimens about 25% larger than the former sphere fit while index cards keep their original framing. Mouse/touch drag or the right stick orbits; wheel or LT/RT zooms; `R`/right-stick click resets. The realistic field photo and identification copy occupy the right leaf. |
| `WrenScreen` (`screenId="wren"`) | A two-column character atelier at the top: Wren's animated, transparently composited 3D model sits in a softly lit living-study arch while identity, lead copy, and a 2×2 stat block occupy the facing column. The supplied painting remains as a dim atmospheric backdrop. Drag/right stick orbits, wheel/LT-RT zooms, and `R`/right-stick click resets. The Background/Perspective dossier, kit, pull-quote, and interactive five-plate gallery continue below with explicit controller navigation. |

## Availability and return-focus contract

Each index/detail pair has one `IsAvailable` predicate. Use it everywhere:

1. Draw the locked/discovered state.
2. Set `Button.interactable` and explicit `Navigation` links.
3. Reject programmatic opens of unavailable content.
4. Compute previous/next and `N of M` through `JournalNavigation`.
5. Remember the originating SO and restore its card through `DefaultSelected` when the detail pops.

This prevents a UI-only lock from becoming a progression or controller-focus leak.

## ScriptableObject content

| SO | Notes |
|---|---|
| `StoryCardData` | 30 ordered assets in `_Hollowfen/Data/StoryCards/`; fields include act/scene/title/subtitle/body/Wren note/beats/image/unlock metadata. All 30 require images. |
| `MushroomFieldGuideData` | 21 ordered assets in `_Hollowfen/Data/Mushrooms/`; common/Latin names, edibility, prose, identification fields, photo/credit, gameplay `_worldPrefab`, optional `_journalPreviewPrefab`, and preview-only `_journalExposure`. Entries 01–20 have dedicated generated journal models; Oyster and Aldermark intentionally have no field photo, and Aldermark alone still lacks a model. |
| `CharacterProfileData` | Wren's profile prose, kit, pull-quote, hero, five field-study plates, dedicated `_journalModelPrefab`, breathing `_journalIdleClip`, and preview-only exposure in `_Hollowfen/Data/Characters/Character_WrenTobin.asset`. `WrenJournalModelImporter` rebuilds and wires the 3D fields. |
| `StoryCardDatabase` / `MushroomFieldGuideDatabase` | Canonical ordered registries. Never sort at runtime unless the design explicitly changes the content order. |
| `DataImporter` | Editor-only JSON import utility. Fresh imports resolve both gameplay and journal prefabs through `MushroomModelImporter` and its shared manifest; there is no second hardcoded model list. Run the model importer before a fresh data import, then run `DataIntegrity.RunAll`. |

## Verification and deferred work

- Batch-68 model evidence lives in `Docs/screenshots/batch-68/`. Batch-69 lighting/interaction evidence lives in `Docs/screenshots/batch-69/` and includes balanced Brightspore plus its zoomed/orbited state at 1280×800.
- Batch-70 detail-framing evidence lives in `Docs/screenshots/batch-70/brightspore-larger-detail-1280x800.png`; the all-model yaw audit found a worst projected fill of 0.980, so no modeled species crosses the camera edge at its default zoom.
- Batch-71 Wren evidence lives in `Docs/screenshots/batch-71/`: the default living-study spread and an orbited/zoomed inspection state at 1280×800. The shipping derivative measures 89,999 triangles / 61,849 vertices, down from the 542,469-triangle / 304,206-vertex source.
- `DataIntegrity` asserts database counts (30/21), required journal fields, all Story images, the Wren hero/five plates/model/idle/exposure, fixed journal localization IDs, and that every manifest-backed species has both a species-correct `MushroomNode` world prefab and dedicated journal prefab.
- Dedicated 3D journal coverage is 20/21. Aldermark shows `Three-dimensional study pending` until a real Maitake model is delivered; substituting a different species is prohibited by the educational/canon rule.
- Oyster intentionally has no field photo; its right-hand description block uses the localized missing-sketch state while retaining the authored 3D study on the left.
- The localization routing is complete; Simplified Chinese translations and per-content LUT rows remain a pre-EA content pass.
