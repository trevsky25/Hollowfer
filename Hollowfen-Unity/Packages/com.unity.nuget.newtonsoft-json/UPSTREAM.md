# Embedded Newtonsoft.Json for Unity

This is a source-controlled snapshot of Unity's official Newtonsoft package, shared
by Coplay MCP and Unity Pipeline to avoid duplicate assembly identities.

- Upstream package version: `3.2.2`
- Newtonsoft assembly version: `13.0.2`
- Repository revision recorded by Unity: `d8e49aef8979bef617144382052ec2f479645eaf`
- Unity package fingerprint: `4dfd81071c6475bb9c114f920bfb4e3fc5e28c6a`

## Local release-safety patch

The AOT/player `Newtonsoft.Json.dll` importer is disabled for every platform. The
Editor DLL remains enabled and unchanged. This keeps Coplay and Pipeline functional
inside the Unity Editor while preventing the JSON dependency from entering a
Hollowfen player.

Do not update this package independently of Coplay and Pipeline. If Hollowfen later
needs Newtonsoft in game runtime code, redesign and re-audit this boundary instead
of simply re-enabling the AOT DLL.
