# Questions for Trevor

The decision inbox. Agents append questions ONLY Trevor can answer (design taste, canon, product scope) — never questions answerable from the code, bible, or docs. Each question: context, options, and a recommendation so answering takes seconds, not research. Trevor answers inline (or in chat); the next agent applies the answer, records it in the relevant doc/worksheet, and moves the item to Answered. The dashboard renders this file.

## Open

### Q11 — Four UI symbols have no font glyph (render as boxes) — substitute or icon-set? (asked 2026-07-12 · batch-32, low stakes, pre-existing)
The batch-32 font audit found four literal Unicode symbols used in authored UI that **no project font
can render** — not Georgia, not LiberationSans, not the fallback (their source .ttf lacks the glyph),
so they render as missing-glyph boxes (□) in **both editor and build** (pre-existing, not caused by
the font fix): **✓ U+2713** ("Saved ✓" toast) · **✕ U+2715** (a close-button label) · **△ U+25B3** and
**◉ U+25C9** (controller-button glyph placeholders, e.g. `gamepadGlyph = "[△]"`). Note → ← ○ • and all
prose/typographic marks now render correctly (baked into Georgia or the fallback this batch).
**Options:** (a) interim ASCII substitutes now — "Saved" / "X" / plain letters — trivial and safe;
(b) fold into the **Steam controller-glyph pass** (backlog): a proper TMP sprite sheet / icon font for
✓✕ and the real Xbox/PS/Deck button glyphs, which is the correct long-term home for △◉ anyway;
(c) leave as-is for now. **Recommendation:** (b) — these are exactly what the controller-glyph pass
owns; do the ASCII interim (a) only if a box on the save toast bothers you before then. Either way it's
a few one-line string edits, not a font change.

### Q10 — AI-generated voiceover: test-only, or a shipping direction? (asked 2026-07-12 · batch-29, product + disclosure stakes)
Batch-29 proves the pipeline: local Kokoro TTS (free, Apache) voices the entrance scene — intro
narration (Wren, warm/slowed) + Bram's chain (older British male) — with clean per-line playback
and the Misty Forest bed underneath. **Decisions that are yours:** (a) is AI VO the direction for
EA, or is this placeholder until human VO / no-VO? (b) if any AI VO ships, **Steam requires an
AI-content disclosure** on the store page (pre-EA checklist item added); (c) voice casting taste —
listen to the test and tell me which voices feel wrong (the cast map is one line per character in
`tools/agent/generate_vo.py`). **Recommendation:** treat as placeholder-quality direction-finding;
decide after hearing it in-game. No further VO generation until you call the direction — the
pipeline makes full coverage a mechanical batch whenever you do.

### Q9 — Credits copy: ship the current 7 lines, or expand for launch? (asked 2026-07-12 · batch-28, low stakes but has a legal edge)
The settings-screen rebuild (batch-28) kept the shipped credits copy verbatim, now with editorial
hierarchy. For a production launch the copy may want: exact asset-pack legal names (per license
terms — some packs require specific attribution wording), font attributions (**Georgia is a
licensed Microsoft font — redistribution rights need verifying before EA**, now on the pre-EA
checklist), a music credit slot (composer TBD), and whether/how to credit AI tooling in
production. **Recommendation:** keep current copy until the pre-EA checklist pass, then do one
"credits + licenses" audit batch against every third-party license file in the project — write
the final copy once, from the audit. **What you decide:** (a) recommendation, or (b) draft the
final copy now and I'll wire it (keys `credits.*` in Localization.cs, 5-minute change).

### Q8 — Aldermark attributed to Sable's seedbook — veto? (asked 2026-07-12 · batch-26, low stakes)
Aldermark's field-guide entry uses the established folk-name formula "**Sable's seedbook name for** the Hen of the Woods." But the shipped cottage-arrival dialogue lists the seedbook's contents as only "Witchwell. And three names in another hand: Moonring. Hollowheart. Wendlight." — Aldermark isn't among them. Attributing it to the seedbook is the most plausible source for Wren knowing the folk name (and helpfully implies "Aldermark" is an old alder-derived name predating Lord Aldric, defusing any pun-timeline worry). **What I applied (veto-able):** kept the seedbook attribution. **Alt:** drop "seedbook" and make it a plain folk/regional name ("Aldermark, the old name for the Hen of the Woods…"). Trivial one-line change either way.

## Answered

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
