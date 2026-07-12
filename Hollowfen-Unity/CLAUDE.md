# Hollowfen — Agent Router (Unity project)

**Hollowfen: The Failing Village** — single-player narrative exploration game in Unity (6000.4.4f1, URP). Player is **Wren**, a miller's daughter rebuilding her dying village through mushroom foraging, cultivation, and NPC relationships. C# only, ScriptableObject-driven content, Coplay MCP for editor control. Target: Steam (Mac+Win), Steam Deck Verified, EA ~month 12.

This file is a ROUTER. Read the docs for the systems you're touching — don't work from memory of them.

## Route to the right doc

Every doc's first 7 lines are a summary — survey them all with:
`head -7 Docs/systems/*.md Docs/*.md`

| Touching… | Read first |
|---|---|
| Story/narrative/dialogue content | `Docs/story.md` (**the bible** — canonical for world, acts, NPC voice, required dialogue; conflicts → ask) |
| Screens, menus, UIManager, focus/navigation | `Docs/systems/ui-framework.md` |
| Story/Wren/Field Guide pages, content SOs | `Docs/systems/menu-pages.md` |
| Foraging, inspect/inventory screens, mushrooms | `Docs/systems/foraging.md` |
| Mini-map, full map, compass, locations/POIs | `Docs/systems/map.md` |
| Quests, scores, story beats, tax deadline | `Docs/systems/quests.md` |
| Dialogue system (code/assets) | `Docs/systems/dialogue.md` + bible |
| NPCs | `Docs/systems/npcs.md` |
| Grow beds / cultivation | `Docs/systems/cultivation.md` |
| Clock, day flags | `Docs/systems/time.md` |
| Saves, persistence | `Docs/systems/save.md` |
| Input actions, gamepad | `Docs/systems/input.md` |
| Strings, translation | `Docs/systems/localization.md` |
| Settings, audio | `Docs/systems/settings.md` |
| Code style, naming, folders, canon rules | `Docs/conventions.md` |
| Anything shippable | `Docs/steam-constraints.md` |

## Workflow (every batch)

1. **Pull work from `../TODOS.md`** (repo root) — top of "Next up" unless directed otherwise.
2. **Start a worksheet**: `Docs/worksheets/batch-NN-<topic>.md` from the TEMPLATE. Update it as you go, not at the end.
3. **Small, verifiable batches** — never bundle disjoint tasks. Build → run in Play mode via the Unity MCP bridge → verify → fix → verify.
4. **Self-healing docs**: any system you changed → update its `Docs/systems/*.md` (including the 7-line header) in the same batch. New gotcha → the doc's gotcha list (or `Docs/conventions.md` if general).
5. **Park decisions, don't guess**: anything only Trevor can decide (canon, taste, product scope) goes in `../QUESTIONS.md` with options + a recommendation; keep building what isn't blocked.
6. **Finish**: verification evidence in the worksheet, test script for Trevor (template in conventions.md), commit work + worksheet + docs together, tag `batch-NN`, regenerate the dashboard (`python3 tools/agent/dashboard.py`) and republish the Artifact.

**Unity MCP**: bridge auto-starts with the editor (`McpBridgeBootstrap.cs`). If session tools aren't registered, use `tools/agent/unitymcp.py` (repo root) over HTTP. Unity must be running for compile/play verification — if it isn't, say so rather than shipping unverified work. Drive play-mode frames with `EditorApplication.Step()` (App-Nap-immune), never real-time polls.

**Overnight/autonomous runs**: follow `Docs/night-shift.md` — it governs the loop around batches (budget, stop conditions, decision parking).

## Non-negotiables (full detail: steam-constraints.md + conventions.md)

- Gamepad-first: every screen navigable by pad, default selection on open, verified with a pad before "done".
- All display text through `Localization.Get(stringId)` — no hardcoded strings.
- Saves only to `Application.persistentDataPath`; new game state goes through SaveCoordinator + round-trip verified.
- Achievement hook (`GameEvents.TriggerAchievement`) on every quest/beat completion.
- New Input System only. No edits to third-party assets (prefab variants / external hooks instead).
- Canon: no invented NPCs/mushrooms/locations; no romance; no emoji; no modern slang; "Lord help me" is the oath ceiling.

## Cast (spell correctly, don't improvise)

**Wren** (protagonist) · **Bram** (trade, mill key) · **Marra** (cook) · **Edda** (quiet observer) · **Sister Almy** (cultivation teacher) · **Joren** (smith, T2 tools) · **Voss** (tax collector — antagonist, not villain) · **Theo** (traveling trader) · **Hollin** (late companion, NOT romance) · **Father Calden** (priest) · **Elder Pell** (village recorder, keeps the ledger) · **Lord Aldric** (Act IV fork) · **The Old Wood** (the 12th character).

## Communication style

Direct, output-first; no preamble. End tasks with a 3–7 step Play-mode test script. For decisions: state options + a recommendation, don't ask open-ended questions. After implementation work, Trevor wants a short educational explanation of what was built and the underlying pattern (he's in an AI-enablement role and transfers these patterns).
