# Hollowfen Picture Book Assets

This folder is the consolidated, reusable art package for the Hollowfen picture book. The original project copies remain in their existing locations so the book-building workflow continues to work.

## Folder guide

- `generated-originals/` - the five new full-resolution PNG illustrations generated specifically for the book. Use these for websites, social posts, promotional layouts, crops, and future design work.
- `story-cards/` - all existing Hollowfen story-card PNGs, including remakes and alternate versions.
- `world-map/` - the illustrated Hollowfen map.
- `textures/` - the journal-paper texture used in the book.
- `print-cmyk-300dpi/` - cropped and prepared CMYK JPEGs used by the print PDF. These are the best versions for the same landscape page crops in professional print layouts.

## Choosing a version

- For editing, recropping, web, video, or social media: start with `generated-originals/` or `story-cards/`.
- For commercial print using the existing book crop: start with `print-cmyk-300dpi/`.
- The CMYK print files use the Coated GRACoL 2006 profile and are prepared at their intended 300-ppi page dimensions.

## Related files

- Digital illustrated storybook (web edition, full 4-act story + 4 endings + appendices incl. mushroom field guide and character gallery): `../picture-book-digital/index.html` — open directly in a browser, or serve the folder with `python3 -m http.server`
- PDF export of the digital storybook (99 pp review copy): `../picture-book-digital/hollowfen-storybook.pdf`
- Press edition interior PDF (52 pp incl. closing flyleaf, no cover page, 10×8 in trim + 0.125 in bleed, TrimBox/BleedBox set, RGB — Mixam converts to CMYK): `../picture-book-digital/hollowfen-press-10x8-bleed.pdf`, built from `../picture-book-digital/press.html` + `press-img/` (regenerate with headless Chrome print-to-pdf, then re-stamp boxes with pikepdf)
- Complete book PDF for reading/sharing (front cover + 52 interior pages + back cover, 54 pp — not for print upload): `../picture-book-digital/hollowfen-complete-book.pdf`
- Mixam hardcover wraparound cover (back + 0.2" hinge + 0.375" spine + 0.2" hinge + front, 0.8" wrap bleed all edges = 22.375×9.6 in): `../picture-book-digital/hollowfen-cover-wrap-spine0375.pdf`, from `cover-wrap.html` — if Mixam quotes a different spine width, change the three values marked SPINE in that file and re-export
- Deployed web copy of the digital book: `../public/book/` (served at `/book/` on the Netlify site; re-copy from `../picture-book-digital/` after edits)
- Print-ready book: `../output/pdf/hollowfen-picture-book-print-ready.pdf`
- Production specifications and generation prompts: `../output/picture-book/PRODUCTION_NOTES.md`
- Rebuild script: `../tools/build_picture_book.py`

