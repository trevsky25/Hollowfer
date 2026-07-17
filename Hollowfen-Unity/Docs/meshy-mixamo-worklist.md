# Meshy / Mixamo worklist — models & animations Trevor generates

The external-generation checklist for the graphics pipeline's Tier 3 (characters + hero items) and the
Mixamo animation work. **You generate; Claude does the prompts, import, URP material recipe, prefab, rigging
hookup, and in-scene placement** (same flow as the existing mushrooms). Check items off as you make them;
tell Claude when a batch is ready to wire in. Priorities: **P0 = needed for the EA slice (Acts I–II, per
QUESTIONS Q3)**, P1 = EA-complete cast/species, P2 = post-EA (Act IV / long-tail).

Current state (verified 2026-07-14): **11 NPCs are all placeholder capsules** (only Wren has a body model);
**20 of 21 mushroom entries have accurate Meshy-backed world + journal prefabs** (batch-68; 16 unique source models,
with same-fungus aliases sharing geometry); Aldermark/Maitake alone remains pending. **No key / book / knife models exist** in any
owned pack. Reference art: `public/concept/character-identity/` (web-prototype identity sheets) + Wren's
`Assets/_Hollowfen/UI/Characters/wren-profile.png`.

---

## MESHY — Characters (the cast models pass)

Each NPC needs: a body model (Meshy), a T-pose/rig suitable for Mixamo, and later a dialogue portrait
(Claude can crop a render or you paint one). Voice/character notes are from the bible — keep them on-model.

### P0 — Act I (first faces the player meets)
- [ ] **Wren** (protagonist, miller's daughter) — *has a model already*; needs the **Mixamo rig FIX** (see
  Animations below), not a re-model. A dialogue portrait exists (`wren-profile.png`).
- [ ] **Bram** — trade/steward figure who hands Wren the mill key; established, weathered villager.
- [ ] **Marra** — the cook; warm, matronly, apron/kitchen dress.
- [ ] **Sister Almy** — cultivation teacher; a religious sister — habit/modest garb, gardening-worn.

### P1 — Act II (completes the EA cast)
- [ ] **Joren** — the smith; broad, leather apron, soot; T2-tool giver.
- [ ] **Voss** — the tax collector; **antagonist, not a villain** — bureaucratic, precise, official ledger-and-seal
  dress; read as burdened-by-duty, never cartoonishly evil.
- [ ] **Edda** — the quiet observer; reserved, plain, watchful.
- [ ] **Elder Pell** — village recorder who keeps the ledger; old, scholarly, robed.
- [ ] **Father Calden** — the priest; clergy vestments.
- [ ] **Hollin** — the late companion (**not a romance**); rugged, outdoorsy — he leads to the deep wood/clear-cut.
- [ ] **Theo** — the traveling trader; road clothes, pack/wares, a bit worldlier than the villagers.

### P2 — Act IV (post-EA)
- [ ] **Lord Aldric** — the manor lord and Act IV fork; noble dress, older authority. (Currently a placeholder
  capsule at the `manor` location — the one Act IV model. Its manor building + props are a separate dressing pass.)
- [ ] *(optional)* **Edda's grandfather** — only if a later scene puts him on-screen; currently referenced, not staged.

---

## MESHY — Hero props / items (no pack models exist)
- [ ] **Ornate mill key** *(P0)* — Bram's mill key; hero close-up prop (the Act I inciting object).
- [ ] **Almy's seedbook** ("Sable's seedbook") *(P0)* — plot-critical book; holds the witch's folk-names
  (Moonring/Hollowheart/Wendlight, and Aldermark's attribution). Hero prop, gets read on-screen.
- [ ] **Joren's forage knife** *(P0)* — the T2 tool that unlocks "Knifework" species.
- [ ] **Aldric's sealed letter** *(P1)* — `item.aldric_letter`; a sealed noble letter (seal continuity matters).
- [ ] **Aldermark / Hen of the Woods world model** *(P1)* — real *Grifola frondosa* clustered at a cut stump
  (Q7); currently represented only by the clear-cut interaction, no forage node.

---

## MESHY — Mushroom species (20 of 21 integrated)
Educational game → every species should read as its **real** identity. Priority by whether the player forages
or the plot leans on it.
- [x] **Wendlight → real Liberty Cap** *(P0)* — batch-68
- [x] **Act I forageables** *(P0)* — Wood Ear, Pinecrest, Goldfoot, Field Cap — batch-68
- [x] **Field Mushroom and full-set decimation** *(P0)* — batch-68 two-budget generated pipeline
- [x] **T4 rares → real models** *(P1)* — Moonring / Destroying Angel and Hollowheart / Death Cap — batch-68
- [x] **Remaining delivered species → real models** *(P2)* — Chanterelle, Lacewig, Coppercup, Bonepale, Brightspore, and the rest — batch-68
- [ ] **Aldermark / Hen of the Woods** *(P1)* — real *Grifola frondosa* model for the clear-cut stump; only remaining species

---

## MIXAMO — Animations
- [ ] **Wren rig FIX** *(P0)* — the current Mixamo clips don't match her rig (palms-up run, curled idle fingers).
  Re-source clean Mixamo anims onto her actual skeleton, or reconfigure the avatar / apply a hand mask. Existing
  clips: SlowRun, Walking, StandingIdle, BreathingIdle, Jump, JumpingUp, FallingIdle.
- [ ] **Wren gameplay anims** *(P1)* — forage/pick (crouch pickup), plant-at-grow-bed, and any carry/interact
  pose the vertical slice wants.
- [ ] **Per-NPC anim sets** *(P1, after each NPC is modeled)* — minimum **idle + talk/gesture loop** each; plus
  role poses: Voss seated at his tax table, Joren working the forge/hammer, Marra kitchen-busy, Pell writing at
  the ledger, Almy tending beds. Mixamo idles + talking + sitting cover most of these.

> Wiring note for Claude: as each Meshy character arrives, import → configure the avatar as **Humanoid** so
> Mixamo clips retarget, build the NPC prefab (replace the placeholder capsule at its staged position), hook the
> dialogue/interaction components, and add an idle Animator. Hero props follow the mushroom recipe (import → URP
> Lit material → prefab → place under the right anchor, e.g. the mill key near Bram / the seedbook at the cottage).
