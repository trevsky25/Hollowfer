# Presentation Image Provenance Register

Last verified: 2026-07-17

## Scope and method

This register covers the raster assets shipped through Hollowfen's main menu, application branding, Wren dossier, story-card UI, opening sequence, journal cinematic, and dialogue-transition presentation. It excludes mushroom field images (tracked separately in `mushroom-image-provenance.md`), model material textures, the world map, controller glyphs, screenshots, and third-party package textures.

The audit traced Unity GUIDs from project settings, enabled scenes, character/story data, and story moments; inspected readable PNG metadata; and recorded SHA-256 hashes. No C2PA verification utility is installed, so the embedded C2PA/JUMBF fields below were inspected but their cryptographic signatures were not independently validated.

## OpenAI C2PA-declared assets

The 39 files in this table contain readable C2PA/JUMBF fields identifying `gpt-image`, `trainedAlgorithmicMedia`, `OpenAI Media Service API`, and signer identity strings for `OpenAI OpCo, LLC` / `OpenAI Media Service`. The metadata establishes each file's claimed generation origin, not its license or account ownership chain.

| Asset | C2PA creation time | Agent version | SHA-256 |
| --- | --- | --- | --- |
| `Assets/UI/Images/main-menu-wren.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `02befd5123e10b29ce5f4060c5fc0a70d7a7038d0f78fea87746d9e447bb135c` |
| `Assets/_Hollowfen/UI/Characters/wren-profile.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `5d5b2c867e11d2cf44b3b22cb141c22a1723cfe2f10939c59b3e2566f76fa593` |
| `Assets/_Hollowfen/UI/StoryCards/aldric-offer.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `74afabd48dc92ad0cd80f09bf59f29a9721a6f1ca84c88a09ad7f4b96a987bd0` |
| `Assets/_Hollowfen/UI/StoryCards/almy-doorway.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `2c3a78159ce8591b2e0c9e3d9f07dabe9bc76e996c3fdebeb31d542aac0221a7` |
| `Assets/_Hollowfen/UI/StoryCards/almy-lessons.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `195205ee9e2398515acc2be920ae72cfdb75a5029b4f339eb750a4ac5cb280a7` |
| `Assets/_Hollowfen/UI/StoryCards/caldens-doubt.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `9f39952090acf2c83837d9113e1fe57d8c2ca024a9bf52bf56fb589159d05de4` |
| `Assets/_Hollowfen/UI/StoryCards/chapel-garden.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `cc22d9d53567108de5f513ed18ccc486610beac2bbdc8850b7d89ca65f7b1ea1` |
| `Assets/_Hollowfen/UI/StoryCards/cottages-reopen.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `521e3ce8b0b19d73c7f0ed2a1ad06b9aaef2a996adb0b419b146d8684da1b98b` |
| `Assets/_Hollowfen/UI/StoryCards/crooked-pintle.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `ead65d24e74737f28328523548557d8c82b523fa8d927e8c1af1f5f6ad59d09d` |
| `Assets/_Hollowfen/UI/StoryCards/edda-apprentice.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `f35fd5ff9f8c9c4aff6a2273c8faaa645e554758d160181bdcdf599f1d3a941d` |
| `Assets/_Hollowfen/UI/StoryCards/edda-grandfather.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `4e33c7f647b9c59488cf3453f922b0086fd9a62db8bba7c5403cd7ce7f5493d2` |
| `Assets/_Hollowfen/UI/StoryCards/ending-capital.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `1b2be311956bafb9423305d4c95a3ddaad78551804bb98977f8aa1368b5d6d5c` |
| `Assets/_Hollowfen/UI/StoryCards/ending-free-hollow.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `bfa703a2223cac3a6dac0d177f568d37393def374cfb881a42e35aae15b66021` |
| `Assets/_Hollowfen/UI/StoryCards/ending-lordly-patronage.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `d5d90c5ba7c112583338ddbadf50a6d6ce6266ba0fac6ce2003a189753554d9b` |
| `Assets/_Hollowfen/UI/StoryCards/ending-witchs-path.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `714d117e451469aaaba1295ca873b6a74fcf41b99349f26bab08971a1bb1524d` |
| `Assets/_Hollowfen/UI/StoryCards/fathers-mill.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `f87e775cb88067b11cb53a3bfb7e0caa52b81b33c456f568edb3337ad3eb5a46` |
| `Assets/_Hollowfen/UI/StoryCards/first-festival.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `b0dbb1433347e5e3990467f1e3f0684a89c616d7859561a7ad7b68f1e588dc9e` |
| `Assets/_Hollowfen/UI/StoryCards/first-forage.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `5e22980519821b757325b1ddf60013edfb3df9794d9e58813e5260df32144771` |
| `Assets/_Hollowfen/UI/StoryCards/hidden-journal.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `c0dfc85fb17db4d69c4420cdc798fe73982792759cde892b4514464ce7177792` |
| `Assets/_Hollowfen/UI/StoryCards/hollin-arrives.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `97a0d5cfb167b2cdcf0bb6e8cb72c4933447bb761c9fe36cb22998b8b7e07c1a` |
| `Assets/_Hollowfen/UI/StoryCards/hollin-inheritance.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `ade804ef3acde26bc3be3803c11e9cc1f885b056ce5940ea84c09788ef247cf5` |
| `Assets/_Hollowfen/UI/StoryCards/homecoming.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `a4ee1a28b248e3a751e87369b6d299af0736590f0517bf6bcc4a61a47f849d3d` |
| `Assets/_Hollowfen/UI/StoryCards/intro-01-ridge.png` | `2026-07-13T00:00:00Z` | `2.0` | `5007fba7ef83a62d359fbdadb3c91322e767ca66948d52ac669558214b3d3865` |
| `Assets/_Hollowfen/UI/StoryCards/intro-02-river.png` | `2026-07-13T00:00:00Z` | `2.0` | `372708cf6a56a3607ac27e6b9b6c29913832ce2a5546d294d9551c97e6fcea13` |
| `Assets/_Hollowfen/UI/StoryCards/intro-03-cottages.png` | `2026-07-13T00:00:00Z` | `2.0` | `a06ffcb7e6eac28ee07e63f4426e875306e1d40348cdffaaa5dfdaba4b4dbff1` |
| `Assets/_Hollowfen/UI/StoryCards/intro-04-square.png` | `2026-07-13T00:00:00Z` | `2.0` | `c6ab0327f021259bf7aab770812340d74d3d72725d2475d51ad7d10a6e74b1b0` |
| `Assets/_Hollowfen/UI/StoryCards/jorens-forge.png` | `2026-05-01T00:00:00Z` | `pre-2.0` | `26d2b08e34835855e19dd365630550806aa9113a23fb59866c10173e9abcf5ed` |
| `Assets/_Hollowfen/UI/StoryCards/journal-01-sketches.png` | `2026-07-13T00:00:00Z` | `2.0` | `d5b57f2201e0f4315ddc3b5192fc34cbec7f195a6b56dc72f31403009aaf142c` |
| `Assets/_Hollowfen/UI/StoryCards/journal-02-spores.png` | `2026-07-13T00:00:00Z` | `2.0` | `b3e937da653a8e58c259f615c817e369b06a179608c9bc911417992756b41e33` |
| `Assets/_Hollowfen/UI/StoryCards/journal-03-note.png` | `2026-07-13T00:00:00Z` | `2.0` | `f4f7eca96739a75c44c0030b32cd6c180e5bfb1609e4f8c141915aca45b4f283` |
| `Assets/_Hollowfen/UI/StoryCards/marra-kitchen.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `4c0c67c4cbbecc4506ee788b2f0ede041976833416f44018432aaa7fc72d49e3` |
| `Assets/_Hollowfen/UI/StoryCards/meeting-aldric.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `ba392c920ce646fa5cfb1058858c596283c522845e35d370a6716cd5e7e626cb` |
| `Assets/_Hollowfen/UI/StoryCards/sealed-letter.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `128209de01e01af4b355489162205860762f2365aedcea2c3241f24df05597ea` |
| `Assets/_Hollowfen/UI/StoryCards/theo-capital-offer.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `a1f7246b17bbfe06da9da9e434269ce216144db243e1247bdc07f73bb11a73e2` |
| `Assets/_Hollowfen/UI/StoryCards/theo-trade.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `b1bf780429db1a557581aa8e97c446a2445ceb3884d4f1226a9eb135d8815dbb` |
| `Assets/_Hollowfen/UI/StoryCards/voss-first-visit.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `aac32ebc1f223a9921340584ceafcb9ccd16f92632d71ffa9d99a8eb1bf1c664` |
| `Assets/_Hollowfen/UI/StoryCards/wend-source.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `7fc8f4298599ad4e46a79bd12d09007beee072a2f89e1b397a01c0dbe8fe5812` |
| `Assets/_Hollowfen/UI/StoryCards/wend-truth.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `5ff9bf76f3fa33d4c4df17416f1a14b8b147957d615fde101e43c4ad837f5f68` |
| `Assets/_Hollowfen/UI/StoryCards/witch-cottage.png` | `2026-04-29T00:00:00Z` | `pre-2.0` | `5df413a5164b8704d9ce0e2eee9267f41483d30f3eff7c27ce179d9a8de2969c` |

## Unresolved source records

These six shipped files have no readable OpenAI/C2PA or other creator attribution sufficient to classify them as original, vendor-licensed, or generative-AI output. Project documentation describes the five Wren study files as "newly supplied" but does not record their creator or source. `wren-figure-front.png` includes generic Adobe XMP namespace data, which identifies neither authorship nor rights.

| Asset | Current use | SHA-256 |
| --- | --- | --- |
| `Assets/_Hollowfen/UI/Branding/Hollowfen_AppIcon.png` | Standalone application icons in `ProjectSettings.asset` | `510f50b184f965e6fe566c70b85c0c9ea9da391d585ff5f35443bcf8d13ff14d` |
| `Assets/_Hollowfen/UI/Characters/wren-figure-back.png` | Wren dossier | `f54d2c71cc9979584fe0322edc8c7a31fa9b139e7608c295e74cf173dfe781b7` |
| `Assets/_Hollowfen/UI/Characters/wren-figure-front.png` | Wren dossier | `9c54cc6b19102248ca801763116223ef100262aa0a792d09844640a8b96d7501` |
| `Assets/_Hollowfen/UI/Characters/wren-figure-threequarter.png` | Wren dossier | `f32f3f7401b248a6575112b6abbdd921048b198648f1aa4c9cc8b7daa14cc515` |
| `Assets/_Hollowfen/UI/Characters/wren-knife-plate.png` | Wren dossier | `a319839611e90c67d0c259fe68e0e97d45cb3a2c82b34609996150aa3b402b76` |
| `Assets/_Hollowfen/UI/Characters/wren-study-sheet.png` | Wren dossier | `6d73c57355e64f3e8c5c1cf7e22d4997d689ecea45968504db5cb18d37098242` |

The source record for each must identify the creator or generation tool, delivery or generation record, applicable license/terms, and any derivative relationship between the study sheet and individual figure crops before release.

## Project-local UI primitives

These simple gradient strips contain no creator metadata. They entered the repository with the original project foundation and are recorded as project-local UI primitives, but the source tool/file is not retained.

| Asset | SHA-256 |
| --- | --- |
| `Assets/UI/Images/gradient-bottom.png` | `71ccc03b09f9277f5db2cd19b739064291bcadfac0ec24ed17878efb44506b60` |
| `Assets/UI/Images/gradient-left.png` | `de34819c746bc064f66c6b825c744a126c0d5e834d2f4c2777c6ea23cddbafab` |

No vendor raster asset was found in this audit's presentation scope. Vendor package textures elsewhere in the project require their own purchase/license evidence and are not covered here.

## Reference integrity

- The main-menu background and application icon GUIDs resolve to existing files.
- All six Wren dossier raster fields resolve.
- All 30 `StoryCardData` image references resolve.
- The four-image opening sequence and three-image hidden-journal cinematic resolve completely.
- The 75 dialogue assets have no portrait or illustration field; dialogue presentation intentionally uses the live 3D camera.
- The only non-null dialogue transition is `Dialogue_Act1_MarraKitchen_FirstBasket`. Its `StoryMoment_Act1_MarraKitchen` has no explicit image by design and correctly falls back to the valid `marra-kitchen.png` Story Card image.
- `DialogueScreen._parchmentSprite` is null, but that field controls an optional parchment wash and is not a missing dialogue illustration.

No genuine missing dialogue or cinematic illustration reference was found.

## Credits and release follow-up

The shipped credits explicitly describe Wren character art as generated, which is directly supported for `wren-profile.png` but not yet evidenced for the five supplied study files. The credits do not currently disclose the OpenAI origin of the main-menu painting, Story Card paintings, opening sequence, or journal sequence. That omission is not recorded here as a proven license violation, but the images must be included accurately in Steam's pre-generated AI Content Survey.

Before release:

1. archive generation receipts/job records, account ownership evidence, and the applicable OpenAI terms for the 39 C2PA-declared files;
2. resolve the app icon and five Wren study-file source records without inferring provenance from appearance;
3. decide whether the in-game credits are intended as a complete production disclosure and, if so, add a truthful Story Card/cinematic art line after the source records are complete; and
4. update this register and hashes whenever a raster is replaced or re-exported.
