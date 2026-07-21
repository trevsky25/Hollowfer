# Vendored-pack build fixes (re-apply on reinstall)

The Magic Pig and NatureManufacture asset packs are **gitignored** (15.6 GB, re-install from the Asset Store —
GUIDs survive). That means any local fix to a vendored script is **invisible to git** and will be **lost** when
the pack is reinstalled or the project is checked out on a fresh machine. This file is the tracked record of
those fixes so a green editor doesn't hide a red build. Re-apply each after reinstalling the relevant pack.

## Fixes

### Unity Starter Assets `ThirdPersonController` — footstep mixer bypass (batch-85, 2026-07-16)

- **File:** `Assets/Starter Assets/Runtime/ThirdPersonController/Scripts/ThirdPersonController.cs`
- **Symptom:** footsteps and landing remained audible when the SFX setting was muted because
  `AudioSource.PlayClipAtPoint` creates an unrouted temporary source on the Master output.
- **Fix:** cache a dedicated spatial AudioSource on the player, route it to the optional
  `FootstepOutput` or Hollowfen's live `GameplaySfx.Output`, and use `PlayOneShot` for the existing clip bank.
  Null clip/bank guards were added at the same boundary.
- **Reinstall note:** Starter Assets package updates can overwrite the controller; reapply this small routing
  patch or move the same behavior into the project's player-controller fork.

### Magic Pig "Equipment System" — build-only compile error (batch-34, 2026-07-12)
- **File:** `Assets/Magic Pig Games (Infinity PBR)/Equipment System/Demo (Read the Read Me!)/IPBR_CharacterEquip.cs`
- **Symptom:** editor compiles fine, but a **player build** fails:
  `IPBR_CharacterEquip.cs(63,54): error CS1061: 'Transform' does not contain a definition for 'thisBoneRoot'`.
- **Cause:** vendor bug. `thisBoneRoot` is a `public static GameObject` field of the class (line 11). The
  `#if UNITY_EDITOR` branch correctly calls `DestroyImmediate(thisBoneRoot)`; the `#else` (build-only) branch
  wrote `Destroy(child.thisBoneRoot)` — `child` is a `Transform`, which has no `thisBoneRoot` member. Their
  non-editor path was never compiled (they only ran it in-editor), so it shipped broken. Note: this class is
  used at runtime by `Equipment System/Scripts/PrefabChildManager.cs:181`, so the demo folder can't just be
  excluded — the class is genuinely part of the module.
- **Fix:** line 63 → `UnityEngine.Object.Destroy(thisBoneRoot);` (mirror the editor branch; drop the bogus
  `child.`). One line. An inline comment tagged "Hollowfen build fix" marks it in the file.
- **Why not exclude the module instead:** Hollowfen doesn't use the Equipment System, but excluding it cascades
  (a core script + an Editor menu + a Medieval-pack demo all reference it) and risks Hollowfen's Magic Pig
  building/prop usage. The one-line vendor correction is lower-risk than module surgery.

> Discovered because the first real player-assembly compile (batch-34, after the App-Nap fix let a build finish)
> surfaced it — earlier build attempts stalled *before* the compile completed, hiding it. The build test earning
> its keep on the very first green run.
