# Presentation Session Ownership

`NarrativePresentationSession` is the single runtime authority for gameplay shortcut blocking, player input suspension, time-scale overrides, cursor release, and gameplay-HUD visibility while a screen or cinematic is active. Owners acquire an `IDisposable` lease with a policy and dispose it when finished. Resource state is reference-counted per claim, so nested presentations may close in any order without restoring a resource that another owner still needs.

Key script: `Assets/_Hollowfen/Scripts/Cinematics/NarrativePresentationSession.cs`.
Focused verifier: `Assets/_Hollowfen/Scripts/Editor/PresentationSessionVerifier.cs`.
Consumers include direct gameplay screens, UIManager transitions, dialogue, narration, prop focus, endings, rest, and the forage-cutting challenge.
Status: centralized and live-verified on 2026-07-17, including direct inventory/map/dialogue overlap attempts and same-frame Escape behavior.

> Self-healing doc: any presentation that writes time scale, player input, cursor state, HUD visibility, or global gameplay-shortcut availability must be represented here and covered by the focused verifier.

---

## Claims and policies

| Claim | Owned resource |
|---|---|
| `BlockGameplayShortcuts` | Prevents global Inventory, Map, Purse, Field Guide, Pause, and interaction shortcuts from opening behind the active presentation. |
| `GameplayInput` | Suspends `PlayerInteractor`, disables the gameplay `PlayerInput`, and clears Starter Assets move/look/jump/sprint state. |
| `TimeOverride` | Applies the minimum requested time scale across all active time owners. The pre-session value is restored only after the final time owner releases. |
| `FreeCursor` | Unlocks and shows the OS cursor and disables Starter Assets cursor-look coupling. Exact prior state is restored after the final owner releases. |
| `HideGameplayHud` | Hides `_HUDCanvas` and `_MiniMapCanvas`, preserving and later restoring their exact `CanvasGroup` state. |

Built-in policies:

- `InputOnly`: shortcut and gameplay-input ownership; time and cursor are unchanged.
- `InteractiveNoPause`: `InputOnly` plus a free cursor.
- `Modal`: shortcut, input, time-scale `0`, and free-cursor ownership.
- `SlowMotion(scale)`: shortcut, input, and a clamped time override. Cinematics add claims with `Policy.With(...)` as needed.

`AcquireIfGameplay` is for UIManager screens that may also appear on the Main Menu. It acquires only if an active gameplay input rig exists, avoiding a false pause/cursor lease in menu-only context.

## Invariants

1. Acquire before exposing a presentation or releasing the previous owner. Inspect-to-cutting is the reference handoff: the challenge acquires synchronously before Inspect disposes.
2. Store one lease per owner and dispose idempotently on every normal, cancel, disable, and destroy path that can end ownership.
3. Never write `Time.timeScale`, `PlayerInteractor.Suspended`, gameplay `PlayerInput.enabled`, global cursor state, or gameplay HUD alpha directly from a presentation consumer.
4. Global shortcuts must consult `NarrativePresentationSession.BlocksGameplayShortcuts`. Releasing the final shortcut owner blocks through that same frame so the close key cannot also open Pause.
5. Nested time owners use the minimum requested scale; disposal order cannot affect the final restored baseline.
6. A zero-owner state is mandatory after a presentation chain ends. `ActiveOwnerDescriptions` exists for leak diagnostics.

## Verification

In bare `Scene_Hollowfen` Play Mode, run `Tools > Hollowfen > Verify Presentation Session Ownership`. The verifier creates nested slow-motion, cursor, HUD, and input owners; releases them out of order; disposes a lease twice; and checks exact time/input/cursor/HUD restoration, same-frame shortcut blocking, next-frame release, and a zero-leak end state.

Expected result: `[PresentationSessionVerifier] PASS - nested leases, minimum time override, idempotent disposal, input/cursor/HUD restoration, same-frame shortcut blocking, and zero-leak end state.`

