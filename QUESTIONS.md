# Questions for Trevor

The decision inbox. Agents append questions ONLY Trevor can answer (design taste, canon, product scope) — never questions answerable from the code, bible, or docs. Each question: context, options, and a recommendation so answering takes seconds, not research. Trevor answers inline (or in chat); the next agent applies the answer, records it in the relevant doc/worksheet, and moves the item to Answered. The dashboard renders this file.

## Open

### Q1 — Lock the mushroom tier display names? (asked 2026-07-11 · batch-12 audit)
CLAUDE.md has always flagged tier names as "placeholder — confirm before locking." Internal ids (T1–T5) are fine forever; only player-facing names matter. Act II B's trade content (Theo's ledger) will start printing tier language into dialogue, so this gets more expensive to change after the next content batch.
**Recommendation:** keep T1–T5 as internal ids, and lock display names before Act II B is written. If you give me a direction ("earthy trade-guild words", "folk names Wren would use"), I'll draft a set for you to approve.

### Q2 — "Autosave" slot semantics vs UI copy (asked 2026-07-11 · save-system audit)
Code reality: autosaves write to whichever slot is ACTIVE (load slot 2 → autosaves land in slot 2). Slot 0 is only the default for new games. The SaveSlot screen presents slot 0 as "the autosave slot," which no longer matches behavior.
**Options:** (a) keep behavior, reword UI ("Journal 1–4," most-recent shown first — current behavior is genuinely better for players); (b) enforce slot 0 as a true dedicated autosave and add copy-to-slot semantics.
**Recommendation:** (a) — behavior is right, copy is stale. One-line UI fix, folded into the hardening pass.

### Q3 — Confirm EA content scope: Acts I–II? (asked 2026-07-11 · roadmap reconciliation)
TODOS assumes Early Access ships with a polished Acts I–II playthrough (bible Act II completion state as the EA ending point), with Acts III–IV landing during EA. This drives when build/store-page milestones enter the queue — confirming it lets me sequence the hardening items honestly against the month-12 target.
**Recommendation:** confirm Acts I–II as the EA floor; revisit only if Act III lands unusually fast.

## Answered

*(none yet — answers move here with the date + where the decision was applied)*
