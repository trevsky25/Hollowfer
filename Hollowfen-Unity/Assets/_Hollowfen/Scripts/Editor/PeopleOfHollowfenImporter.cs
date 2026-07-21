using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hollowfen.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Imports the production People of Hollowfen manifest into lightweight
    /// profile assets, lazy-loaded gallery art, and visual-only preview prefabs.
    /// The Blender optimization pass must run before this importer.
    /// </summary>
    public static class PeopleOfHollowfenImporter
    {
        public const string ManifestRelativePath = "tools/people_of_hollowfen_manifest.json";
        public const string DatabasePath = "Assets/_Hollowfen/Resources/PeopleOfHollowfenDatabase.asset";

        private const string GeneratedRoot = "Assets/_Hollowfen/Models/Characters/People/Generated";
        private const string MaterialNamePrefix = "M_";
        private const string ProfileRoot = "Assets/_Hollowfen/Data/Characters/People";
        private const string ResourceArtRoot = "Assets/_Hollowfen/Resources/People/Art";
        private const string ResourceModelRoot = "Assets/_Hollowfen/Resources/People/Models";
        private const string WrenId = "wren-tobin";
        private const string WrenProfilePath = "Assets/_Hollowfen/Data/Characters/Character_WrenTobin.asset";
        private const float DefaultJournalExposure = 0.15f;

        [Serializable]
        private sealed class ManifestEnvelope
        {
            public ManifestCharacter[] people;
        }

        [Serializable]
        private sealed class ManifestCharacter
        {
            public string id;
            public string displayName;
            public string category;
            public string edition;
            public string role;
            public string age;
            public string home;
            public string work;
            public string keepsake;
            public string tagline;
            public string leadParagraph;
            public string backgroundParagraph;
            public string perspectiveParagraph;
            public string pullquote;
            public ManifestMission[] missions;
            public ManifestQuote[] quotes;
            public string modelSource;
            public string albedoSource;
            public string normalSource;
            public string artSource;
            public string heroArtSource;
            public int targetTriangles;
            public int textureSize;
            public float previewScale;
        }

        [Serializable]
        private sealed class ManifestMission
        {
            public int act;
            public string questId;
            public string title;
            public string summary;
        }

        [Serializable]
        private sealed class ManifestQuote
        {
            public string text;
            public string context;
        }

        private sealed class ImportResult
        {
            public CharacterProfileData Profile;
            public long TriangleCount;
        }

        [MenuItem("Hollowfen/Assets/Rebuild People of Hollowfen")]
        private static void RebuildMenu()
        {
            Debug.Log(RebuildAll());
        }

        /// <summary>
        /// Unity batch entrypoint. Example:
        /// -batchmode -quit -executeMethod Hollowfen.EditorTools.PeopleOfHollowfenImporter.BatchRebuild
        /// </summary>
        public static void BatchRebuild()
        {
            try
            {
                Debug.Log(RebuildAll());
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string RebuildAll()
        {
            string manifestPath = GetManifestAbsolutePath();
            ManifestCharacter[] people = LoadAndValidateManifest(manifestPath);

            EnsureAssetFolder(GeneratedRoot);
            EnsureAssetFolder(ProfileRoot);
            EnsureAssetFolder(ResourceArtRoot);
            EnsureAssetFolder(ResourceModelRoot);
            EnsureAssetFolder(Path.GetDirectoryName(DatabasePath)?.Replace('\\', '/'));

            // Generated model files come from Blender; source art is copied by this importer.
            foreach (ManifestCharacter person in people)
            {
                RequireGeneratedFile(ModelPath(person));
                RequireGeneratedFile(AlbedoPath(person));
                RequireGeneratedFile(NormalPath(person));
            }
            foreach (ManifestCharacter person in people) CopyGalleryArt(person);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var results = new List<ImportResult>(people.Length);
            for (int index = 0; index < people.Length; index++)
                results.Add(ImportCharacter(people[index], index));

            CharacterProfileData[] profiles = results.Select(result => result.Profile).ToArray();
            BuildDatabase(profiles);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            long totalTriangles = results.Sum(result => result.TriangleCount);
            return $"[PeopleOfHollowfenImporter] Imported {people.Length} profiles in manifest order " +
                   $"with {totalTriangles:N0} total preview triangles and rebuilt {DatabasePath}.";
        }

        private static ImportResult ImportCharacter(ManifestCharacter person, int sortOrder)
        {
            ConfigureModelImporter(ModelPath(person));
            ConfigureModelTextureImporter(AlbedoPath(person), false, person.textureSize);
            ConfigureModelTextureImporter(NormalPath(person), true, person.textureSize);
            ConfigureSpriteImporter(TPoseAssetPath(person), person.textureSize);
            if (HasDistinctHeroArt(person)) ConfigureSpriteImporter(HeroAssetPath(person), person.textureSize);

            long triangleCount = ValidateTriangleBudget(person);
            Material material = BuildMaterial(person);
            BuildPreviewPrefab(person, material);
            CharacterProfileData profile = BuildProfile(person, sortOrder);
            return new ImportResult { Profile = profile, TriangleCount = triangleCount };
        }

        private static ManifestCharacter[] LoadAndValidateManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException(
                    "People of Hollowfen manifest is missing. The importer expects the repository-level manifest at " +
                    ManifestRelativePath + ".",
                    manifestPath);

            string json = File.ReadAllText(manifestPath).Trim();
            if (json.Length == 0 || json[0] != '[' || json[json.Length - 1] != ']')
                throw new InvalidDataException("People of Hollowfen manifest must be a non-empty top-level JSON array.");

            ManifestEnvelope envelope;
            try
            {
                envelope = JsonUtility.FromJson<ManifestEnvelope>("{\"people\":" + json + "}");
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("People of Hollowfen manifest contains invalid JSON.", exception);
            }

            if (envelope?.people == null || envelope.people.Length == 0)
                throw new InvalidDataException("People of Hollowfen manifest contains no character entries.");

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < envelope.people.Length; index++)
            {
                ManifestCharacter person = envelope.people[index];
                if (person == null)
                    throw new InvalidDataException($"Manifest entry {index} is null or is not a JSON object.");

                ValidateCharacter(person, index);
                if (!ids.Add(person.id))
                    throw new InvalidDataException($"Manifest contains duplicate character id '{person.id}'.");
            }

            return envelope.people;
        }

        private static void ValidateCharacter(ManifestCharacter person, int index)
        {
            person.id = RequireText(person.id, $"entry {index}.id");
            ValidateId(person.id, index);
            person.displayName = RequireText(person.displayName, person.id + ".displayName");
            person.category = RequireText(person.category, person.id + ".category");
            person.edition = RequireText(person.edition, person.id + ".edition");
            person.role = RequireText(person.role, person.id + ".role");
            person.age = RequireText(person.age, person.id + ".age");
            person.home = RequireText(person.home, person.id + ".home");
            person.work = RequireText(person.work, person.id + ".work");
            person.keepsake = RequireText(person.keepsake, person.id + ".keepsake");
            person.tagline = RequireText(person.tagline, person.id + ".tagline");
            person.leadParagraph = RequireText(person.leadParagraph, person.id + ".leadParagraph");
            person.backgroundParagraph = RequireText(person.backgroundParagraph, person.id + ".backgroundParagraph");
            person.perspectiveParagraph = RequireText(person.perspectiveParagraph, person.id + ".perspectiveParagraph");
            person.pullquote = OptionalText(person.pullquote);

            ParseCategory(person.category, person.id);

            person.modelSource = ValidateRepositorySource(person.modelSource, person.id + ".modelSource", ".fbx");
            person.albedoSource = ValidateRepositorySource(person.albedoSource, person.id + ".albedoSource");
            person.normalSource = ValidateRepositorySource(person.normalSource, person.id + ".normalSource");
            person.artSource = ValidateRepositorySource(person.artSource, person.id + ".artSource", ".png");
            person.heroArtSource = ValidateRepositorySource(person.heroArtSource, person.id + ".heroArtSource", ".png");

            if (person.targetTriangles <= 0)
                throw new InvalidDataException(person.id + ".targetTriangles must be a positive integer.");
            if (person.textureSize < 32 || person.textureSize > 8192 || !Mathf.IsPowerOfTwo(person.textureSize))
                throw new InvalidDataException(person.id + ".textureSize must be a power of two from 32 through 8192.");
            if (person.previewScale <= 0f || float.IsNaN(person.previewScale) || float.IsInfinity(person.previewScale))
                throw new InvalidDataException(person.id + ".previewScale must be a finite positive number.");

            person.missions = person.missions ?? Array.Empty<ManifestMission>();
            for (int missionIndex = 0; missionIndex < person.missions.Length; missionIndex++)
            {
                ManifestMission mission = person.missions[missionIndex];
                if (mission == null)
                    throw new InvalidDataException($"{person.id}.missions[{missionIndex}] must be an object.");
                if (mission.act < 1 || mission.act > 4)
                    throw new InvalidDataException(
                        $"{person.id}.missions[{missionIndex}].act must be an integer from 1 through 4.");
                mission.questId = RequireText(mission.questId, $"{person.id}.missions[{missionIndex}].questId");
                mission.title = RequireText(mission.title, $"{person.id}.missions[{missionIndex}].title");
                mission.summary = RequireText(mission.summary, $"{person.id}.missions[{missionIndex}].summary");
            }

            person.quotes = person.quotes ?? Array.Empty<ManifestQuote>();
            for (int quoteIndex = 0; quoteIndex < person.quotes.Length; quoteIndex++)
            {
                ManifestQuote quote = person.quotes[quoteIndex];
                if (quote == null)
                    throw new InvalidDataException($"{person.id}.quotes[{quoteIndex}] must be an object.");
                quote.text = RequireText(quote.text, $"{person.id}.quotes[{quoteIndex}].text");
                quote.context = RequireText(quote.context, $"{person.id}.quotes[{quoteIndex}].context");
            }
        }

        private static string RequireText(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(fieldName + " must be a non-empty string.");
            return value.Trim();
        }

        private static string OptionalText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void ValidateId(string id, int index)
        {
            if (id == "." || id == ".." || id.IndexOf('/') >= 0 || id.IndexOf('\\') >= 0)
                throw new InvalidDataException($"Manifest entry {index} has unsafe id '{id}'.");
            foreach (char character in id)
            {
                bool allowed = character >= 'a' && character <= 'z' ||
                               character >= 'A' && character <= 'Z' ||
                               character >= '0' && character <= '9' ||
                               character == '_' || character == '-';
                if (!allowed)
                    throw new InvalidDataException(
                        $"Manifest entry {index} id may contain only ASCII letters, numbers, '_' and '-': '{id}'.");
            }
        }

        private static CharacterProfileData.CharacterCategory ParseCategory(string rawCategory, string id)
        {
            if (!Enum.TryParse(rawCategory, true, out CharacterProfileData.CharacterCategory category) ||
                !Enum.IsDefined(typeof(CharacterProfileData.CharacterCategory), category))
            {
                throw new InvalidDataException(
                    $"{id}.category '{rawCategory}' is invalid. Expected Story, Family, or Villager.");
            }

            return category;
        }

        private static string ValidateRepositorySource(string value, string fieldName, string requiredExtension = null)
        {
            string relative = RequireText(value, fieldName).Replace('\\', '/');
            if (Path.IsPathRooted(relative))
                throw new InvalidDataException(fieldName + " must be repository-relative, not absolute.");

            string absolute = ResolveInsideRepository(relative, fieldName);
            if (!File.Exists(absolute))
                throw new FileNotFoundException(fieldName + " does not exist: " + absolute, absolute);
            if (!string.IsNullOrEmpty(requiredExtension) &&
                !string.Equals(Path.GetExtension(absolute), requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(fieldName + " must reference a " + requiredExtension + " file.");
            }

            return relative;
        }

        private static string ResolveInsideRepository(string relativePath, string fieldName)
        {
            string repositoryRoot = GetRepositoryRoot();
            string absolute = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
            string rootWithSeparator = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                       Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(fieldName + " escapes the repository root.");
            return absolute;
        }

        private static void CopyGalleryArt(ManifestCharacter person)
        {
            CopyFileIfChanged(ResolveInsideRepository(person.artSource, person.id + ".artSource"), TPoseAssetPath(person));
            if (HasDistinctHeroArt(person))
            {
                CopyFileIfChanged(
                    ResolveInsideRepository(person.heroArtSource, person.id + ".heroArtSource"),
                    HeroAssetPath(person));
            }
            else
            {
                DeleteGeneratedAssetIfPresent(HeroAssetPath(person));
            }
        }

        private static bool HasDistinctHeroArt(ManifestCharacter person)
        {
            string tPose = ResolveInsideRepository(person.artSource, person.id + ".artSource");
            string hero = ResolveInsideRepository(person.heroArtSource, person.id + ".heroArtSource");
            return !string.Equals(tPose, hero, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteGeneratedAssetIfPresent(string assetPath)
        {
            string absolutePath = AssetPathToAbsolute(assetPath);
            if (!File.Exists(absolutePath) && AssetDatabase.LoadMainAssetAtPath(assetPath) == null) return;
            if (AssetDatabase.DeleteAsset(assetPath)) return;

            // Covers an unimported file left by an interrupted first pass.
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            if (File.Exists(absolutePath + ".meta")) File.Delete(absolutePath + ".meta");
        }

        private static void CopyFileIfChanged(string sourceAbsolutePath, string destinationAssetPath)
        {
            string destinationAbsolutePath = AssetPathToAbsolute(destinationAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationAbsolutePath) ??
                                      throw new InvalidOperationException("Destination has no directory: " + destinationAssetPath));

            if (FilesAreEqual(sourceAbsolutePath, destinationAbsolutePath)) return;
            File.Copy(sourceAbsolutePath, destinationAbsolutePath, true);
        }

        private static bool FilesAreEqual(string firstPath, string secondPath)
        {
            if (!File.Exists(secondPath)) return false;
            var firstInfo = new FileInfo(firstPath);
            var secondInfo = new FileInfo(secondPath);
            if (firstInfo.Length != secondInfo.Length) return false;

            const int BufferSize = 81920;
            var firstBuffer = new byte[BufferSize];
            var secondBuffer = new byte[BufferSize];
            using (FileStream first = File.OpenRead(firstPath))
            using (FileStream second = File.OpenRead(secondPath))
            {
                while (true)
                {
                    int firstRead = first.Read(firstBuffer, 0, firstBuffer.Length);
                    int secondRead = second.Read(secondBuffer, 0, secondBuffer.Length);
                    if (firstRead != secondRead) return false;
                    if (firstRead == 0) return true;
                    for (int index = 0; index < firstRead; index++)
                        if (firstBuffer[index] != secondBuffer[index]) return false;
                }
            }
        }

        private static void ConfigureModelImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No ModelImporter is available for " + assetPath);

            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.optimizeGameObjects = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Low;
            importer.isReadable = false;
            importer.weldVertices = true;
            importer.addCollider = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureModelTextureImporter(string assetPath, bool normalMap, int maximumSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter is available for " + assetPath);

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maximumSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureSpriteImporter(string assetPath, int maximumSize)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No TextureImporter is available for " + assetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maximumSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static long ValidateTriangleBudget(ManifestCharacter person)
        {
            Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath(person)).OfType<Mesh>().ToArray();
            if (meshes.Length == 0)
                throw new InvalidOperationException(person.id + " optimized FBX contains no Mesh assets.");

            long total = 0;
            foreach (Mesh mesh in meshes)
            {
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                        throw new InvalidOperationException(
                            $"{person.id} mesh '{mesh.name}' submesh {subMesh} is not triangulated.");
                    total += (long)mesh.GetIndexCount(subMesh) / 3L;
                }
            }

            if (total <= 0)
                throw new InvalidOperationException(person.id + " optimized FBX contains no triangles.");
            if (total > person.targetTriangles)
                throw new InvalidOperationException(
                    $"{person.id} optimized FBX has {total:N0} triangles, above its {person.targetTriangles:N0} budget.");
            return total;
        }

        private static Material BuildMaterial(ManifestCharacter person)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Universal Render Pipeline/Lit shader is unavailable.");

            string materialPath = MaterialPath(person);
            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(materialPath);
            if (existingAsset != null && !(existingAsset is Material))
                throw new InvalidOperationException(materialPath + " exists but is not a Material asset.");

            var material = existingAsset as Material;
            if (material == null)
            {
                material = new Material(shader) { name = MaterialNamePrefix + person.id + "_Journal" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D albedo = RequireAsset<Texture2D>(AlbedoPath(person));
            Texture2D normal = RequireAsset<Texture2D>(NormalPath(person));
            material.SetTexture("_BaseMap", albedo);
            material.SetTexture("_MainTex", albedo);
            material.SetTexture("_BumpMap", normal);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_BumpScale", 0.82f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.2f);
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_EMISSION");
            material.enableInstancing = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildPreviewPrefab(ManifestCharacter person, Material material)
        {
            string prefabPath = PrefabPath(person);
            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(prefabPath);
            if (existingAsset != null && !(existingAsset is GameObject))
                throw new InvalidOperationException(prefabPath + " exists but is not a prefab GameObject.");

            bool existingPrefab = existingAsset != null;
            GameObject root = existingPrefab ? PrefabUtility.LoadPrefabContents(prefabPath) : new GameObject();
            try
            {
                root.name = "Character_" + person.id + "_JournalPreview";
                root.tag = "Untagged";
                root.layer = 0;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                while (root.transform.childCount > 0)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                foreach (Component component in root.GetComponents<Component>())
                    if (!(component is Transform)) UnityEngine.Object.DestroyImmediate(component);

                GameObject source = RequireAsset<GameObject>(ModelPath(person));
                GameObject visual = UnityEngine.Object.Instantiate(source, root.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                RemoveNonVisualComponents(visual);

                Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                    throw new InvalidOperationException(person.id + " optimized model has no renderers.");
                foreach (Renderer renderer in renderers)
                {
                    int materialSlots = Math.Max(1, renderer.sharedMaterials.Length);
                    var materials = new Material[materialSlots];
                    for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, (StaticEditorFlags)0);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save preview prefab at " + prefabPath);
            }
            finally
            {
                if (existingPrefab) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RemoveNonVisualComponents(GameObject visual)
        {
            Component[] components = visual.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null || component is Transform || component is Renderer || component is MeshFilter) continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static CharacterProfileData BuildProfile(ManifestCharacter person, int sortOrder)
        {
            CharacterProfileData profile = FindProfile(person.id, out bool created);
            var serialized = new SerializedObject(profile);
            serialized.Update();

            SetString(serialized, "_id", person.id);
            SetString(serialized, "_characterName", person.displayName);
            SetString(serialized, "_role", person.role);
            SetString(serialized, "_age", person.age);
            SetString(serialized, "_home", person.home);
            SetString(serialized, "_work", person.work);
            SetString(serialized, "_keepsake", person.keepsake);
            SetString(serialized, "_tagline", person.tagline);
            SetString(serialized, "_leadParagraph", person.leadParagraph);
            SetString(serialized, "_backgroundParagraph", person.backgroundParagraph);
            SetString(serialized, "_perspectiveParagraph", person.perspectiveParagraph);
            SetString(serialized, "_pullquote", person.pullquote);
            Property(serialized, "_sortOrder").intValue = sortOrder;
            Property(serialized, "_category").enumValueIndex =
                (int)ParseCategory(person.category, person.id);
            SetString(serialized, "_edition", person.edition);
            Property(serialized, "_previewScale").floatValue = person.previewScale;

            string heroResourcePath = HasDistinctHeroArt(person)
                ? HeroResourcePath(person)
                : TPoseResourcePath(person);
            SetString(serialized, "_heroPortraitResourcePath", heroResourcePath);
            SetString(serialized, "_tPosePlateResourcePath", TPoseResourcePath(person));
            // heroArtSource is the production character sheet (and may deliberately fall back to the T-pose card).
            SetString(serialized, "_characterSheetResourcePath", heroResourcePath);
            SetString(serialized, "_journalModelResourcePath", ModelResourcePath(person));
            Property(serialized, "_journalTriangleBudget").intValue = person.targetTriangles;
            Property(serialized, "_journalTextureSize").intValue = person.textureSize;

            SetMissions(Property(serialized, "_missions"), person.missions);
            SetQuotes(Property(serialized, "_quotes"), person.quotes);

            bool isWren = string.Equals(person.id, WrenId, StringComparison.OrdinalIgnoreCase);
            if (!isWren)
            {
                string localizationToken = person.id.ToLowerInvariant().Replace('-', '_');
                SetString(serialized, "_displayNameId", "character." + localizationToken + ".name");
                SetString(serialized, "_descriptionId", "character." + localizationToken + ".lead");

                // Non-Wren profiles must stay lightweight on every rebuild, including after manual edits.
                Property(serialized, "_heroPortrait").objectReferenceValue = null;
                Property(serialized, "_studySheet").objectReferenceValue = null;
                Property(serialized, "_figureFront").objectReferenceValue = null;
                Property(serialized, "_figureBack").objectReferenceValue = null;
                Property(serialized, "_figureThreeQuarter").objectReferenceValue = null;
                Property(serialized, "_knifePlate").objectReferenceValue = null;
            }

            // The static manifest resources are authoritative for every character, including Wren.
            Property(serialized, "_tPosePlate").objectReferenceValue = null;
            Property(serialized, "_characterSheet").objectReferenceValue = null;
            Property(serialized, "_journalModelPrefab").objectReferenceValue = null;
            Property(serialized, "_journalIdleClip").objectReferenceValue = null;

            if (created)
            {
                Property(serialized, "_kitItems").arraySize = 0;
                Property(serialized, "_journalExposure").floatValue = DefaultJournalExposure;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static CharacterProfileData FindProfile(string id, out bool created)
        {
            if (string.Equals(id, WrenId, StringComparison.OrdinalIgnoreCase))
            {
                CharacterProfileData wren = AssetDatabase.LoadAssetAtPath<CharacterProfileData>(WrenProfilePath);
                if (wren == null) throw new InvalidOperationException("Missing Wren profile at " + WrenProfilePath);
                created = false;
                return wren;
            }

            string expectedPath = ProfilePath(id);
            UnityEngine.Object occupiedAsset = AssetDatabase.LoadMainAssetAtPath(expectedPath);
            if (occupiedAsset != null && !(occupiedAsset is CharacterProfileData))
                throw new InvalidOperationException(expectedPath + " exists but is not a CharacterProfileData asset.");

            CharacterProfileData expected = AssetDatabase.LoadAssetAtPath<CharacterProfileData>(expectedPath);
            if (expected != null)
            {
                if (!string.IsNullOrWhiteSpace(expected.Id) &&
                    !string.Equals(expected.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{expectedPath} contains character id '{expected.Id}', not expected id '{id}'.");
                }
                created = false;
                return expected;
            }

            CharacterProfileData match = null;
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterProfileData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<CharacterProfileData>(path);
                if (candidate == null || !string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
                if (match != null)
                    throw new InvalidOperationException($"Multiple CharacterProfileData assets use id '{id}'.");
                match = candidate;
            }

            if (match != null)
            {
                created = false;
                return match;
            }

            var profile = ScriptableObject.CreateInstance<CharacterProfileData>();
            profile.name = "Character_" + id;
            AssetDatabase.CreateAsset(profile, expectedPath);
            created = true;
            return profile;
        }

        private static void SetMissions(SerializedProperty array, ManifestMission[] missions)
        {
            array.arraySize = missions.Length;
            for (int index = 0; index < missions.Length; index++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Act").intValue = missions[index].act;
                element.FindPropertyRelative("QuestId").stringValue = missions[index].questId;
                element.FindPropertyRelative("Title").stringValue = missions[index].title;
                element.FindPropertyRelative("Summary").stringValue = missions[index].summary;
            }
        }

        private static void SetQuotes(SerializedProperty array, ManifestQuote[] quotes)
        {
            array.arraySize = quotes.Length;
            for (int index = 0; index < quotes.Length; index++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Text").stringValue = quotes[index].text;
                element.FindPropertyRelative("Context").stringValue = quotes[index].context;
            }
        }

        private static void BuildDatabase(CharacterProfileData[] profiles)
        {
            UnityEngine.Object existingAsset = AssetDatabase.LoadMainAssetAtPath(DatabasePath);
            if (existingAsset != null && !(existingAsset is CharacterProfileDatabase))
                throw new InvalidOperationException(DatabasePath + " exists but is not a CharacterProfileDatabase asset.");

            var database = existingAsset as CharacterProfileDatabase;
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<CharacterProfileDatabase>();
                database.name = "PeopleOfHollowfenDatabase";
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var serialized = new SerializedObject(database);
            serialized.Update();
            SerializedProperty profileArray = Property(serialized, "_profiles");
            profileArray.arraySize = profiles.Length;
            for (int index = 0; index < profiles.Length; index++)
                profileArray.GetArrayElementAtIndex(index).objectReferenceValue = profiles[index];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            Property(serialized, propertyName).stringValue = value ?? string.Empty;
        }

        private static SerializedProperty Property(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + " is missing serialized property " + propertyName + ".");
            return property;
        }

        private static T RequireAsset<T>(string assetPath) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null) throw new InvalidOperationException("Could not load " + typeof(T).Name + " at " + assetPath);
            return asset;
        }

        private static void RequireGeneratedFile(string assetPath)
        {
            string absolute = AssetPathToAbsolute(assetPath);
            if (!File.Exists(absolute))
                throw new FileNotFoundException(
                    "Missing optimized People of Hollowfen asset " + assetPath +
                    ". Run tools/optimize_people_journal_models.py through Blender first.",
                    absolute);
        }

        private static string ModelPath(ManifestCharacter person)
        {
            return GeneratedCharacterRoot(person) + "/Character_" + person.id + "_Journal.fbx";
        }

        private static string AlbedoPath(ManifestCharacter person)
        {
            return GeneratedCharacterRoot(person) + "/Character_" + person.id + "_Journal_Albedo.png";
        }

        private static string NormalPath(ManifestCharacter person)
        {
            return GeneratedCharacterRoot(person) + "/Character_" + person.id + "_Journal_Normal.png";
        }

        private static string MaterialPath(ManifestCharacter person)
        {
            return GeneratedCharacterRoot(person) + "/" + MaterialNamePrefix + person.id + "_Journal.mat";
        }

        private static string GeneratedCharacterRoot(ManifestCharacter person)
        {
            return GeneratedRoot + "/" + person.id;
        }

        private static string TPoseAssetPath(ManifestCharacter person)
        {
            return ResourceArtRoot + "/" + person.id + "_tpose.png";
        }

        private static string HeroAssetPath(ManifestCharacter person)
        {
            return ResourceArtRoot + "/" + person.id + "_hero.png";
        }

        private static string PrefabPath(ManifestCharacter person)
        {
            return ResourceModelRoot + "/" + person.id + ".prefab";
        }

        private static string ProfilePath(string id)
        {
            return ProfileRoot + "/Character_" + id + ".asset";
        }

        private static string TPoseResourcePath(ManifestCharacter person)
        {
            return "People/Art/" + person.id + "_tpose";
        }

        private static string HeroResourcePath(ManifestCharacter person)
        {
            return "People/Art/" + person.id + "_hero";
        }

        private static string ModelResourcePath(ManifestCharacter person)
        {
            return "People/Models/" + person.id;
        }

        private static string GetManifestAbsolutePath()
        {
            return Path.Combine(GetRepositoryRoot(), "tools", "people_of_hollowfen_manifest.json");
        }

        private static string GetRepositoryRoot()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            DirectoryInfo parent = Directory.GetParent(projectRoot);
            if (parent == null) throw new InvalidOperationException("Unity project has no repository parent directory.");
            return parent.FullName;
        }

        private static string AssetPathToAbsolute(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !(assetPath.Equals("Assets", StringComparison.Ordinal) ||
                  assetPath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Expected an Assets-relative Unity path.", nameof(assetPath));
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static void EnsureAssetFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Asset folder path is empty.", nameof(path));

            string normalized = path.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
                throw new ArgumentException("Asset folder must be rooted at Assets: " + path, nameof(path));

            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
