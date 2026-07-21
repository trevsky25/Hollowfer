#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Hollowfen.GameTime;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring pass for the Living Restoration foundation and cottage project.</summary>
    public static class RestorationContentImporter
    {
        private const string DataRoot = "Assets/_Hollowfen/Data/Restoration";
        private const string ProjectPath = DataRoot + "/Restoration_Cottages.asset";
        private const string BridgeProjectPath = DataRoot + "/Restoration_WendBridge.asset";
        private const string DatabasePath = "Assets/_Hollowfen/Resources/RestorationProjectDatabase.asset";
        private const string ArtRoot = "Assets/_Hollowfen/Art/Restoration";
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string SceneRootName = "_LivingRestoration_Cottages";
        private const string EnvironmentPrefabRoot =
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs";
        private const string BucketPrefab = EnvironmentPrefabRoot + "/Storage/Bucket.prefab";
        private const string FirewoodPrefab = EnvironmentPrefabRoot +
            "/Storage/Firewood Basket With Wood Variant.prefab";
        private const string CratePrefab = EnvironmentPrefabRoot + "/Storage/Crate1.prefab";
        private const string BenchPrefab = EnvironmentPrefabRoot + "/Furniture/Bench Variant 1.prefab";
        private const string BroomPrefab = EnvironmentPrefabRoot + "/Farm Equipment/Broom.prefab";
        private const string PotPrefab = EnvironmentPrefabRoot +
            "/Jugs, Jars, Cutlery, Etc/PotLidded.prefab";
        private const string LadderPrefab = EnvironmentPrefabRoot + "/Props/Ladder.prefab";

        [MenuItem("Hollowfen/Restoration/Build Cottage Foundation")]
        private static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            EnsureFolder(DataRoot);
            EnsureFolder(ArtRoot);
            var project = UpsertProject();
            RestorationCatalogueImporter.UpsertDatabase();
            var materials = UpsertMaterials();
            BuildScene(project, materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "LIVING RESTORATION — BUILT: save-backed cottage project, two staged worksites, occupied dressing, chimney smoke, and village board";
        }

        private static RestorationProjectData UpsertProject()
        {
            var project = AssetDatabase.LoadAssetAtPath<RestorationProjectData>(ProjectPath);
            if (project == null)
            {
                project = ScriptableObject.CreateInstance<RestorationProjectData>();
                AssetDatabase.CreateAsset(project, ProjectPath);
            }

            Set(project, "_id", "cottages");
            Set(project, "_titleId", "restoration.cottages.title");
            Set(project, "_summaryId", "restoration.cottages.summary");
            Set(project, "_locationId", "restoration.cottages.location");
            Set(project, "_promptTargetId", "restoration.site.unknown");
            Set(project, "_benefitId", "restoration.cottages.benefit");
            Set(project, "_activeQuestId", "cottagesReopen");
            Set(project, "_completedQuestId", "cottagesReopen");
            Set(project, "_stageRules", new[]
            {
                new RestorationStageRule(RestorationCondition.ActiveQuest, "cottagesReopen", RestorationStage.Surveyed),
                new RestorationStageRule(RestorationCondition.FlagSet, "shutters_funded", RestorationStage.SuppliesCommitted),
                new RestorationStageRule(RestorationCondition.FlagSet, "cottages_reopened_1", RestorationStage.WorkUnderway),
                new RestorationStageRule(RestorationCondition.FlagSet, "cottages_reopened_2", RestorationStage.Restored),
                new RestorationStageRule(RestorationCondition.CompletedQuest, "cottagesReopen", RestorationStage.Occupied),
            });
            Set(project, "_stageCopy", new[]
            {
                new RestorationStageCopy(RestorationStage.Surveyed,
                    "restoration.cottages.stage.surveyed.title", "restoration.cottages.stage.surveyed.body"),
                new RestorationStageCopy(RestorationStage.SuppliesCommitted,
                    "restoration.cottages.stage.supplies.title", "restoration.cottages.stage.supplies.body"),
                new RestorationStageCopy(RestorationStage.WorkUnderway,
                    "restoration.cottages.stage.work.title", "restoration.cottages.stage.work.body"),
                new RestorationStageCopy(RestorationStage.Restored,
                    "restoration.cottages.stage.restored.title", "restoration.cottages.stage.restored.body"),
                new RestorationStageCopy(RestorationStage.Occupied,
                    "restoration.cottages.stage.occupied.title", "restoration.cottages.stage.occupied.body"),
            });
            Set(project, "_milestones", new[]
            {
                new RestorationMilestone("restoration.cottages.milestone.tax",
                    "restoration.cottages.milestone.tax.detail", RestorationCondition.CompletedQuest, "firstTax"),
                new RestorationMilestone("restoration.cottages.milestone.shutters",
                    "restoration.cottages.milestone.shutters.detail", RestorationCondition.FlagSet, "shutters_funded"),
                new RestorationMilestone("restoration.cottages.milestone.wenmar",
                    "restoration.cottages.milestone.wenmar.detail", RestorationCondition.FlagSet, "cottages_reopened_1"),
                new RestorationMilestone("restoration.cottages.milestone.north",
                    "restoration.cottages.milestone.north.detail", RestorationCondition.FlagSet, "cottages_reopened_2"),
                new RestorationMilestone("restoration.cottages.milestone.record",
                    "restoration.cottages.milestone.record.detail", RestorationCondition.CompletedQuest, "cottagesReopen"),
            });
            EditorUtility.SetDirty(project);
            return project;
        }

        private sealed class MaterialSet
        {
            public Material DarkWood;
            public Material FreshWood;
            public Material Iron;
            public Material Parchment;
            public Material Smoke;
        }

        private static MaterialSet UpsertMaterials()
        {
            return new MaterialSet
            {
                DarkWood = UpsertMaterial("Restoration_DarkWood", new Color(0.18f, 0.105f, 0.055f), false),
                FreshWood = UpsertMaterial("Restoration_FreshWood", new Color(0.48f, 0.30f, 0.13f), false),
                Iron = UpsertMaterial("Restoration_Iron", new Color(0.13f, 0.14f, 0.12f), false, 0.65f),
                Parchment = UpsertMaterial("Restoration_Parchment", new Color(0.80f, 0.70f, 0.47f), false),
                Smoke = UpsertParticleMaterial(),
            };
        }

        private static Material UpsertMaterial(string name, Color color, bool emission, float metallic = 0f)
        {
            string path = ArtRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", metallic > 0f ? 0.42f : 0.18f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2.2f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material UpsertParticleMaterial()
        {
            string path = ArtRoot + "/Restoration_Smoke.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                             Shader.Find("Particles/Standard Unlit");
                material = new Material(shader) { name = "Restoration_Smoke" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = new Color(0.38f, 0.39f, 0.36f, 0.38f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildScene(RestorationProjectData project, MaterialSet materials)
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();

            var existing = GameObject.Find(SceneRootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var root = new GameObject(SceneRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            BuildWenmarSite(root.transform, project, materials);
            var northLane = BuildNorthLaneSite(root.transform, project, materials);
            BuildRevealDirector(root, project, northLane.transform);
            BuildVillageBoard(root.transform, project, materials);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildWenmarSite(Transform parent, RestorationProjectData project, MaterialSet materials)
        {
            var host = SiteHost("RestorationSite_Wenmar", parent, new Vector3(268f, 37.12f, 163f),
                project, "restoration.site.wenmar");
            var survey = StageRoot("Surveyed", host.transform);
            AddSurveyDressing(survey.transform, materials, new Vector3(-1.45f, -0.12f, -1.35f));

            var homeOpen = StageRoot("FirstHomeOpen", host.transform);
            AddRestoredFront(homeOpen.transform, materials, new Vector3(0f, 1.46f, -0.08f), 6.5f);
            AddMoveInSupplies(homeOpen.transform, new Vector3(-1.2f, -0.04f, -1.05f), -1f);

            var occupied = StageRoot("Occupied", host.transform);
            AddRestoredFront(occupied.transform, materials, new Vector3(0f, 1.46f, -0.08f), 6.5f);
            AddMoveInSupplies(occupied.transform, new Vector3(-1.2f, -0.04f, -1.05f), -1f);
            AddOccupiedDressing(occupied.transform, new Vector3(2.15f, -0.02f, -1.20f), 12f);

            Set(host.GetComponent<RestorationSite>(), "_presentations", new[]
            {
                new RestorationPresentation(survey, RestorationStage.Surveyed, RestorationStage.SuppliesCommitted),
                new RestorationPresentation(homeOpen, RestorationStage.WorkUnderway, RestorationStage.Restored),
                new RestorationPresentation(occupied, RestorationStage.Occupied, RestorationStage.Occupied),
            });
        }

        private static GameObject BuildNorthLaneSite(Transform parent, RestorationProjectData project, MaterialSet materials)
        {
            var host = SiteHost("RestorationSite_NorthLane", parent, new Vector3(224f, 35.18f, 305f),
                project, "restoration.site.north_lane");
            var survey = StageRoot("Surveyed", host.transform);
            AddSurveyDressing(survey.transform, materials, new Vector3(1.55f, -0.10f, -1.25f));

            var work = StageRoot("WorkUnderway", host.transform);
            AddWorkDressing(work.transform, materials);

            var restored = StageRoot("Restored", host.transform);
            AddRestoredFront(restored.transform, materials, new Vector3(0f, 1.46f, -0.08f), 7.2f);
            AddMoveInSupplies(restored.transform, new Vector3(1.15f, -0.03f, -1.0f), 1f);

            var occupied = StageRoot("Occupied", host.transform);
            AddRestoredFront(occupied.transform, materials, new Vector3(0f, 1.46f, -0.08f), 7.2f);
            AddMoveInSupplies(occupied.transform, new Vector3(1.15f, -0.03f, -1.0f), 1f);
            AddOccupiedDressing(occupied.transform, new Vector3(-2.10f, -0.02f, -1.20f), -10f);

            Set(host.GetComponent<RestorationSite>(), "_presentations", new[]
            {
                new RestorationPresentation(survey, RestorationStage.Surveyed, RestorationStage.SuppliesCommitted),
                new RestorationPresentation(work, RestorationStage.WorkUnderway, RestorationStage.WorkUnderway),
                new RestorationPresentation(restored, RestorationStage.Restored, RestorationStage.Restored),
                new RestorationPresentation(occupied, RestorationStage.Occupied, RestorationStage.Occupied),
            });
            return host;
        }

        private static GameObject SiteHost(string name, Transform parent, Vector3 position,
            RestorationProjectData project, string promptTargetId)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.position = position;
            int layer = LayerMask.NameToLayer("Foraging");
            if (layer >= 0) host.layer = layer;
            var trigger = host.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2.45f;
            trigger.center = new Vector3(0f, 0.9f, -0.9f);
            var site = host.AddComponent<RestorationSite>();
            Set(site, "_project", project);
            Set(site, "_promptTargetId", promptTargetId);
            Set(site, "_interactableFromStage", RestorationStage.Surveyed);
            return host;
        }

        private static GameObject StageRoot(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static void AddSurveyDressing(Transform root, MaterialSet materials, Vector3 origin)
        {
            AddTimberStack(root, materials.FreshWood, origin);
            AddSawhorse(root, materials, origin + new Vector3(1.15f, 0f, 0.08f), 18f);
            Primitive("MeasureStake", PrimitiveType.Cube, root, origin + new Vector3(-0.75f, 0.55f, 0.2f),
                new Vector3(0.07f, 1.15f, 0.07f), Quaternion.identity, materials.DarkWood);
            Primitive("ChalkMark", PrimitiveType.Cube, root, origin + new Vector3(-0.75f, 1.05f, 0.17f),
                new Vector3(0.34f, 0.05f, 0.05f), Quaternion.Euler(0f, 0f, 8f), materials.Parchment);
        }

        private static void AddWorkDressing(Transform root, MaterialSet materials)
        {
            AddTimberStack(root, materials.FreshWood, new Vector3(1.35f, -0.1f, -1.25f));
            AddSawhorse(root, materials, new Vector3(-1.3f, -0.08f, -1.15f), -12f);
            EnvironmentPrefab("RepairLadder", LadderPrefab, root, new Vector3(-1.05f, 0f, -0.15f),
                Quaternion.Euler(0f, 90f, -7f), Vector3.one * 0.53f);
            EnvironmentPrefab("ToolCrate", CratePrefab, root, new Vector3(0.75f, 0f, -1.45f),
                Quaternion.Euler(0f, 8f, 0f), Vector3.one);
            AddWorkDust(root, materials.Smoke, new Vector3(-1.3f, .72f, -1.15f));
        }

        private static void AddTimberStack(Transform root, Material material, Vector3 origin)
        {
            for (int i = 0; i < 5; i++)
            {
                int row = i / 3;
                float x = (i % 3 - 1) * 0.24f;
                Primitive("FreshTimber_" + i, PrimitiveType.Cube, root,
                    origin + new Vector3(x, 0.10f + row * 0.16f, 0f),
                    new Vector3(0.18f, 0.14f, 1.55f), Quaternion.Euler(0f, 7f, 0f), material);
            }
        }

        private static void AddSawhorse(Transform root, MaterialSet materials, Vector3 origin, float yaw)
        {
            var group = new GameObject("Sawhorse");
            group.transform.SetParent(root, false);
            group.transform.localPosition = origin;
            group.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Primitive("Top", PrimitiveType.Cube, group.transform, new Vector3(0f, 0.72f, 0f),
                new Vector3(1.4f, 0.14f, 0.16f), Quaternion.identity, materials.DarkWood);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("LegA", PrimitiveType.Cube, group.transform, new Vector3(side * 0.42f, 0.34f, -0.16f),
                    new Vector3(0.12f, 0.82f, 0.12f), Quaternion.Euler(15f, 0f, side * 12f), materials.DarkWood);
                Primitive("LegB", PrimitiveType.Cube, group.transform, new Vector3(side * 0.42f, 0.34f, 0.16f),
                    new Vector3(0.12f, 0.82f, 0.12f), Quaternion.Euler(-15f, 0f, side * 12f), materials.DarkWood);
            }
            var saw = new GameObject("HandSaw");
            saw.transform.SetParent(group.transform, false);
            saw.transform.localPosition = new Vector3(.12f, .82f, 0f);
            saw.transform.localRotation = Quaternion.Euler(0f, 8f, -7f);
            Primitive("Blade", PrimitiveType.Cube, saw.transform, Vector3.zero,
                new Vector3(.64f, .025f, .13f), Quaternion.identity, materials.Iron);
            Primitive("Handle", PrimitiveType.Cube, saw.transform, new Vector3(.38f, .035f, 0f),
                new Vector3(.18f, .08f, .20f), Quaternion.identity, materials.DarkWood);
        }

        private static void AddWorkDust(Transform root, Material material, Vector3 position)
        {
            var go = new GameObject("SawingDust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .24f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .065f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.62f, .46f, .25f, .45f), new Color(.82f, .69f, .44f, .24f));
            main.gravityModifier = .08f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 3.2f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(.72f, .04f, .20f);
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = .035f;
            noise.frequency = .35f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        private static void AddRestoredFront(Transform root, MaterialSet materials, Vector3 window, float smokeHeight)
        {
            AddNightLight(root, window + new Vector3(0f, 0f, -1.1f));
            AddSmoke(root, materials.Smoke, new Vector3(0f, smokeHeight, 0.35f));
        }

        private static void AddNightLight(Transform root, Vector3 position)
        {
            var go = new GameObject("WindowGlow");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.24f);
            light.range = 5.5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            var night = go.AddComponent<NightLight>();
            Set(night, "_light", light);
            Set(night, "_nightIntensity", 2.1f);
            Set(night, "_flickerAmount", 0.045f);
        }

        private static void AddSmoke(Transform root, Material material, Vector3 position)
        {
            var go = new GameObject("ChimneySmoke");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.6f, 6.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.42f, 0.72f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.42f, 0.92f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.31f, 0.32f, 0.29f, 0.34f), new Color(0.47f, 0.46f, 0.40f, 0.18f));
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 5.5f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.18f;
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.16f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.12f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.72f, 0.72f, 0.68f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.42f, 0.18f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        private static void AddMoveInSupplies(Transform root, Vector3 origin, float direction)
        {
            float sign = Mathf.Sign(direction);
            EnvironmentPrefab("WaterBucket", BucketPrefab, root, origin,
                Quaternion.Euler(0f, 16f * sign, 0f), Vector3.one * 0.72f);
            EnvironmentPrefab("WinterFirewood", FirewoodPrefab, root,
                origin + new Vector3(0.72f * sign, 0f, 0.08f),
                Quaternion.Euler(0f, -12f * sign, 0f), Vector3.one * 0.90f);
            EnvironmentPrefab("PantryCrate", CratePrefab, root,
                origin + new Vector3(1.35f * sign, 0f, 0.20f),
                Quaternion.Euler(0f, 7f * sign, 0f), Vector3.one * 0.88f);
        }

        private static void AddOccupiedDressing(Transform root, Vector3 origin, float yaw)
        {
            EnvironmentPrefab("DoorstepBench", BenchPrefab, root, origin,
                Quaternion.Euler(0f, yaw, 0f), Vector3.one * 0.88f);
            EnvironmentPrefab("HouseBroom", BroomPrefab, root,
                origin + new Vector3(yaw < 0f ? -1.05f : 1.05f, 0.02f, 0.12f),
                Quaternion.Euler(0f, yaw, yaw < 0f ? 9f : -9f), Vector3.one * 0.82f);
            EnvironmentPrefab("KitchenPot", PotPrefab, root,
                origin + new Vector3(yaw < 0f ? 0.82f : -0.82f, 0.03f, -0.06f),
                Quaternion.Euler(0f, -yaw, 0f), Vector3.one * 0.92f);
        }

        private static GameObject EnvironmentPrefab(string name, string assetPath, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null) throw new FileNotFoundException("Missing restoration prop", assetPath);
            var instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate " + assetPath);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            return instance;
        }

        private static void BuildVillageBoard(Transform parent, RestorationProjectData project, MaterialSet materials)
        {
            var host = new GameObject("RestorationBoard_VillageSquare");
            host.transform.SetParent(parent, false);
            host.transform.position = new Vector3(291.2f, 37.02f, 159.15f);
            host.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
            int layer = LayerMask.NameToLayer("Foraging");
            if (layer >= 0) host.layer = layer;

            var visual = new GameObject("BoardVisual");
            visual.transform.SetParent(host.transform, false);
            Primitive("PostL", PrimitiveType.Cube, visual.transform, new Vector3(-1.18f, 1.25f, 0f),
                new Vector3(0.18f, 2.5f, 0.18f), Quaternion.identity, materials.DarkWood);
            Primitive("PostR", PrimitiveType.Cube, visual.transform, new Vector3(1.18f, 1.25f, 0f),
                new Vector3(0.18f, 2.5f, 0.18f), Quaternion.identity, materials.DarkWood);
            Primitive("LedgerBoard", PrimitiveType.Cube, visual.transform, new Vector3(0f, 1.72f, 0f),
                new Vector3(2.75f, 1.45f, 0.16f), Quaternion.identity, materials.FreshWood);
            Primitive("LedgerPage", PrimitiveType.Cube, visual.transform, new Vector3(-0.46f, 1.78f, -0.095f),
                new Vector3(0.98f, 1.04f, 0.022f), Quaternion.Euler(0f, 0f, -2f), materials.Parchment);
            Primitive("InkLineA", PrimitiveType.Cube, visual.transform, new Vector3(-0.46f, 1.92f, -0.112f),
                new Vector3(0.72f, 0.025f, 0.015f), Quaternion.Euler(0f, 0f, -2f), materials.Iron);
            Primitive("InkLineB", PrimitiveType.Cube, visual.transform, new Vector3(-0.46f, 1.72f, -0.112f),
                new Vector3(0.62f, 0.022f, 0.015f), Quaternion.Euler(0f, 0f, -2f), materials.Iron);
            Primitive("CottageSketch", PrimitiveType.Cube, visual.transform, new Vector3(0.67f, 1.80f, -0.095f),
                new Vector3(0.78f, 0.72f, 0.022f), Quaternion.Euler(0f, 0f, 2f), materials.Parchment);
            Primitive("RoofMark", PrimitiveType.Cube, visual.transform, new Vector3(0.67f, 1.98f, -0.114f),
                new Vector3(0.52f, 0.035f, 0.014f), Quaternion.Euler(0f, 0f, 18f), materials.Iron);

            var trigger = host.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.35f, -0.55f);
            trigger.size = new Vector3(3.3f, 2.8f, 1.5f);
            var board = host.AddComponent<RestorationBoard>();
            Set(board, "_unlockProject", project);
            Set(board, "_visualRoot", visual);
            Set(board, "_interactionCollider", trigger);
            Set(board, "_unlockStage", RestorationStage.Occupied);
        }

        private static void BuildRevealDirector(GameObject root, RestorationProjectData project,
            Transform northLane)
        {
            var focus = new GameObject("NorthLane_RevealFocus");
            focus.transform.SetParent(northLane, false);
            // The supplies sit beside the lane, but the reveal belongs to the cottage itself.
            // Frame its south-facing front from the clear approach instead of inheriting Wren's
            // arbitrary pre-rest camera direction (which can put a mature tree in the hero shot).
            focus.transform.localPosition = new Vector3(-10.3f, 4.42f, .5f);
            var director = root.AddComponent<RestorationRevealDirector>();
            Set(director, "_project", project);
            Set(director, "_focusTarget", focus.transform);
            Set(director, "_promotionFlagId", "cottages_reopened_2");
            Set(director, "_minimumStage", RestorationStage.Restored);
            Set(director, "_eyebrowId", "restoration.reveal.eyebrow");
            Set(director, "_titleId", "restoration.reveal.title");
            Set(director, "_bodyId", "restoration.reveal.body");
            Set(director, "_cameraDistance", 7.6f);
            Set(director, "_cameraHeight", .8f);
            Set(director, "_cameraFov", 46f);
            Set(director, "_frameDirection", new Vector3(-.44f, 0f, -.90f));
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
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return go;
        }

        private static void Set(object target, string fieldName, object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
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
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid asset folder " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
