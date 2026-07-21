# Meshy Asset Drop-off — Delivery Spec & Wants List
How Trevor delivers Meshy models (characters, mushrooms, hero items) and what the agent does with each drop. Nothing here blocks content production — placeholders (capsules, tinted mushroom variants) stand in until real models arrive; each delivery is a small drop-in batch.
Drop location: `Hollowfen-Unity/Assets/_Hollowfen/Models/Dropoff/<AssetName>/` (FBX + textures together), or just tell the agent where the files are.
Characters swap ZERO-RISK: every NPC is a root (trigger + NPCInteractable + quest wiring) with a `Visual` capsule child — the model replaces the child only; quest logic is never touched.
Mushrooms follow the proven 3-species recipe in systems/foraging.md (mask-map packing, 0.08-scale wrap, colliders, SO wiring) — same-day integration.
Hard deadline: pre-EA store assets/trailer (~month 10–12) need the real cast; everything earlier is drop-in-when-ready.
Status: batch-68 integrated the full delivered mushroom set through a reusable two-budget optimization/import pipeline; Aldermark/Maitake remains the only mushroom model wanted.

> Self-healing doc: update the wants list as species/characters/props enter the bible pipeline; check items off with the batch that integrated them.

---

## What to export (per asset type)

**Characters** (Meshy + rig):
- FBX (or GLB) with the rig, **T-pose or A-pose** base. Humanoid-compatible skeleton preferred (Mixamo-style is fine — Wren's precedent).
- Textures alongside: albedo/base color, normal, metallic, roughness (4K fine — the agent packs mask maps).
- **Animations**: idle loop is the MUST for NPCs (they stand and talk); a subtle talk/gesture loop is a welcome bonus (future dialogue-camera pass will use it). In-file or separate FBX both fine.
- Optional: intended real-world height (else the agent matches the capsule's ~1.8m and eyeballs against Wren).
- Naming: `Char_<Name>` (e.g. `Char_Theo.fbx`, `Char_Theo_Idle.fbx`).

**Mushrooms**:
- FBX + textures (albedo/normal/metallic/roughness). Pivot anywhere — the recipe re-centers.
- Naming: `Mushroom_<SpeciesId>` (e.g. `Mushroom_Wendlight.fbx`).
- Note the growth axis if it's a mounted species (Oyster grows on local +Y — that mattered).

**Hero items** (inspect/keepsake close-ups): FBX + textures, `Prop_<Name>`.

## What the agent does with each drop (Trevor does none of this)

Characters: import → avatar setup → URP materials (mask-map packing) → Animator with idle → swap the NPC's `Visual` child → scale/ground check → screenshot verify → integrity/smoke gates → commit.
Mushrooms: preserve the source drop → Blender-generate world/journal triangle budgets from the shared manifest → material/mask-map importer → normalized gameplay + dedicated journal prefabs → SO wiring → world/guide verification. Full recipe and commands live in `systems/foraging.md`.

## Wants list (priority order)

### P1 — Core cast, on screen constantly (all currently capsules)
- [x] Char_Bram (innkeeper build, apron) — mesh in-scene (`Bram-TPose.fbx`); ⚠️ T-pose, still needs a rig/idle pass to be truly done
- [ ] Char_Marra (cook, flour-dusted)
- [ ] Char_Almy (Sister Almy — older, habit/wimple, gardener's hands)
- [ ] Char_Joren (smith, leather apron)
- [ ] Char_Voss (tax collector — neat, traveled, ledger-carrier; antagonist not villain)

### P2 — Act II–III cast
- [ ] Char_Theo (traveling trader — good boots, charming)
- [ ] Char_Edda (quiet observer, Wren's age-ish)
- [ ] Char_Pell (Elder Pell — old recorder, ledger)
- [ ] Char_Calden (Father Calden — priest, principled worry)
- [ ] Char_Hollin (Wren's age, pale from travel, dark-haired, slight, carefully-mended satchel — bible-specified look)
- [ ] **Wren animation set** (idle/walk/run matched to her existing rig — clears the palms-up-run TODOS item)

### P3 — Mushrooms
- [x] Mushroom_Goldfoot (story-critical — "gold-stemmed", Theo's obsession) — batch-68
- [x] Mushroom_Brightspore ("lacquered, faintly bright at the edge, like firelight seen through horn") — batch-68
- [x] Mushroom_WoodEar (cultivation species — seen constantly in grow beds) — batch-68
- [x] Mushroom_Wendlight / real Liberty Cap — batch-68
- [x] Mushroom_FieldCap, Mushroom_Pinecrest (Act I basics) — batch-68
- [ ] Mushroom_Aldermark / Hen of the Woods (*Grifola frondosa*) — the sole remaining canonical model

### P4 — Hero items (no kitbash source exists — memory: packs have NO key/book models)
- [ ] Prop_MillKey (Act I keepsake)
- [ ] Prop_FathersJournal (Act I)
- [ ] Prop_Seedbook (Almy's — Act II/III)
- [ ] Prop_TonicBottle (Brightspore tonic keepsake)
- [ ] Prop_ForagingKnife (horn-handled — Joren's)

### Later (as acts demand)
- Aldermark species + Lord Aldric (Act IV) · festival dressing (Act III) · Witch's Cottage props (Act III)
