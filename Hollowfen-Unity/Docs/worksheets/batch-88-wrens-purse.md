# Batch 88 — Wren's purse and pouch trade

**Date:** 2026-07-16 · **Status:** DONE in working tree (compile, focused sale/save fixture, UI lint, input/pause flow, and visual QA green)
**Directive:** Turn the unreadable bottom-left coin hint into a production-ready purse, make Wren's earnings legible, and let her sell mushrooms from the pouch without duplicating buyer rules.

## Player-facing result

- Press `P` or gamepad L3 from gameplay to open **Wren's Purse**; the bottom-left coin pill now advertises that control.
- Pause includes a clickable `Wren's Purse · 5s 8c`-style row for mouse/controller discovery.
- The purse explains silver/copper conversion, shows the total balance and six newest visible ledger rows, summarizes the mushroom pouch, and shows simultaneous Marra/Theo quotes.
- Opening beside Marra or Theo exposes one explicit sale action. Only mushrooms that buyer values are sold; every refused specimen stays in the pouch.
- Sale results, delivery rewards, dialogue income, and purchases now carry readable transaction reasons across save/load.

## Implementation

- Added runtime `PurseScreen` registration under the persistent UI stack, with inherited-Pause and direct-gameplay pause ownership handled separately.
- Expanded `CoinHUD` to a 274×48 amount + shortcut pill and added `P` / L3 input without changing the generated InputActions asset.
- Added a dynamic Pause row whose label includes the current formatted balance.
- Added `InventoryRuntime.QuoteFor`; the read-only preview and `SellTo` share the exact `MushroomRules.SaleValue` boundary.
- Added an eight-entry newest-first `CoinPurse.Transaction` ledger plus `CoinLedgerSnapshot`; old saves load with their balance and an empty ledger.
- Routed village deliveries, merchant sales, dialogue grants, and purchases through stable localized reason IDs.
- Extended the gameplay-foundation verifier with non-mutating quote, ledger running-balance, and ledger persistence assertions.

## Verification

- Unity C# compile: zero errors.
- Data Integrity: zero errors / zero warnings; 26 quests, 75 dialogues, 21 mushrooms, and 10 village requests covered.
- `GameplayFoundationVerifier.RunAll`: PASS with 22 stable wild nodes, non-mutating quotes, buyer refusal/copper policy, purse-ledger persistence, forage respawn, 4 cultivation recipes, and clock boundaries.
- Focused Theo fixture: `68c + 14c = 82c`; `5 → 4` mushrooms; accepted `1`, refused/preserved `4`; ledger and disk both stored `purse.transaction.theo_sale` with balance `82`.
- Pause click path and Back stack restored exact time/cursor state.
- Direct `P`/L3 path froze time, suspended interaction, unlocked cursor, and restored all three on close.
- Live Marra focus mapped to the correct buyer and hid the sale action when her quote was zero.
- Production UI verifier: `0 critical / 0 advisory`.
- Visual evidence: `Docs/screenshots/batch-88/purse-overview.png`, `purse-theo-quote.png`, and `pause-purse-row.png`.
- Real saves restored from `/tmp/hollowfen-purse-save-backup-20260716-181409` after the state-mutating fixture.

## Primary files

- `Assets/_Hollowfen/Scripts/UI/{PurseScreen,CoinHUD,PauseScreen}.cs`
- `Assets/_Hollowfen/Scripts/Items/CoinPurse.cs`
- `Assets/_Hollowfen/Scripts/Foraging/InventoryRuntime.cs`
- `Assets/_Hollowfen/Scripts/Save/{SaveSlotMeta,SaveManager,SaveCoordinator}.cs`
- `Assets/_Hollowfen/Scripts/{Dialogue/DialogueScreen,Requests/VillageRequests}.cs`
- `Assets/_Hollowfen/Scripts/Editor/GameplayFoundationVerifier.cs`
- `Docs/systems/economy.md`

## Intentional follow-on

- Balance per-species prices after a full playthrough; the purse deliberately consumes the authored tables unchanged.
- A future shop/catalog can call `CoinPurse.TrySpend` with a more specific reason ID. The purse does not invent a store before stock and pricing content exist.
- Lifetime earnings/spending statistics are not stored; the ledger is intentionally a compact recent-history affordance rather than analytics save data.
