# Questions for Trevor

The decision inbox. Agents append questions ONLY Trevor can answer (design taste, canon, product scope) — never questions answerable from the code, bible, or docs. Each question: context, options, and a recommendation so answering takes seconds, not research. Trevor answers inline (or in chat); the next agent applies the answer, records it in the relevant doc/worksheet, and moves the item to Answered. The dashboard renders this file.

## Open

### Q4 — T4 species presentation: Latin names + field-guide photos (asked 2026-07-12 · batch-18)
The bible names Moonring, Hollowheart, and Wendlight but gives no Latin binomials (they're fictional species, unlike the real-world 16) and no photos exist. **What I applied (veto-able):** latin field reads "Unrecorded" (canon-flavored — suppressed knowledge, unclassified by scholarship), edibility "Unknown — old knowledge", descriptions derived strictly from bible text. Photos are null — their field-guide cells render without images until art exists (added to the asset-dropoff wants list).
**Options if you want different:** (a) keep "Unrecorded" (my pick); (b) invent folk-Latin binomials (e.g. *Lunaria sabelae*) — reads scholarly but fabricates canon; (c) italic folk descriptor ("Sable's book only").
**Also:** want me to generate placeholder journal-sketch images for the three (pale ink drawings, matching the seedbook fiction), or wait for real art?

## Answered

### Q1 — Lock the mushroom tier display names? (asked 2026-07-11 · answered 2026-07-11: "implement your recommendation")
**Decision:** internal ids stay T1–T5; display names drafted in the folk/trade-ledger register and locked as canon-pending: T1 "Basket Common" · T2 "Knifework" · T3 "Yard-Grown" · T4 "Deepwood" · T5 unnamed (bible-reserved). Recorded in `Docs/conventions.md`; localization ids `tier.tN.name` reserved in the LUT. **Trevor may veto/rename any of these before they first render in UI or dialogue** — none are player-visible yet.

### Q2 — "Autosave" slot semantics vs UI copy (asked 2026-07-11 · answered 2026-07-11: option (a))
**Decision:** keep the follow-the-active-slot behavior (better for players), fix the copy. `SaveSlotScreen` now labels all four slots "Journal 1–4" and the delete confirm matches; no slot claims to be "the autosave." Applied in Batch 16.

### Q3 — Confirm EA content scope: Acts I–II? (asked 2026-07-11 · answered 2026-07-11: confirmed)
**Decision:** EA ships with a polished Acts I–II playthrough (bible Act II completion state as the EA ending); Acts III–IV land during EA. Recorded in `Docs/steam-constraints.md`; the pre-EA checklist in TODOS.md sequences against this floor. Revisit only if Act III lands unusually fast.
