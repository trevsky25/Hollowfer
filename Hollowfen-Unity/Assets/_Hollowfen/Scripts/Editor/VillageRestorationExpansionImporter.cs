#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Hollowfen.Cultivation;
using Hollowfen.GameTime;
using Hollowfen.Restoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>Idempotent authoring for the five post-bridge Living Restoration projects.</summary>
    public static class VillageRestorationExpansionImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string DataRoot = "Assets/_Hollowfen/Data/Restoration";
        private const string ArtRoot = "Assets/_Hollowfen/Art/Restoration";
        private const string PrefabRoot =
            "Assets/Magic Pig Games (Infinity PBR)/Medieval Environment Pack/_Prefabs";
        private const string CratePrefab = PrefabRoot + "/Storage/Crate1.prefab";
        private const string BarrelPrefab = PrefabRoot + "/Storage/Barrel.prefab";
        private const string BucketPrefab = PrefabRoot + "/Storage/Bucket.prefab";
        private const string FirewoodPrefab = PrefabRoot + "/Storage/Firewood Basket With Wood Variant.prefab";
        private const string BenchPrefab = PrefabRoot + "/Furniture/Bench Variant 1.prefab";
        private const string TablePrefab = PrefabRoot + "/Furniture/Table A Variant 2.prefab";
        private const string ShelfPrefab = PrefabRoot + "/Furniture/Shelf.prefab";
        private const string BroomPrefab = PrefabRoot + "/Farm Equipment/Broom.prefab";
        private const string AlchemyShowcaseScene = "Assets/Alchemy/Scenes/_Scene.unity";

        private sealed class ProjectSpec
        {
            public string Id;
            public string Key;
            public string AssetName;
            public string UnlockQuest;
            public string Prefix;
            public Vector3 Center;
            public Vector3 SiteOffset;
            public Vector3 UseOffset;
            public Vector3 FocusOffset;
            public Vector3 FrameDirection;
            public float CameraDistance;
            public float CameraHeight;
            public int FirstCost;
            public int SecondCost;
            public int Hope;
            public int Knowledge;
            public string[] Consequences;
        }

        private sealed class Materials
        {
            public Material DarkWood;
            public Material FreshWood;
            public Material Iron;
            public Material Parchment;
            public Material Soil;
            public Material Herb;
            public Material Ember;
            public Material Smoke;
        }

        private static readonly ProjectSpec[] Specs =
        {
            new ProjectSpec
            {
                Id = "jorens_forge", Key = "forge", AssetName = "Restoration_JorensForge.asset",
                UnlockQuest = "forgeKnife", Prefix = "forge",
                Center = new Vector3(198f, 32.65f, 197.9f),
                SiteOffset = new Vector3(0f, .9f, -3.2f), UseOffset = new Vector3(0f, 1f, -2.35f),
                // The finished hearth sits against the smithy's east face. Frame it straight-on;
                // the former south-west angle looked through the awning and hid the restoration.
                FocusOffset = new Vector3(3.25f, 1.05f, -.35f), FrameDirection = Vector3.right,
                CameraDistance = 7.2f, CameraHeight = 1.55f,
                FirstCost = 16, SecondCost = 14, Hope = 3, Knowledge = 0,
                Consequences = new[] { "forge_service_unlocked" },
            },
            new ProjectSpec
            {
                Id = "chapel_garden", Key = "garden", AssetName = "Restoration_ChapelGarden.asset",
                UnlockQuest = "caldenReconcile", Prefix = "chapel_garden",
                Center = new Vector3(214f, 38f, 314f),
                SiteOffset = new Vector3(0f, 1f, -2.4f), UseOffset = new Vector3(0f, 1f, 2.1f),
                FocusOffset = new Vector3(0f, .85f, 4.5f), FrameDirection = Vector3.forward,
                CameraDistance = 6f, CameraHeight = 1.25f,
                FirstCost = 12, SecondCost = 10, Hope = 4, Knowledge = 3,
                Consequences = new[] { "chapel_cultivation_restored" },
            },
            new ProjectSpec
            {
                Id = "crooked_pintle", Key = "pintle", AssetName = "Restoration_CrookedPintle.asset",
                UnlockQuest = "festivalHosted", Prefix = "crooked_pintle",
                Center = new Vector3(275.2f, 35f, 87.6f),
                SiteOffset = new Vector3(4.45f, 1f, 3.2f), UseOffset = new Vector3(3.85f, 1f, 3.15f),
                // Public-facing dressing lives on the east wall beside the work crew, not inside
                // the tavern shell. This makes the overnight change readable from the road.
                FocusOffset = new Vector3(7.2f, 1.35f, 1.25f), FrameDirection = Vector3.right,
                CameraDistance = 8.25f, CameraHeight = 1.35f,
                FirstCost = 18, SecondCost = 20, Hope = 6, Knowledge = 0,
                Consequences = new[] { "pintle_orders_restored" },
            },
            new ProjectSpec
            {
                Id = "witch_cottage", Key = "witch", AssetName = "Restoration_WitchCottage.asset",
                UnlockQuest = "wendlightFound", Prefix = "witch_cottage",
                Center = new Vector3(262f, 35f, 455f),
                SiteOffset = new Vector3(0f, 1f, -3.4f), UseOffset = new Vector3(0f, 1f, -2.25f),
                FocusOffset = new Vector3(.25f, 1.45f, -2.0f), FrameDirection = new Vector3(.71f, 0f, -.71f),
                CameraDistance = 8.5f, CameraHeight = 1.65f,
                FirstCost = 22, SecondCost = 16, Hope = 4, Knowledge = 8,
                Consequences = new[] { "witch_cottage_restored", "old_knowledge_restored" },
            },
            new ProjectSpec
            {
                Id = "tobin_workshop", Key = "mill", AssetName = "Restoration_TobinWorkshop.asset",
                UnlockQuest = "almyTeach", Prefix = "tobin_workshop",
                Center = new Vector3(232.9f, 32.9f, 317.8f),
                // The purchased laboratory occupies the wooded terrace north-west of the mill.
                // Work, first use, and the overnight reveal now point at that actual building.
                SiteOffset = new Vector3(-10.35f, 3.9f, 12.9f),
                UseOffset = new Vector3(-10.35f, 3.9f, 12.9f),
                FocusOffset = new Vector3(-17.9f, 6.8f, 22.2f),
                FrameDirection = new Vector3(.63f, 0f, -.78f),
                CameraDistance = 28f, CameraHeight = 5f,
                FirstCost = 20, SecondCost = 18, Hope = 6, Knowledge = 4,
                Consequences = new[] { "mill_home_restored" },
            },
        };

        [MenuItem("Hollowfen/Restoration/Build Full Village Expansion")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        public static string BuildAll()
        {
            BridgeRestorationImporter.BuildAll();
            EnsureFolder(DataRoot);
            EnsureFolder(ArtRoot);
            var projects = Specs.Select(UpsertProject).ToArray();
            RestorationCatalogueImporter.UpsertDatabase();
            var materials = LoadMaterials();
            for (int i = 0; i < Specs.Length; i++) BuildProjectScene(Specs[i], projects[i], materials);
            NPCScheduleImporter.ApplyAll();
            // If the purchased showcase is present, replace the fallback mill dressing with its
            // complete authored laboratory building after every restoration rebuild.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AlchemyShowcaseScene) != null)
                ApothecaryPreparationImporter.BuildAll();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "VILLAGE RESTORATION EXPANSION — BUILT: forge, chapel garden, Crooked Pintle, Witch's Cottage, and Tobin workshop";
        }

        private static RestorationProjectData UpsertProject(ProjectSpec spec)
        {
            string path = DataRoot + "/" + spec.AssetName;
            var project = AssetDatabase.LoadAssetAtPath<RestorationProjectData>(path);
            if (project == null)
            {
                project = ScriptableObject.CreateInstance<RestorationProjectData>();
                AssetDatabase.CreateAsset(project, path);
            }

            string root = "restoration." + spec.Key;
            Set(project, "_id", spec.Id);
            Set(project, "_titleId", root + ".title");
            Set(project, "_summaryId", root + ".summary");
            Set(project, "_locationId", root + ".location");
            Set(project, "_promptTargetId", root + ".site");
            Set(project, "_benefitId", root + ".benefit");
            Set(project, "_villageHopeReward", spec.Hope);
            Set(project, "_knowledgeReward", spec.Knowledge);
            Set(project, "_activeQuestId", "");
            Set(project, "_completedQuestId", "");
            Set(project, "_stageRules", new[]
            {
                new RestorationStageRule(RestorationCondition.CompletedQuest, spec.UnlockQuest, RestorationStage.Surveyed),
                new RestorationStageRule(RestorationCondition.FlagSet, spec.Prefix + "_supplies_ready", RestorationStage.SuppliesCommitted),
                new RestorationStageRule(RestorationCondition.FlagSet, spec.Prefix + "_work_started", RestorationStage.WorkUnderway),
                new RestorationStageRule(RestorationCondition.FlagSet, spec.Prefix + "_restored", RestorationStage.Restored),
                new RestorationStageRule(RestorationCondition.FlagSet, spec.Prefix + "_in_use", RestorationStage.Occupied),
            });
            Set(project, "_stageCopy", new[]
            {
                StageCopy(root, "surveyed", RestorationStage.Surveyed),
                StageCopy(root, "supplies", RestorationStage.SuppliesCommitted),
                StageCopy(root, "work", RestorationStage.WorkUnderway),
                StageCopy(root, "restored", RestorationStage.Restored),
                StageCopy(root, "occupied", RestorationStage.Occupied),
            });
            Set(project, "_contributions", new[]
            {
                new RestorationContribution(root + ".contribution.first",
                    root + ".contribution.first.detail", spec.Prefix + "_first_funded", spec.FirstCost),
                new RestorationContribution(root + ".contribution.second",
                    root + ".contribution.second.detail", spec.Prefix + "_second_funded", spec.SecondCost),
            });
            Set(project, "_contributionsCompleteFlagId", spec.Prefix + "_supplies_ready");
            Set(project, "_milestones", new[]
            {
                new RestorationMilestone(root + ".milestone.unlock", root + ".milestone.unlock.detail",
                    RestorationCondition.CompletedQuest, spec.UnlockQuest),
                new RestorationMilestone(root + ".milestone.first", root + ".milestone.first.detail",
                    RestorationCondition.FlagSet, spec.Prefix + "_first_funded"),
                new RestorationMilestone(root + ".milestone.second", root + ".milestone.second.detail",
                    RestorationCondition.FlagSet, spec.Prefix + "_second_funded"),
                new RestorationMilestone(root + ".milestone.work", root + ".milestone.work.detail",
                    RestorationCondition.FlagSet, spec.Prefix + "_work_started"),
                new RestorationMilestone(root + ".milestone.use", root + ".milestone.use.detail",
                    RestorationCondition.FlagSet, spec.Prefix + "_in_use"),
            });
            EditorUtility.SetDirty(project);
            return project;
        }

        private static RestorationStageCopy StageCopy(string root, string suffix, RestorationStage stage) =>
            new RestorationStageCopy(stage, root + ".stage." + suffix + ".title",
                root + ".stage." + suffix + ".body", root + ".stage." + suffix + ".short");

        private static Materials LoadMaterials()
        {
            return new Materials
            {
                DarkWood = Required<Material>(ArtRoot + "/Restoration_DarkWood.mat"),
                FreshWood = Required<Material>(ArtRoot + "/Restoration_FreshWood.mat"),
                Iron = Required<Material>(ArtRoot + "/Restoration_Iron.mat"),
                Parchment = Required<Material>(ArtRoot + "/Restoration_Parchment.mat"),
                Soil = UpsertMaterial("Restoration_Soil", new Color(.18f, .10f, .055f), .02f, .12f),
                Herb = UpsertMaterial("Restoration_Herb", new Color(.19f, .34f, .16f), .01f, .14f),
                Ember = UpsertMaterial("Restoration_Ember", new Color(.72f, .19f, .045f), 0f, .08f,
                    new Color(1f, .17f, .025f) * 3.2f),
                Smoke = Required<Material>(ArtRoot + "/Restoration_Smoke.mat"),
            };
        }

        private static Material UpsertMaterial(string name, Color color, float metallic,
            float smoothness, Color? emission = null)
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
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildProjectScene(ProjectSpec spec, RestorationProjectData project,
            Materials materials)
        {
            if (EditorSceneManager.GetActiveScene().path != GameplayScene)
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();
            string rootName = "_LivingRestoration_" + spec.Id;
            var existing = GameObject.Find(rootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var root = new GameObject(rootName);
            root.transform.position = spec.Center;
            SceneManager.MoveGameObjectToScene(root, scene);
            int forageLayer = LayerMask.NameToLayer("Foraging");
            if (forageLayer >= 0) root.layer = forageLayer;

            var siteCollider = root.AddComponent<SphereCollider>();
            siteCollider.isTrigger = true;
            siteCollider.radius = 2.15f;
            siteCollider.center = spec.SiteOffset;
            var site = root.AddComponent<RestorationSite>();
            Set(site, "_project", project);
            Set(site, "_promptTargetId", "restoration." + spec.Key + ".site");
            Set(site, "_interactableFromStage", RestorationStage.Surveyed);

            var surveyed = StageRoot("Surveyed", root.transform);
            BuildSurveyed(surveyed.transform, spec.SiteOffset, materials);
            var supplies = StageRoot("SuppliesCommitted", root.transform);
            BuildSupplies(supplies.transform, spec.SiteOffset, materials);
            var work = StageRoot("WorkUnderway", root.transform);
            BuildWorksite(work.transform, spec.SiteOffset, materials);
            var restored = StageRoot("Restored", root.transform);
            BuildRestored(spec, restored.transform, materials);
            var occupied = StageRoot("Occupied", root.transform);
            BuildOccupied(spec, occupied.transform, materials, forageLayer);

            Set(site, "_presentations", new[]
            {
                new RestorationPresentation(surveyed, RestorationStage.Surveyed, RestorationStage.Surveyed),
                new RestorationPresentation(supplies, RestorationStage.SuppliesCommitted, RestorationStage.SuppliesCommitted),
                new RestorationPresentation(work, RestorationStage.WorkUnderway, RestorationStage.WorkUnderway),
                new RestorationPresentation(restored, RestorationStage.Restored, RestorationStage.Occupied),
                new RestorationPresentation(occupied, RestorationStage.Occupied, RestorationStage.Occupied),
            });

            var use = new GameObject("FirstUseTrigger");
            use.transform.SetParent(root.transform, false);
            var useCollider = use.AddComponent<BoxCollider>();
            useCollider.isTrigger = true;
            useCollider.center = spec.UseOffset;
            useCollider.size = new Vector3(3.2f, 2.5f, 2.5f);
            var useTrigger = use.AddComponent<RestorationUseTrigger>();
            Set(useTrigger, "_project", project);
            Set(useTrigger, "_requiredStage", RestorationStage.Restored);
            Set(useTrigger, "_completedStage", RestorationStage.Occupied);
            Set(useTrigger, "_completionFlagId", spec.Prefix + "_in_use");
            Set(useTrigger, "_consequenceFlagIds", spec.Consequences ?? Array.Empty<string>());

            var scheduler = root.AddComponent<DayFlagScheduler>();
            Set(scheduler, "_whenFlags", new[]
            {
                spec.Prefix + "_work_started", spec.Prefix + "_supplies_ready",
            });
            Set(scheduler, "_thenFlags", new[]
            {
                spec.Prefix + "_restored", spec.Prefix + "_work_started",
            });
            BuildReveal(root, spec, project);

            surveyed.SetActive(false);
            supplies.SetActive(false);
            work.SetActive(false);
            restored.SetActive(false);
            occupied.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void BuildSurveyed(Transform root, Vector3 origin, Materials materials)
        {
            var board = new GameObject("PellSurveyBoard");
            board.transform.SetParent(root, false);
            board.transform.localPosition = origin + new Vector3(-1.1f, -.85f, -.25f);
            Primitive("Stake", PrimitiveType.Cube, board.transform, new Vector3(0f, .72f, 0f),
                new Vector3(.10f, 1.45f, .10f), Quaternion.identity, materials.DarkWood);
            Primitive("Notice", PrimitiveType.Cube, board.transform, new Vector3(0f, 1.22f, 0f),
                new Vector3(.92f, .55f, .07f), Quaternion.Euler(0f, -8f, 0f), materials.Parchment);
            for (int i = 0; i < 3; i++)
                Primitive("MeasureStake_" + i, PrimitiveType.Cube, root,
                    origin + new Vector3(.55f + i * .42f, -.25f, -.4f + i * .23f),
                    new Vector3(.06f, 1.2f, .06f), Quaternion.Euler(0f, i * 7f, i * 2f), materials.DarkWood);
        }

        private static void BuildSupplies(Transform root, Vector3 origin, Materials materials)
        {
            Prefab("SupplyCrate", CratePrefab, root, origin + new Vector3(-1.2f, -1f, -.5f),
                Quaternion.Euler(0f, 12f, 0f), Vector3.one * .85f);
            Prefab("SupplyBarrel", BarrelPrefab, root, origin + new Vector3(-.25f, -1f, -.7f),
                Quaternion.Euler(0f, -8f, 0f), Vector3.one * .82f);
            for (int i = 0; i < 5; i++)
                Primitive("Timber_" + i, PrimitiveType.Cube, root,
                    origin + new Vector3(.65f + (i % 2) * .22f, -.84f + (i / 2) * .16f, -.5f),
                    new Vector3(.16f, .13f, 1.45f), Quaternion.Euler(0f, 8f, 0f), materials.FreshWood);
        }

        private static void BuildWorksite(Transform root, Vector3 origin, Materials materials)
        {
            var trestle = new GameObject("Trestle");
            trestle.transform.SetParent(root, false);
            trestle.transform.localPosition = origin + new Vector3(0f, -1f, -.45f);
            Primitive("Top", PrimitiveType.Cube, trestle.transform, new Vector3(0f, .8f, 0f),
                new Vector3(1.5f, .12f, .18f), Quaternion.identity, materials.FreshWood);
            for (int side = -1; side <= 1; side += 2)
                for (int depth = -1; depth <= 1; depth += 2)
                    Primitive("Leg", PrimitiveType.Cube, trestle.transform,
                        new Vector3(side * .45f, .38f, depth * .17f), new Vector3(.11f, .82f, .11f),
                        Quaternion.Euler(depth * 12f, 0f, side * 10f), materials.DarkWood);
            Prefab("WorkCrate", CratePrefab, root, origin + new Vector3(1.25f, -1f, -.55f),
                Quaternion.Euler(0f, -13f, 0f), Vector3.one * .78f);
            AddDust(root, origin + new Vector3(0f, -.08f, -.45f), materials.Smoke,
                new Color(.72f, .54f, .31f, .34f), 14);
        }

        private static void BuildRestored(ProjectSpec spec, Transform root, Materials materials)
        {
            switch (spec.Key)
            {
                case "forge": BuildForge(root, materials); break;
                case "garden": BuildGarden(root, materials); break;
                case "pintle": BuildPintle(root, materials); break;
                case "witch": BuildWitch(root, materials); break;
                case "mill": BuildMill(root, materials); break;
            }
        }

        private static void BuildOccupied(ProjectSpec spec, Transform root, Materials materials,
            int forageLayer)
        {
            switch (spec.Key)
            {
                case "forge":
                    BuildToolRack(root, new Vector3(3.15f, .1f, 1.35f), materials);
                    AddNightLight(root, new Vector3(3.25f, 1.2f, -.35f), new Color(1f, .38f, .11f), 2.7f, 5f);
                    break;
                case "garden":
                    BuildGrowBed(root, "chapel_restored_bed_1", new Vector3(-1.65f, .28f, 4.4f), forageLayer);
                    BuildGrowBed(root, "chapel_restored_bed_2", new Vector3(1.65f, .28f, 4.4f), forageLayer);
                    Prefab("GardenBucket", BucketPrefab, root, new Vector3(0f, .05f, 6.25f),
                        Quaternion.identity, Vector3.one * .85f);
                    break;
                case "pintle":
                    Prefab("CommonRoomFirewood", FirewoodPrefab, root, new Vector3(7.28f, 0f, -.15f),
                        Quaternion.Euler(0f, -12f, 0f), Vector3.one * .82f);
                    AddNightLight(root, new Vector3(7.38f, 2.15f, 2.75f), new Color(1f, .58f, .21f), 2.2f, 6f);
                    break;
                case "witch":
                    BuildGrowBed(root, "witch_restored_nursery", new Vector3(-3.1f, .28f, .4f), forageLayer);
                    AddDust(root, new Vector3(4.2f, .45f, 2.2f), materials.Smoke,
                        new Color(.55f, .71f, .42f, .28f), 12);
                    AddNightLight(root, new Vector3(.2f, 1.8f, -2.35f), new Color(.72f, .86f, .42f), 1.5f, 4.2f);
                    break;
                case "mill":
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AlchemyShowcaseScene) != null) break;
                    Prefab("WorkshopShelf", ShelfPrefab, root, new Vector3(-2.7f, .05f, -4.25f),
                        Quaternion.Euler(0f, 8f, 0f), Vector3.one * .78f);
                    Prefab("WorkshopFirewood", FirewoodPrefab, root, new Vector3(2.75f, .05f, -4.4f),
                        Quaternion.Euler(0f, -10f, 0f), Vector3.one * .82f);
                    AddNightLight(root, new Vector3(.8f, 2.05f, -4.7f), new Color(1f, .54f, .2f), 2.0f, 5f);
                    break;
            }
        }

        private static void BuildForge(Transform root, Materials materials)
        {
            Vector3 p = new Vector3(3.25f, 0f, -.35f);
            Primitive("ForgeStone", PrimitiveType.Cube, root, p + new Vector3(0f, .45f, 0f),
                new Vector3(1.4f, .8f, 1.05f), Quaternion.identity, materials.Iron);
            Primitive("CoalBed", PrimitiveType.Cube, root, p + new Vector3(0f, .91f, 0f),
                new Vector3(1.05f, .12f, .72f), Quaternion.identity, materials.Ember);
            Primitive("AnvilBase", PrimitiveType.Cube, root, new Vector3(1.6f, .52f, -.35f),
                new Vector3(.38f, .92f, .42f), Quaternion.identity, materials.DarkWood);
            Primitive("Anvil", PrimitiveType.Cube, root, new Vector3(1.6f, 1.05f, -.35f),
                new Vector3(1.18f, .26f, .45f), Quaternion.identity, materials.Iron);
            Primitive("AnvilHorn", PrimitiveType.Cylinder, root, new Vector3(2.25f, 1.05f, -.35f),
                new Vector3(.19f, .55f, .19f), Quaternion.Euler(0f, 0f, 90f), materials.Iron);
            AddDust(root, p + new Vector3(0f, 1.55f, 0f), materials.Smoke,
                new Color(.28f, .28f, .25f, .30f), 18);
        }

        private static void BuildToolRack(Transform root, Vector3 p, Materials materials)
        {
            Primitive("ToolRack", PrimitiveType.Cube, root, p + new Vector3(0f, 1.1f, 0f),
                new Vector3(1.7f, .12f, .12f), Quaternion.identity, materials.FreshWood);
            for (int i = 0; i < 4; i++)
                Primitive("Tool_" + i, PrimitiveType.Cube, root,
                    p + new Vector3(-.6f + i * .4f, .65f, 0f), new Vector3(.08f, .9f, .08f),
                    Quaternion.Euler(0f, 0f, -8f + i * 5f), materials.Iron);
        }

        private static void BuildGarden(Transform root, Materials materials)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 p = new Vector3(side * 1.65f, .18f, 4.4f);
                Primitive("RaisedBed", PrimitiveType.Cube, root, p,
                    new Vector3(2.6f, .35f, 1.35f), Quaternion.identity, materials.FreshWood);
                Primitive("RichSoil", PrimitiveType.Cube, root, p + new Vector3(0f, .24f, 0f),
                    new Vector3(2.32f, .15f, 1.08f), Quaternion.identity, materials.Soil);
                for (int i = -2; i <= 2; i++)
                    Primitive("Herb", PrimitiveType.Sphere, root,
                        p + new Vector3(i * .42f, .40f, (i % 2) * .23f), new Vector3(.19f, .25f, .19f),
                        Quaternion.identity, materials.Herb);
            }
            for (int x = -4; x <= 4; x += 2)
                Primitive("FencePost", PrimitiveType.Cube, root, new Vector3(x, .62f, 6.5f),
                    new Vector3(.09f, 1.25f, .09f), Quaternion.identity, materials.FreshWood);
            Primitive("FenceRail", PrimitiveType.Cube, root, new Vector3(0f, .72f, 6.5f),
                new Vector3(8.2f, .10f, .10f), Quaternion.identity, materials.FreshWood);
        }

        private static void BuildPintle(Transform root, Materials materials)
        {
            // The tavern mesh's east wall is near local X=4.  Keep the restored common-room
            // furniture outside that shell so the reveal and ordinary road approach both show it.
            // The actual vendor tavern collider reaches local X=6.96 at this elevation.
            const float facadeX = 7.2f;
            Prefab("FrontBench", BenchPrefab, root, new Vector3(facadeX, 0f, 1.45f),
                Quaternion.Euler(0f, 90f, 0f), Vector3.one * .82f);
            Prefab("CommonTable", TablePrefab, root, new Vector3(facadeX + .12f, 0f, -1.0f),
                Quaternion.Euler(0f, 90f, 0f), Vector3.one * .78f);
            Prefab("CellarBarrel", BarrelPrefab, root, new Vector3(facadeX - .22f, 0f, 3.55f),
                Quaternion.Euler(0f, 7f, 0f), Vector3.one * .84f);
            // One wall-mounted board provides the long-distance landmark without spending seven
            // separate renderers on a decorative post-and-garland cluster.
            Primitive("RestoredSign", PrimitiveType.Cube, root,
                new Vector3(facadeX + .18f, 2.05f, 1.25f), new Vector3(.14f, .72f, 1.42f),
                Quaternion.Euler(0f, 0f, -3f), materials.FreshWood);
        }

        private static void BuildWitch(Transform root, Materials materials)
        {
            Primitive("FreshDoor", PrimitiveType.Cube, root, new Vector3(0f, 1.25f, -2.08f),
                new Vector3(1.15f, 2.35f, .12f), Quaternion.identity, materials.FreshWood);
            Primitive("DoorBrace", PrimitiveType.Cube, root, new Vector3(0f, 1.25f, -2.16f),
                new Vector3(1.0f, .11f, .08f), Quaternion.Euler(0f, 0f, -22f), materials.DarkWood);
            for (int side = -1; side <= 1; side += 2)
                Primitive("Shutter", PrimitiveType.Cube, root, new Vector3(side * 1.45f, 1.75f, -2.05f),
                    new Vector3(.72f, 1.22f, .10f), Quaternion.identity, materials.FreshWood);
            Vector3 spring = new Vector3(4.2f, .08f, 2.2f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f;
                Primitive("SpringStone_" + i, PrimitiveType.Sphere, root,
                    spring + new Vector3(Mathf.Cos(angle) * 1.15f, 0f, Mathf.Sin(angle) * 1.15f),
                    new Vector3(.32f, .18f, .32f), Quaternion.identity, materials.Iron);
            }
            Prefab("HerbShelf", ShelfPrefab, root, new Vector3(-2.75f, .05f, -.15f),
                Quaternion.Euler(0f, 82f, 0f), Vector3.one * .72f);
        }

        private static void BuildMill(Transform root, Materials materials)
        {
            // The purchased showcase supplies the complete architecture, furniture, and roof.
            // Retain the lightweight fallback only when that optional package is absent.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(AlchemyShowcaseScene) != null) return;
            Prefab("WorkshopTable", TablePrefab, root, new Vector3(-1.25f, 0f, -5.25f),
                Quaternion.Euler(0f, 4f, 0f), Vector3.one * .86f);
            Prefab("WorkshopBench", BenchPrefab, root, new Vector3(1.45f, 0f, -5.25f),
                Quaternion.Euler(0f, 180f, 0f), Vector3.one * .82f);
            Prefab("WorkshopBroom", BroomPrefab, root, new Vector3(3.0f, .05f, -4.75f),
                Quaternion.Euler(0f, 6f, -8f), Vector3.one * .8f);
            // Keep the transformation at eye level. The previous five roof-patch cubes sat above
            // the real shingle pitch and read as floating panels from the reveal camera.
            Primitive("WorkshopAwning", PrimitiveType.Cube, root, new Vector3(0f, 2.42f, -5.15f),
                new Vector3(4.6f, .12f, 1.7f), Quaternion.Euler(-10f, 0f, 0f), materials.DarkWood);
            for (int side = -1; side <= 1; side += 2)
            {
                Primitive("AwningPost", PrimitiveType.Cube, root,
                    new Vector3(side * 2.05f, 1.12f, -5.58f), new Vector3(.14f, 2.25f, .14f),
                    Quaternion.Euler(0f, 0f, side * 2f), materials.FreshWood);
                Primitive("AwningBrace", PrimitiveType.Cube, root,
                    new Vector3(side * 1.65f, 2.1f, -5.18f), new Vector3(.12f, 1.05f, .12f),
                    Quaternion.Euler(0f, 0f, side * 43f), materials.FreshWood);
            }
            Primitive("WorkshopNameboard", PrimitiveType.Cube, root,
                new Vector3(0f, 2.05f, -5.72f), new Vector3(1.9f, .52f, .10f),
                Quaternion.identity, materials.FreshWood);
        }

        private static void BuildGrowBed(Transform root, string id, Vector3 localPosition, int layer)
        {
            var bed = new GameObject(id);
            bed.transform.SetParent(root, false);
            bed.transform.localPosition = localPosition;
            if (layer >= 0) bed.layer = layer;
            var trigger = bed.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, .35f, 0f);
            trigger.size = new Vector3(2.45f, .75f, 1.25f);
            var anchor = new GameObject("SpawnAnchor").transform;
            anchor.SetParent(bed.transform, false);
            anchor.localPosition = new Vector3(0f, .28f, 0f);
            var growBed = bed.AddComponent<GrowBed>();
            Set(growBed, "_bedId", id);
            Set(growBed, "_spawnAnchor", anchor);
            Set(growBed, "_matureGameHours", 6f);
            Set(growBed, "_yield", 3);
            Set(growBed, "_clusterRadius", .42f);
            Set(growBed, "_matureScale", 1.8f);
        }

        private static void AddNightLight(Transform root, Vector3 position, Color color,
            float intensity, float range)
        {
            var go = new GameObject("RestoredNightLight");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            var night = go.AddComponent<NightLight>();
            Set(night, "_light", light);
            Set(night, "_nightIntensity", intensity);
            Set(night, "_fadeSpeed", 1.6f);
            Set(night, "_flickerAmount", .045f);
        }

        private static void AddDust(Transform root, Vector3 position, Material material,
            Color color, int maxParticles)
        {
            var go = new GameObject("WorkAtmosphere");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .18f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .10f);
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Max(1f, maxParticles * .35f);
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .45f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void BuildReveal(GameObject root, ProjectSpec spec, RestorationProjectData project)
        {
            var focus = new GameObject("RevealFocus");
            focus.transform.SetParent(root.transform, false);
            focus.transform.localPosition = spec.FocusOffset;
            var director = root.AddComponent<RestorationRevealDirector>();
            Set(director, "_project", project);
            Set(director, "_focusTarget", focus.transform);
            Set(director, "_promotionFlagId", spec.Prefix + "_restored");
            Set(director, "_minimumStage", RestorationStage.Restored);
            Set(director, "_eyebrowId", "restoration." + spec.Key + ".reveal.eyebrow");
            Set(director, "_titleId", "restoration." + spec.Key + ".reveal.title");
            Set(director, "_bodyId", "restoration." + spec.Key + ".reveal.body");
            Set(director, "_cameraDistance", spec.CameraDistance);
            Set(director, "_cameraHeight", spec.CameraHeight);
            Set(director, "_cameraFov", 47f);
            Set(director, "_frameDirection", spec.FrameDirection);
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
            go.transform.localScale = localScale;
            go.transform.localRotation = localRotation;
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

        private static GameObject Prefab(string name, string path, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            var asset = Required<GameObject>(path);
            var instance = PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate " + path);
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
            if (asset == null) throw new FileNotFoundException("Missing village restoration asset", path);
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
