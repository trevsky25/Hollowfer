#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Reapplies the two small physics repairs required by the imported environment packs.
    /// Those packs are installed outside source control, so the repair must be reproducible
    /// from project-owned code instead of relying on locally modified vendor assets.
    /// </summary>
    [InitializeOnLoad]
    public static class ShippingPhysicsSanitizer
    {
        private static readonly string[] BoxColliderPrefabPaths =
        {
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Stairs/Stairs1.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Building - Exploded Parts/Exterior Building 6/ExteriorBuilding6_LODGroup.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Storage/Food_Crate_Frame.prefab",
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs/Buiding Parts/Tower1d.prefab",
        };

        private const string TerrainFernPrefabPath =
            "Assets/NatureManufacture Assets/Forest Environment Dynamic Nature/Foliage and Grass/Prefabs/prefab_fern_01_1.prefab";
        private const string TerrainFernColliderHierarchy =
            "prefab_fern_01_1/foliage_fern_01_1_LOD0";
        private const string TerrainFernMeshGuid = "b057516a5b05d9b4f8965eb46886c45f";

        private static bool _running;

        static ShippingPhysicsSanitizer()
        {
            EditorApplication.delayCall += RepairAfterDomainReload;
        }

        private static void RepairAfterDomainReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            int repaired = RepairVendorAssets();
            if (repaired > 0)
                Debug.Log($"[ShippingPhysics] Repaired {repaired} imported collider transform/component(s).");
        }

        [MenuItem("Hollowfen/Production/Repair Imported Physics")]
        public static void RepairMenu()
        {
            int repaired = RepairVendorAssets();
            Debug.Log(repaired == 0
                ? "[ShippingPhysics] Imported physics already clean."
                : $"[ShippingPhysics] Repaired {repaired} imported collider transform/component(s).");
        }

        public static int RepairVendorAssets()
        {
            if (_running) return 0;
            _running = true;
            int repaired = 0;
            try
            {
                foreach (string path in BoxColliderPrefabPaths)
                    repaired += RepairNegativeBoxScales(path);
                repaired += RemoveTerrainTreeMeshCollider(TerrainFernPrefabPath);
                if (repaired > 0) AssetDatabase.SaveAssets();
                return repaired;
            }
            finally
            {
                _running = false;
            }
        }

        public static string ValidateLoadedScenes()
        {
            var invalidBoxes = new List<string>();
            foreach (var box in Resources.FindObjectsOfTypeAll<BoxCollider>())
            {
                if (box == null || !box.enabled || !box.gameObject.activeInHierarchy ||
                    !box.gameObject.scene.IsValid() || !box.gameObject.scene.isLoaded) continue;
                Vector3 scale = box.transform.lossyScale;
                Vector3 size = box.size;
                if (scale.x < 0f || scale.y < 0f || scale.z < 0f ||
                    size.x < 0f || size.y < 0f || size.z < 0f)
                    invalidBoxes.Add(HierarchyPath(box.transform));
            }

            var invalidTrees = new List<string>();
            foreach (var terrain in Resources.FindObjectsOfTypeAll<Terrain>())
            {
                if (terrain == null || !terrain.gameObject.scene.IsValid() ||
                    !terrain.gameObject.scene.isLoaded || terrain.terrainData == null) continue;
                var prototypes = terrain.terrainData.treePrototypes;
                for (int i = 0; i < prototypes.Length; i++)
                {
                    var prefab = prototypes[i].prefab;
                    if (prefab != null && prefab.GetComponentsInChildren<MeshCollider>(true).Length > 0)
                        invalidTrees.Add($"{HierarchyPath(terrain.transform)} tree[{i}] {AssetDatabase.GetAssetPath(prefab)}");
                }
            }

            if (invalidBoxes.Count > 0 || invalidTrees.Count > 0)
            {
                string details = string.Join("\n", invalidBoxes.Concat(invalidTrees).Take(20));
                throw new InvalidOperationException(
                    $"[ShippingPhysics] Invalid colliders remain: boxes={invalidBoxes.Count}, terrain trees={invalidTrees.Count}\n{details}");
            }

            return "SHIPPING PHYSICS — PASS: 0 active negative BoxColliders, 0 terrain-tree MeshColliders";
        }

        private static int RepairNegativeBoxScales(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return 0;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int repaired = 0;
            try
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    Vector3 worldScale = box.transform.lossyScale;
                    if (worldScale.x >= 0f && worldScale.y >= 0f && worldScale.z >= 0f) continue;

                    Vector3 local = box.transform.localScale;
                    Vector3 positive = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
                    if (local == positive)
                    {
                        Debug.LogError($"[ShippingPhysics] Negative scale comes from an ancestor; not auto-repaired: {path} :: {HierarchyPath(box.transform)}");
                        continue;
                    }

                    var renderer = box.GetComponent<Renderer>();
                    bool centered = box.center.sqrMagnitude <= 0.000001f;
                    bool positiveSize = box.size.x > 0f && box.size.y > 0f && box.size.z > 0f;
                    bool safeCubeProxy = centered && positiveSize && box.transform.childCount == 0 &&
                                         (renderer == null || !renderer.enabled);
                    if (!safeCubeProxy)
                        throw new InvalidOperationException(
                            $"[ShippingPhysics] Refused changed vendor collider shape: {path} :: {HierarchyPath(box.transform)}");

                    // These vendor objects use centered cube meshes as visible collision blocks.
                    // A box is reflection-symmetric, so removing only the sign preserves its
                    // position, dimensions, rotation, rendering, and collision volume.
                    box.transform.localScale = positive;
                    repaired++;
                }
                if (repaired > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return repaired;
        }

        private static int RemoveTerrainTreeMeshCollider(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return 0;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int repaired = 0;
            try
            {
                var meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                if (meshColliders.Length == 0) return 0;
                if (meshColliders.Length != 1)
                    throw new InvalidOperationException(
                        $"[ShippingPhysics] Refused unexpected terrain-tree MeshCollider count: {path} :: {meshColliders.Length}");

                MeshCollider meshCollider = meshColliders[0];
                string hierarchy = HierarchyPath(meshCollider.transform);
                string meshPath = AssetDatabase.GetAssetPath(meshCollider.sharedMesh);
                string meshGuid = string.IsNullOrEmpty(meshPath)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(meshPath);
                if (!string.Equals(hierarchy, TerrainFernColliderHierarchy, StringComparison.Ordinal) ||
                    !string.Equals(meshGuid, TerrainFernMeshGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "[ShippingPhysics] Refused unknown terrain-tree MeshCollider: " +
                        $"{path} :: hierarchy={hierarchy}, meshGuid={meshGuid}, meshPath={meshPath}");

                UnityEngine.Object.DestroyImmediate(meshCollider);
                repaired = 1;
                if (repaired > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return repaired;
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }

    public sealed class ShippingPhysicsBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ShippingPhysicsSanitizer.RepairVendorAssets();
            try
            {
                ShippingPhysicsSanitizer.ValidateLoadedScenes();
            }
            catch (Exception exception)
            {
                throw new BuildFailedException(exception.Message);
            }
        }
    }

    /// <summary>Validates the actual cloned scene Unity is about to write into the player.</summary>
    public sealed class ShippingPhysicsSceneGate : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var invalid = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (box == null || !box.enabled || !box.gameObject.activeInHierarchy) continue;
                    Vector3 scale = box.transform.lossyScale;
                    Vector3 size = box.size;
                    if (scale.x < 0f || scale.y < 0f || scale.z < 0f ||
                        size.x < 0f || size.y < 0f || size.z < 0f)
                        invalid.Add(scene.name + ": " + box.name);
                }

                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (terrain.terrainData == null) continue;
                    var prototypes = terrain.terrainData.treePrototypes;
                    for (int i = 0; i < prototypes.Length; i++)
                    {
                        var prefab = prototypes[i].prefab;
                        if (prefab != null && prefab.GetComponentsInChildren<MeshCollider>(true).Length > 0)
                            invalid.Add(scene.name + $": terrain tree[{i}] {AssetDatabase.GetAssetPath(prefab)}");
                    }
                }
            }

            if (invalid.Count > 0)
                throw new BuildFailedException("[ShippingPhysics] Processed build scene contains invalid colliders:\n" +
                                               string.Join("\n", invalid.Take(20)));
        }
    }
}
#endif
