# Night Shift — Autonomous Run Orchestration
How an unattended session works the queue: preconditions, the batch loop, stop conditions, hard rules, and the end-of-shift wrap-up. The router's per-batch workflow still governs each batch — this doc governs the LOOP around batches.
Preconditions: Unity open with the project loaded (bridge auto-starts), Mac awake (`caffeinate -dims` or Energy settings), clean git tree, saves backed up once at shift start.
Loop: pull top undone TODOS item → run the full batch workflow → verified+committed+tagged → regenerate dashboard → next item. Default budget: 3 batches per shift.
Stop cleanly (worksheet the exact state) on: batch budget reached · a gate that stays red after one focused fix attempt · rate-limit throttling · Unity/bridge unrecoverable · queue item needs a Trevor-only decision AND no other item is workable.
Never: push to a remote · touch `Hollowfen-Picture-Book-Assets/` or third-party assets · exceed one queue item per batch · guess on canon (park in QUESTIONS.md, move on) · leave play mode running.
Status: adopted 2026-07-11 (Batch 17). Trevor's kickoff prompt is at the bottom.

> Self-healing doc: night shifts that hit new failure modes append them to the Gotchas section AND encode the fix into tools where possible.

---

## Preconditions (Trevor's 3-step setup)

1. **Unity open** with Hollowfen-Unity loaded — `McpBridgeBootstrap` starts the bridge automatically. Don't leave a modal dialog open.
2. **Mac awake**: run `caffeinate -dims` in a terminal (or set Energy settings to never sleep). Display may sleep; the machine may not.
3. **Start the session** with the kickoff prompt below. Nothing else needed — the queue, gates, and dashboard do the rest.

## The loop (per batch)

1. Pull the **top undone item** from `TODOS.md`. If it's oversized, split it in TODOS first and take the first slice.
2. Follow the router workflow (`Hollowfen-Unity/CLAUDE.md`): read the system docs you're touching, bible for narrative, worksheet from the TEMPLATE **updated as you go**.
3. Build. Compose from existing systems before writing new ones; new code follows conventions.md.
4. **Verify before commit**: compile clean → `run_integrity.py` → flow verification in Play mode via the bridge. Drive frames with `EditorApplication.Step()` (App-Nap-immune — never real-time polls). Back up `saves/` before play runs, restore after. World placement → survey screenshots, look at them.
5. Docs: self-healing updates to every touched `Docs/systems/*.md`.
6. Commit (the pre-commit gate runs lint + integrity) + tag `batch-NN`. Regenerate the dashboard (`tools/agent/dashboard.py`) and republish the Artifact.
7. Next item.

## Decision handling

- Canon/taste/scope calls only Trevor can make → write the question to `QUESTIONS.md` (context + options + recommendation), **skip that item**, take the next workable one.
- Ambiguity resolvable from the bible/docs/code is NOT a Trevor question — resolve it and record the decision in the worksheet.

## Stop conditions (stop CLEANLY: current worksheet updated with exact resume state, tree committed or stashed-clean, play mode stopped)

- **Batch budget reached** (default 3 — rate-limit headroom per the Max-plan analysis; Trevor can override in the kickoff prompt).
- A gate (compile/lint/integrity/verification) stays red after **one focused fix attempt** — don't thrash; worksheet the failure and stop.
- **Rate-limit throttling** observed — stop rather than degrade.
- Unity or the bridge unrecoverable (editor crash, bridge dead after retries + `open -a Unity`).
- Every remaining queue item is blocked on Trevor.

## Hard rules

- No `git push`, no remote operations, no publishing beyond the dashboard Artifact.
- Never modify third-party assets, `Hollowfen-Picture-Book-Assets/`, or anything outside the repo.
- One TODOS item per batch; no drive-by refactors (park them in TODOS).
- Canon rules are absolute (conventions.md): no invented NPCs/species/locations, no romance, voice rules.
- Saves hygiene: back up before every play-mode run that mutates state; restore after.

## Model tiering (Trevor runs shifts on Opus 4.8)

- **Main loop**: the session model (Opus 4.8). Do NOT try to change your own model — tiering happens through subagents.
- **Mechanical fan-outs** (doc readers, code survey, search sweeps): spawn subagents with `model: "sonnet"` (or `"haiku"` for pure grep-and-summarize) — extraction work doesn't need the big model and it preserves rate-limit headroom.
- **Critical-decision gate**: BEFORE committing a batch that involves any of the following, spawn a REVIEW subagent with `model: "fable"` and address its findings first:
  - a new system or a change to an existing system's architecture (not just new content on existing rails)
  - save-schema changes (`SaveSlotMeta` fields, hydrate/persist paths)
  - canon-sensitive authoring beyond the bible's literal text (new mechanics implied by a scene, ending-engine logic)
  - anything the integrity/lint gates can't validate (design judgment, not correctness)
  Prompt the reviewer with: the worksheet-so-far, the diff summary, and the specific question. Its verdict goes in the worksheet's Decisions table.
- **Trevor-only decisions** still go to QUESTIONS.md regardless of model — Fable review is for engineering/canon judgment, not product taste or scope.

## The dashboard from a new session

The board's stable Artifact URL is `https://claude.ai/code/artifact/cc6edb78-f4cd-4f44-a608-fc51635870c3` — a session that didn't originally publish it must pass this as the `url` parameter when republishing, or it will mint a new link and orphan Trevor's bookmark.

## End of shift

1. Final `run_integrity.py` + `lint_hollowfen.py` + `smoke_play.py` — leave the project provably green.
2. Regenerate + republish the dashboard (this is Trevor's morning surface).
3. Final message in the session: batches shipped (tags), verification evidence, anything parked in QUESTIONS.md, and the one most useful thing Trevor could do next.

## Gotchas (append as discovered)

- App Nap: real-time frame polls fail when Unity is backgrounded; `EditorApplication.Step()` always works. `open -a Unity` is best-effort, not guaranteed.
- The bridge returns `success:false`/drops during play-mode entry and domain reloads — tolerate and retry in polls.
- `Physics.Raycast` hits RegionTrigger volumes — always pass `QueryTriggerInteraction.Ignore` for ground snapping.
- `execute_code` uses CodeDom C#6: no local functions, no string interpolation; >20s calls may time out client-side but complete — check `get_history`.

## Trevor's kickoff prompt (copy-paste, continuous mode)

> Run the night shift per Hollowfen-Unity/Docs/night-shift.md. Budget: 4 batches (stop earlier on rate-limit signs). Unity is open and the Mac is caffeinated. Work the TODOS queue top-down. Use sonnet/haiku subagents for mechanical reading and search; before committing anything architecture-, save-schema-, or canon-critical, get a fable-model review subagent's verdict per the Model Tiering section. Park anything only I can decide in QUESTIONS.md and keep building the next workable item. Republish the dashboard (URL in this doc) and end with a shift report: batches tagged, evidence, what's parked, what's next.

Run this every night; the queue in TODOS.md runs all the way to the finished game, and each shift consumes it top-down. The finalized game is the queue reaching empty — many shifts, one loop.
