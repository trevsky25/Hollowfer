using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Replaces the scene's primitive NPC stand-ins with the optimized People of Hollowfen
    /// T-pose prefabs. Interaction, quest data, schedule hosts, and colliders remain on the
    /// original actor roots, so this is a visual upgrade rather than a gameplay rewrite.
    /// </summary>
    public static class NPCWorldCharacterImporter
    {
        private const string MenuPath = "Hollowfen/World/Place NPC Character Models";
        private const string VisualName = "CharacterVisual";
        private const string ResourceRoot = "Assets/_Hollowfen/Resources/People/Models/";

        private static readonly Dictionary<string, string> ModelIds =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "almy", "sister-almy" },
                { "bram", "old-bram" },
                { "calden", "father-calden" },
                { "edda", "edda" },
                { "hollin", "hollin" },
                { "joren", "joren" },
                { "marra", "marra" },
                { "pell", "elder-pell" },
                { "theo", "theo" },
                { "voss", "master-voss" },
                { "aldric", "lord-aldric" },
            };

        [MenuItem(MenuPath)]
        public static void ApplyToActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding NPC visuals.");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Scene_Hollowfen")
                throw new InvalidOperationException("Open Scene_Hollowfen before rebuilding NPC visuals.");

            int placed = 0;
            foreach (NPCInteractable npc in SceneObjects<NPCInteractable>(scene)
                         .OrderBy(candidate => candidate.gameObject.name, StringComparer.Ordinal))
            {
                if (npc.Data == null || !ModelIds.TryGetValue(npc.Data.Id, out string modelId))
                    throw new InvalidOperationException(
                        npc.gameObject.name + " has no People of Hollowfen model mapping.");

                // Bram already owns the full rigged/skinned production actor used by his key
                // handoff cinematic. Preserve that higher-fidelity setup rather than replacing it
                // with the static journal derivative.
                if (npc.Data.Id == "bram" &&
                    npc.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0)
                {
                    RemovePrimitiveStandIns(npc.transform);
                    placed++;
                    continue;
                }

                string prefabPath = ResourceRoot + modelId + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("Missing NPC world model at " + prefabPath);

                Transform oldVisual = npc.transform.Find(VisualName);
                if (oldVisual != null) Undo.DestroyObjectImmediate(oldVisual.gameObject);
                RemovePrimitiveStandIns(npc.transform);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                if (instance == null)
                    throw new InvalidOperationException("Could not instantiate " + prefabPath);
                Undo.RegisterCreatedObjectUndo(instance, "Place Hollowfen NPC model");
                instance.name = VisualName;
                instance.transform.SetParent(npc.transform, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                SetLayerRecursively(instance.transform, npc.gameObject.layer);

                foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                EditorUtility.SetDirty(npc.gameObject);
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[NPCWorldCharacterImporter] Placed production T-pose visuals on " +
                      placed + " NPC actors. " + VerifyActiveScene());
        }

        [MenuItem("Hollowfen/Verify/NPC World Character Models")]
        public static void VerifyMenu()
        {
            string report = VerifyActiveScene();
            if (report.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static string VerifyActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            var errors = new List<string>();
            int checkedActors = 0;
            foreach (NPCInteractable npc in SceneObjects<NPCInteractable>(scene))
            {
                if (npc.Data == null || !ModelIds.ContainsKey(npc.Data.Id)) continue;
                checkedActors++;
                bool riggedBram = npc.Data.Id == "bram" &&
                                  npc.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
                Transform visual = npc.transform.Find(VisualName);
                if (!riggedBram && visual == null)
                    errors.Add(npc.gameObject.name + " has no " + VisualName + " child");
                if (!riggedBram && visual != null &&
                    visual.GetComponentsInChildren<Renderer>(true).Length == 0)
                    errors.Add(npc.gameObject.name + " visual has no renderer");
                if (HasPrimitiveStandIn(npc.transform))
                    errors.Add(npc.gameObject.name + " still contains a primitive stand-in");
            }

            if (checkedActors < 12)
                errors.Add("expected at least 12 NPC actor instances, found " + checkedActors);
            return errors.Count == 0
                ? "PASS — " + checkedActors + " NPC actor instances use real character models."
                : "FAIL — " + string.Join(" | ", errors);
        }

        private static void RemovePrimitiveStandIns(Transform actor)
        {
            for (int i = actor.childCount - 1; i >= 0; i--)
            {
                Transform child = actor.GetChild(i);
                if (child.name == VisualName) continue;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (renderer == null || filter == null || filter.sharedMesh == null) continue;
                string meshName = filter.sharedMesh.name.ToLowerInvariant();
                if (child.name == "Placeholder" || child.name == "Visual" ||
                    meshName.Contains("capsule") || meshName.Contains("cylinder"))
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static bool HasPrimitiveStandIn(Transform actor)
        {
            for (int i = 0; i < actor.childCount; i++)
            {
                Transform child = actor.GetChild(i);
                if (child.name == "Placeholder") return true;
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (child.name != "Visual" || filter == null || filter.sharedMesh == null) continue;
                string meshName = filter.sharedMesh.name.ToLowerInvariant();
                if (meshName.Contains("capsule") || meshName.Contains("cylinder")) return true;
            }
            return false;
        }

        private static IEnumerable<T> SceneObjects<T>(Scene scene) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .Where(component => component != null && component.gameObject.scene == scene);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
