#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Replaces the candle-flame prefab omitted from the vendor's 4.2.1 package with one
    /// project-owned, instancing-friendly mesh. Repair the six source variants once instead of
    /// serializing hundreds of scene overrides. The replacement deliberately has no realtime
    /// light or per-instance behaviour.
    /// </summary>
    public static class CandleFlameRepair
    {
        private const string MissingPrefix = "CandleFlame (Missing Prefab";
        private const string AssetFolder = "Assets/_Hollowfen/Visuals/CandleFlame";
        private const string MeshPath = AssetFolder + "/CandleFlameMesh.asset";
        private const string MaterialPath = AssetFolder + "/CandleFlame.mat";
        private const string PrefabPath = AssetFolder + "/CandleFlame.prefab";

        private static readonly string[] VariantPaths =
        {
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/CandleLarge with Flame Variant.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/CandleMedium with Flame Variant.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/CandleSmall with Flame Variant.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/Candleholder with Candle Variant.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/WallCandle With Candle & Flame Variant.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Lighting and Candles/WallCandleB with Candle Lit Variant.prefab",
        };

        public static string RunAll()
        {
            EnsureFolder();
            Mesh mesh = CreateOrUpdateMesh();
            Material material = CreateOrUpdateMaterial();
            GameObject flamePrefab = CreateOrUpdatePrefab(mesh, material);

            int repairedVariants = 0;
            foreach (string path in VariantPaths)
                if (RepairVariant(path, flamePrefab)) repairedVariants++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"CANDLE FLAME — repaired {repairedVariants}/{VariantPaths.Length} source variants " +
                   "with one shared 64-triangle, instanced, unlit flame and no realtime lights";
        }

        public static string ValidateAssets()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(mesh != null && mesh.triangles.Length / 3 == 64,
                "project-owned flame mesh is missing or changed unexpectedly");
            Require(material != null && material.enableInstancing,
                "flame material is missing or GPU instancing is disabled");
            Require(prefab != null && prefab.GetComponentsInChildren<Renderer>(true).Length == 1,
                "flame prefab must contain exactly one renderer");

            foreach (string path in VariantPaths)
            {
                var dependencies = AssetDatabase.GetDependencies(path, true);
                Require(dependencies.Contains(PrefabPath), path + " is not wired to the replacement flame");
            }

            Transform[] sceneTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            Require(!sceneTransforms.Any(candidate =>
                    candidate.name.StartsWith(MissingPrefix, StringComparison.Ordinal)),
                "the loaded scene still contains an unpacked missing candle flame");
            Require(!sceneTransforms.Any(candidate =>
                    candidate.parent == null && candidate.name == "Ambient Audio Objects"),
                "the loaded scene still contains the broken legacy ambience hierarchy");
            int missingScripts = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include)
                .Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            Require(missingScripts == 0, "the loaded scene contains missing scripts");
            return "CANDLE/SCENE REFERENCES — PASS: six source variants and the loaded scene use " +
                   "the shared one-renderer flame; legacy ambience and missing scripts are absent";
        }

        private static void EnsureFolder()
        {
            const string visuals = "Assets/_Hollowfen/Visuals";
            if (!AssetDatabase.IsValidFolder(visuals))
                AssetDatabase.CreateFolder("Assets/_Hollowfen", "Visuals");
            if (!AssetDatabase.IsValidFolder(AssetFolder))
                AssetDatabase.CreateFolder(visuals, "CandleFlame");
        }

        private static Mesh CreateOrUpdateMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "CandleFlameMesh" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                mesh.Clear();
            }

            const int sides = 8;
            float[] heights = { 0f, .016f, .043f, .063f };
            float[] radii = { .008f, .017f, .010f, .004f };
            float[] offsets = { 0f, 0f, .002f, .005f };
            var vertices = new List<Vector3>(heights.Length * sides + 2);
            var uvs = new List<Vector2>(heights.Length * sides + 2);

            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * Mathf.PI * 2f / sides;
                    vertices.Add(new Vector3(offsets[ring] + Mathf.Cos(angle) * radii[ring],
                        heights[ring], Mathf.Sin(angle) * radii[ring]));
                    uvs.Add(new Vector2((float)side / sides, heights[ring] / .078f));
                }
            }

            int bottom = vertices.Count;
            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(.5f, 0f));
            int tip = vertices.Count;
            vertices.Add(new Vector3(.008f, .078f, 0f));
            uvs.Add(new Vector2(.5f, 1f));

            var triangles = new List<int>(64 * 3);
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles.Add(bottom);
                triangles.Add(next);
                triangles.Add(side);
            }
            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                int current = ring * sides;
                int above = (ring + 1) * sides;
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    triangles.Add(current + side);
                    triangles.Add(current + next);
                    triangles.Add(above + next);
                    triangles.Add(current + side);
                    triangles.Add(above + next);
                    triangles.Add(above + side);
                }
            }
            int topRing = (heights.Length - 1) * sides;
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                triangles.Add(topRing + side);
                triangles.Add(topRing + next);
                triangles.Add(tip);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateOrUpdateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Require(shader != null, "URP Unlit shader is unavailable");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "CandleFlame" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;
            Color flame = new Color(1f, .38f, .055f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", flame);
            if (material.HasProperty("_Color")) material.SetColor("_Color", flame);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateOrUpdatePrefab(Mesh mesh, Material material)
        {
            var root = new GameObject("CandleFlame");
            var visual = new GameObject("Flame", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(root.transform, false);
            const StaticEditorFlags staticFlags = StaticEditorFlags.BatchingStatic |
                                                  StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(root, staticFlags);
            GameObjectUtility.SetStaticEditorFlags(visual, staticFlags);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Require(prefab != null, "failed to create the candle-flame prefab");
            return prefab;
        }

        private static bool RepairVariant(string path, GameObject flamePrefab)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform[] missing = contents.GetComponentsInChildren<Transform>(true)
                    .Where(candidate => candidate.name.StartsWith(MissingPrefix, StringComparison.Ordinal))
                    .ToArray();
                if (missing.Length == 0)
                {
                    Require(contents.GetComponentsInChildren<Transform>(true)
                        .Any(candidate => candidate.name == "CandleFlame"),
                        path + " contains neither a missing nor replacement flame");
                    return false;
                }

                foreach (Transform placeholder in missing)
                {
                    Transform parent = placeholder.parent;
                    Vector3 localPosition = placeholder.localPosition;
                    Quaternion localRotation = placeholder.localRotation;
                    UnityEngine.Object.DestroyImmediate(placeholder.gameObject);

                    var replacement = (GameObject)PrefabUtility.InstantiatePrefab(flamePrefab, parent);
                    replacement.name = "CandleFlame";
                    replacement.transform.localPosition = localPosition;
                    replacement.transform.localRotation = localRotation;
                    // Two vendor variants scaled the omitted unit primitive. The owned mesh is
                    // authored at final candle scale, so normalize every replacement instance.
                    replacement.transform.localScale = Vector3.one;
                    AddToNearestLodGroup(replacement.transform,
                        replacement.GetComponentInChildren<Renderer>(true));
                }

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void AddToNearestLodGroup(Transform flame, Renderer renderer)
        {
            if (renderer == null) return;
            Transform ancestor = flame.parent;
            while (ancestor != null)
            {
                LODGroup group = ancestor.GetComponent<LODGroup>();
                if (group != null)
                {
                    LOD[] lods = group.GetLODs();
                    if (lods.Length == 0) lods = new[] { new LOD(.027f, Array.Empty<Renderer>()) };
                    for (int i = 0; i < lods.Length; i++)
                    {
                        var renderers = (lods[i].renderers ?? Array.Empty<Renderer>()).Where(r => r != null).ToList();
                        if (!renderers.Contains(renderer)) renderers.Add(renderer);
                        lods[i].renderers = renderers.ToArray();
                    }
                    group.SetLODs(lods);
                    group.RecalculateBounds();
                    return;
                }
                ancestor = ancestor.parent;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[CandleFlameRepair] " + message);
        }
    }
}
#endif
