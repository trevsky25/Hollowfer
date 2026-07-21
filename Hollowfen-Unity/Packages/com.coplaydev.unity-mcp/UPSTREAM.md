# Embedded MCP for Unity

This package is a source-controlled snapshot of the `MCPForUnity` subtree from
CoplayDev/unity-mcp. It is embedded so Hollowfen can keep its proven Coplay bridge
while sharing Unity's supported Newtonsoft package with Unity Pipeline.

- Upstream: https://github.com/CoplayDev/unity-mcp
- Commit: `c14de1e6dc01ab42d2bb358730cff954bce0ce6b`
- Tag / upstream version: `v10.1.0`
- Upstream source-tree aggregate SHA-256: `cbc7f24493367c02bc9bbe997b2ca28064835a47b9a46cdf14aa8f4e69e881d2`
- Local identity: upstream version `10.1.0`, embedded with the patches below.

The version string must remain exactly `10.1.0`: Coplay uses it to select the
matching `mcpforunityserver` Python distribution at launch.

## Local release-safety patches

- `MCPForUnity.Runtime` is Editor-only, explicitly referenced, and not automatically
  referenced by player assemblies.
- The upstream dependency on `com.unity.nuget.newtonsoft-json` is retained so Coplay
  and Unity Pipeline bind the same assembly instead of embedding duplicate DLLs.
- Hollowfen embeds and hardens that Newtonsoft package separately so only its Editor
  DLL is importable; its AOT/player DLL is disabled on every target.

Coplay, Unity Pipeline, and Newtonsoft form one dependency set in Hollowfen. Review
and test them together when updating any member. Do not replace this package with a
floating `#main` dependency.
