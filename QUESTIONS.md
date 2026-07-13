# Questions for Trevor

The decision inbox. Agents append questions ONLY Trevor can answer (design taste, canon, product scope) — never questions answerable from the code, bible, or docs. Each question: context, options, and a recommendation so answering takes seconds, not research. Trevor answers inline (or in chat); the next agent applies the answer, records it in the relevant doc/worksheet, and moves the item to Answered. The dashboard renders this file.

## Open

### Q14 — Mill mission: two assets block the cinematic finale + the interior (asked 2026-07-13 · batch-51)
**Context:** The mill mission spine (Bram→key→door→journal) works, and batch-51 added the journal's teaching
(Field Cap/Wood Ear/Pinecrest) + Tobin's farewell note as live Georgia narration. Two remaining bible beats
are blocked on **assets only I can't generate**:
1. **Journal painted spreads** (51b) — you said you're generating `journal-01-sketches.png` /
   `journal-02-spores.png` / opt `journal-03-note.png` in Codex. They're **not in
   `Assets/_Hollowfen/UI/StoryCards/` yet**. Drop them there and I'll wrap the note in the multi-image
   `NarrationOverlay.ShowCinematic` over the paintings + a camera push-in into the book (the current narration
   is the placeholder-free interim).
2. **Interior inspect props** (51c) + **cinematic key/journal pickups** (51d) — the bible's coat / kettle /
   ledgers / mill-wheel / drawer + the "hand you the key" and "open the journal" focus-pickups need **meshes
   the kitbash packs don't have** (no coat/kettle/book/key models; graphics-pipeline memory). Options: (a) you
   Meshy the hero props (coat-on-peg, black kettle, tied ledgers, iron mill key, leather journal) — recommend;
   (b) I kitbash rough stand-ins from existing packs (barrels/crates) for the inspect beats and use an
   abstract focus-glow for the pickups (lower fidelity); (c) defer the interior/pickups until the Meshy set
   exists. **Recommendation: (a)** for the 5 hero props, then I build 51c/51d. Tell me which.


### Q12 — Bram is missing his texture asset (blocker, needs your Meshy files) (asked 2026-07-13 · batch-43)
**Context:** Bram renders now, but as a dark "statue" — his FBX material's `_BaseMap` resolves to
**Wren's** `Assets/Characters/Wren/Image_0.jpg` (name collision) because the Bram FBX has **no embedded
textures** and no Bram texture exists in the project (only `Bram-TPose.fbx` + the idle controller).
**Need from you:** drop Bram's Meshy texture set (at least the albedo; ideally normal + metallic/roughness)
into `Assets/Characters/Bram/`. **Then I'll:** extract the FBX materials (currently InPrefab), point
`_BaseMap`/`_BumpMap`/`_MetallicGlossMap` at the Bram maps, and verify he's textured. No code needed — just
the asset. (His 34MB mesh still wants decimation before EA — separate.)

### Q13 — Pell & Voss placeholders stand in the opening square — clear them for the homecoming? (asked 2026-07-13 · batch-43)
**Context:** `NPC_Pell` (288,161) and `NPC_Voss` (moved to 287,164.5 in batch-43) are gray placeholder
capsules by the Village Well. The bible's opening is deliberately empty — "the village did not greet her…
the only one who stood there was Bram." Two other figures loitering by the well undercut that.
**Options:** (a) hide/disable Pell & Voss until after the homecoming beat (recommend — preserves the empty
square; re-enable when their scenes arrive); (b) leave them (they read as villagers); (c) move them out of
the square. **Recommendation: (a).** Say the word and I'll gate their placeholders on the arrive/post-intro flag.

## Answered

### Q11 — Four UI symbols (✓ ✕ △ ◉) have no font glyph (asked 2026-07-12 · batch-32 · answered 2026-07-12: "recommendation")
**Decision:** option (b) — fold into the **Steam controller-glyph pass** (backlog). ✓ U+2713 / ✕ U+2715 /
△ U+25B3 / ◉ U+25C9 have no glyph in any project font and box in editor+build; that pass gives them a
proper TMP sprite sheet / icon set (the correct long-term home for the △◉ controller-button glyphs anyway).
No interim ASCII swap. Tracked in the controller-glyph backlog item; not a font-mode change.
**RESOLVED (batch-48, 2026-07-13):** Trevor requested PS5 icons directly — shipped the procedural
`ControllerGlyphs` TMP sprite sheet (DualSense ✕○□△ chips + ui_x/ui_check/coin), registered as TMP's
default sprite asset; brand-aware `ControllerGlyphs.For(Face)` resolver feeds prompt/inspect/inventory.
All four boxed symbols eliminated.

### Q10 — AI-generated voiceover: test-only, or a shipping direction? (asked 2026-07-12 · batch-29 · answered 2026-07-12; UPDATED batch-39)
**Decision (batch-29):** AI VO stays placeholder direction-finding; don't mass-generate yet.
**UPDATE (batch-39):** Trevor **directed AI VO for the whole opening scene** — the 6-beat homecoming intro is
now fully voiced (Narrator = `af_heart`), and Bram's dialogues were revoiced older/deeper (`bm_lewis`). So AI
VO IS the direction for the opening at least. **Still open:** whether to voice the *entire remaining cast*
(a mechanical batch once decided) and the voice-casting taste pass. **The Steam AI-content disclosure is now a
firm pre-EA requirement** (AI VO ships). The Voice-mixer-group + settings-slider plumbing is still a
prerequisite before broad VO (needed for the per-voice volume slider).

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
