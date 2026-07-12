# Hollowfen — Repo Router

Two codebases live here:

- **`Hollowfen-Unity/`** — the real game (Unity). **All active development happens here.** Its router is `Hollowfen-Unity/CLAUDE.md` — read it before any Unity work.
- **`src/` + `public/`** — the retired web prototype. **Design source of truth** for visuals (Georgia serif, sage/gold/cream palette) and content data (`src/data/*.js`). Reference only; don't extend it.

Shared artifacts:

- **`TODOS.md`** (this directory) — the production task queue. Agents pull from the top of "Next up".
- **`tools/agent/`** — agent helper scripts (Unity MCP HTTP client, etc.). Add new scripts here when you find yourself repeating an incantation; document each in its README.
- **`Hollowfen-Unity/Docs/`** — story bible, system docs, conventions, worksheets. Doc summaries: `head -7 Hollowfen-Unity/Docs/systems/*.md`.
- Root `HANDOFF_*.md` files are historical (web-prototype era).

The story bible is `Hollowfen-Unity/Docs/story.md` — canonical for all narrative content. (`docs/story.md` at root is the stale web-era copy.)
