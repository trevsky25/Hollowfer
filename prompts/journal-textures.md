# Journal Texture Generation — Brief for Codex

## Purpose

Generate two photographic-quality texture images that the Hollowfen pause-menu journal will use as background layers, replacing the current CSS-only gradient + procedural noise. Goal: make the open journal in `src/ui/Menu.js` (`_renderPausePopup`) feel like a real, weathered, hand-bound forager's notebook lying on Wren's table — not a UI element.

## Deliverables

Generate **two** WebP images, save them to disk at the exact paths below. JPG is an acceptable fallback if WebP is unavailable.

| File | Path (absolute) | Pixel size | Aspect | Purpose |
|---|---|---|---|---|
| `journal-paper.webp` | `public/ui/journal/journal-paper.webp` | **1400 × 1900** | 7:9.5 (portrait, single page) | Used twice — left and right page background |
| `journal-leather.webp` | `public/ui/journal/journal-leather.webp` | **2200 × 1500** | ~22:15 (landscape) | Used once — full leather binding wrapping both pages |

Create the directory `public/ui/journal/` if it doesn't exist.

Target file size: **under 500 KB each** at ~85% WebP quality. These are page backgrounds, not hero images — visible detail at viewport size matters more than zoomable resolution.

---

## Image 1 — `journal-paper.webp`

### Style
A close-up, top-down photograph of a single page from a hand-stitched leather-bound notebook that's been carried in a forager's pack for years. Daylight from a window, soft. Macro photography, sharp focus, no depth-of-field blur (the texture must be uniform across the entire frame). No props, no objects, no writing, no drawings — just the paper surface.

### What it shows
- The full surface of a single ivory/cream-colored paper page. Heavy, slightly textured, hand-cut paper (think "mid-19th-century rag paper" or modern artisan journal stock — Khadi paper, Cotton Comfort, Strathmore Mixed Media).
- Visible **fiber grain** running subtly across the surface. A few stray paper fibers near the edges OK.
- **Natural color variation** across the surface — warmer cream in some areas, cooler ivory in others, very slight foxing (small reddish-brown age spots) sparsely distributed (4-7 across the whole page, none larger than ~5mm).
- **Subtle aging marks**: faint tea-stain blooms in 1-2 corners, very faint shadows where the page has been folded once. Nothing dramatic — the page should read as "well-loved" not "trashed."
- The edge of the page is **deckle (rough, hand-torn)** on at least one side, suggesting hand-cut paper. Other edges can be cleanly cut.
- No ruled lines, no grid, no margins, no writing, no ink, no pencil marks. Blank surface only.

### Color palette
The page should sit comfortably on top of (and blend with) this CSS gradient that provides the base color: `linear-gradient(180deg, #fbf6e8 0%, #f3ead0 100%)`.

- **Dominant hue**: warm cream / soft ivory (#fbf6e8 → #f3ead0 range)
- **Foxing spots**: muted rust / amber (#9a6c3a-ish), very low contrast
- **Tea staining**: warm umber wash, barely visible
- **Avoid**: pure white, pure black, gray (cool-tone), blue tint, any saturated color, any modern bleached-white paper look

### Lighting
Soft, diffuse, even — like a north-facing window in late afternoon. **No harsh shadows, no directional gradient across the page** (the page must look the same brightness in all four corners — this is critical for tiling/repeating into the right page slot). No vignette.

### What to avoid (these will break the integration)
- No printed, ruled, or grid lines.
- No watermark, no logo, no signature, no stamp, no seal, no border decoration.
- No drawn elements (mushrooms, leaves, sketches, writing). The page is **completely blank**.
- No directional lighting / shadow gradient.
- No people, hands, fingers, or held objects.
- No frame around the page (no leather visible — that's the other image).
- No 3D perspective, no skew. Camera is dead-overhead, page is flat.
- No motion blur, no bokeh, no shallow depth of field.

### How it will be used in CSS

```css
.journal-page {
  background:
    /* Soft fractal-noise overlay for high-frequency grain */
    url('data:image/svg+xml...'),
    /* The generated paper photo, blended onto the base color */
    url('/ui/journal/journal-paper.webp'),
    /* Base color gradient (fallback if image fails to load) */
    linear-gradient(180deg, #fbf6e8 0%, #f3ead0 100%);
  background-size: cover, cover, 100% 100%;
  background-blend-mode: multiply, multiply, normal;
}
```

The paper image is sandwiched between the noise overlay and the base color. **Multiply blending means the image's white areas become transparent and its dark areas tint the base color**. So the whitest parts of the photo MUST be near-white (RGB 245+) so they don't darken the warm cream base.

---

## Image 2 — `journal-leather.webp`

### Style
A close-up, top-down photograph of the leather cover of a hand-bound, well-used notebook. Old, dark brown leather with deep grain, scuffs, edge wear, and subtle highlights from soft daylight. Same lighting and style as the paper image.

### What it shows
- The full leather surface of a journal cover seen from above, **flat — no curvature, no spine bumps, no embossing/title**. Just the leather hide texture itself.
- Rich, complex **grain pattern** — pebbled or pull-up leather, with natural variation in the surface.
- **Worn highlights** along where edges and corners would be (visible lighter spots from years of handling). The center can be more uniform; the outer ~10% of the frame should show subtle wear/lightening.
- **Tonal variation**: darker in some areas, slightly amber in others, with deep shadow nooks in the grain. Reads as warm dark brown, not black.
- A few **micro-scuffs and tiny scratches** scattered across the surface — not too many, not too uniform.
- No metal hardware, no thread, no stitching, no clasp, no buckle, no lettering, no embossing, no logo.

### Color palette
Sits on top of (and blends with) this CSS gradient: `linear-gradient(135deg, #3a2415 0%, #2a190d 55%, #1d100a 100%)`.

- **Dominant hue**: deep walnut / dark chestnut brown (#2a1a0e → #4a3016 range)
- **Highlights**: warm umber / honey brown, never approaching cream
- **Shadow nooks**: near-black, but never pure black
- **Avoid**: red-tinted leather, orange leather, light tan / saddle leather, any cool-tone leather, any colored dye

### Lighting
Same as paper — soft, diffuse, even. **Subtle directional light is OK here** to bring out the grain (a slight gradient from upper-left brighter to lower-right darker is fine), but no harsh shadows or hot spots.

### What to avoid
- No stitching, thread, cords, or visible bookbinding (we draw those in CSS).
- No title, no embossing, no debossing, no foil, no lettering, no decoration.
- No metal corners, clasps, or hardware.
- No vintage paper / parchment color — this is the EXTERIOR cover, not the inside.
- No 3D perspective. Camera dead-overhead, surface flat.
- No background other than the leather itself — the entire frame is leather.

### How it will be used in CSS

```css
.journal-book {
  background:
    /* Existing dark fractal-noise grain layer */
    url('data:image/svg+xml...'),
    /* The generated leather photo */
    url('/ui/journal/journal-leather.webp'),
    /* Base color gradient (fallback) */
    linear-gradient(135deg, #3a2415 0%, #2a190d 55%, #1d100a 100%);
  background-size: cover, cover, 100% 100%;
  background-blend-mode: multiply, multiply, normal;
}
```

---

## Generation approach (suggested)

1. Use a high-quality image generation model that supports text-to-image at 2200×1500 — **gpt-image-1**, FLUX.1, SDXL, or Midjourney v6. Lower-end models will struggle with the "no decoration, no writing, even lighting" constraints.
2. Generate paper first (smaller, 1400×1900). Confirm it's blank and evenly lit before moving on.
3. For leather, you may need to retry several times — models love adding fake stitching, embossed titles, or buckles. Be explicit about excluding them in negative prompts if the model supports them.
4. Save each output to the path specified in the **Deliverables** table above using WebP at quality ~85.

## Verification checklist

After generation, both files should:
- [ ] Exist at the exact paths in the Deliverables table.
- [ ] Be under 500 KB each.
- [ ] Be in WebP (preferred) or JPG format.
- [ ] Have **uniform** lighting across the frame (no vignette, no directional gradient strong enough to read as "shadow").
- [ ] Contain **no text, no decoration, no objects, no hands, no signature, no drawn elements**.
- [ ] Have a dominant color in the correct palette zone (cream paper / dark brown leather — not white, not black, not gray).
- [ ] Be photographically realistic, not painted/illustrated.

## Integration (after images are generated)

Once both files are in place, the CSS in `src/styles.css` needs three edits:

1. `.journal-page` (around line 1854) — add the `journal-paper.webp` URL as a middle background layer, set `background-blend-mode: multiply, multiply, normal` to combine the noise + photo + gradient.
2. `.journal-book` (around line 1773) — add the `journal-leather.webp` URL similarly.
3. Verify both pages render correctly at 1× and 2× DPR; if the paper photo is too dark, drop blend opacity or generate a brighter version.

The JS in `src/ui/Menu.js` does **not** need to change — these are pure CSS background swaps.

## Out of scope

- Spine stitching, raised cords, gold-leaf piping — keep these as CSS (they already render correctly).
- Page edge curl, shadow under the open book — keep these as CSS box-shadows.
- Inside-cover liner, endpapers, ribbon bookmark — not needed for current journal layout.
