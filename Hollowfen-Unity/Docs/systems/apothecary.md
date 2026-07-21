# Tobin's apothecary

The mill-terrace apothecary is the complete purchased Alchemy and Magic Lab showcase, owned through `PF_TobinApothecaryBuilding.prefab`. Hollowfen retains the vendor architecture and props while removing demo controllers/audio, opening both door thresholds, adding bounded collision and shelter behavior, and applying the project's URP/material/texture budgets.

## Preparation loop

The preparation table exposes three four-step fictional recipes: field ink, hearth broth, and shelf tonic. A recipe remains hidden or unavailable until its story flag is earned, every ingredient species has been identified in Wren's field journal, and the basket holds the full measure. A successful preparation consumes the complete ingredient batch and adds one result to persistent shelf stock in a single save transaction. The purchased showcase shelf props visualize the three result counts.

The Theo → Marra → Edda delivery chapter teaches this loop in order, then opens recurring prepared-product requests. The occupied workshop also grants the existing cultivated-yield benefit.

## Appointment ledger

The purchased open book beside the alchemy table opens the cream-paper `ApothecaryCaseScreen` after the workshop story is complete. Six cases arrive in story order: Bram, Pell, Joren, Marra, Almy, and Theo. Each later page opens one dawn after the prior follow-up closes.

Each case has four explicit stages:

1. Listen — accept the appointment and hear the voiced intake with Edda.
2. Observe — reveal two physical clues and two patient answers.
3. Decide — choose among the three preparations already present on the workshop shelf.
4. Follow-up — wait the authored one or two days, then hear the result and close the case.

The cases reward reasoning, not recipe memorization. Some careful answers use a preparation as support; Pell's page and Marra's pantry instead require treating an object or preventing exposure. The UI carries a permanent fictional-story disclaimer and never presents its content as real medical guidance.

`ApothecaryCases` persists stage, evidence/interview masks, decision, and follow-up/resolution days. Decision and resolution commits are atomic across shelf stock, flags, hope/knowledge, Wren's NPC relationship, the patient–Edda bond, and a localized durable memory. Rejected final writes restore all stores and publish no UI event. Completing all six cases sets `apothecary_casework_complete` and `apothecary_care_record_trusted` and grants the chapter's final hope reward once.

## Physical story staging

`NPCScheduleSlot` can require a case id/stage and, for returns, the recorded due day. Accepting a case moves that patient to the showcase's original alchemy-chair mark and Edda to the opposite side of the open book. They leave after Wren records a choice and return together only when the follow-up date arrives. Case slots are first-priority story overrides, while ordinary schedule relocation still defers visible pop-in.

The workshop also participates in the living village after its story chapter opens. During a deterministic wet afternoon, Bram and Edda meet inside to recover Tobin's old rain ledger; the five-beat voiced scene is available only while both schedules occupy the room and leaves a durable memory for each of them plus a stronger Bram–Edda bond.

## Verification

- `ApothecaryPreparationVerifier` covers purchased-building structure, open traversal, collision/material/performance budgets, recipe gates, and atomic preparation.
- `ApothecaryCaseworkVerifier` covers all authored copy/data plus isolated end-to-end case commits and rollback.
- `NPCScheduleVerifier` covers physical intake and due-day return staging.
- Data Integrity, project lint, and the dialogue voice manifest cover all six portraits, 12 dialogues, 30 spoken lines, localization ids, and 24 kHz mono clip wiring.
