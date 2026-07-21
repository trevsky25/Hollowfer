# Embedded Unity Pipeline

This is a source-controlled snapshot of Unity's experimental Pipeline package.
It is embedded to keep Hollowfen's agent integration Editor-only and auditable.

- Upstream package version: `0.3.1-exp.1`
- Repository revision recorded by Unity: `528431a225335e21313c86a2d4eac0b0212cba47`
- Unity package fingerprint: `58c16695e488f3b514078778bb94bfca22c9379d`
- Required Unity CLI: tested with `1.0.0-beta.2`

## Local release-safety patches

- `Unity.Pipeline` and `Unity.Pipeline.Tests.Runtime` are Editor-only and are not
  automatically referenced by player assemblies.
- Pipeline's Roslyn plugin importers are Editor-only.
- Pipeline's bundled `System.Runtime.CompilerServices.Unsafe.dll` is disabled and
  its explicit reference removed because Unity Collections already supplies the
  newer compatible assembly; this prevents duplicate-assembly warnings.
- The shared `com.unity.nuget.newtonsoft-json` dependency is embedded and hardened
  separately so its AOT/player DLL is unavailable.
- Server startup does not write `PlayerSettings.runInBackground` merely by opening
  the Editor; the upstream assignment is restricted to non-Editor Players.

This intentionally disables Pipeline's Player-runtime automation. Hollowfen adopts
the live Editor command and MCP surfaces only. Reapply these patches and run the
data-integrity, player-assembly exclusion, production preflight, and audit-build
checks whenever the experimental package changes.
