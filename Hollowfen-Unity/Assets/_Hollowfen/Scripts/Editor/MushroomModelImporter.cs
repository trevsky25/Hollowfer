using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using Hollowfen.Foraging;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Idempotently turns the optimized Meshy derivatives into shipping materials,
    /// world prefabs, journal prefabs, and MushroomFieldGuideData references.
    /// The Blender optimizer and this importer share one JSON manifest.
    /// </summary>
    public static class MushroomModelImporter
    {
        private const string ManifestPath = "Assets/_Hollowfen/Models/Mushrooms/MushroomModelManifest.json";
        private const string GeneratedRoot = "Assets/_Hollowfen/Models/Mushrooms/Generated";
        private const string WorldPrefabRoot = "Assets/_Hollowfen/Prefabs/Foraging";
        private const string JournalPrefabRoot = "Assets/_Hollowfen/Prefabs/Journal/Mushrooms";
        private const string DatabasePath = "Assets/_Hollowfen/Resources/MushroomFieldGuideDatabase.asset";

        [Serializable]
        private sealed class Manifest
        {
            public ModelEntry[] models;
            public SpeciesEntry[] species;
        }

        [Serializable]
        private sealed class ModelEntry
        {
            public string key;
            public bool grounded;
            public string scaleAxis;
            public float worldSize;
            public float journalSize;
            public float emissionStrength;
            public float journalExposure;
        }

        [Serializable]
        private sealed class SpeciesEntry
        {
            public string id;
            public string prefabName;
            public string model;
        }

        private enum TextureKind
        {
            Albedo,
            Normal,
            Data,
            Emission,
            Mask
        }

        [MenuItem("Hollowfen/Assets/Rebuild Mushroom Models")]
        private static void RebuildMenu()
        {
            Debug.Log(RebuildAll());
        }

        public static string RebuildAll()
        {
            Manifest manifest = LoadManifest();
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(WorldPrefabRoot);
            EnsureAssetFolder(JournalPrefabRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var modelLookup = manifest.models.ToDictionary(model => model.key, StringComparer.Ordinal);
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (ModelEntry model in manifest.models)
            {
                ValidateGeneratedModel(model);
                ConfigureModelImporter(WorldModelPath(model.key), ModelImporterMeshCompression.Medium);
                ConfigureModelImporter(JournalModelPath(model.key), ModelImporterMeshCompression.Low);
                ConfigureTextureImporter(TexturePath(model.key, "Albedo"), TextureKind.Albedo);
                ConfigureTextureImporter(TexturePath(model.key, "Normal"), TextureKind.Normal);
                ConfigureTextureImporter(TexturePath(model.key, "Metallic"), TextureKind.Data);
                ConfigureTextureImporter(TexturePath(model.key, "Roughness"), TextureKind.Data);
                ConfigureTextureImporter(TexturePath(model.key, "Emission"), TextureKind.Emission);
                BuildMaskMap(model.key);
                materials.Add(model.key, BuildMaterial(model));
            }

            var database = AssetDatabase.LoadAssetAtPath<MushroomFieldGuideDatabase>(DatabasePath);
            if (database == null) throw new InvalidOperationException("Missing mushroom database at " + DatabasePath);
            var dataLookup = database.Entries.Where(entry => entry != null)
                .ToDictionary(entry => entry.Id, StringComparer.Ordinal);

            int wired = 0;
            foreach (SpeciesEntry species in manifest.species)
            {
                ModelEntry model;
                MushroomFieldGuideData data;
                if (!modelLookup.TryGetValue(species.model, out model))
                    throw new InvalidOperationException(species.id + " references unknown model " + species.model);
                if (!dataLookup.TryGetValue(species.id, out data))
                    throw new InvalidOperationException("Database is missing mushroom id " + species.id);

                string worldPath = WorldPrefabPath(species);
                string journalPath = JournalPrefabPath(species);
                BuildPrefab(worldPath, species, model, data, materials[model.key], true);
                BuildPrefab(journalPath, species, model, data, materials[model.key], false);

                var serializedData = new SerializedObject(data);
                serializedData.FindProperty("_worldPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(worldPath);
                serializedData.FindProperty("_journalPreviewPrefab").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameObject>(journalPath);
                serializedData.FindProperty("_journalExposure").floatValue = JournalExposure(model);
                serializedData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                wired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return string.Format(
                "[MushroomModelImporter] Built {0} source model sets and wired {1} species. Aldermark remains pending its Maitake model.",
                manifest.models.Length,
                wired);
        }

        public static bool HasModelForSpecies(string id)
        {
            return FindSpecies(LoadManifest(), id) != null;
        }

        public static string WorldPrefabPathForSpecies(string id)
        {
            SpeciesEntry species = FindSpecies(LoadManifest(), id);
            return species != null ? WorldPrefabPath(species) : null;
        }

        public static string JournalPrefabPathForSpecies(string id)
        {
            SpeciesEntry species = FindSpecies(LoadManifest(), id);
            return species != null ? JournalPrefabPath(species) : null;
        }

        public static float JournalExposureForSpecies(string id)
        {
            Manifest manifest = LoadManifest();
            SpeciesEntry species = FindSpecies(manifest, id);
            if (species == null) return 0.42f;
            ModelEntry model = manifest.models.FirstOrDefault(candidate =>
                string.Equals(candidate.key, species.model, StringComparison.Ordinal));
            return model != null ? JournalExposure(model) : 0.42f;
        }

        private static float JournalExposure(ModelEntry model)
        {
            return Mathf.Clamp(model.journalExposure > 0f ? model.journalExposure : 0.42f, 0.15f, 1.10f);
        }

        private static SpeciesEntry FindSpecies(Manifest manifest, string id)
        {
            return manifest.species.FirstOrDefault(species => string.Equals(species.id, id, StringComparison.Ordinal));
        }

        private static Manifest LoadManifest()
        {
            string fullPath = FullPath(ManifestPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Missing mushroom model manifest", fullPath);
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(fullPath));
            if (manifest == null || manifest.models == null || manifest.species == null)
                throw new InvalidOperationException("Could not parse " + ManifestPath);
            return manifest;
        }

        private static void ValidateGeneratedModel(ModelEntry model)
        {
            string[] required =
            {
                WorldModelPath(model.key),
                JournalModelPath(model.key),
                TexturePath(model.key, "Albedo"),
                TexturePath(model.key, "Normal"),
                TexturePath(model.key, "Metallic"),
                TexturePath(model.key, "Roughness"),
                TexturePath(model.key, "Emission")
            };
            foreach (string path in required)
                if (!File.Exists(FullPath(path)))
                    throw new FileNotFoundException(
                        "Missing generated asset " + path + ". Run tools/optimize_mushroom_models.py through Blender first.",
                        FullPath(path));
        }

        private static void ConfigureModelImporter(string path, ModelImporterMeshCompression compression)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No ModelImporter for " + path);

            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = compression;
            importer.isReadable = false;
            importer.weldVertices = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string path, TextureKind kind)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter for " + path);

            importer.textureType = kind == TextureKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = kind == TextureKind.Albedo || kind == TextureKind.Emission;
            importer.alphaSource = kind == TextureKind.Mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = kind == TextureKind.Data ? 512 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void BuildMaskMap(string key)
        {
            string metallicPath = TexturePath(key, "Metallic");
            string roughnessPath = TexturePath(key, "Roughness");
            string maskPath = TexturePath(key, "MaskMap");

            Texture2D metallic = LoadPng(metallicPath);
            Texture2D roughness = LoadPng(roughnessPath);
            if (metallic.width != roughness.width || metallic.height != roughness.height)
                throw new InvalidOperationException(key + " metallic and roughness texture dimensions differ");

            Color32[] metallicPixels = metallic.GetPixels32();
            Color32[] roughnessPixels = roughness.GetPixels32();
            var packedPixels = new Color32[metallicPixels.Length];
            for (int i = 0; i < packedPixels.Length; i++)
            {
                byte metallicValue = metallicPixels[i].r;
                byte smoothness = (byte)(255 - roughnessPixels[i].r);
                packedPixels[i] = new Color32(metallicValue, 255, 255, smoothness);
            }

            var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(packedPixels);
            packed.Apply(false, false);
            File.WriteAllBytes(FullPath(maskPath), packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(metallic);
            UnityEngine.Object.DestroyImmediate(roughness);
            UnityEngine.Object.DestroyImmediate(packed);
            ConfigureTextureImporter(maskPath, TextureKind.Mask);
        }

        private static Texture2D LoadPng(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(FullPath(assetPath)), false))
                throw new InvalidOperationException("Could not decode " + assetPath);
            return texture;
        }

        private static Material BuildMaterial(ModelEntry model)
        {
            string path = MaterialPath(model.key);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable");
            if (material == null)
            {
                material = new Material(shader) { name = "M_Mushroom_" + model.key };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(model.key, "Albedo"));
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(model.key, "Normal"));
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(model.key, "MaskMap"));
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(model.key, "Emission"));
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetColor("_EmissionColor", new Color(
                model.emissionStrength,
                model.emissionStrength,
                model.emissionStrength,
                1f));
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_EMISSION");
            material.enableInstancing = true;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPrefab(
            string path,
            SpeciesEntry species,
            ModelEntry model,
            MushroomFieldGuideData data,
            Material material,
            bool world)
        {
            bool exists = File.Exists(FullPath(path));
            GameObject root = exists
                ? PrefabUtility.LoadPrefabContents(path)
                : new GameObject();
            try
            {
                root.name = (world ? "MushroomWorld_" : "MushroomJournal_") + species.prefabName;
                root.tag = "Untagged";
                root.layer = world ? RequireLayer("Foraging") : 0;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                while (root.transform.childCount > 0)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                foreach (Collider collider in root.GetComponents<Collider>())
                    UnityEngine.Object.DestroyImmediate(collider);

                MushroomNode node = root.GetComponent<MushroomNode>();
                if (world)
                {
                    if (node == null) node = root.AddComponent<MushroomNode>();
                    var serializedNode = new SerializedObject(node);
                    serializedNode.FindProperty("_data").objectReferenceValue = data;
                    serializedNode.FindProperty("_respawnSeconds").floatValue = 0f;
                    serializedNode.ApplyModifiedPropertiesWithoutUndo();
                }
                else if (node != null)
                {
                    UnityEngine.Object.DestroyImmediate(node);
                }

                string sourcePath = world ? WorldModelPath(model.key) : JournalModelPath(model.key);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (source == null) throw new InvalidOperationException("Could not load " + sourcePath);
                GameObject visual = UnityEngine.Object.Instantiate(source, root.transform);
                visual.name = "Visual";
                SetLayerRecursively(visual, 0);
                AssignMaterial(visual, material);
                Bounds bounds = NormalizeVisual(
                    visual,
                    root.transform,
                    world ? model.worldSize : model.journalSize,
                    model.grounded,
                    model.scaleAxis);

                if (world)
                {
                    var trigger = root.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;
                    trigger.center = root.transform.InverseTransformPoint(bounds.center);
                    trigger.radius = 0.72f;

                    var collision = new GameObject("Collision");
                    collision.layer = 0;
                    collision.transform.SetParent(root.transform, false);
                    var box = collision.AddComponent<BoxCollider>();
                    box.center = root.transform.InverseTransformPoint(bounds.center);
                    box.size = new Vector3(
                        Mathf.Max(0.04f, bounds.size.x),
                        Mathf.Max(0.04f, bounds.size.y),
                        Mathf.Max(0.04f, bounds.size.z));
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Bounds NormalizeVisual(GameObject visual, Transform root, float targetSize, bool grounded, string scaleAxis)
        {
            Bounds bounds = RendererBounds(visual);
            bool scaleByMaximumExtent = !grounded || string.Equals(scaleAxis, "max", StringComparison.OrdinalIgnoreCase);
            float referenceSize = scaleByMaximumExtent
                ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
                : bounds.size.y;
            if (referenceSize <= 0.00001f) throw new InvalidOperationException(visual.name + " has zero-sized renderer bounds");

            visual.transform.localScale *= targetSize / referenceSize;
            bounds = RendererBounds(visual);
            Vector3 anchor = new Vector3(bounds.center.x, grounded ? bounds.min.y : bounds.center.y, bounds.center.z);
            visual.transform.position += root.position - anchor;
            return RendererBounds(visual);
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " contains no renderers");
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var assigned = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < assigned.Length; i++) assigned[i] = material;
                renderer.sharedMaterials = assigned;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }

        private static int RequireLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) throw new InvalidOperationException("Missing required layer " + layerName);
            return layer;
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string WorldModelPath(string key)
        {
            return string.Format("{0}/{1}/Mushroom_{1}_World.fbx", GeneratedRoot, key);
        }

        private static string JournalModelPath(string key)
        {
            return string.Format("{0}/{1}/Mushroom_{1}_Journal.fbx", GeneratedRoot, key);
        }

        private static string TexturePath(string key, string suffix)
        {
            return string.Format("{0}/{1}/Mushroom_{1}_{2}.png", GeneratedRoot, key, suffix);
        }

        private static string MaterialPath(string key)
        {
            return string.Format("{0}/{1}/M_Mushroom_{1}.mat", GeneratedRoot, key);
        }

        private static string WorldPrefabPath(SpeciesEntry species)
        {
            return WorldPrefabRoot + "/MushroomWorld_" + species.prefabName + ".prefab";
        }

        private static string JournalPrefabPath(SpeciesEntry species)
        {
            return JournalPrefabRoot + "/MushroomJournal_" + species.prefabName + ".prefab";
        }

        private static string FullPath(string assetPath)
        {
            return Path.GetFullPath(assetPath);
        }
    }
}
