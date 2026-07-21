#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.GameTime;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring pass for the Wend bridge restoration project.</summary>
    public static class BridgeRestorationImporter
    {
        private const string DataRoot = "Assets/_Hollowfen/Data/Restoration";
        private const string CottagePath = DataRoot + "/Restoration_Cottages.asset";
        private const string ProjectPath = DataRoot + "/Restoration_WendBridge.asset";
        private const string DatabasePath = "Assets/_Hollowfen/Resources/RestorationProjectDatabase.asset";
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string SceneRootName = "_LivingRestoration_WendBridge";
        private const string ArtRoot = "Assets/_Hollowfen/Art/Restoration";
        private const string EnvironmentRoot =
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs";
        private const string CratePrefab = EnvironmentRoot + "/Storage/Crate1.prefab";
        private const string BarrelPrefab = EnvironmentRoot + "/Storage/Barrel.prefab";
        private const string CartPrefab = EnvironmentRoot + "/Carts/Cart1 Variant 1.prefab";
        private const string BridgeWoodMaterial =
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/Materials - Objects/Wood1 B.mat";
        // Existing bridge deck is level at y 32.67; the owned dressing is authored from its top.
        private static readonly Vector3 BridgeCenter = new Vector3(218.21f, 32.63f, 224.97f);

        private sealed class Materials
        {
            public Material DarkWood;
            public Material FreshWood;
            public Material Iron;
            public Material Parchment;
            public Material Lamp;
        }

        [MenuItem("Hollowfen/Restoration/Build Wend Bridge Project")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            RestorationContentImporter.BuildAll();
            var project = UpsertProject();
            RestorationCatalogueImporter.UpsertDatabase();
            BuildScene(project, LoadMaterials());
            NPCScheduleImporter.ApplyAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "WEND BRIDGE RESTORATION — BUILT: catalogue funding, safe staged footway, crew rhythm, dawn reveal, and first-crossing completion";
        }

        private static RestorationProjectData UpsertProject()
        {
            EnsureFolder(DataRoot);
            var project = AssetDatabase.LoadAssetAtPath<RestorationProjectData>(ProjectPath);
            if (project == null)
            {
                project = ScriptableObject.CreateInstance<RestorationProjectData>();
                AssetDatabase.CreateAsset(project, ProjectPath);
            }

            Set(project, "_id", "wend_bridge");
            Set(project, "_titleId", "restoration.bridge.title");
            Set(project, "_summaryId", "restoration.bridge.summary");
            Set(project, "_locationId", "restoration.bridge.location");
            Set(project, "_promptTargetId", "restoration.bridge.site");
            Set(project, "_benefitId", "restoration.bridge.benefit");
            Set(project, "_activeQuestId", "");
            Set(project, "_completedQuestId", "");
            Set(project, "_stageRules", new[]
            {
                new RestorationStageRule(RestorationCondition.CompletedQuest, "cottagesReopen", RestorationStage.Surveyed),
                new RestorationStageRule(RestorationCondition.FlagSet, "wend_bridge_supplies_ready", RestorationStage.SuppliesCommitted),
                new RestorationStageRule(RestorationCondition.FlagSet, "wend_bridge_work_started", RestorationStage.WorkUnderway),
                new RestorationStageRule(RestorationCondition.FlagSet, "wend_bridge_restored", RestorationStage.Restored),
                new RestorationStageRule(RestorationCondition.FlagSet, "wend_bridge_in_use", RestorationStage.Occupied),
            });
            Set(project, "_stageCopy", new[]
            {
                new RestorationStageCopy(RestorationStage.Surveyed,
                    "restoration.bridge.stage.surveyed.title", "restoration.bridge.stage.surveyed.body",
                    "restoration.bridge.stage.surveyed.short"),
                new RestorationStageCopy(RestorationStage.SuppliesCommitted,
                    "restoration.bridge.stage.supplies.title", "restoration.bridge.stage.supplies.body",
                    "restoration.bridge.stage.supplies.short"),
                new RestorationStageCopy(RestorationStage.WorkUnderway,
                    "restoration.bridge.stage.work.title", "restoration.bridge.stage.work.body",
                    "restoration.bridge.stage.work.short"),
                new RestorationStageCopy(RestorationStage.Restored,
                    "restoration.bridge.stage.restored.title", "restoration.bridge.stage.restored.body",
                    "restoration.bridge.stage.restored.short"),
                new RestorationStageCopy(RestorationStage.Occupied,
                    "restoration.bridge.stage.occupied.title", "restoration.bridge.stage.occupied.body",
                    "restoration.bridge.stage.occupied.short"),
            });
            Set(project, "_contributions", new[]
            {
                new RestorationContribution("restoration.bridge.contribution.timber",
                    "restoration.bridge.contribution.timber.detail", "wend_bridge_timber_funded", 24),
                new RestorationContribution("restoration.bridge.contribution.iron",
                    "restoration.bridge.contribution.iron.detail", "wend_bridge_iron_funded", 12),
            });
            Set(project, "_contributionsCompleteFlagId", "wend_bridge_supplies_ready");
            Set(project, "_milestones", new[]
            {
                new RestorationMilestone("restoration.bridge.milestone.survey",
                    "restoration.bridge.milestone.survey.detail", RestorationCondition.CompletedQuest, "cottagesReopen"),
                new RestorationMilestone("restoration.bridge.milestone.timber",
                    "restoration.bridge.milestone.timber.detail", RestorationCondition.FlagSet, "wend_bridge_timber_funded"),
                new RestorationMilestone("restoration.bridge.milestone.iron",
                    "restoration.bridge.milestone.iron.detail", RestorationCondition.FlagSet, "wend_bridge_iron_funded"),
                new RestorationMilestone("restoration.bridge.milestone.crew",
                    "restoration.bridge.milestone.crew.detail", RestorationCondition.FlagSet, "wend_bridge_work_started"),
                new RestorationMilestone("restoration.bridge.milestone.crossing",
                    "restoration.bridge.milestone.crossing.detail", RestorationCondition.FlagSet, "wend_bridge_in_use"),
            });
            EditorUtility.SetDirty(project);
            return project;
        }

        private static Materials LoadMaterials()
        {
            var materials = new Materials
            {
                DarkWood = Required<Material>(ArtRoot + "/Restoration_DarkWood.mat"),
                FreshWood = UpsertBridgeWood(),
                Iron = Required<Material>(ArtRoot + "/Restoration_Iron.mat"),
                Parchment = Required<Material>(ArtRoot + "/Restoration_Parchment.mat"),
            };
            string lampPath = ArtRoot + "/Restoration_LampGlow.mat";
            materials.Lamp = AssetDatabase.LoadAssetAtPath<Material>(lampPath);
            if (materials.Lamp == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                materials.Lamp = new Material(shader) { name = "Restoration_LampGlow" };
                AssetDatabase.CreateAsset(materials.Lamp, lampPath);
            }
            materials.Lamp.color = new Color(1f, .52f, .16f);
            materials.Lamp.EnableKeyword("_EMISSION");
            materials.Lamp.SetColor("_EmissionColor", new Color(1f, .36f, .09f) * 2.7f);
            EditorUtility.SetDirty(materials.Lamp);
            return materials;
        }

        private static Material UpsertBridgeWood()
        {
            var source = Required<Material>(BridgeWoodMaterial);
            string path = ArtRoot + "/Restoration_BridgeFreshWood.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = "Restoration_BridgeFreshWood" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, material);
                material.name = "Restoration_BridgeFreshWood";
            }
            // Preserve the vendor's real timber grain while reading a shade newer than the old span.
            material.color = new Color(.84f, .73f, .58f, 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildScene(RestorationProjectData project, Materials materials)
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            var existing = GameObject.Find(SceneRootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var root = new GameObject(SceneRootName);
            root.transform.position = BridgeCenter;
            SceneManager.MoveGameObjectToScene(root, scene);

            int layer = LayerMask.NameToLayer("Foraging");
            if (layer >= 0) root.layer = layer;
            var siteCollider = root.AddComponent<BoxCollider>();
            siteCollider.isTrigger = true;
            siteCollider.center = new Vector3(4.5f, .8f, -11.3f);
            siteCollider.size = new Vector3(3.2f, 2.8f, 3.5f);
            var site = root.AddComponent<RestorationSite>();
            Set(site, "_project", project);
            Set(site, "_promptTargetId", "restoration.bridge.site");
            Set(site, "_interactableFromStage", RestorationStage.Surveyed);

            var restricted = StageRoot("RestrictedFootway", root.transform);
            BuildRestrictedFootway(restricted.transform, materials);
            var survey = StageRoot("SurveyMarkers", root.transform);
            BuildSurvey(survey.transform, materials);
            var supplies = StageRoot("SupplyDelivery", root.transform);
            BuildSupplies(supplies.transform, materials);
            var work = StageRoot("BridgeCrewWorksite", root.transform);
            BuildWorksite(work.transform, materials);
            var restored = StageRoot("RestoredSpan", root.transform);
            BuildRestoredSpan(restored.transform, materials);
            var occupied = StageRoot("CrossingInUse", root.transform);
            BuildOccupiedBanks(occupied.transform);

            Set(site, "_presentations", new[]
            {
                new RestorationPresentation(restricted, RestorationStage.Surveyed, RestorationStage.WorkUnderway),
                new RestorationPresentation(survey, RestorationStage.Surveyed, RestorationStage.Surveyed),
                new RestorationPresentation(supplies, RestorationStage.SuppliesCommitted, RestorationStage.SuppliesCommitted),
                new RestorationPresentation(work, RestorationStage.WorkUnderway, RestorationStage.WorkUnderway),
                new RestorationPresentation(restored, RestorationStage.Restored, RestorationStage.Occupied),
                new RestorationPresentation(occupied, RestorationStage.Occupied, RestorationStage.Occupied),
            });

            var use = new GameObject("FirstCrossingTrigger");
            use.transform.SetParent(root.transform, false);
            var useCollider = use.AddComponent<BoxCollider>();
            useCollider.isTrigger = true;
            useCollider.center = new Vector3(0f, .9f, 0f);
            useCollider.size = new Vector3(6.4f, 2.6f, 3.2f);
            var useTrigger = use.AddComponent<RestorationUseTrigger>();
            Set(useTrigger, "_project", project);
            Set(useTrigger, "_requiredStage", RestorationStage.Restored);
            Set(useTrigger, "_completedStage", RestorationStage.Occupied);
            Set(useTrigger, "_completionFlagId", "wend_bridge_in_use");

            var scheduler = root.AddComponent<DayFlagScheduler>();
            // Reverse dependency order prevents both overnight beats cascading on one rollover.
            Set(scheduler, "_whenFlags", new[] { "wend_bridge_work_started", "wend_bridge_supplies_ready" });
            Set(scheduler, "_thenFlags", new[] { "wend_bridge_restored", "wend_bridge_work_started" });
            BuildReveal(root, project);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildRestrictedFootway(Transform root, Materials materials)
        {
            // These low ropes make the safe 2.3m central lane legible and keep Wren off the
            // condemned outer boards without ever closing this story-critical route.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 1.28f;
                for (int z = -9; z <= 9; z += 3)
                    Primitive("WarningPost", PrimitiveType.Cylinder, root, new Vector3(x, .52f, z),
                        new Vector3(.07f, .52f, .07f), Quaternion.identity, materials.DarkWood);
                for (int z = -8; z <= 8; z += 4)
                    Primitive("RopeRail", PrimitiveType.Cube, root, new Vector3(x, .72f, z),
                        new Vector3(.055f, .055f, 3.85f), Quaternion.Euler(0f, 0f, side * 1.5f),
                        materials.Parchment);

                var blocker = new GameObject(side < 0 ? "CondemnedWestDeck" : "CondemnedEastDeck");
                blocker.transform.SetParent(root, false);
                blocker.transform.localPosition = new Vector3(side * 2.68f, .72f, 0f);
                var collider = blocker.AddComponent<BoxCollider>();
                collider.size = new Vector3(2.45f, 1.45f, 21.5f);
            }
            BuildBankGate(root, materials, -10.75f);
            BuildBankGate(root, materials, 10.75f);
        }

        private static void BuildBankGate(Transform root, Materials materials, float z)
        {
            for (int side = -1; side <= 1; side += 2)
                Primitive("GatePost", PrimitiveType.Cube, root, new Vector3(side * 1.35f, .72f, z),
                    new Vector3(.12f, 1.44f, .12f), Quaternion.identity, materials.DarkWood);
            Primitive("SafeFootwayMarker", PrimitiveType.Cube, root, new Vector3(0f, 1.22f, z),
                new Vector3(2.48f, .14f, .10f), Quaternion.identity, materials.Parchment);
        }

        private static void BuildSurvey(Transform root, Materials materials)
        {
            Primitive("SurveyBoard", PrimitiveType.Cube, root, new Vector3(4.25f, 1.28f, -10.9f),
                new Vector3(1.5f, 1.05f, .12f), Quaternion.Euler(0f, -12f, 0f), materials.FreshWood);
            Primitive("SurveyPage", PrimitiveType.Cube, root, new Vector3(4.23f, 1.33f, -10.82f),
                new Vector3(1.12f, .76f, .02f), Quaternion.Euler(0f, -12f, 0f), materials.Parchment);
            for (int i = 0; i < 4; i++)
                Primitive("ChalkJoint_" + i, PrimitiveType.Cube, root,
                    new Vector3(i % 2 == 0 ? -3.05f : 3.05f, .20f, -6f + i * 4f),
                    new Vector3(.38f, .035f, .07f), Quaternion.Euler(0f, i * 9f, 0f), materials.Parchment);
        }

        private static void BuildSupplies(Transform root, Materials materials)
        {
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                    Primitive("SeasonedOak", PrimitiveType.Cube, root,
                        new Vector3(-4.35f + i * .24f, .12f + row * .17f, -10.2f),
                        new Vector3(.18f, .14f, 2.7f), Quaternion.Euler(0f, -4f, 0f), materials.FreshWood);
            Prefab("IronFittingsCrate", CratePrefab, root, new Vector3(4.0f, 0f, -9.8f),
                Quaternion.Euler(0f, 16f, 0f), Vector3.one * .9f);
            for (int i = 0; i < 5; i++)
                Primitive("IronBrace", PrimitiveType.Cube, root, new Vector3(4f, .82f + i * .06f, -9.8f),
                    new Vector3(.68f, .05f, .09f), Quaternion.Euler(0f, i * 11f, i * 5f), materials.Iron);
        }

        private static void BuildWorksite(Transform root, Materials materials)
        {
            BuildSupplies(root, materials);
            for (int z = -7; z <= 7; z += 7)
            {
                Primitive("FreshOuterDeckL", PrimitiveType.Cube, root, new Vector3(-2.18f, .08f, z),
                    new Vector3(1.48f, .12f, 5.9f), Quaternion.identity, materials.FreshWood);
                Primitive("FreshOuterDeckR", PrimitiveType.Cube, root, new Vector3(2.18f, .08f, z),
                    new Vector3(1.48f, .12f, 5.9f), Quaternion.identity, materials.FreshWood);
            }
            BuildSawhorse(root, materials, new Vector3(-4.1f, 0f, 9.2f), 15f);
            BuildSawhorse(root, materials, new Vector3(4.0f, 0f, 8.7f), -12f);
            Prefab("CrewWaterBarrel", BarrelPrefab, root, new Vector3(4.1f, 0f, 10.2f),
                Quaternion.identity, Vector3.one * .82f);
        }

        private static void BuildRestoredSpan(Transform root, Materials materials)
        {
            var deckCollider = new GameObject("ReopenedCartDeckCollider");
            deckCollider.transform.SetParent(root, false);
            deckCollider.transform.localPosition = new Vector3(0f, .045f, 0f);
            var deck = deckCollider.AddComponent<BoxCollider>();
            deck.size = new Vector3(6.0f, .16f, 21.35f);
            for (int z = -8; z <= 8; z += 4)
            {
                Primitive("FreshDeckL", PrimitiveType.Cube, root, new Vector3(-2.2f, .075f, z),
                    new Vector3(1.52f, .105f, 3.85f), Quaternion.identity, materials.FreshWood);
                Primitive("FreshDeckR", PrimitiveType.Cube, root, new Vector3(2.2f, .075f, z),
                    new Vector3(1.52f, .105f, 3.85f), Quaternion.identity, materials.FreshWood);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int z = -9; z <= 9; z += 3)
                    Primitive("NewRailPost", PrimitiveType.Cube, root, new Vector3(side * 3.22f, .72f, z),
                        new Vector3(.13f, 1.4f, .13f), Quaternion.identity, materials.FreshWood);
                Primitive("NewRailCap", PrimitiveType.Cube, root, new Vector3(side * 3.22f, 1.32f, 0f),
                    new Vector3(.16f, .16f, 21.5f), Quaternion.identity, materials.FreshWood);
                BuildLamp(root, materials, new Vector3(side * 3.25f, 2.25f, -10.1f));
                BuildLamp(root, materials, new Vector3(side * 3.25f, 2.25f, 10.1f));
            }
        }

        private static void BuildOccupiedBanks(Transform root)
        {
            Prefab("WestRoadTradeCart", CartPrefab, root, new Vector3(-5.7f, 0f, 12.7f),
                Quaternion.Euler(0f, 168f, 0f), Vector3.one * .78f);
            Prefab("DeliveredBarrel", BarrelPrefab, root, new Vector3(4.7f, 0f, -12.2f),
                Quaternion.Euler(0f, 8f, 0f), Vector3.one * .85f);
            Prefab("DeliveredCrate", CratePrefab, root, new Vector3(5.5f, 0f, -12.0f),
                Quaternion.Euler(0f, -9f, 0f), Vector3.one * .78f);
        }

        private static void BuildLamp(Transform root, Materials materials, Vector3 position)
        {
            Primitive("LampBracket", PrimitiveType.Cube, root, position + new Vector3(0f, -.52f, 0f),
                new Vector3(.07f, 1.05f, .07f), Quaternion.identity, materials.Iron);
            Primitive("LampGlow", PrimitiveType.Sphere, root, position,
                Vector3.one * .22f, Quaternion.identity, materials.Lamp);
            var lightObject = new GameObject("NightLamp");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .52f, .22f);
            light.range = 5.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            var night = lightObject.AddComponent<NightLight>();
            Set(night, "_light", light);
            Set(night, "_nightIntensity", 1.9f);
            Set(night, "_flickerAmount", .04f);
        }

        private static void BuildSawhorse(Transform root, Materials materials, Vector3 position, float yaw)
        {
            var host = new GameObject("Sawhorse");
            host.transform.SetParent(root, false);
            host.transform.localPosition = position;
            host.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Primitive("Top", PrimitiveType.Cube, host.transform, new Vector3(0f, .68f, 0f),
                new Vector3(1.35f, .13f, .15f), Quaternion.identity, materials.DarkWood);
            for (int side = -1; side <= 1; side += 2)
                for (int depth = -1; depth <= 1; depth += 2)
                    Primitive("Leg", PrimitiveType.Cube, host.transform,
                        new Vector3(side * .42f, .32f, depth * .15f), new Vector3(.11f, .76f, .11f),
                        Quaternion.Euler(depth * 12f, 0f, side * 11f), materials.DarkWood);
        }

        private static void BuildReveal(GameObject root, RestorationProjectData project)
        {
            var focus = new GameObject("Bridge_RevealFocus");
            focus.transform.SetParent(root.transform, false);
            focus.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var director = root.AddComponent<RestorationRevealDirector>();
            Set(director, "_project", project);
            Set(director, "_focusTarget", focus.transform);
            Set(director, "_promotionFlagId", "wend_bridge_restored");
            Set(director, "_minimumStage", RestorationStage.Restored);
            Set(director, "_eyebrowId", "restoration.bridge.reveal.eyebrow");
            Set(director, "_titleId", "restoration.bridge.reveal.title");
            Set(director, "_bodyId", "restoration.bridge.reveal.body");
            Set(director, "_cameraDistance", 18f);
            Set(director, "_cameraHeight", 4.6f);
            Set(director, "_cameraFov", 50f);
            Set(director, "_frameDirection", new Vector3(.92f, 0f, -.38f));
        }

        private static GameObject StageRoot(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            var collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            return go;
        }

        private static GameObject Prefab(string name, string assetPath, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var asset = Required<GameObject>(assetPath);
            var instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate " + assetPath);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException("Missing bridge restoration asset", path);
            return asset;
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
            if (target is UnityEngine.Object unityObject) EditorUtility.SetDirty(unityObject);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid folder " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
