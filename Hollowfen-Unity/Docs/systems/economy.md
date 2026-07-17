# Economy and Wren's Purse
Wren's money is one active-slot purse stored as total copper and displayed as silver/copper at a fixed `12 copper = 1 silver`. The purse is a readable balance, recent transaction ledger, pouch quote, and merchant-aware sale surface—not a second inventory or pricing system.
Key scripts: `Items/CoinPurse.cs`, `UI/PurseScreen.cs`, `UI/CoinHUD.cs`, `Foraging/InventoryRuntime.cs`, and the buyer profile on `Data/MushroomFieldGuideData.cs`.
Entry points: `P` / gamepad L3 toggles the purse from unobstructed gameplay; Pause exposes a clickable `Wren's Purse · balance` row; the bottom-left coin pill advertises the shortcut after Wren earns her first coin.
Persistence: `SaveSlotMeta.CoinsCopper` stores the balance and `CoinLedgerSnapshot` stores the eight newest signed entries, running balances, and localization reason IDs.
Biggest gotcha: selling is available only when the gameplay shortcut captures Marra or Theo as `PlayerInteractor.Current`; the Pause entry is informational because opening Pause clears live world focus.
Status: balance, ledger persistence, non-mutating quotes, accepted-only sales, refused-item retention, direct-gameplay pause restoration, Pause-stack return, and production UI lint verified 2026-07-16.

> Self-healing doc: if you change currency, buyer policy, purse entry points, or transaction persistence, update this doc and the batch worksheet in the same change.

---

## How Wren earns money

| Source | Runtime path | Ledger reason |
|---|---|---|
| Story promises and fixed dialogue payments | `DialogueScreen` → `CoinPurse.Add` | `A promise paid` |
| Mushrooms sold to Marra | Per-species Marra value → `InventoryRuntime.SellTo` | `Mushrooms sold to Marra` |
| Mushrooms sold to Theo | Per-species Theo value → `InventoryRuntime.SellTo` | `Mushrooms sold to Theo` |
| Rotating orders, remedies, and gatherings | `VillageRequests.Complete` | `Village delivery` |

Purchases call `CoinPurse.TrySpend` and enter the ledger as `Village purchase`. Unknown or legacy callers still receive generic `Coin earned` / `Coin spent` reasons, so the balance remains safe while their presentation stays honest.

## Pouch selling

`MushroomFieldGuideData.ValueFor(MushroomBuyer)` is the only price table. A value of zero means that buyer refuses the species. `InventoryRuntime.QuoteFor` reads the live pouch without mutation; `SellTo` recomputes the same policy at commit time, removes only positive-value rows, preserves refused mushrooms, persists the pouch once, and returns sold/refused/copper totals.

The purse always shows both Marra and Theo quotes so the player can make an informed trip. When Wren is focused on either merchant and presses `P` / L3, the matching sale action appears. After confirmation:

1. eligible mushrooms leave the pouch;
2. refused mushrooms remain;
3. copper enters `CoinPurse` with the merchant-specific reason;
4. inventory and purse autosaves run through the crash-safe slot writer;
5. the coin cue plays, the ledger refreshes, and the summary states how many mushrooms stayed behind.

No remote selling is allowed. Pause can open the ledger with mouse/controller, but trading requires returning to gameplay and opening the purse while close enough to the buyer.

## Presentation and input ownership

`PurseScreen` is a runtime-registered `UIScreen` under the persistent `UIManager`. A direct gameplay open owns the pause: it caches time/cursor state, freezes time, suspends `PlayerInteractor`, disables Wren's `PlayerInput`, and restores the exact state on close. A push from Pause inherits its existing frozen state and returns to Pause on Back.

The bottom-left `CoinHUD` remains hidden before the first coin, but its `P`/L3 InputAction is active so a zero-balance player can still learn the earning routes. The pill mirrors the passive HUD CanvasGroup and displays its shortcut. The Pause row is the reliable clickable path while the normal gameplay cursor is locked.

## Persistence contract

`CoinPurse` keeps at most eight newest-first `Transaction` rows: signed copper, balance after the entry, and a stable localization reason ID. `AutoSaveCoins(total, ledger)` writes balance and history together. Full saves gather both fields; load and new-game hydration accept a null ledger, so old slots retain their balance and simply begin with an empty history.

Reason IDs are save data. Do not rename one without a fallback/migration, because an unknown ID deliberately renders as the raw key instead of inventing a description.

## Verification

- Sale fixture: 5 carried → Theo quote accepted 1/refused 4 for 14c; commit produced 4 carried, +14c, and no remaining Theo-eligible stock.
- Disk round-trip: `CoinsCopper=82` and the newest ledger row stored `+14`, balance `82`, `purse.transaction.theo_sale` immediately after the test sale.
- Entry paths: Pause rendered `Wren's Purse · 5s 8c`; clicking pushed `purse`, Back resumed Pause at time scale 0, a second Back restored gameplay at time scale 1 with cursor locked.
- Direct shortcut: bindings are `<Keyboard>/p` and `<Gamepad>/leftStickPress`; open froze/suspended gameplay and close restored it.
- Merchant capture: a live Marra `NPCInteractable` mapped to `MushroomBuyer.Marra`; with no eligible stock the screen showed the refusal explanation and no misleading disabled sale button.
- `ProductionUIVerifier`: `PASS · 0 critical · 0 advisory` on the settled purse.
- The tester's save directory was restored byte-for-byte from its pre-test backup.

