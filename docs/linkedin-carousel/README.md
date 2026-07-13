# Hollowfen — LinkedIn Carousels

Two companion build-in-public carousels (landscape 16:9, 13.33″ × 7.5″ document PDFs):

1. **"How I Built This"** — the AI-enablement workflow (9 slides).
2. **"An Illustrated Story"** — Wren's story told through the project's story-card art (8 slides).

## Files
- `Hollowfen-How-I-Built-This.pdf` + `hollowfen-carousel.html` — the workflow deck.
- `Hollowfen-The-Story.pdf` + `hollowfen-story-carousel.html` — the illustrated story deck
  (image-forward; the HTML references `public/story/cards/*.png`, so keep it in this folder).
- `LINKEDIN-POST-story.md` — the caption to paste above the story PDF.

## Slide flow
1. Hook — building a game to learn to work alongside AI
2. What Hollowfen is + the stack
3. The loop — Claude reads/writes the Unity editor over an MCP server (diagram)
4. World — walkable village from the purchased Medieval Village pack
5. Wren — Meshy character + animation set + third-person controller
6. Forage-and-identify loop — 16 species, field-guide data (Chanterelle example)
7. Before → after — plain-language ask vs. the driven sky/fog time-of-day system
8. What it taught me — where the line between me and the model sits
9. CTA — still in progress; full illustrated story linked separately

## Fill these in before posting

**Caption text placeholder**
- `[MCP SERVER NAME]` (slide 3) — replace with the exact Unity MCP server you ran.

**Screenshot placeholders** (labeled dashed frames in the deck)
- Slide 3 — Unity editor + Claude session side-by-side, MCP server connected
- Slide 4 — Hollowfen village in third-person (Wren on a foggy lane between timber houses)
- Slide 5 — Wren close-up + animation states (idle · walk · run · jump)
- Slide 6 — the in-game Field Guide entry / identification prompt

## Re-render to PDF
Any Chromium/Chrome will do (prints one page per `.slide` via the `@page` rule):

```bash
chrome --headless --disable-gpu --no-pdf-header-footer \
  --print-to-pdf=Hollowfen-How-I-Built-This.pdf \
  "file://$PWD/hollowfen-carousel.html"
```

The page size (1280×720 px → 960×540 pt) is set in CSS `@page { size: 1280px 720px; margin: 0 }`,
so no print flags are needed for dimensions.

## A note on accuracy
Every mechanic is grounded in the real project: the 16-species field guide and data
(`src/data/*.js`), the village-from-prefabs layout (`src/world/`), the Meshy Wren character
and its animation set, and the day/night sky + fog system. The carousel frames the workflow
around the Unity-editor-over-MCP loop per the author's direction; the playable prototype in
this repo is the Three.js web build, listed honestly in the stack.
