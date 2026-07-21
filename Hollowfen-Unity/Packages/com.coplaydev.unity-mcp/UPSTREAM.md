# Embedded MCP for Unity

This package is a source-controlled snapshot of the `MCPForUnity` subtree from
CoplayDev/unity-mcp. It is embedded so Hollowfen can keep its editor automation
while guaranteeing that MCP and its JSON dependency cannot enter a player build.

- Upstream: https://github.com/CoplayDev/unity-mcp
- Commit: `b92c05a25820cfc9f59ce4094eb46aaec8632ea2`
- Upstream version: `9.6.8`
- Git archive SHA-256: `702133f23dbc93747d75e76c0ffcaba2501743f4cd657f42365b5404f9f21431`
- Local version: `9.6.8-hollowfen.1`

## Local release-safety patches

- `MCPForUnity.Runtime` is Editor-only, explicitly referenced, and not automatically
  referenced by player assemblies.
- Newtonsoft.Json `3.2.2` is embedded as an Editor-only explicit plugin. Its DLL
  SHA-256 is `7292d3eb508652d14726749dd27094f2d481aeccf2db6427b62f68a71460897e`.
- The package dependency on `com.unity.nuget.newtonsoft-json` is removed so Unity's
  AOT/player DLL is not pulled into release builds.
- Coplay's MIT license and Newtonsoft's license/notices are retained in this package.

Do not replace this package with a floating `#main` dependency. Review upstream
changes, refresh the commit and hashes above, and reapply the release-safety patches.
