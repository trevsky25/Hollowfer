# Hollowfen Handoff For Claude Code

## Project Goal

We are rebuilding **Forest Explorer** into a cleaner game prototype called:

**Hollowfen - The Failing Village**

The player character is **Wren**, a young mushroom identifier/forager returning to the failing village of Hollowfen. The core gameplay is mushroom exploration, accurate identification, field-guide learning, and story progression through NPC missions.

The desired world direction has changed. We no longer want to salvage the old procedural/hand-assembled Forest Explorer world. We want to build from a fresh project using a purchased medieval village asset pack as the foundation.

## Fresh Project Path

Current fresh project:

```txt
/Users/TrevorKist/Desktop/Hollowfen - The Failing Village
```

Old project with useful assets/systems:

```txt
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer
```

Unity import project containing the purchased asset pack:

```txt
/Users/TrevorKist/Forest Explorer
```

## Purchased Asset Pack

Purchased Unity Asset Store package:

**Medieval Fantasy Town Village Environment for RPG FPS**

Asset Store URL:

```txt
https://assetstore.unity.com/packages/3d/environments/fantasy/medieval-fantasy-town-village-environment-for-rpg-fps-107464
```

The target is to make the playable web/Three.js world feel like the product demo: realistic medieval town/village, atmospheric, explorable, and suitable for Hollowfen.

Important finding: the pack readme says the main demo scene uses extra packages for trees/rocks/environment ambience, especially:

```txt
Forest Environment - Dynamic Nature
Ian's Fire Pack
Forest Birds
Flow - the voice of rivers
```

So if the exported Demo 3 scene looks sparse, that may be because some demo environment dependencies are missing.

## Current Fresh App State

The fresh app is a minimal Vite + Three.js project.

Dev server command:

```bash
npm run dev
```

Expected local URL:

```txt
http://127.0.0.1:5175/
```

The fresh project currently includes:

```txt
index.html
src/main.js
src/styles.css
public/world/demo3/demo3_village.glb
public/world/demo3/manifest.json
public/draco/gltf/*
public/models/wren/*
```

## What Was Exported From Unity

A Unity editor exporter was added in the Unity import project:

```txt
/Users/TrevorKist/Forest Explorer/Assets/Editor/ForestExplorerDemo3Exporter.cs
```

It opens:

```txt
Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Demo Scenes/Medieval Demo 3 (6.3 URP Release)/Medieval Village Demo 3.unity
```

It exports a baked OBJ + manifest, then Blender converts the OBJ to GLB:

```txt
/Users/TrevorKist/Desktop/Hollowfen - The Failing Village/public/world/demo3/demo3_village.glb
```

The exported GLB loads, but visually it currently looks sparse compared to the product demo. It has buildings/props but not the full beautiful terrain/forest feel from the demo page.

## Important Current Problem

After adding Wren and player controls to the fresh app, the browser is currently showing a **black screen**.

Likely causes to inspect first:

1. Runtime JS error while loading Wren FBX or animation clips.
2. FBXLoader import or large Wren assets causing a blocking load/failure.
3. `AnimationAction.userData` assignment or animation clip parsing issue.
4. Camera/player spawn happening before model/world load completes.
5. Path issue for Wren model or animation FBX assets.

Check browser console first.

The fastest recovery may be:

1. Temporarily disable Wren loading in `src/main.js`.
2. Confirm the village GLB renders again.
3. Re-add Wren with only the base model.
4. Then add idle/walk/run/jump animations one at a time.

## Wren Assets

Wren was generated in Meshy and rigged/animated. The useful files live in the old project:

```txt
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/public/models/wren
```

The active Wren model in the old project:

```txt
public/models/wren/Meshy_AI_Dustbound_Survivor_0428154114_texture_fbx/wren-final.fbx
```

Manifest:

```txt
public/models/wren/manifest.json
```

Important animations:

```txt
public/models/wren/Animations/Wren-Idle2/Meshy_AI_Dustbound_Survivor_biped_Animation_Idle_11_frame_rate_60.fbx
public/models/wren/Animations/Wren-Walk/Meshy_AI_Dustbound_Survivor_biped_Animation_Walking_frame_rate_60.fbx
public/models/wren/Animations/Wren-Run/Meshy_AI_Dustbound_Survivor_biped_Animation_Running_frame_rate_60.fbx
public/models/wren/Animations/Wren-Jump/Meshy_AI_Dustbound_Survivor_biped_Animation_Jump_Run_frame_rate_60.fbx
```

Current fresh project copied only the Wren files needed for player preview, but this may still need cleanup/optimization.

Recommended next step: convert Wren and animations to optimized GLB/GLTF files instead of loading heavy FBX files directly in the browser.

## Old Project Features To Preserve Later

From the old Forest Explorer project, we want to eventually bring back:

1. Main menu / journal UI.
2. Simple ESC pause screen.
3. Story cards.
4. Wren 3D character model and animations.
5. Mushroom field guide.
6. 16 mushroom species data, images, descriptions, and 3D mushroom models.
7. Mushroom inspection and identification loop.
8. NPCs and Act I Hollowfen story progression.

But for the immediate task, focus only on:

**Get the purchased medieval village world loaded correctly and playable with Wren walking through it.**

## Old Project Files Worth Inspecting

Useful old systems:

```txt
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/entities/Character.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/entities/Player.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/animation/AnimationController.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/core/Game.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/ui/PauseMenu.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/story/StoryCards.js
/Users/TrevorKist/Desktop/Claude Projects/Forest Explorer/src/world/mushrooms/StoryMushrooms.js
```

Do not blindly copy the old world implementation. The old world is intentionally being abandoned.

## Desired Immediate Build Plan

1. Fix black screen in the fresh project.
2. Confirm the medieval Demo 3 GLB renders.
3. Make a playable third-person camera.
4. Add Wren model only.
5. Add Wren idle/walk/run/jump animations.
6. Add simple WASD/Shift/Space controls.
7. Add collision against buildings/walls.
8. Improve spawn location and camera framing.
9. Assess whether Unity export is good enough or whether the scene should be exported differently.

## If The Demo 3 Export Is Still Sparse

Investigate the Unity scene directly:

```txt
/Users/TrevorKist/Forest Explorer
```

Open:

```txt
Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Demo Scenes/Medieval Demo 3 (6.3 URP Release)/Medieval Village Demo 3.unity
```

Questions to answer:

1. Does the scene look sparse inside Unity too?
2. Are terrain, trees, rocks, and atmosphere missing because dependency packages are absent?
3. Are prefab variants disabled and needing the pack's "Update the Prefabs" button?
4. Would Demo 1 or Demo 2 be a better base than Demo 3?
5. Is there a better export path than custom OBJ baking?

Possible better export routes:

1. Export directly from Unity to glTF with a Unity glTF exporter.
2. Use Blender as a staging cleanup tool.
3. Build a curated Hollowfen map from the pack's prefabs instead of trying to use the entire demo scene.

## Design Direction

The final Hollowfen world should feel:

- Realistic medieval village.
- Atmospheric, damp, lived-in, slightly failing.
- Mushroom-foraging friendly.
- Not cartoony.
- Close to the style of the generated story cards and mushroom images already made in the old project.
- More focused than a huge open world: the village can be large enough, with a smaller realistic forest/outskirts area for mushroom discovery.

## User Preference

The user prefers to do visual testing directly in browser, so do not spend too much time preview-testing unless needed. Make code changes, run builds, keep localhost running, and tell the user what to refresh/test.

## Current Known URL

Fresh build should use:

```txt
http://127.0.0.1:5175/
```

Old Forest Explorer preview often used:

```txt
http://127.0.0.1:5174/
```

Use a different port from the old preview.

