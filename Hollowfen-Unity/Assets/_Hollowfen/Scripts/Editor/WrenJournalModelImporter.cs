using System;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Idempotently turns the optimized Wren derivative into a visual-only
    /// journal prefab and wires it to the character profile.
    /// </summary>
    public static class WrenJournalModelImporter
    {
        public const int TriangleBudget = 90000;
        public const string ModelPath = "Assets/_Hollowfen/Models/Characters/Wren/Generated/Wren_Journal.fbx";
        public const string PrefabPath = "Assets/_Hollowfen/Prefabs/Journal/Characters/WrenJournalPreview.prefab";

        private const string GeneratedRoot = "Assets/_Hollowfen/Models/Characters/Wren/Generated";
        private const string PrefabRoot = "Assets/_Hollowfen/Prefabs/Journal/Characters";
        private const string AlbedoPath = GeneratedRoot + "/Wren_Journal_Albedo.png";
        private const string NormalPath = GeneratedRoot + "/Wren_Journal_Normal.png";
        private const string EmissionPath = GeneratedRoot + "/Wren_Journal_Emission.png";
        private const string MaterialPath = GeneratedRoot + "/M_Wren_Journal.mat";
        private const string ProfilePath = "Assets/_Hollowfen/Data/Characters/Character_WrenTobin.asset";
        private const string IdlePath = "Assets/_Hollowfen/Animation/Wren/Mixamo/Wren_BreathingIdle.fbx";
        private const float JournalExposure = 0.15f;

        [MenuItem("Hollowfen/Assets/Rebuild Wren Journal Model")]
        private static void RebuildMenu()
        {
            Debug.Log(RebuildAll());
        }

        public static string RebuildAll()
        {
            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(PrefabRoot);
            RequireGeneratedFile(ModelPath);
            RequireGeneratedFile(AlbedoPath);
            RequireGeneratedFile(NormalPath);
            RequireGeneratedFile(EmissionPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureModelImporter();
            ConfigureTextureImporter(AlbedoPath, false, 2048);
            ConfigureTextureImporter(NormalPath, true, 2048);
            ConfigureTextureImporter(EmissionPath, false, 1024);
            Material material = BuildMaterial();
            GameObject prefab = BuildPrefab(material);
            WireProfile(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "[WrenJournalModelImporter] Built visual-only 90k-triangle Wren preview and wired Character_WrenTobin.";
        }

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No ModelImporter for " + ModelPath);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.isReadable = false;
            importer.weldVertices = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter(string path, bool normalMap, int maxSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter for " + path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_Wren_Journal" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_EmissionMap", emission);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetColor("_EmissionColor", new Color(0.18f, 0.18f, 0.18f, 1f));
            material.SetFloat("_BumpScale", 0.82f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.20f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_EMISSION");
            material.enableInstancing = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildPrefab(Material material)
        {
            bool loadedPrefab = File.Exists(Path.GetFullPath(PrefabPath));
            GameObject root = loadedPrefab
                ? PrefabUtility.LoadPrefabContents(PrefabPath)
                : new GameObject();
            try
            {
                root.name = "WrenJournalPreview";
                root.tag = "Untagged";
                root.layer = 0;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                while (root.transform.childCount > 0)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                foreach (Component component in root.GetComponents<Component>())
                    if (!(component is Transform)) UnityEngine.Object.DestroyImmediate(component);

                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (source == null) throw new InvalidOperationException("Could not load " + ModelPath);
                GameObject visual = UnityEngine.Object.Instantiate(source, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < assigned.Length; i++) assigned[i] = material;
                    renderer.sharedMaterials = assigned;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save " + PrefabPath);
                return saved;
            }
            finally
            {
                if (loadedPrefab) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireProfile(GameObject prefab)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterProfileData>(ProfilePath);
            if (profile == null) throw new InvalidOperationException("Missing Wren profile at " + ProfilePath);
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IdlePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name == "Wren_BreathingIdle");
            if (idle == null) throw new InvalidOperationException("Missing Wren_BreathingIdle clip at " + IdlePath);

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_journalModelPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("_journalIdleClip").objectReferenceValue = idle;
            serialized.FindProperty("_journalExposure").floatValue = JournalExposure;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }

        private static void RequireGeneratedFile(string assetPath)
        {
            if (!File.Exists(Path.GetFullPath(assetPath)))
                throw new FileNotFoundException(
                    "Missing " + assetPath + ". Run tools/optimize_wren_journal_model.py through Blender first.",
                    Path.GetFullPath(assetPath));
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
    }
}
