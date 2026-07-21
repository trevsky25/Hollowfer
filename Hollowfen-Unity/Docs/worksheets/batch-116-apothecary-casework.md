# Batch 116 — Apothecary patient casework

**Date:** 2026-07-19 · **Status:** implemented and focused-verifier green

## Goal

Turn Tobin's restored laboratory into a character-driven place Wren repeatedly uses: listen carefully, inspect evidence, choose from preparations she physically made, wait for consequences, and let the village remember how she reasoned.

## Shipped

- Six save-backed sequential cases for Bram, Pell, Joren, Marra, Almy, and Theo.
- 24 interactive evidence/interview beats and 18 distinct outcomes across careful, supportive, and mistaken reasoning.
- A controller-ready cream-paper appointment ledger using each patient's authored character sheet.
- One prepared item consumed per recommendation, with a one- or two-day follow-up.
- Physical patient and Edda staging inside the purchased alchemy room for intake and due-day returns.
- Durable patient memories, patient–Edda bonds, relationship/hope/knowledge consequences, and a one-time six-case completion flag/reward.
- 12 voiced dialogues / 30 lines: 12 reference-conditioned Wren reads and 18 supporting-cast reads.
- Save snapshot normalization, old-save empty hydration, atomic commit rollback, and no-publication failure behavior.
- A domain-reload-safe test journal override so an armed isolated save can never fall through to real player journals during Play Mode recompilation.

## Verification evidence

- Unity C# compilation: no project compile errors.
- Data Integrity: `ERRORS=0 WARNINGS=0`, 141 dialogues and 15 locations covered.
- Project lint: `ERRORS=0 WARNINGS=0 WAIVED=1`.
- Voice manifest: `390` current dialogue lines; Unity wired 390 lines across 141 assets.
- `ApothecaryCaseworkVerifier.RunAll()`: PASS including injected final-commit rejection.
- `NPCScheduleVerifier.RunAll()`: PASS with seven routines and physical intake/due-day return staging.
- Live 1730×1080 UI inspection: all six rows fit, portrait/copy/stage rail remain legible, first action is controller selectable, and no inner black modal artifact exists.
- Real slot 0 restored and rechecked at SHA-256 `6c230ecbb0be37ce0a1ccef84b6f5f97e415d145ed05998acbac4ff95ad92657` after deliberately exercising the reload-safety edge case.
