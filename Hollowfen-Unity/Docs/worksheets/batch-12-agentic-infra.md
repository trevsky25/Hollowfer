# Batch 12 — Agentic Infrastructure Phase 1 + Doc Backfill

**Date:** 2026-07-11 · **Status:** committed (tag `batch-12`; Batch 11 committed separately first as `batch-11` @ c6d7e70)

## Goal
Stand up the foundation for autonomous production work, adapted from an 18-point agentic-setup post Trevor shared: doc router + self-healing per-system docs, production task queue, worksheet/tag convention, and durable agent tooling. Extended same-session with the doc backfill (TODOS item 2): five parallel subagent readers documented quests/dialogue/npcs/time/cultivation from code and re-verified save + map, replacing every `[BACKFILL]` marker. Phases 2 (tests + linters) and 3 (personas, night shift, visual/perf regression) are queued in TODOS.md.

## Plan
- [x] Split the 410-line `Hollowfen-Unity/CLAUDE.md` monolith into a thin router + `Docs/systems/` docs with greppable 7-line headers
- [x] Root `CLAUDE.md` repo router (Unity vs web-prototype orientation)
- [x] `Docs/conventions.md` + `Docs/steam-constraints.md` extracted
- [x] `TODOS.md` production queue (road to 1.0, ordered Next-up, systems backlog, pre-EA checklist)
- [x] `Docs/worksheets/TEMPLATE.md` + batch-NN tag convention
- [x] `tools/agent/` seeded; `unitymcp.py` recreated in-repo (original lost to /tmp wipe)
- [x] This worksheet

## Decisions made
| Decision | Choice | Why |
|---|---|---|
| Router file | Reuse `CLAUDE.md` (auto-loaded) rather than a separate `AGENTS.md` | Claude Code loads CLAUDE.md automatically; a second router would drift |
| Doc summaries | First 7 lines of every doc, surveyed via `head -7 Docs/systems/*.md` | Cheap grep-ability without extra index files to maintain |
| Undocumented systems (quests, dialogue, npcs, time, cultivation) | Honest SKELETON docs with explicit `[BACKFILL]` markers | Wrong docs are worse than absent docs; backfill is TODOS item 2 and assigned to the first agent touching each system |
| Stale monolith claims (map layout, save TODO-ness) | Marked ⚠️ stale/verify rather than silently copied | c2b9405 + the 2026-06-11 map redesign superseded them |
| Task queue location | Repo root `TODOS.md` | Visible from session cwd; spans game + infra work |
| unitymcp.py | Rewritten from MCP protocol knowledge, flagged unverified | Bridge was down (Unity not running) — cannot live-test this session |

## Verification evidence
- All files written and listed below; `python3 -m py_compile tools/agent/unitymcp.py` passes.
- `unitymcp.py` VERIFIED against the live bridge same-session (Unity launched, `list` returned the full 30-tool catalog) — protocol rewrite is correct.
- Doc backfill verified by five parallel subagents reading the actual code (quests: 13 files; dialogue/npcs: 4 + 2 assets; time/cultivation: 4; save: 4; map: 6 + asset). Audit findings (achievement-hook gap, non-atomic saves, dialogue localization gaps, TimeManager event reset, dead code) recorded as the TODOS "Hardening pass" item.
- No Unity code/assets touched — no play-mode verification applicable.

## Docs updated
Everything under `Docs/systems/` is NEW this batch (13 docs): ui-framework, menu-pages, foraging, map, save, input, localization, settings, quests*, dialogue*, npcs*, time*, cultivation* (*=skeleton). Plus `Docs/conventions.md`, `Docs/steam-constraints.md`, `Docs/worksheets/TEMPLATE.md`, both `CLAUDE.md` routers, root `TODOS.md`, `tools/agent/{README.md,unitymcp.py}`.

## Unfinished / handoff
- Working tree ALSO contains uncommitted Batch 11 (Act II A game changes). Commit order: Batch 11 first (game files, after re-verification), then Batch 12 (infra files), tags `batch-11` / `batch-12`. File split: Batch 12 = all `Docs/systems|worksheets|conventions|steam-constraints` + both CLAUDE.md + TODOS.md + tools/agent; Batch 11 = everything under `Assets/`.
- `[BACKFILL]` markers across the 5 skeleton docs + map/save details = TODOS item 2.
- Old CLAUDE.md content was fully redistributed — nothing was dropped; if something seems missing, check conventions.md / steam-constraints.md / the system docs before re-adding.

## Feedback to Trevor
- The /tmp wipe losing `unitymcp.py` is exactly why agent tooling must live in the repo — worth remembering for work setups too.
- Next highest-leverage step is TODOS item 3 (EditMode data-integrity tests): content volume is about to 3× across Acts II–IV and asset-reference breakage is the most likely silent failure.
