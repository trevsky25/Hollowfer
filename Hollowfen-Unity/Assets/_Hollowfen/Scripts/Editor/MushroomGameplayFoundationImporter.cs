#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Foraging;
using Hollowfen.GameTime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    public static class MushroomGameplayFoundationImporter
    {
        private const string MushroomRoot = "Assets/_Hollowfen/Data/Mushrooms";
        private const string DialogueRoot = "Assets/_Hollowfen/Data/Dialogue";
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";

        private sealed class Profile
        {
            public ForageTier Tier;
            public string ForageFlag;
            public int RespawnDays;
            public int Marra;
            public int Theo;
            public bool Cultivable;
            public float GrowHours;
            public int Yield;
            public string GrowFlag;
        }

        private static readonly Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>
        {
            { "fieldCap",        P(ForageTier.BasketCommon, null, 1, 4, 5) },
            { "fieldMushroom",   P(ForageTier.BasketCommon, null, 1, 4, 5) },
            { "woodEar",         P(ForageTier.BasketCommon, null, 2, 5, 7, true, 6, 3) },
            { "pinecrest",       P(ForageTier.BasketCommon, null, 2, 5, 8) },
            { "goldfoot",        P(ForageTier.BasketCommon, null, 3, 8, 12) },

            { "chanterelle",     P(ForageTier.Knifework, "foraging_knife_unlocked", 2, 7, 10) },
            { "coppercup",       P(ForageTier.Knifework, "foraging_knife_unlocked", 2, 5, 7) },
            { "lacewig",         P(ForageTier.Knifework, "foraging_knife_unlocked", 2, 6, 8, true, 8, 3, "foraging_knife_unlocked") },
            { "bonepale",        P(ForageTier.Knifework, "foraging_knife_unlocked", 4, 0, 9) },
            { "brightspore",     P(ForageTier.Knifework, "foraging_knife_unlocked", 4, 0, 14, true, 12, 2, "edda_grandfather_recovering") },
            { "oyster",          P(ForageTier.Knifework, "foraging_knife_unlocked", 2, 6, 8, true, 8, 4, "foraging_knife_unlocked") },

            { "porcini",         P(ForageTier.Deepwood, "foraging_knife_unlocked", 3, 9, 13) },
            { "flyAgaric",       P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 4, 0, 0) },
            { "libertyCap",      P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 3, 0, 0) },
            { "deadlyGalerina",  P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 4, 0, 0) },
            { "moonring",        P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 5, 0, 0) },
            { "hollowheart",     P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 5, 0, 0) },
            { "wendlight",       P(ForageTier.Deepwood, "tier4_mushrooms_unlocked", 4, 0, 0) },

            { "deathCap",        P(ForageTier.FinalLesson, "tier4_mushrooms_unlocked", 5, 0, 0) },
            { "destroyingAngel", P(ForageTier.FinalLesson, "tier4_mushrooms_unlocked", 5, 0, 0) },
            { "aldermark",       P(ForageTier.FinalLesson, "sable_seedbook_recovered", 5, 10, 18) },
        };

        [MenuItem("Hollowfen/Gameplay Foundation/Apply Profiles and Scene Wiring")]
        public static void ApplyAll()
        {
            ApplyProfiles();
            ApplyBuyerRouting();
            ApplySceneWiring();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GameplayFoundation] Applied 21 species profiles, buyer routing, wild-node ids, and mill rest spot.");
        }

        private static Profile P(ForageTier tier, string forageFlag, int respawn, int marra, int theo,
            bool cultivable = false, float growHours = 6f, int yield = 3, string growFlag = null) =>
            new Profile
            {
                Tier = tier,
                ForageFlag = forageFlag,
                RespawnDays = respawn,
                Marra = marra,
                Theo = theo,
                Cultivable = cultivable,
                GrowHours = growHours,
                Yield = yield,
                GrowFlag = growFlag,
            };

        private static void ApplyProfiles()
        {
            var assets = AssetDatabase.FindAssets("t:MushroomFieldGuideData", new[] { MushroomRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<MushroomFieldGuideData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(x => x != null)
                .ToArray();
            foreach (var species in assets)
            {
                if (!Profiles.TryGetValue(species.Id, out var profile))
                    throw new InvalidOperationException("No gameplay profile authored for mushroom '" + species.Id + "'.");
                var so = new SerializedObject(species);
                so.FindProperty("_forageTier").enumValueIndex = (int)profile.Tier - 1;
                so.FindProperty("_requiredForageFlagId").stringValue = profile.ForageFlag ?? "";
                so.FindProperty("_wildRespawnDays").intValue = profile.RespawnDays;
                so.FindProperty("_marraValueCopper").intValue = profile.Marra;
                so.FindProperty("_theoValueCopper").intValue = profile.Theo;
                so.FindProperty("_cultivable").boolValue = profile.Cultivable;
                so.FindProperty("_cultivationHours").floatValue = profile.GrowHours;
                so.FindProperty("_cultivationYield").intValue = profile.Yield;
                so.FindProperty("_cultivationUnlockFlagId").stringValue = profile.GrowFlag ?? "";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(species);
            }
            if (assets.Length != Profiles.Count)
                throw new InvalidOperationException($"Gameplay profile coverage mismatch: {assets.Length} assets / {Profiles.Count} profiles.");
        }

        private static void ApplyBuyerRouting()
        {
            SetBuyer("Dialogue_Act2_Marra_SellBasket", MushroomBuyer.Marra);
            SetBuyer("Dialogue_Act2_Theo_FirstSale", MushroomBuyer.Theo);
            SetBuyer("Dialogue_Act2_Theo_RepeatSale", MushroomBuyer.Theo);
        }

        private static void SetBuyer(string assetName, MushroomBuyer buyer)
        {
            string guid = AssetDatabase.FindAssets(assetName + " t:DialogueData", new[] { DialogueRoot }).FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Missing dialogue " + assetName);
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(asset);
            so.FindProperty("_basketBuyer").enumValueIndex = (int)buyer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        private static void ApplySceneWiring()
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);

            var nodes = UnityEngine.Object.FindObjectsByType<MushroomNode>(FindObjectsInactive.Include)
                .Where(node => node.gameObject.scene.path == GameplayScene)
                .OrderBy(node => node.Data != null ? node.Data.Id : "")
                .ThenBy(node => node.transform.position.x)
                .ThenBy(node => node.transform.position.z)
                .ThenBy(node => node.transform.position.y)
                .ToArray();

            // Once shipped, a node id is save data. Preserve every valid unique id even if a
            // later world-dressing pass moves a specimen or inserts another one earlier in the
            // spatial sort. Only missing, wrong-species, or duplicate ids receive new values.
            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            var needsId = new List<MushroomNode>();
            foreach (var node in nodes)
            {
                string species = node.Data != null ? node.Data.Id : "unknown";
                string prefix = "wild." + species + ".";
                if (!string.IsNullOrEmpty(node.NodeId) && node.NodeId.StartsWith(prefix, StringComparison.Ordinal) &&
                    usedIds.Add(node.NodeId))
                    continue;
                needsId.Add(node);
            }

            var counters = new Dictionary<string, int>();
            foreach (var node in needsId)
            {
                string species = node.Data != null ? node.Data.Id : "unknown";
                counters.TryGetValue(species, out int index);
                string candidate;
                do
                {
                    index++;
                    candidate = $"wild.{species}.{index:000}";
                } while (!usedIds.Add(candidate));
                counters[species] = index;
                var so = new SerializedObject(node);
                so.FindProperty("_nodeId").stringValue = candidate;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(node);
            }

            var rest = GameObject.Find("_RestSpot_MillHearth");
            if (rest == null)
            {
                var journal = GameObject.Find("Journal_FathersJournal");
                if (journal == null) throw new InvalidOperationException("Journal_FathersJournal missing; cannot anchor mill rest spot.");
                rest = new GameObject("_RestSpot_MillHearth");
                rest.layer = LayerMask.NameToLayer("Foraging");
                rest.transform.position = journal.transform.position;
                var collider = rest.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 1.15f;
                rest.AddComponent<RestSpot>();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[GameplayFoundation] Verified {nodes.Length} wild node ids; assigned {needsId.Count}; rest spot ready.");
        }
    }
}
#endif
