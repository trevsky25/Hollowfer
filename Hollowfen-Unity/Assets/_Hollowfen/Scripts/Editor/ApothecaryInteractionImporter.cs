#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Apothecary;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Owns the gameplay wiring layered onto the purchased Alchemy and Magic Lab building.
    /// Keeping this beside the showcase importer makes a source-scene rebuild deterministic.
    /// </summary>
    public static class ApothecaryInteractionImporter
    {
        private const string PrefabPath =
            "Assets/_Hollowfen/Art/Apothecary/PF_TobinApothecaryBuilding.prefab";
        private const string InteractionName = "LightInteraction";

        private static readonly string[] SwitchPaths =
        {
            "Entrance_Room/Candle_Stand",
            "Hall_Room/Candle_Stand (3)",
            "Hall_Room/Candle_Stand (4)",
            "Hall_Room/Niche/Candlestick",
            "Hall_Room/Candle_Stand",
            "Hall_Room/Candle_Stand (1)",
            "Cage_Room/Lantern",
        };

        [MenuItem("Hollowfen/Apothecary/Wire Automatic Door and Candlelights")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) throw new InvalidOperationException("Missing apothecary prefab.");
            try
            {
                ConfigureRoot(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save the wired apothecary prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "APOTHECARY INTERACTIONS — WIRED: inward-opening automatic entrance, " +
                   "automatic rear chain gate, and seven Triangle/E candlelight controls";
        }

        public static void ConfigureRoot(GameObject root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            ConfigureDoor(root.transform);
            ConfigureChainDoor(root.transform);
            ConfigureCandlelights(root);
        }

        private static void ConfigureDoor(Transform root)
        {
            Transform doorway = Required(root, "Entrance_Room/Locked_Door");
            Transform left = Required(doorway, "Wood_Door_01");
            Transform right = Required(doorway, "Wood_Door_02");

            // This is the authored open pose. The runtime controller closes it in Awake and then
            // restores this spread when Wren enters the threshold radius.
            // The showcase pose folds the leaves toward Hollowfen's exterior approach on the
            // positive-local-Z side, making them sweep at Wren. Reverse both hinge arcs so the
            // doors open naturally into the apothecary instead.
            Vector3 leftOpen = new Vector3(0f, 250f, 0f);
            Vector3 rightOpen = new Vector3(0f, 110f, 0f);
            left.localRotation = Quaternion.Euler(leftOpen);
            right.localRotation = Quaternion.Euler(rightOpen);
            EnsureLeafCollider(left);
            EnsureLeafCollider(right);
            ClearStaticFlags(doorway);

            var controller = doorway.GetComponent<ApothecaryProximityDoor>();
            if (controller == null) controller = doorway.gameObject.AddComponent<ApothecaryProximityDoor>();
            controller.Configure(left, right,
                Vector3.zero, Vector3.zero,
                leftOpen, rightOpen, 4.25f, 5.35f, 125f);
        }

        private static void ConfigureChainDoor(Transform root)
        {
            Transform doorway = Required(root, "Cage_Room/Wire_Door");
            Transform gate = Required(doorway, "Wire");
            Vector3 closed = Vector3.zero;
            Vector3 open = new Vector3(0f, 2.2f, 0f);
            gate.localPosition = closed;
            ClearStaticFlags(doorway);

            var controller = doorway.GetComponent<ApothecaryChainDoor>();
            if (controller == null) controller = doorway.gameObject.AddComponent<ApothecaryChainDoor>();
            controller.Configure(gate, closed, open, 3.85f, 5f, 2.25f, 1.7f);
        }

        private static void ConfigureCandlelights(GameObject root)
        {
            Light[] lights = root.GetComponentsInChildren<Light>(true)
                .OrderBy(light => HierarchyPath(light.transform), StringComparer.Ordinal)
                .ToArray();
            Renderer[] flames = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => IsCandleFlame(renderer.transform))
                .OrderBy(renderer => HierarchyPath(renderer.transform), StringComparer.Ordinal)
                .ToArray();
            Animator[] animators = root.GetComponentsInChildren<Animator>(true)
                .Where(animator => IsCandleAnimator(animator.runtimeAnimatorController))
                .OrderBy(animator => HierarchyPath(animator.transform), StringComparer.Ordinal)
                .ToArray();

            var lighting = root.GetComponent<ApothecaryLightingController>();
            if (lighting == null) lighting = root.AddComponent<ApothecaryLightingController>();
            lighting.Configure(lights, flames, animators, .55f);

            int layer = LayerMask.NameToLayer("Foraging");
            if (layer < 0) throw new InvalidOperationException("Foraging layer is missing.");
            foreach (string path in SwitchPaths)
            {
                Transform rig = Required(root.transform, path);
                Transform interaction = rig.Find(InteractionName);
                if (interaction == null)
                {
                    var created = new GameObject(InteractionName);
                    interaction = created.transform;
                    interaction.SetParent(rig, false);
                }
                interaction.gameObject.layer = layer;
                interaction.localPosition = InteractionOffset(rig.name);
                interaction.localRotation = Quaternion.identity;
                interaction.localScale = Vector3.one;

                var sphere = interaction.GetComponent<SphereCollider>();
                if (sphere == null) sphere = interaction.gameObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.center = Vector3.zero;
                sphere.radius = .72f;
                foreach (Collider duplicate in interaction.GetComponents<Collider>())
                    if (duplicate != sphere) UnityEngine.Object.DestroyImmediate(duplicate);

                var lightSwitch = interaction.GetComponent<ApothecaryLightSwitch>();
                if (lightSwitch == null)
                    lightSwitch = interaction.gameObject.AddComponent<ApothecaryLightSwitch>();
                lightSwitch.Configure(lighting);
                GameObjectUtility.SetStaticEditorFlags(interaction.gameObject, 0);
            }
        }

        private static void EnsureLeafCollider(Transform leaf)
        {
            MeshFilter meshFilter = leaf.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                throw new InvalidOperationException("Door leaf has no source mesh: " + leaf.name);
            BoxCollider collider = leaf.GetComponent<BoxCollider>();
            if (collider == null) collider = leaf.gameObject.AddComponent<BoxCollider>();
            collider.isTrigger = false;
            collider.center = meshFilter.sharedMesh.bounds.center;
            collider.size = meshFilter.sharedMesh.bounds.size;
        }

        private static bool IsCandleFlame(Transform candidate)
        {
            if (candidate == null ||
                !(candidate.name.StartsWith("Fire_", StringComparison.Ordinal) ||
                  candidate.name == "Candle_Fire")) return false;
            string path = HierarchyPath(candidate);
            return path.IndexOf("Fireplace", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsCandleAnimator(RuntimeAnimatorController controller)
        {
            if (controller == null) return false;
            switch (controller.name)
            {
                case "Candle_Stand":
                case "Candle_Stand_2":
                case "Candlestick":
                case "Lantern":
                case "Chandelier":
                    return true;
                default:
                    return false;
            }
        }

        private static Vector3 InteractionOffset(string rigName)
        {
            if (rigName == "Candlestick") return new Vector3(0f, .18f, 0f);
            if (rigName == "Lantern") return new Vector3(0f, .10f, 0f);
            return new Vector3(0f, 1f, 0f);
        }

        private static Transform Required(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null)
                throw new InvalidOperationException("Purchased apothecary hierarchy is missing " + path);
            return found;
        }

        private static void ClearStaticFlags(Transform root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new List<string>();
            while (transform != null)
            {
                names.Add(transform.name);
                transform = transform.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
#endif
