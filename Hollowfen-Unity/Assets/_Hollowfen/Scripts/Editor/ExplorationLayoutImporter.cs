using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Applies the production exploration route to the existing two-district village. The three
    /// approved anchors (well, Crooked Pintle, and Tobin mill) are deliberately untouched.
    /// Other destinations are assigned to real existing buildings, with their NPC and prop roots
    /// moved to the same site.
    /// </summary>
    public static class ExplorationLayoutImporter
    {
        private const string MenuPath = "Hollowfen/World/Apply Exploration Layout";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying the world layout.");
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "Scene_Hollowfen")
                throw new InvalidOperationException("Open Scene_Hollowfen before applying the world layout.");

            // West foundry: repurpose the real house at (198, 198), across the lower bridge.
            Vector3 oldForgeMarker = Require("Marker_JorensForge", scene).transform.position;
            Vector3 newForgeMarker = new Vector3(198f, 32.65f, 197.9f);
            MoveBy("_JorensForge", newForgeMarker - oldForgeMarker, scene);
            Place("Marker_JorensForge", newForgeMarker, 35f, scene);
            Place("Joren_Forge", new Vector3(206f, 32.55f, 198f), 270f, scene);

            // Western doorway: the isolated large house was already pinned correctly; put Almy
            // at its approach instead of beside the mill.
            Place("NPC_Almy", new Vector3(153f, 31.45f, 271f), 270f, scene);

            // Edda's home now uses the raised cottage on the west side of the chapel district.
            Place("Marker_EddasCottage", new Vector3(197f, 38.1f, 279.3f), 0f, scene);
            Place("EddaGroup", new Vector3(205f, 37.95f, 279f), 270f, scene);

            // Calden belongs at the chapel door, not in the mill yard.
            Place("CaldenGroup", new Vector3(212f, 38.05f, 314f), 270f, scene);

            // Move the entire trader camp to the eastern market road. The physical wagon group,
            // destination marker, and schedule anchor remain coincident.
            Vector3 oldWagonMarker = Require("Marker_TheosWagon", scene).transform.position;
            Vector3 newWagonMarker = new Vector3(325f, 36.45f, 213f);
            MoveBy("_TheoWagon", newWagonMarker - oldWagonMarker, scene);
            Place("Marker_TheosWagon", newWagonMarker, 90f, scene);
            Place("Theo_Wagon", new Vector3(327f, 36.45f, 213f), 270f, scene);
            Place("NPC_Voss", new Vector3(321f, 36.45f, 216f), 135f, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ExplorationLayoutImporter] Applied whole-map route. " + VerifyActiveScene());
        }

        [MenuItem("Hollowfen/Verify/Exploration Layout")]
        public static void VerifyMenu()
        {
            string result = VerifyActiveScene();
            if (result.StartsWith("PASS", StringComparison.Ordinal)) Debug.Log(result);
            else Debug.LogError(result);
        }

        public static string VerifyActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            string[] names =
            {
                "Marker_VillageWell", "Marker_CrookedPintle", "Marker_FathersMill",
                "Marker_JorensForge", "Marker_AlmyDoorway", "Marker_EddasCottage",
                "Marker_Chapel", "Marker_TheosWagon", "Marker_Manor",
                "Marker_OldWoodEdge", "Marker_WitchsCottage", "_ClearCutSite",
            };
            var points = names.Select(name => Require(name, scene).transform.position).ToArray();
            float minX = points.Min(point => point.x);
            float maxX = points.Max(point => point.x);
            float minZ = points.Min(point => point.z);
            float maxZ = points.Max(point => point.z);
            if (maxX - minX < 200f || maxZ - minZ < 340f)
                return "FAIL — destination spread is only " + (maxX - minX).ToString("0") +
                       "m by " + (maxZ - minZ).ToString("0") + "m.";

            if (FlatDistance("Marker_JorensForge", "Marker_CrookedPintle", scene) < 80f)
                return "FAIL — Joren's Forge is still bunched against the Pintle.";
            if (FlatDistance("Marker_TheosWagon", "Marker_VillageWell", scene) < 65f)
                return "FAIL — Theo's wagon is still bunched against the well.";
            if (FlatDistance("NPC_Almy", "Marker_AlmyDoorway", scene) > 15f)
                return "FAIL — Almy is not at her western doorway.";

            return "PASS — 12 destinations span " + (maxX - minX).ToString("0") + "m east-west and " +
                   (maxZ - minZ).ToString("0") + "m south-north; quest NPCs share their sites.";
        }

        private static void Place(string name, Vector3 position, float yaw, Scene scene)
        {
            GameObject go = Require(name, scene);
            Undo.RecordObject(go.transform, "Reposition Hollowfen destination");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            EditorUtility.SetDirty(go);
        }

        private static void MoveBy(string name, Vector3 delta, Scene scene)
        {
            GameObject go = Require(name, scene);
            Undo.RecordObject(go.transform, "Move Hollowfen destination dressing");
            go.transform.position += delta;
            EditorUtility.SetDirty(go);
        }

        private static float FlatDistance(string a, string b, Scene scene)
        {
            Vector3 first = Require(a, scene).transform.position;
            Vector3 second = Require(b, scene).transform.position;
            first.y = second.y = 0f;
            return Vector3.Distance(first, second);
        }

        private static GameObject Require(string name, Scene scene)
        {
            GameObject found = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(candidate => candidate != null && candidate.scene == scene &&
                                             candidate.name == name);
            if (found == null) throw new InvalidOperationException("Missing scene object '" + name + "'.");
            return found;
        }
    }
}
