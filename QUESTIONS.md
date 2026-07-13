# Questions for Trevor

The decision inbox. Agents append questions ONLY Trevor can answer (design taste, canon, product scope) — never questions answerable from the code, bible, or docs. Each question: context, options, and a recommendation so answering takes seconds, not research. Trevor answers inline (or in chat); the next agent applies the answer, records it in the relevant doc/worksheet, and moves the item to Answered. The dashboard renders this file.

## Open

_Inbox clear — Q8–Q11 answered 2026-07-12 (Trevor: "take all your recommendations")._

## Answered

### Q11 — Four UI symbols (✓ ✕ △ ◉) have no font glyph (asked 2026-07-12 · batch-32 · answered 2026-07-12: "recommendation")
**Decision:** option (b) — fold into the **Steam controller-glyph pass** (backlog). ✓ U+2713 / ✕ U+2715 /
△ U+25B3 / ◉ U+25C9 have no glyph in any project font and box in editor+build; that pass gives them a
proper TMP sprite sheet / icon set (the correct long-term home for the △◉ controller-button glyphs anyway).
No interim ASCII swap. Tracked in the controller-glyph backlog item; not a font-mode change.

### Q10 — AI-generated voiceover: test-only, or a shipping direction? (asked 2026-07-12 · batch-29 · answered 2026-07-12: "recommendation")
**Decision:** AI VO stays **placeholder-quality direction-finding, not a committed EA direction**. Keep the
batch-29 entrance-scene test; **do NOT mass-generate VO yet** — revisit after Trevor evaluates it in-game and
locks casting. If any AI VO ships to EA, the **Steam AI-content disclosure** is required (stays on the pre-EA
checklist). The Voice-mixer-group + settings-slider plumbing is a prerequisite that proceeds regardless of
this call (it's needed for AI-VO, human-VO, or no-VO alike).

### Q9 — Credits copy: ship current 7 lines, or expand for launch? (asked 2026-07-12 · batch-28 · answered 2026-07-12: "recommendation")
**Decision:** keep the current 7-line credits copy (batch-28 editorial hierarchy) as-is now; write the final
launch copy in **one "credits + licenses" audit batch during pre-EA** — against every third-party license file
in the project, so the copy is authored once from the audit. Feeds the pre-EA checklist's font+asset licensing
item (Georgia licensing: batch-32 nulled the shipped .ttf, but the SDF glyphs still derive from it, so
redistribution rights still need verifying — or swap to an OFL serif).

### Q8 — Aldermark attributed to Sable's seedbook — veto? (asked 2026-07-12 · batch-26 · answered 2026-07-12: "recommendation, keep it")
**Decision:** keep the **Sable's-seedbook attribution** for Aldermark's field-guide entry (as applied in
batch-26). It's the most plausible source for Wren knowing the folk name and implies "Aldermark" is an old
alder-derived name predating Lord Aldric (defusing the pun-timeline worry). No change.

### Q7 — Aldermark (Act IV leverage species): which REAL mushroom? (asked 2026-07-12 · answered 2026-07-12: "map to a real stump-colonizer, build wendSource")
**Decision (batch-26):** Aldermark → **Hen of the Woods / Maitake (Grifola frondosa)** — a real fungus that colonizes the base of stressed and cut hardwoods (esp. oak), grey-brown and "pale and stubborn in the shade," and genuinely valuable (prized medicinal-edible) = the "something useful grows where harm stops" leverage. Folk name "Aldermark" kept as the entry title; real Latin + accurate ID/habitat/uses. Photo null for now → added to the Meshy/photo wants list; world model deferred (sampling represented by the clear-cut interaction, not a forage node yet).

### Q6 — Festival: real cook/gather mechanic, or keep it a coordination scene? (asked 2026-07-12 · answered 2026-07-12: "recommendation")
**Decision:** option (a) — keep `festivalHosted` as the coordination dialogue scene (shipped in batch-22); "cooking" stays flavor, no gather/cook gate. Lacewig staying non-forageable is fine.

### Q5 — "Wren's internal conflict journal entry" — mechanism or narrator-voice only? (asked 2026-07-12 · answered 2026-07-12: "recommendation")
**Decision:** option (a) — no Wren-journal mechanic; the introspective beats are carried by the narrator-voice StoryCards (e.g. StoryCard_21). Revisit only if a later Act wants a first-person journal voice.

### Q4 — T4 species presentation: real species vs fictional (asked 2026-07-12 · answered 2026-07-12: "make all mushrooms real; tie the T4 to a real mushroom with one of the 16 photos, real Latin binomials")
**Decision (batch-20):** Hollowfen is an educational mushroom game — every species must be real. The 3 fictional T4 names become Sable's seedbook folk-names for real deadly/psychoactive species, reusing existing photos + real Latin: **Moonring = Destroying Angel (Amanita virosa)**, **Hollowheart = Death Cap (Amanita phalloides)**, **Wendlight = Liberty Cap (Psilocybe semilanceata)**. Field-guide entries keep the folk name as title, lead the description with the real species reveal, and carry accurate real ID features / lookalikes / safety notes. Trevor accepted that those 3 species now appear under two names (common entry + witch's-name entry). Follow-up: Wendlight's glowing world prefab should be remodeled to read like a real Liberty Cap (Meshy pass).

### Q1 — Lock the mushroom tier display names? (asked 2026-07-11 · answered 2026-07-11: "implement your recommendation")
**Decision:** internal ids stay T1–T5; display names drafted in the folk/trade-ledger register and locked as canon-pending: T1 "Basket Common" · T2 "Knifework" · T3 "Yard-Grown" · T4 "Deepwood" · T5 unnamed (bible-reserved). Recorded in `Docs/conventions.md`; localization ids `tier.tN.name` reserved in the LUT. **Trevor may veto/rename any of these before they first render in UI or dialogue** — none are player-visible yet.

### Q2 — "Autosave" slot semantics vs UI copy (asked 2026-07-11 · answered 2026-07-11: option (a))
**Decision:** keep the follow-the-active-slot behavior (better for players), fix the copy. `SaveSlotScreen` now labels all four slots "Journal 1–4" and the delete confirm matches; no slot claims to be "the autosave." Applied in Batch 16.

### Q3 — Confirm EA content scope: Acts I–II? (asked 2026-07-11 · answered 2026-07-11: confirmed)
**Decision:** EA ships with a polished Acts I–II playthrough (bible Act II completion state as the EA ending); Acts III–IV land during EA. Recorded in `Docs/steam-constraints.md`; the pre-EA checklist in TODOS.md sequences against this floor. Revisit only if Act III lands unusually fast.
