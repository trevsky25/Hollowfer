#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hollowfen.Apothecary;
using Hollowfen.Data;
using Hollowfen.Map;
using Hollowfen.Restoration;
using Hollowfen.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Converts the purchased Alchemy and Magic Lab showcase into a self-contained Hollowfen
    /// building. The complete authored rooms and dressing are retained; only package demo runtime,
    /// duplicate audio, and baked-lighting metadata are removed before the building is installed.
    /// </summary>
    public static class ApothecaryPreparationImporter
    {
        private const string GameplayScene = "Assets/_Hollowfen/Scenes/Scene_Hollowfen.unity";
        private const string SourceScene = "Assets/Alchemy/Scenes/_Scene.unity";
        private const string DataRoot = "Assets/_Hollowfen/Data/Apothecary";
        private const string LocationPath =
            "Assets/_Hollowfen/Data/Locations/LocationData_TobinsApothecary.asset";
        private const string ArtRoot = "Assets/_Hollowfen/Art/Apothecary";
        private const string CompatibilityMaterialRoot = ArtRoot + "/Materials/URPCompatibility";
        private const string GeneratedArtRoot = ArtRoot + "/Generated";
        private const string ExteriorWallSurfaceMeshPath =
            GeneratedArtRoot + "/Mesh_ApothecaryExteriorWallSurface.asset";
        private const string ExteriorWallTrimMeshPath =
            GeneratedArtRoot + "/Mesh_ApothecaryExteriorWallTrim.asset";
        private const string PrefabPath = ArtRoot + "/PF_TobinApothecaryBuilding.prefab";
        private const string BuildingName = "TobinApothecaryBuilding";
        private const string RestorationRootName = "_LivingRestoration_tobin_workshop";
        private const string LocationMarkerName = "Location_TobinsApothecary";

        // A wooded terrace above the mill. The entrance faces back toward the mill yard.
        private static readonly Vector3 BuildingPosition = new Vector3(215f, 35.72f, 340f);
        private const float BuildingYaw = 141f;
        private static readonly string[] SourceRoomNames =
        {
            "Entrance_Room", "Hall_Room", "Open_Room", "Cage_Room",
        };

        private sealed class RecipeSpec
        {
            public string Id;
            public PreparationKind Kind;
            public string AssetName;
            public string[] SpeciesPaths;
            public int[] Amounts;
            public string[] Steps;
            public string RequiredFlag;
            public string UnlockHintId;
            public string ResultUseId;
            public int Knowledge;
        }

        private static readonly RecipeSpec[] Specs =
        {
            new RecipeSpec
            {
                Id = "field_ink", Kind = PreparationKind.Fieldwork,
                AssetName = "Preparation_FieldInk.asset",
                SpeciesPaths = new[]
                {
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_10_WoodEar.asset",
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_11_Pinecrest.asset",
                },
                Amounts = new[] { 1, 1 },
                Steps = new[]
                {
                    "apothecary.step.weigh", "apothecary.step.grind",
                    "apothecary.step.fold", "apothecary.step.bottle",
                },
                RequiredFlag = "apothecary_almy_lesson_seen",
                UnlockHintId = "apothecary.recipe.field_ink.unlock",
                ResultUseId = "apothecary.recipe.field_ink.next",
                Knowledge = 1,
            },
            new RecipeSpec
            {
                Id = "goldfoot_broth", Kind = PreparationKind.Pantry,
                AssetName = "Preparation_GoldfootBroth.asset",
                SpeciesPaths = new[]
                {
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_12_Goldfoot.asset",
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_10_WoodEar.asset",
                },
                Amounts = new[] { 1, 2 },
                Steps = new[]
                {
                    "apothecary.step.weigh", "apothecary.step.slice",
                    "apothecary.step.steep", "apothecary.step.bottle",
                },
                RequiredFlag = "apothecary_field_ink_delivered",
                UnlockHintId = "apothecary.recipe.goldfoot_broth.unlock",
                ResultUseId = "apothecary.recipe.goldfoot_broth.next",
                Knowledge = 1,
            },
            new RecipeSpec
            {
                Id = "brightspore_tonic", Kind = PreparationKind.VillageCare,
                AssetName = "Preparation_BrightsporeTonic.asset",
                SpeciesPaths = new[]
                {
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_16_Brightspore.asset",
                    "Assets/_Hollowfen/Data/Mushrooms/Mushroom_10_WoodEar.asset",
                },
                Amounts = new[] { 1, 1 },
                Steps = new[]
                {
                    "apothecary.step.weigh", "apothecary.step.grind",
                    "apothecary.step.strain", "apothecary.step.bottle",
                },
                RequiredFlag = "apothecary_goldfoot_delivered",
                UnlockHintId = "apothecary.recipe.brightspore_tonic.unlock",
                ResultUseId = "apothecary.recipe.brightspore_tonic.next",
                Knowledge = 1,
            },
        };

        [MenuItem("Hollowfen/Apothecary/Build Authored Apothecary Building")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        [MenuItem("Hollowfen/Apothecary/Repair Exterior Cutaway Wall")]
        public static void RepairExteriorMenu() => Debug.Log(RepairExteriorCutaway());

        public static string RepairExteriorCutaway()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) throw new InvalidOperationException("Missing apothecary prefab.");
            try
            {
                Transform architecture = FindDescendant(root.transform, "Hall_Room/Alchemy_Room");
                if (architecture == null)
                    throw new InvalidOperationException("Alchemy showcase architecture is missing.");
                AddExteriorSideWall(architecture);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save repaired apothecary prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "APOTHECARY EXTERIOR — REPAIRED: purchased-brick side shell closes the " +
                   "showcase cutaway and occludes the fireplace from outside";
        }

        public static string BuildAll()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new FileNotFoundException("The imported Alchemy showcase scene is missing", SourceScene);

            EnsureFolder(DataRoot);
            EnsureFolder(ArtRoot);
            EnsureFolder(CompatibilityMaterialRoot);
            var recipes = new PreparationRecipeData[Specs.Length];
            for (int i = 0; i < Specs.Length; i++) recipes[i] = UpsertRecipe(Specs[i]);
            var location = UpsertLocation();
            GameObject prefab = BuildScenePrefab(recipes);

            Scene active = SceneManager.GetActiveScene();
            if (active.path != GameplayScene)
            {
                if (active.IsValid() && active.isDirty)
                    throw new InvalidOperationException(
                        "Save the current Unity scene before installing Tobin's apothecary.");
                EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            }

            InstallIntoOpenScene(prefab, location);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return "APOTHECARY — BUILT: complete purchased laboratory building, cleared mill terrace, " +
                   "open entrance, three field-ledger recipes, and save-backed preparation loop";
        }

        public static void InstallIntoOpenScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var location = AssetDatabase.LoadAssetAtPath<LocationData>(LocationPath);
            if (prefab != null) InstallIntoOpenScene(prefab, location ?? UpsertLocation());
        }

        private static PreparationRecipeData UpsertRecipe(RecipeSpec spec)
        {
            string path = DataRoot + "/" + spec.AssetName;
            var recipe = AssetDatabase.LoadAssetAtPath<PreparationRecipeData>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<PreparationRecipeData>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            var ingredients = new MushroomFieldGuideData[spec.SpeciesPaths.Length];
            for (int i = 0; i < ingredients.Length; i++)
                ingredients[i] = Required<MushroomFieldGuideData>(spec.SpeciesPaths[i]);
            string root = "apothecary.recipe." + spec.Id;
            Set(recipe, "_id", spec.Id);
            Set(recipe, "_kind", spec.Kind);
            Set(recipe, "_titleId", root + ".title");
            Set(recipe, "_summaryId", root + ".summary");
            Set(recipe, "_resultId", "preparation." + spec.Id);
            Set(recipe, "_resultNameId", "apothecary.product." + spec.Id + ".name");
            Set(recipe, "_resultDescriptionId", "apothecary.product." + spec.Id + ".body");
            Set(recipe, "_ingredients", ingredients);
            Set(recipe, "_amounts", spec.Amounts);
            Set(recipe, "_stepIds", spec.Steps);
            Set(recipe, "_requiredFlagId", spec.RequiredFlag);
            Set(recipe, "_unlockHintId", spec.UnlockHintId);
            Set(recipe, "_resultUseId", spec.ResultUseId);
            Set(recipe, "_completionFlagId", "apothecary_prepared_" + spec.Id);
            Set(recipe, "_firstCraftKnowledge", spec.Knowledge);
            EditorUtility.SetDirty(recipe);
            return recipe;
        }

        private static LocationData UpsertLocation()
        {
            var location = AssetDatabase.LoadAssetAtPath<LocationData>(LocationPath);
            if (location == null)
            {
                location = ScriptableObject.CreateInstance<LocationData>();
                AssetDatabase.CreateAsset(location, LocationPath);
            }
            Set(location, "_id", "tobins_apothecary");
            Set(location, "_displayNameId", "loc.tobins_apothecary.name");
            Set(location, "_shortDescriptionId", "loc.tobins_apothecary.desc");
            Set(location, "_mapIcon", null);
            Set(location, "_discoveredByDefault", false);
            Set(location, "_regionId", "village");
            EditorUtility.SetDirty(location);
            return location;
        }

        private static GameObject BuildScenePrefab(PreparationRecipeData[] recipes)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Additive);
            GameObject root = null;
            try
            {
                SceneManager.SetActiveScene(source);
                root = new GameObject(BuildingName);
                foreach (string roomName in SourceRoomNames)
                {
                    GameObject sourceRoot = Array.Find(source.GetRootGameObjects(),
                        candidate => candidate.name == roomName);
                    if (sourceRoot == null)
                        throw new InvalidOperationException("Alchemy showcase is missing " + roomName);
                    GameObject clone = UnityEngine.Object.Instantiate(sourceRoot);
                    clone.name = roomName;
                    clone.transform.SetParent(root.transform, false);
                    clone.transform.localPosition = sourceRoot.transform.position;
                    clone.transform.localRotation = sourceRoot.transform.rotation;
                    clone.transform.localScale = sourceRoot.transform.localScale;
                }

                PrepareOpenDoors(root.transform);
                StripVendorRuntime(root);
                ConvertUnsupportedMaterials(root);
                ConfigureExteriorShell(root.transform);
                ConfigureRenderers(root);
                ConfigureInteriorLights(root.transform);
                AddPreparationInteraction(root, recipes);
                ApplyStaticFlags(root);
                ApothecaryInteractionImporter.ConfigureRoot(root);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save " + PrefabPath);
                OptimizeReferencedTextures();
                return saved;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (source.IsValid() && source.isLoaded)
                    EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void PrepareOpenDoors(Transform root)
        {
            Transform hallDoor = FindDescendant(root, "Hall_Room/Wood_Door");
            OpenDoubleDoor(hallDoor);
            Transform outerDoor = FindDescendant(root, "Entrance_Room/Locked_Door");
            OpenDoubleDoor(outerDoor);

            // The showcase controller moved the door leaves but relied on separate closed-door
            // blockers. Once the demo controller is stripped those blockers would remain across
            // the opening forever, so retain leaf collision and remove only the stale thresholds.
            if (outerDoor != null)
            {
                Transform blocker = outerDoor.Find("Box_Collider");
                if (blocker != null) UnityEngine.Object.DestroyImmediate(blocker.gameObject, true);
            }
            if (hallDoor != null)
                foreach (Collider collider in hallDoor.GetComponents<Collider>())
                    UnityEngine.Object.DestroyImmediate(collider, true);
        }

        private static void OpenDoubleDoor(Transform door)
        {
            if (door == null) return;
            Transform left = door.Find("Wood_Door_01");
            Transform right = door.Find("Wood_Door_02");
            if (left != null) left.localRotation = Quaternion.Euler(0f, 110f, 0f);
            if (right != null) right.localRotation = Quaternion.Euler(0f, -110f, 0f);
        }

        private static void StripVendorRuntime(GameObject root)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = components.Length - 1; i >= 0; i--)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is Renderer ||
                    component is MeshFilter || component is Collider || component is LODGroup ||
                    component is Animator || component is ParticleSystem || component is Light)
                    continue;
                if (component is AudioSource || component is AudioReverbZone ||
                    component is Rigidbody || component is MonoBehaviour)
                    UnityEngine.Object.DestroyImmediate(component, true);
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != null && collider.isTrigger)
                    UnityEngine.Object.DestroyImmediate(collider, true);
        }

        private static void ConfigureRenderers(GameObject root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.lightmapIndex = -1;
                renderer.realtimeLightmapIndex = -1;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderer.receiveShadows = true;
                if (!(renderer is ParticleSystemRenderer))
                    renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        private static void ConvertUnsupportedMaterials(GameObject root)
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
                throw new InvalidOperationException("URP particle shader is unavailable.");

            var conversions = new Dictionary<Material, Material>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || source.shader == null) continue;
                    string shaderName = source.shader.name;
                    if (shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                        shaderName.EndsWith("_URP", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Material converted;
                    if (!conversions.TryGetValue(source, out converted))
                    {
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                        string suffix = string.IsNullOrEmpty(guid) ? source.GetInstanceID().ToString() :
                            guid.Substring(0, Mathf.Min(8, guid.Length));
                        string filename = SanitizeFilename(source.name) + "_" + suffix + ".mat";
                        string destination = CompatibilityMaterialRoot + "/" + filename;
                        converted = AssetDatabase.LoadAssetAtPath<Material>(destination);
                        if (converted == null)
                        {
                            converted = new Material(particleShader) { name = source.name + " URP" };
                            AssetDatabase.CreateAsset(converted, destination);
                        }
                        else converted.shader = particleShader;

                        Texture texture = source.mainTexture;
                        Color tint = source.HasProperty("_Color") ? source.color : Color.white;
                        converted.SetTexture("_BaseMap", texture);
                        converted.SetColor("_BaseColor", tint);
                        if (texture != null)
                        {
                            converted.SetTextureScale("_BaseMap", source.mainTextureScale);
                            converted.SetTextureOffset("_BaseMap", source.mainTextureOffset);
                        }
                        bool additive = shaderName.IndexOf("Additive", StringComparison.OrdinalIgnoreCase) >= 0;
                        converted.SetFloat("_Surface", 1f);
                        converted.SetFloat("_Blend", additive ? 2f : 0f);
                        converted.SetFloat("_SrcBlend", additive ? (float)BlendMode.SrcAlpha :
                            (float)BlendMode.SrcAlpha);
                        converted.SetFloat("_DstBlend", additive ? (float)BlendMode.One :
                            (float)BlendMode.OneMinusSrcAlpha);
                        converted.SetFloat("_ZWrite", 0f);
                        converted.SetFloat("_Cull", (float)CullMode.Off);
                        converted.SetOverrideTag("RenderType", "Transparent");
                        converted.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        converted.renderQueue = (int)RenderQueue.Transparent;
                        EditorUtility.SetDirty(converted);
                        conversions.Add(source, converted);
                    }
                    materials[i] = converted;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private static void ConfigureExteriorShell(Transform root)
        {
            Transform architecture = FindDescendant(root, "Hall_Room/Alchemy_Room");
            if (architecture == null)
                throw new InvalidOperationException("Alchemy showcase architecture is missing.");

            var variants = new Dictionary<Material, Material>();
            foreach (Renderer renderer in architecture.GetComponentsInChildren<Renderer>(true))
            {
                // Floor tiles already contain their own visible top faces. The authored walls,
                // vault, pillars, and niche are showcase cutaways with inward-facing surfaces;
                // render those purchased meshes from both sides to give Hollowfen a real exterior.
                if (renderer.name.StartsWith("Floor_", StringComparison.Ordinal) ||
                    renderer.name == "Elements_Floor") continue;
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) continue;
                    Material variant;
                    if (!variants.TryGetValue(source, out variant))
                    {
                        string sourcePath = AssetDatabase.GetAssetPath(source);
                        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                        string suffix = string.IsNullOrEmpty(guid) ? source.GetInstanceID().ToString() :
                            guid.Substring(0, Mathf.Min(8, guid.Length));
                        string destination = CompatibilityMaterialRoot + "/" +
                            SanitizeFilename(source.name) + "_TwoSided_" + suffix + ".mat";
                        variant = AssetDatabase.LoadAssetAtPath<Material>(destination);
                        if (variant == null)
                        {
                            variant = new Material(source) { name = source.name + " Two Sided" };
                            AssetDatabase.CreateAsset(variant, destination);
                        }
                        else variant.CopyPropertiesFromMaterial(source);
                        variant.shader = source.shader;
                        if (variant.HasProperty("_Cull")) variant.SetFloat("_Cull", (float)CullMode.Off);
                        variant.doubleSidedGI = true;
                        EditorUtility.SetDirty(variant);
                        variants.Add(source, variant);
                    }
                    materials[i] = variant;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            AddExteriorSideWall(architecture);
        }

        private static void AddExteriorSideWall(Transform architecture)
        {
            Transform existing = architecture.Find("ExteriorSideWall");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            EnsureFolder(GeneratedArtRoot);
            var wall = new GameObject("ExteriorSideWall");
            wall.transform.SetParent(architecture, false);

            // The package's +X face is a dollhouse cutaway. Mirror its fully modelled -X face
            // across the room centre so the repair retains the purchased arches, brick topology,
            // UV density, materials, and silhouette instead of introducing a flat replacement.
            const float mirrorCentreX = -3.225f;
            AddMirroredPurchasedSection(wall.transform, architecture.Find("Walls"),
                "PurchasedWallSurface", ExteriorWallSurfaceMeshPath, mirrorCentreX);
            AddMirroredPurchasedSection(wall.transform, architecture.Find("Elements_Pillar_2"),
                "PurchasedWallTrim", ExteriorWallTrimMeshPath, mirrorCentreX);

            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(wall, flags);
            foreach (Transform child in wall.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
        }

        private static void AddMirroredPurchasedSection(Transform parent, Transform source,
            string name, string assetPath, float mirrorCentreX)
        {
            if (source == null) throw new InvalidOperationException("Missing purchased " + name);
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            Renderer sourceRenderer = source.GetComponent<Renderer>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null ||
                sourceRenderer.sharedMaterial == null)
                throw new InvalidOperationException("Incomplete purchased " + name);

            Mesh generated = BuildMirroredPurchasedMesh(source, sourceFilter.sharedMesh,
                parent.parent, mirrorCentreX, name);
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                saved = generated;
            }
            else
            {
                EditorUtility.CopySerialized(generated, saved);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(saved);
            }

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = saved;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            var collider = child.AddComponent<MeshCollider>();
            collider.sharedMesh = saved;
            collider.convex = false;
        }

        private static Mesh BuildMirroredPurchasedMesh(Transform source, Mesh sourceMesh,
            Transform architecture, float mirrorCentreX, string name)
        {
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector2[] sourceUvs = sourceMesh.uv;
            int[] sourceTriangles = sourceMesh.triangles;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                int ia = sourceTriangles[i];
                int ib = sourceTriangles[i + 1];
                int ic = sourceTriangles[i + 2];
                Vector3 a = architecture.InverseTransformPoint(
                    source.TransformPoint(sourceVertices[ia]));
                Vector3 b = architecture.InverseTransformPoint(
                    source.TransformPoint(sourceVertices[ib]));
                Vector3 c = architecture.InverseTransformPoint(
                    source.TransformPoint(sourceVertices[ic]));
                if (a.x >= -9.5f || b.x >= -9.5f || c.x >= -9.5f) continue;

                a.x = mirrorCentreX * 2f - a.x;
                b.x = mirrorCentreX * 2f - b.x;
                c.x = mirrorCentreX * 2f - c.x;
                int start = vertices.Count;
                // The source cutaway wall faces inward. Reflection turns that normal outward on
                // the new +X exterior, which is exactly the lighting/collision orientation needed.
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                uvs.Add(sourceUvs.Length > ia ? sourceUvs[ia] : Vector2.zero);
                uvs.Add(sourceUvs.Length > ib ? sourceUvs[ib] : Vector2.zero);
                uvs.Add(sourceUvs.Length > ic ? sourceUvs[ic] : Vector2.zero);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            if (triangles.Count == 0)
                throw new InvalidOperationException("Purchased " + name + " mirror selected no faces.");

            var mesh = new Mesh { name = "Apothecary " + name };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static string SanitizeFilename(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "AlchemyEffect" : value;
        }

        private static void ConfigureInteriorLights(Transform root)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
                UnityEngine.Object.DestroyImmediate(light, true);

            AddInteriorLight(root, "MainTableLight", new Vector3(0f, 2.65f, -2.15f),
                new Color(1f, .54f, .25f), 2.1f, 6.5f);
            AddInteriorLight(root, "EntranceLight", new Vector3(0f, 2.45f, 7.9f),
                new Color(1f, .61f, .31f), 1.55f, 5.5f);
            AddInteriorLight(root, "WestAlcoveLight", new Vector3(-5.8f, 2.1f, -.15f),
                new Color(.77f, .48f, .25f), 1.35f, 4.8f);
            AddInteriorLight(root, "CageAlcoveLight", new Vector3(-9.3f, 1.9f, .1f),
                new Color(.58f, .44f, .25f), .85f, 4.2f);
        }

        private static void AddInteriorLight(Transform parent, string name, Vector3 position,
            Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        private static void AddPreparationInteraction(GameObject root,
            PreparationRecipeData[] recipes)
        {
            var station = root.AddComponent<ApothecaryStation>();
            Set(station, "_recipes", recipes);
            Set(station, "_restorationProjectId", "tobin_workshop");
            Set(station, "_requiredStage", RestorationStage.Occupied);

            Transform table = FindDescendant(root.transform, "Hall_Room/Alchemy_Table");
            if (table == null) throw new InvalidOperationException("Alchemy table is missing from showcase");
            Transform scale = table.Find("Sacle");
            Transform mortar = table.Find("Mortar");
            Transform sandwatch = table.Find("Sandwatch");
            Transform bottle = table.Find("Gas_Jar (1)");
            Set(station, "_stepProps", new[] { scale, mortar, sandwatch, bottle });

            var shelfDisplay = root.AddComponent<ApothecaryShelfDisplay>();
            Set(shelfDisplay, "_recipes", recipes);
            Set(shelfDisplay, "_stockProps", new[]
            {
                FindDescendant(root.transform, "Hall_Room/Alchemy_Table/Shelf/Gas_Jar"),
                FindDescendant(root.transform, "Hall_Room/Alchemy_Table/Shelf/Square_Jar"),
                FindDescendant(root.transform, "Hall_Room/Alchemy_Table/Shelf/Conical_Flask"),
            });

            // The purchased showcase intentionally exposes sections of roof for presentation.
            // Mark its occupied footprint as cover without adding visible replacement geometry.
            var shelterObject = new GameObject("WeatherShelter");
            shelterObject.transform.SetParent(root.transform, false);
            shelterObject.transform.localPosition = new Vector3(-2.5f, 2.4f, 2.5f);
            var shelter = shelterObject.AddComponent<BoxCollider>();
            shelter.isTrigger = true;
            shelter.size = new Vector3(19f, 5.6f, 18f);
            shelterObject.AddComponent<WeatherShelterVolume>();

            var triggerObject = new GameObject("PreparationInteraction");
            triggerObject.transform.SetParent(table, false);
            int layer = LayerMask.NameToLayer("Foraging");
            if (layer >= 0) triggerObject.layer = layer;
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1.15f, 1.25f);
            trigger.size = new Vector3(4.1f, 2.35f, 2.45f);
        }

        private static void ApplyStaticFlags(GameObject root)
        {
            Transform table = FindDescendant(root.transform, "Hall_Room/Alchemy_Table");
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic |
                                      StaticEditorFlags.ReflectionProbeStatic;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root.transform || table != null &&
                    (transform == table || transform.IsChildOf(table))) continue;
                if (transform.GetComponentInParent<Animator>() != null ||
                    transform.GetComponentInParent<ParticleSystem>() != null) continue;
                if (transform.GetComponent<Renderer>() != null || transform.GetComponent<Collider>() != null)
                    GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
            }
        }

        private static void OptimizeReferencedTextures()
        {
            foreach (string path in AssetDatabase.GetDependencies(PrefabPath, true))
            {
                if (!path.StartsWith("Assets/Alchemy/Textures/", StringComparison.Ordinal)) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, 2048);
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.crunchedCompression = false;
                AssetDatabase.WriteImportSettingsIfDirty(path);
            }
        }

        private static void InstallIntoOpenScene(GameObject prefab, LocationData location)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != GameplayScene)
                throw new InvalidOperationException("Scene_Hollowfen must be open before installation.");

            GameObject restoration = FindSceneObject(RestorationRootName);
            if (restoration == null)
                throw new InvalidOperationException("Tobin's restoration project must be authored first.");
            Transform restored = restoration.transform.Find("Restored");
            Transform occupied = restoration.transform.Find("Occupied");
            if (restored == null || occupied == null)
                throw new InvalidOperationException("Tobin's restoration stage roots are missing.");

            ClearChildren(restored);
            ClearChildren(occupied);
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate != null && candidate.scene == scene &&
                    (candidate.name == "HollowfenApothecaryStation" ||
                     candidate.name == BuildingName ||
                     candidate.name == LocationMarkerName))
                    UnityEngine.Object.DestroyImmediate(candidate);

            // The architecture is a permanent landmark. Restoration controls access, crew
            // dressing, the reveal, and the preparation station's usable stage; it must never
            // make the complete building disappear for a fresh or partially progressed save.
            var instance = PrefabUtility.InstantiatePrefab(prefab, restoration.transform) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not place the apothecary building.");
            instance.name = BuildingName;
            instance.transform.SetPositionAndRotation(BuildingPosition,
                Quaternion.Euler(0f, BuildingYaw, 0f));
            instance.transform.localScale = Vector3.one;

            var markerObject = new GameObject(LocationMarkerName);
            markerObject.transform.SetParent(restoration.transform, true);
            markerObject.transform.position = instance.transform.TransformPoint(new Vector3(0f, 0f, 10.8f));
            var marker = markerObject.AddComponent<LocationMarker>();
            Set(marker, "_data", location);
            Set(marker, "_discoverRadius", 24f);

            FlattenAndClearParcel();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void FlattenAndClearParcel()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null) return;
            TerrainData data = terrain.terrainData;
            const float innerX = 12.4f;
            const float innerZ = 14.2f;
            const float falloff = 4.5f;
            Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0f, BuildingYaw, 0f));

            float centralMin = float.MaxValue;
            float centralMax = float.MinValue;
            for (float x = -innerX; x <= innerX; x += 4f)
                for (float z = -innerZ; z <= innerZ; z += 4f)
                {
                    Vector3 sample = BuildingPosition + Quaternion.Euler(0f, BuildingYaw, 0f) *
                        new Vector3(x, 0f, z);
                    float height = terrain.SampleHeight(sample) + terrain.transform.position.y;
                    centralMin = Mathf.Min(centralMin, height);
                    centralMax = Mathf.Max(centralMax, height);
                }

            if (centralMax - centralMin > .12f)
            {
                Undo.RegisterCompleteObjectUndo(data, "Level Tobin apothecary parcel");
                int resolution = data.heightmapResolution;
                float radius = Mathf.Sqrt(Mathf.Pow(innerX + falloff, 2f) +
                                          Mathf.Pow(innerZ + falloff, 2f));
                int xMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.x - radius -
                    terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.x + radius -
                    terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
                int zMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.z - radius -
                    terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
                int zMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.z + radius -
                    terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
                int width = xMax - xMin + 1;
                int height = zMax - zMin + 1;
                float[,] heights = data.GetHeights(xMin, zMin, width, height);
                float target = (BuildingPosition.y - terrain.transform.position.y) / data.size.y;
                for (int z = 0; z < height; z++)
                    for (int x = 0; x < width; x++)
                    {
                        float worldX = terrain.transform.position.x +
                            (xMin + x) / (float)(resolution - 1) * data.size.x;
                        float worldZ = terrain.transform.position.z +
                            (zMin + z) / (float)(resolution - 1) * data.size.z;
                        Vector3 local = inverse * new Vector3(worldX - BuildingPosition.x, 0f,
                            worldZ - BuildingPosition.z);
                        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - innerX);
                        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - innerZ);
                        float distance = Mathf.Sqrt(dx * dx + dz * dz);
                        if (distance > falloff) continue;
                        float weight = Mathf.Clamp01(1f - distance / falloff);
                        weight = weight * weight * (3f - 2f * weight);
                        heights[z, x] = Mathf.Lerp(heights[z, x], target, weight);
                    }
                data.SetHeights(xMin, zMin, heights);
            }

            SculptEntranceApproach(data, terrain, inverse);

            var keptTrees = new List<TreeInstance>();
            foreach (TreeInstance tree in data.treeInstances)
            {
                Vector3 world = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
                Vector3 local = inverse * (world - BuildingPosition);
                bool insideParcel = Mathf.Abs(local.x) <= innerX + 3f &&
                                    Mathf.Abs(local.z) <= innerZ + 3f;
                bool insideApproach = Mathf.Abs(local.x) <= 7.5f &&
                                      local.z >= innerZ - 1f && local.z <= 31f;
                if (insideParcel || insideApproach)
                    continue;
                keptTrees.Add(tree);
            }
            if (keptTrees.Count != data.treeInstanceCount)
            {
                Undo.RegisterCompleteObjectUndo(data, "Clear Tobin apothecary trees");
                data.treeInstances = keptTrees.ToArray();
            }

            ClearDetails(data, terrain, inverse, innerX + 1.5f, innerZ + 1.5f);
            ClearApproachDetails(data, terrain, inverse, innerZ - 1f, 31f, 6.5f);
            EditorUtility.SetDirty(data);
            terrain.Flush();
        }

        private static void SculptEntranceApproach(TerrainData data, Terrain terrain,
            Quaternion inverse)
        {
            const float startZ = 12f;
            const float endZ = 30f;
            const float startHalfWidth = 6f;
            const float endHalfWidth = 4f;
            const float sideFalloff = 2.5f;
            int resolution = data.heightmapResolution;
            float radius = endZ + sideFalloff + 1f;
            int xMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.x - radius -
                terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.x + radius -
                terrain.transform.position.x) / data.size.x * (resolution - 1)), 0, resolution - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.z - radius -
                terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.z + radius -
                terrain.transform.position.z) / data.size.z * (resolution - 1)), 0, resolution - 1);
            int width = xMax - xMin + 1;
            int height = zMax - zMin + 1;
            float[,] heights = data.GetHeights(xMin, zMin, width, height);
            Quaternion rotation = Quaternion.Euler(0f, BuildingYaw, 0f);
            Vector3 endWorld = BuildingPosition + rotation * new Vector3(0f, 0f, endZ);
            float endHeight = terrain.SampleHeight(endWorld) + terrain.transform.position.y;
            float startNormalized = (BuildingPosition.y - terrain.transform.position.y) / data.size.y;
            float endNormalized = (endHeight - terrain.transform.position.y) / data.size.y;
            bool changed = false;
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    float worldX = terrain.transform.position.x +
                        (xMin + x) / (float)(resolution - 1) * data.size.x;
                    float worldZ = terrain.transform.position.z +
                        (zMin + z) / (float)(resolution - 1) * data.size.z;
                    Vector3 local = inverse * new Vector3(worldX - BuildingPosition.x, 0f,
                        worldZ - BuildingPosition.z);
                    if (local.z < startZ || local.z > endZ) continue;
                    float progress = Mathf.InverseLerp(startZ, endZ, local.z);
                    float eased = progress * progress * (3f - 2f * progress);
                    float halfWidth = Mathf.Lerp(startHalfWidth, endHalfWidth, progress);
                    float sideDistance = Mathf.Max(0f, Mathf.Abs(local.x) - halfWidth);
                    if (sideDistance > sideFalloff) continue;
                    float weight = Mathf.Clamp01(1f - sideDistance / sideFalloff);
                    weight = weight * weight * (3f - 2f * weight);
                    float desired = Mathf.Lerp(startNormalized, endNormalized, eased);
                    float next = Mathf.Lerp(heights[z, x], desired, weight);
                    if (Mathf.Abs(next - heights[z, x]) <= .00001f) continue;
                    heights[z, x] = next;
                    changed = true;
                }
            if (!changed) return;
            Undo.RegisterCompleteObjectUndo(data, "Shape Tobin apothecary approach");
            data.SetHeights(xMin, zMin, heights);
        }

        private static void ClearDetails(TerrainData data, Terrain terrain, Quaternion inverse,
            float halfX, float halfZ)
        {
            int resolution = data.detailResolution;
            if (resolution <= 0 || data.detailPrototypes.Length == 0) return;
            float radius = Mathf.Sqrt(halfX * halfX + halfZ * halfZ);
            int xMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.x - radius -
                terrain.transform.position.x) / data.size.x * resolution), 0, resolution - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.x + radius -
                terrain.transform.position.x) / data.size.x * resolution), 0, resolution - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.z - radius -
                terrain.transform.position.z) / data.size.z * resolution), 0, resolution - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.z + radius -
                terrain.transform.position.z) / data.size.z * resolution), 0, resolution - 1);
            int width = xMax - xMin + 1;
            int height = zMax - zMin + 1;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] details = data.GetDetailLayer(xMin, zMin, width, height, layer);
                bool changed = false;
                for (int z = 0; z < height; z++)
                    for (int x = 0; x < width; x++)
                    {
                        if (details[z, x] == 0) continue;
                        float worldX = terrain.transform.position.x +
                            (xMin + x + .5f) / resolution * data.size.x;
                        float worldZ = terrain.transform.position.z +
                            (zMin + z + .5f) / resolution * data.size.z;
                        Vector3 local = inverse * new Vector3(worldX - BuildingPosition.x, 0f,
                            worldZ - BuildingPosition.z);
                        if (Mathf.Abs(local.x) > halfX || Mathf.Abs(local.z) > halfZ) continue;
                        details[z, x] = 0;
                        changed = true;
                    }
                if (changed) data.SetDetailLayer(xMin, zMin, layer, details);
            }
        }

        private static void ClearApproachDetails(TerrainData data, Terrain terrain,
            Quaternion inverse, float startZ, float endZ, float halfWidth)
        {
            int resolution = data.detailResolution;
            if (resolution <= 0 || data.detailPrototypes.Length == 0) return;
            float radius = endZ + halfWidth;
            int xMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.x - radius -
                terrain.transform.position.x) / data.size.x * resolution), 0, resolution - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.x + radius -
                terrain.transform.position.x) / data.size.x * resolution), 0, resolution - 1);
            int zMin = Mathf.Clamp(Mathf.FloorToInt((BuildingPosition.z - radius -
                terrain.transform.position.z) / data.size.z * resolution), 0, resolution - 1);
            int zMax = Mathf.Clamp(Mathf.CeilToInt((BuildingPosition.z + radius -
                terrain.transform.position.z) / data.size.z * resolution), 0, resolution - 1);
            int width = xMax - xMin + 1;
            int height = zMax - zMin + 1;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] details = data.GetDetailLayer(xMin, zMin, width, height, layer);
                bool changed = false;
                for (int z = 0; z < height; z++)
                    for (int x = 0; x < width; x++)
                    {
                        if (details[z, x] == 0) continue;
                        float worldX = terrain.transform.position.x +
                            (xMin + x + .5f) / resolution * data.size.x;
                        float worldZ = terrain.transform.position.z +
                            (zMin + z + .5f) / resolution * data.size.z;
                        Vector3 local = inverse * new Vector3(worldX - BuildingPosition.x, 0f,
                            worldZ - BuildingPosition.z);
                        if (local.z < startZ || local.z > endZ ||
                            Mathf.Abs(local.x) > halfWidth) continue;
                        details[z, x] = 0;
                        changed = true;
                    }
                if (changed) data.SetDetailLayer(xMin, zMin, layer, details);
            }
        }

        private static Transform FindDescendant(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            return root.Find(path);
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate != null && candidate.name == name && candidate.scene.IsValid()) return candidate;
            return null;
        }

        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException("Required asset is missing", path);
            return asset;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
