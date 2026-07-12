# Worksheet Template & Convention

**What worksheets are:** one markdown file per work batch, committed WITH the work, capturing goal → decisions → verification → resume state. If a session dies mid-batch (Unity crash, context exhaustion, interrupted night shift), the next agent finishes the job from the worksheet alone. They're also the project's history — future sessions reference them ("how did we solve X in batch 9?").

**Convention:**
- File: `Docs/worksheets/batch-NN-<kebab-topic>.md` (NN continues the batch numbering; Batch 11 = Act II A, Batch 12 = agentic infra).
- Commit the worksheet in the same commit as the work it describes.
- Tag that commit `batch-NN` (`git tag batch-NN && git push --tags` when pushing).
- Update the worksheet DURING the batch, not retroactively at the end — it must be useful if the session dies at any point.
- Every system doc touched gets a line in "Docs updated".

---

Copy below this line for a new worksheet:

# Batch NN — <Title>

**Date:** YYYY-MM-DD · **Status:** in progress | verified | committed (`<sha>`, tag `batch-NN`)

## Goal
One paragraph: what this batch delivers and why now (link the TODOS.md item).

## Plan
- [ ] Step…
- [ ] Step…
- [ ] Play-mode verification
- [ ] Docs updated + worksheet finalized

## Decisions made
| Decision | Choice | Why |
|---|---|---|

## Verification evidence
What was actually run/observed (play-mode steps executed, console output, screenshots). "It compiles" is not verification.

## Docs updated
- `Docs/systems/….md` — what changed

## Unfinished / handoff
Exact state + next actions if someone else picks this up cold. Include any temporary/dirty state (disabled objects, debug flags, uncommitted files).

## Feedback to Trevor
Friction encountered, workflow-improvement suggestions, tooling gaps (this section feeds the periodic workflow-improvement review).
