#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Hollowfen.Apothecary;
using Hollowfen.Foraging;
using Hollowfen.Map;
using Hollowfen.Quests;
using Hollowfen.Save;
using Hollowfen.Weather;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowfen.EditorTools
{
    public static class ApothecaryPreparationVerifier
    {
        private const string PrefabPath =
            "Assets/_Hollowfen/Art/Apothecary/PF_TobinApothecaryBuilding.prefab";
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string DataRoot = "Assets/_Hollowfen/Data/Apothecary";

        [MenuItem("Hollowfen/Apothecary/Verify Preparation Table")]
        public static void RunMenu() => Debug.Log(RunAll());

        public static string RunAll()
        {
            VerifyContentAndWorld();
            VerifyAtomicPreparation();
            return "APOTHECARY — PASS: complete purchased laboratory building, sealed purchased-" +
                   "brick cutaway, inward-opening solid entrance, automatic rear chain gate, " +
                   "seven candlelight interactions, " +
                   "open traversal, level mill terrace, " +
                   "identification gates, four-step recipes, isolated atomic commit/rollback, " +
                   "old-save hydration, persisted light state, and persisted shelf stock";
        }

        private static void VerifyContentAndWorld()
        {
            var prefab = Required<GameObject>(PrefabPath);
            var station = prefab.GetComponent<ApothecaryStation>();
            Require(station != null, "preparation prefab has no ApothecaryStation");
            Require(station.Recipes != null && station.Recipes.Length == 3,
                "preparation station does not contain three authored recipes");
            var shelfDisplay = prefab.GetComponent<ApothecaryShelfDisplay>();
            Require(shelfDisplay != null && shelfDisplay.DisplayCount == 3,
                "prepared stock is not represented by three purchased showcase props");

            string[] expected = { "field_ink", "goldfoot_broth", "brightspore_tonic" };
            Require(station.Recipes.Select(recipe => recipe != null ? recipe.Id : "").
                    SequenceEqual(expected), "recipe order or ids drifted");
            foreach (PreparationRecipeData recipe in station.Recipes)
            {
                Require(recipe != null && recipe.HasValidIngredients,
                    "a recipe has invalid ingredient arrays");
                Require(recipe.StepIds != null && recipe.StepIds.Length == 4,
                    recipe.Id + " does not have four physical bench marks");
                Require(recipe.HeroSpecies != null && recipe.HeroSpecies.JournalPage != null,
                    recipe.Id + " has no illustrated field-journal plate");
                Require(Localization.Get(recipe.TitleId) != recipe.TitleId &&
                        Localization.Get(recipe.SummaryId) != recipe.SummaryId &&
                        Localization.Get(recipe.UnlockHintId) != recipe.UnlockHintId &&
                        Localization.Get(recipe.ResultUseId) != recipe.ResultUseId,
                    recipe.Id + " copy is not localized");
                Require(!string.IsNullOrWhiteSpace(recipe.RequiredFlagId),
                    recipe.Id + " has no story progression gate");
            }

            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            var triggers = colliders.Where(collider => collider.isTrigger).ToArray();
            Require(colliders.Length >= 120 && colliders.Length <= 190,
                "authored building collision count drifted: " + colliders.Length);
            Collider preparationTrigger = triggers.SingleOrDefault(trigger =>
                trigger != null && trigger.name == "PreparationInteraction");
            Collider caseLedgerTrigger = triggers.SingleOrDefault(trigger =>
                trigger != null && trigger.name == "CaseLedgerInteraction");
            Require(preparationTrigger is BoxCollider,
                "building should expose one preparation trigger and retain authored solid collision");
            Require(preparationTrigger.gameObject.layer == LayerMask.NameToLayer("Foraging"),
                "preparation trigger is not on PlayerInteractor's Foraging layer");
            Require(caseLedgerTrigger is BoxCollider &&
                    caseLedgerTrigger.GetComponent<ApothecaryCaseLedgerStation>() != null &&
                    caseLedgerTrigger.gameObject.layer == LayerMask.NameToLayer("Foraging"),
                "the purchased open book is not wired as the case ledger interaction");
            var shelter = prefab.GetComponentInChildren<WeatherShelterVolume>(true);
            var lightSwitches = prefab.GetComponentsInChildren<ApothecaryLightSwitch>(true);
            Require(triggers.Length == 10 && shelter != null &&
                    shelter.GetComponent<BoxCollider>() != null && lightSwitches.Length == 7 &&
                    lightSwitches.All(lightSwitch => lightSwitch.Controller != null &&
                        lightSwitch.GetComponent<SphereCollider>() != null &&
                        lightSwitch.GetComponent<SphereCollider>().isTrigger &&
                        lightSwitch.gameObject.layer == LayerMask.NameToLayer("Foraging")),
                "the showcase should contain preparation, case-ledger, shelter, and seven " +
                "Foraging-layer candlelight triggers");
            Require(prefab.GetComponentsInChildren<Animator>(true).Length >= 8 &&
                    prefab.GetComponentsInChildren<AudioSource>(true).Length == 0,
                "showcase animation was lost or duplicate vendor audio leaked into Hollowfen");
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
                Require(component == null || component.GetType().Name != "ActionController",
                    "vendor ActionController leaked into the owned prefab");
            Transform outerDoor = prefab.transform.Find("Entrance_Room/Locked_Door");
            Transform hallDoor = prefab.transform.Find("Hall_Room/Wood_Door");
            Require(outerDoor != null && outerDoor.Find("Box_Collider") == null &&
                    hallDoor != null && hallDoor.GetComponents<Collider>().Length == 0,
                "a showcase closed-door blocker still seals an open threshold");
            Require(outerDoor.Find("Wood_Door_01") != null &&
                    Mathf.Abs(Mathf.DeltaAngle(outerDoor.Find("Wood_Door_01").localEulerAngles.y,
                        250f)) < .1f &&
                    outerDoor.Find("Wood_Door_02") != null &&
                    Mathf.Abs(Mathf.DeltaAngle(outerDoor.Find("Wood_Door_02").localEulerAngles.y,
                        110f)) < .1f &&
                    hallDoor.Find("Wood_Door_02") != null &&
                    Mathf.Abs(Mathf.DeltaAngle(hallDoor.Find("Wood_Door_02").localEulerAngles.y,
                        -110f)) < .1f,
                "the front double doors do not open inward or the hall doors lost their pose");
            var proximityDoor = outerDoor.GetComponent<ApothecaryProximityDoor>();
            Require(proximityDoor != null && proximityDoor.LeftLeaf != null &&
                    proximityDoor.RightLeaf != null &&
                    proximityDoor.LeftLeaf.GetComponent<BoxCollider>() != null &&
                    proximityDoor.RightLeaf.GetComponent<BoxCollider>() != null &&
                    Mathf.Abs(Mathf.DeltaAngle(proximityDoor.LeftClosedEuler.y, 0f)) < .1f &&
                    Mathf.Abs(Mathf.DeltaAngle(proximityDoor.RightClosedEuler.y, 0f)) < .1f &&
                    Mathf.Abs(Mathf.DeltaAngle(proximityDoor.LeftOpenEuler.y, 250f)) < .1f &&
                    Mathf.Abs(Mathf.DeltaAngle(proximityDoor.RightOpenEuler.y, 110f)) < .1f &&
                    proximityDoor.OpenRadius >= 4f &&
                    proximityDoor.CloseRadius > proximityDoor.OpenRadius,
                "the front entrance is not a solid, hysteresis-safe automatic double door");
            Transform chainDoorTransform = prefab.transform.Find("Cage_Room/Wire_Door");
            var chainDoor = chainDoorTransform != null
                ? chainDoorTransform.GetComponent<ApothecaryChainDoor>() : null;
            Require(chainDoor != null && chainDoor.Gate != null &&
                    chainDoor.Gate.GetComponent<BoxCollider>() != null &&
                    chainDoor.OpenRadius >= 3.5f &&
                    chainDoor.CloseRadius > chainDoor.OpenRadius,
                "the rear chain gate is not wired as a collider-safe automatic lift door");

            Transform architecture = prefab.transform.Find("Hall_Room/Alchemy_Room");
            Renderer walls = architecture != null ? architecture.Find("Walls")?.GetComponent<Renderer>() : null;
            Require(walls != null && walls.sharedMaterials.All(material => material != null &&
                    material.HasProperty("_Cull") &&
                    Mathf.Approximately(material.GetFloat("_Cull"), (float)CullMode.Off)),
                "purchased wall shell is still invisible from its exterior side");
            Transform exteriorSideWall = architecture.Find("ExteriorSideWall");
            Transform exteriorSurface = exteriorSideWall != null
                ? exteriorSideWall.Find("PurchasedWallSurface") : null;
            Transform exteriorTrim = exteriorSideWall != null
                ? exteriorSideWall.Find("PurchasedWallTrim") : null;
            Renderer exteriorSurfaceRenderer = exteriorSurface != null
                ? exteriorSurface.GetComponent<Renderer>() : null;
            Renderer exteriorTrimRenderer = exteriorTrim != null
                ? exteriorTrim.GetComponent<Renderer>() : null;
            Mesh exteriorSurfaceMesh = exteriorSurface != null
                ? exteriorSurface.GetComponent<MeshFilter>()?.sharedMesh : null;
            Mesh exteriorTrimMesh = exteriorTrim != null
                ? exteriorTrim.GetComponent<MeshFilter>()?.sharedMesh : null;
            Renderer sourceTrim = architecture.Find("Elements_Pillar_2")?.GetComponent<Renderer>();
            Require(exteriorSurfaceRenderer != null && exteriorTrimRenderer != null &&
                    exteriorSurfaceMesh != null && exteriorTrimMesh != null &&
                    exteriorSurfaceMesh.vertexCount >= 60 && exteriorSurfaceMesh.vertexCount <= 90 &&
                    exteriorTrimMesh.vertexCount >= 9000 && exteriorTrimMesh.vertexCount <= 9500 &&
                    exteriorSurface.GetComponent<MeshCollider>() != null &&
                    exteriorTrim.GetComponent<MeshCollider>() != null &&
                    exteriorSurfaceRenderer.sharedMaterial == walls.sharedMaterial &&
                    sourceTrim != null &&
                    exteriorTrimRenderer.sharedMaterial == sourceTrim.sharedMaterial,
                "the purchased demo's +X cutaway is not repaired by its mirrored authored wall");

            int renderers = 0;
            int materialSlots = 0;
            long triangles = 0;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                materialSlots += renderer.sharedMaterials.Length;
                foreach (Material material in renderer.sharedMaterials)
                {
                    // ParticleSystemRenderer reserves a second trail slot even when trails are
                    // disabled. The authored package intentionally leaves those slots empty.
                    if (material == null)
                    {
                        Require(renderer is ParticleSystemRenderer,
                            "a non-particle showcase renderer contains a missing material");
                        continue;
                    }
                    Require(material.shader != null &&
                            (material.shader.name.StartsWith("Universal Render Pipeline/",
                                 StringComparison.Ordinal) ||
                             material.shader.name.EndsWith("_URP",
                                 StringComparison.OrdinalIgnoreCase)),
                        "station contains a missing or non-URP material");
                    string materialPath = AssetDatabase.GetAssetPath(material);
                    Require(materialPath.StartsWith("Assets/Alchemy/", StringComparison.Ordinal) ||
                            materialPath.StartsWith(
                                "Assets/_Hollowfen/Art/Apothecary/Materials/URPCompatibility/",
                                StringComparison.Ordinal),
                        "showcase renderer no longer points at a purchased or URP-compatibility material");
                }
            }
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3L;
            }
            Require(renderers >= 420 && renderers <= 470,
                "complete showcase renderer count drifted: " + renderers);
            Require(materialSlots <= 520, "showcase material-slot count exceeds 520");
            Require(triangles <= 1500000,
                "complete showcase exceeds the 1.5m serialized-LOD triangle budget: " + triangles);
            var lighting = prefab.GetComponent<ApothecaryLightingController>();
            Require(prefab.GetComponentsInChildren<Light>(true).Length == 4 &&
                    lighting != null && lighting.LightCount == 4 &&
                    lighting.FlameCount >= 30 && lighting.AnimatorCount >= 8,
                "apothecary should use exactly four bounded realtime interior lights");

            foreach (string dependency in AssetDatabase.GetDependencies(PrefabPath, true))
            {
                if (!dependency.StartsWith("Assets/Alchemy/Textures/", StringComparison.Ordinal)) continue;
                var importer = AssetImporter.GetAtPath(dependency) as TextureImporter;
                Require(importer != null && importer.maxTextureSize <= 2048 &&
                        importer.mipmapEnabled && importer.streamingMipmaps,
                    "showcase texture is not capped/streamed: " + dependency);
            }

            GameObject sceneInstance = FindSceneObject("TobinApothecaryBuilding");
            Require(sceneInstance != null && sceneInstance.transform.parent != null &&
                    sceneInstance.transform.parent.name == "_LivingRestoration_tobin_workshop",
                "complete apothecary is not installed as an always-present Tobin landmark");
            Require(sceneInstance.activeInHierarchy,
                "complete apothecary is hidden in an unprogressed scene");
            Transform sceneExteriorWall = sceneInstance.transform.Find(
                "Hall_Room/Alchemy_Room/ExteriorSideWall");
            Require(sceneExteriorWall != null,
                "scene apothecary did not inherit the exterior cutaway repair");
            Vector3 exteriorEye = sceneInstance.transform.TransformPoint(
                new Vector3(15f, 1.83f, 19.62f));
            Vector3 fireplaceEmbers = sceneInstance.transform.TransformPoint(
                new Vector3(0f, .45f, -6.95f));
            Vector3 sightline = fireplaceEmbers - exteriorEye;
            Physics.SyncTransforms();
            Require(Physics.Raycast(exteriorEye, sightline.normalized, out RaycastHit exteriorHit,
                        sightline.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                    (exteriorHit.transform == sceneExteriorWall ||
                     exteriorHit.transform.IsChildOf(sceneExteriorWall)),
                "the reported exterior viewpoint still has line of sight to the fireplace");
            var location = Required<LocationData>(
                "Assets/_Hollowfen/Data/Locations/LocationData_TobinsApothecary.asset");
            var locationMarker = UnityEngine.Object.FindObjectsByType<LocationMarker>(
                    FindObjectsInactive.Include)
                .SingleOrDefault(marker => marker != null && marker.Data == location);
            Require(location.Id == "tobins_apothecary" && location.RegionId == "village" &&
                    locationMarker != null &&
                    Vector3.Distance(locationMarker.transform.position,
                        sceneInstance.transform.TransformPoint(new Vector3(0f, 0f, 10.8f))) < .1f,
                "apothecary discovery marker is missing or misplaced");
            Physics.SyncTransforms();
            for (float z = 12.75f; z >= 2.5f; z -= .5f)
            {
                Vector3 center = sceneInstance.transform.TransformPoint(new Vector3(0f, 0f, z));
                Collider blocker = Physics.OverlapCapsule(
                        center + Vector3.up * .34f,
                        center + Vector3.up * 1.46f,
                        .27f, ~0, QueryTriggerInteraction.Ignore)
                    .FirstOrDefault(collider => collider != null &&
                        collider.GetComponentInParent<ApothecaryStation>(true) == station &&
                        !collider.name.StartsWith("Floor_", StringComparison.Ordinal));
                Require(blocker == null,
                    "walkable entrance corridor is blocked near local z=" + z.ToString("F2") +
                    (blocker != null ? " by " + blocker.name : ""));
            }
            VerifyPlayerSizedTraversal(sceneInstance);
            // Prefab instance metadata is not reliable on the deserialized Play Mode clone.
            // Runtime structure and edit-time import ownership are the useful contract here.
            Require(sceneInstance.GetComponent<ApothecaryStation>() != null &&
                    sceneInstance.GetComponentInChildren<ApothecaryCaseLedgerStation>(true) != null &&
                    sceneInstance.GetComponentsInChildren<Renderer>(true).Length == renderers,
                "scene building does not match the complete owned showcase structure");
            Require(Vector3.Distance(sceneInstance.transform.position,
                        new Vector3(215f, 35.72f, 340f)) < .05f &&
                    Mathf.Abs(Mathf.DeltaAngle(sceneInstance.transform.eulerAngles.y, 141f)) < .05f,
                "apothecary placement drifted from the cleared mill terrace");
            Terrain terrain = Terrain.activeTerrain;
            Require(terrain != null, "terrain is missing beneath the apothecary");
            float min = float.MaxValue;
            float max = float.MinValue;
            Quaternion rotation = Quaternion.Euler(0f, 141f, 0f);
            for (float x = -11f; x <= 11f; x += 3.5f)
                for (float z = -13f; z <= 13f; z += 3.5f)
                {
                    Vector3 point = sceneInstance.transform.position + rotation * new Vector3(x, 0f, z);
                    float height = terrain.SampleHeight(point) + terrain.transform.position.y;
                    min = Mathf.Min(min, height);
                    max = Mathf.Max(max, height);
                }
            Require(max - min <= .12f,
                "apothecary footprint is not level: " + (max - min).ToString("F3") + "m");
            float previousHeight = sceneInstance.transform.position.y;
            for (float z = 14f; z <= 30f; z += 2f)
            {
                Vector3 point = sceneInstance.transform.position + rotation * new Vector3(0f, 0f, z);
                float height = terrain.SampleHeight(point) + terrain.transform.position.y;
                Require(height <= previousHeight + .18f && previousHeight - height <= .9f,
                    "mill approach contains an abrupt or uphill terrain step near " +
                    z.ToString("F0") + "m");
                previousHeight = height;
            }
            Require(AssetDatabase.LoadAssetAtPath<PreparationRecipeData>(
                        DataRoot + "/Preparation_FieldInk.asset") != null,
                "field-ink recipe asset is missing");
        }

        private static void VerifyAtomicPreparation()
        {
            string originalOverride = SaveManager.EditorSaveDirectoryOverride;
            int originalSlot = SaveManager.ActiveSlot;
            InventorySnapshot originalInventory = InventoryRuntime.ToSnapshot();
            string[] originalDiscovery = MushroomDiscovery.ToArray();
            ApothecarySnapshot originalApothecary = ApothecaryRuntime.ToSnapshot();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            string temp = Path.Combine(Path.GetTempPath(),
                "hollowfen-apothecary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);

            try
            {
                SaveManager.EditorSaveDirectoryOverride = temp;
                SaveManager.SetActiveSlot(0);
                var recipe = Required<PreparationRecipeData>(
                    DataRoot + "/Preparation_FieldInk.asset");
                Prepare(recipe);
                long revision = SaveManager.InspectSlot(0).Revision;
                int events = 0;
                Action changed = () => events++;
                ApothecaryRuntime.OnChanged += changed;
                try
                {
                    Require(ApothecaryRuntime.Availability(recipe) ==
                            ApothecaryRuntime.CraftResult.Prepared,
                        "known, fully stocked recipe was not available");
                    Require(ApothecaryRuntime.TryPrepare(recipe) ==
                            ApothecaryRuntime.CraftResult.Prepared,
                        "valid preparation did not commit");
                    SaveSlotInspection committed = SaveManager.InspectSlot(0);
                    Require(committed.CanLoad && committed.Revision == revision + 1 && events == 1,
                        "preparation did not publish exactly one durable revision/event");
                    Require(ApothecaryRuntime.ProductCount(recipe.ResultId) == 1 &&
                            ApothecaryRuntime.HasCrafted(recipe.Id) &&
                            SnapshotProduct(committed.Meta.Apothecary, recipe.ResultId) == 1,
                        "prepared shelf stock or practiced recipe did not persist");
                    for (int i = 0; i < recipe.Ingredients.Length; i++)
                        Require(InventoryRuntime.GetCount(recipe.Ingredients[i]) == 0,
                            "successful preparation did not consume its full measure");

                    Prepare(recipe);
                    revision = SaveManager.InspectSlot(0).Revision;
                    events = 0;
                    SaveManager.EditorRejectNextAtomicCommit = true;
                    Require(ApothecaryRuntime.TryPrepare(recipe) ==
                            ApothecaryRuntime.CraftResult.SaveUnavailable,
                        "injected final-commit failure did not reach the caller");
                    Require(SaveManager.InspectSlot(0).Revision == revision && events == 0 &&
                            ApothecaryRuntime.ProductCount(recipe.ResultId) == 0 &&
                            !ApothecaryRuntime.HasCrafted(recipe.Id) &&
                            GameScores.Knowledge == 0,
                        "failed final commit leaked stock, knowledge, practice, revision, or event");
                    for (int i = 0; i < recipe.Ingredients.Length; i++)
                        Require(InventoryRuntime.GetCount(recipe.Ingredients[i]) == recipe.Amounts[i],
                            "failed final commit consumed an ingredient");

                    revision = SaveManager.InspectSlot(0).Revision;
                    events = 0;
                    Require(ApothecaryRuntime.TrySetInteriorLights(true),
                        "candlelights could not be switched on");
                    SaveSlotInspection lit = SaveManager.InspectSlot(0);
                    Require(ApothecaryRuntime.InteriorLightsOn && lit.CanLoad &&
                            lit.Revision == revision + 1 && lit.Meta.Apothecary.InteriorLightsOn &&
                            events == 1,
                        "candlelight state did not publish one durable revision/event");

                    revision = lit.Revision;
                    events = 0;
                    SaveManager.EditorRejectNextAtomicCommit = true;
                    Require(!ApothecaryRuntime.TrySetInteriorLights(false) &&
                            ApothecaryRuntime.InteriorLightsOn &&
                            SaveManager.InspectSlot(0).Revision == revision && events == 0,
                        "failed candlelight save leaked a visual state, revision, or event");
                }
                finally { ApothecaryRuntime.OnChanged -= changed; }

                ApothecaryRuntime.HydrateFrom(null);
                Require(ApothecaryRuntime.ProductCount("missing") == 0 &&
                        !ApothecaryRuntime.InteriorLightsOn,
                    "historical null save did not hydrate as an empty, unlit apothecary");
            }
            finally
            {
                if (SaveManager.IsAtomicTransactionActive)
                    SaveManager.EditorCancelAtomicTransactionForVerification();
                SaveManager.EditorRejectNextAtomicCommit = false;
                SaveManager.EditorSaveDirectoryOverride = originalOverride;
                SaveManager.SetActiveSlot(originalSlot);
                InventoryRuntime.HydrateFrom(originalInventory);
                MushroomDiscovery.HydrateFrom(originalDiscovery);
                ApothecaryRuntime.HydrateFrom(originalApothecary);
                GameScores.HydrateFrom(originalScores);
                if (Directory.Exists(temp)) Directory.Delete(temp, true);
            }
        }

        private static void Prepare(PreparationRecipeData recipe)
        {
            SaveManager.DeleteSlot(0);
            var totals = recipe.Ingredients.Select((species, index) =>
                    new { species.Id, Count = recipe.Amounts[index] })
                .GroupBy(row => row.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Count),
                    StringComparer.Ordinal);
            InventoryRuntime.HydrateFrom(new InventorySnapshot
            {
                Ids = totals.Keys.ToArray(),
                Counts = totals.Values.ToArray(),
            });
            MushroomDiscovery.HydrateFrom(recipe.Ingredients.Select(species => species.Id).
                Distinct(StringComparer.Ordinal).ToArray());
            ApothecaryRuntime.HydrateFrom(null);
            GameScores.HydrateFrom(new SaveSlotMeta
            {
                GameFlagIds = string.IsNullOrWhiteSpace(recipe.RequiredFlagId)
                    ? Array.Empty<string>()
                    : new[] { recipe.RequiredFlagId },
            });
            var meta = new SaveSlotMeta
            {
                CurrentQuest = "Apothecary verifier",
                CurrentQuestId = "apothecary-verifier",
                CurrentAct = 4,
                Inventory = InventoryRuntime.ToSnapshot(),
                DiscoveredMushroomIds = MushroomDiscovery.ToArray(),
                Apothecary = ApothecaryRuntime.ToSnapshot(),
            };
            GameScores.WriteTo(meta);
            SaveManager.WriteSlot(0, meta);
        }

        private static int SnapshotProduct(ApothecarySnapshot snapshot, string id)
        {
            int count = Math.Min(snapshot?.ProductIds?.Length ?? 0,
                snapshot?.ProductCounts?.Length ?? 0);
            for (int i = 0; i < count; i++)
                if (snapshot.ProductIds[i] == id) return snapshot.ProductCounts[i];
            return 0;
        }

        private static void VerifyPlayerSizedTraversal(GameObject building)
        {
            CharacterController playerController = Resources.FindObjectsOfTypeAll<CharacterController>()
                .FirstOrDefault(controller => controller != null &&
                    controller.gameObject.scene.IsValid() &&
                    controller.gameObject.scene.path == GameplayScene &&
                    controller.CompareTag("Player"));
            Require(playerController != null,
                "Scene_Hollowfen has no player CharacterController to size the entrance test");

            // The production door tracks the real tagged player. The traversal probe is
            // intentionally untagged, so open the threshold explicitly before asking its
            // CharacterController to cross it; otherwise this test measures the closed-door
            // idle state rather than the walkable open state.
            var proximityDoor = building.GetComponentInChildren<ApothecaryProximityDoor>(true);
            Require(proximityDoor != null, "automatic front-door controller is missing");
            proximityDoor.SetOpenInstant(true);

            var probe = new GameObject("__ApothecaryPlayerTraversalProbe")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = playerController.gameObject.layer,
            };
            try
            {
                var controller = probe.AddComponent<CharacterController>();
                controller.center = playerController.center;
                controller.radius = playerController.radius;
                controller.height = playerController.height;
                controller.slopeLimit = playerController.slopeLimit;
                controller.stepOffset = playerController.stepOffset;
                controller.skinWidth = playerController.skinWidth;
                controller.minMoveDistance = playerController.minMoveDistance;
                probe.transform.SetPositionAndRotation(
                    building.transform.TransformPoint(new Vector3(0f, .04f, 13.25f)),
                    building.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
                Physics.SyncTransforms();

                Vector3 step = building.transform.TransformDirection(Vector3.back) * .25f;
                for (int i = 0; i < 43; i++) controller.Move(step);
                Vector3 reached = building.transform.InverseTransformPoint(probe.transform.position);
                Require(reached.z <= 2.75f && Mathf.Abs(reached.x) <= .35f,
                    "player-sized controller stopped before crossing both open doorways at local " +
                    reached.ToString("F2"));
            }
            finally
            {
                proximityDoor.SetOpenInstant(false);
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate != null && candidate.name == name && candidate.scene.IsValid() &&
                    candidate.scene.path == GameplayScene) return candidate;
            return null;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "missing asset: " + path);
            return asset;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("APOTHECARY — FAIL: " + message);
        }
    }
}
#endif
