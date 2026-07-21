#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using Hollowfen.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    public static class MiniMapBakeTool
    {
        private const string OutputDirectory = "Assets/_Hollowfen/Visuals/Map";
        private const string OutputPath = OutputDirectory + "/Hollowfen_Overhead.png";
        private const int Resolution = 4096;
        private const int TilesPerAxis = 8;

        [MenuItem("Hollowfen/Production/Bake Static Minimap")]
        public static void Bake()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                throw new System.InvalidOperationException("The loaded scene has no active Terrain to frame.");

            Camera source = Object.FindAnyObjectByType<MiniMapCamera>(FindObjectsInactive.Include)
                ?.GetComponent<Camera>();
            Vector3 terrainPosition = terrain.GetPosition();
            Vector3 terrainSize = terrain.terrainData.size;
            float worldSpan = Mathf.Max(terrainSize.x, terrainSize.z);
            int tileResolution = Resolution / TilesPerAxis;
            float tileWorldSpan = worldSpan / TilesPerAxis;

            float[,] heights = terrain.terrainData.GetHeights(
                0, 0, terrain.terrainData.heightmapResolution, terrain.terrainData.heightmapResolution);
            float maximumHeight = 0f;
            foreach (float height in heights)
                maximumHeight = Mathf.Max(maximumHeight, height);
            float cameraY = terrainPosition.y + maximumHeight * terrainSize.y + 120f;

            var temporary = new GameObject("__StaticMiniMapBakeCamera", typeof(Camera));
            temporary.hideFlags = HideFlags.HideAndDontSave;
            Camera camera = temporary.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = tileWorldSpan * 0.5f;
            camera.aspect = 1f;
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = cameraY - terrainPosition.y + 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.18f, 0.12f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            if (source != null) camera.cullingMask = source.cullingMask;
            int foraging = LayerMask.NameToLayer("Foraging");
            if (foraging >= 0) camera.cullingMask &= ~(1 << foraging);

            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderShadows = false;
            cameraData.requiresDepthOption = CameraOverrideOption.Off;
            cameraData.requiresColorOption = CameraOverrideOption.Off;

            var renderTexture = new RenderTexture(
                tileResolution, tileResolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "StaticMiniMapBakeRT",
                antiAliasing = 1
            };
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false, false);
            bool savedFog = RenderSettings.fog;
            RenderTexture previous = RenderTexture.active;
            float savedLodBias = QualitySettings.lodBias;
            float savedTreeDistance = terrain.treeDistance;
            float savedTreeBillboardDistance = terrain.treeBillboardDistance;
            int savedFullLodTrees = terrain.treeMaximumFullLODCount;
            var savedWaterMaterials = new Dictionary<Renderer, Material[]>();
            var mapWater = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = "__StaticMiniMapWater",
                hideFlags = HideFlags.HideAndDontSave
            };
            mapWater.SetColor("_BaseColor", new Color(0.12f, 0.23f, 0.28f, 1f));

            try
            {
                renderTexture.Create();
                camera.targetTexture = renderTexture;
                RenderSettings.fog = false;
                QualitySettings.lodBias = Mathf.Max(savedLodBias, 8f);
                terrain.treeDistance = Mathf.Max(savedTreeDistance, 1000f);
                terrain.treeBillboardDistance = Mathf.Max(savedTreeBillboardDistance, 1000f);
                terrain.treeMaximumFullLODCount = Mathf.Max(savedFullLodTrees, 50000);
                ReplaceWaterMaterialsForBake(mapWater, savedWaterMaterials);

                for (int y = 0; y < TilesPerAxis; y++)
                {
                    for (int x = 0; x < TilesPerAxis; x++)
                    {
                        camera.transform.position = new Vector3(
                            terrainPosition.x + (x + 0.5f) * terrainSize.x / TilesPerAxis,
                            cameraY,
                            terrainPosition.z + (y + 0.5f) * terrainSize.z / TilesPerAxis);
                        camera.Render();
                        RenderTexture.active = renderTexture;
                        texture.ReadPixels(
                            new Rect(0f, 0f, tileResolution, tileResolution),
                            x * tileResolution, y * tileResolution, false);
                    }
                }
                texture.Apply(false, false);
                RemoveErrorShaderPixels(texture);

                Directory.CreateDirectory(OutputDirectory);
                File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
            }
            finally
            {
                RenderSettings.fog = savedFog;
                QualitySettings.lodBias = savedLodBias;
                terrain.treeDistance = savedTreeDistance;
                terrain.treeBillboardDistance = savedTreeBillboardDistance;
                terrain.treeMaximumFullLODCount = savedFullLodTrees;
                foreach (var pair in savedWaterMaterials)
                    if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mapWater);
                Object.DestroyImmediate(temporary);
            }

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 1;
            importer.maxTextureSize = Resolution;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            Texture2D bakedMap = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
            MiniMapWidget widget = Object.FindAnyObjectByType<MiniMapWidget>(FindObjectsInactive.Include);
            if (widget == null)
                throw new System.InvalidOperationException("The loaded scene has no MiniMapWidget to receive the bake.");

            var widgetObject = new SerializedObject(widget);
            widgetObject.FindProperty("_bakedMap").objectReferenceValue = bakedMap;
            widgetObject.FindProperty("_worldBounds").rectValue = new Rect(
                terrainPosition.x, terrainPosition.z, terrainSize.x, terrainSize.z);
            widgetObject.FindProperty("_viewWorldSize").floatValue = 60f;
            widgetObject.ApplyModifiedPropertiesWithoutUndo();

            MapCamera fullMap = Object.FindAnyObjectByType<MapCamera>(FindObjectsInactive.Include);
            if (fullMap != null)
            {
                var fullMapObject = new SerializedObject(fullMap);
                fullMapObject.FindProperty("_bakedMap").objectReferenceValue = bakedMap;
                fullMapObject.ApplyModifiedPropertiesWithoutUndo();

                Camera fullMapCamera = fullMap.GetComponent<Camera>();
                RenderTexture legacyTexture = fullMapCamera != null ? fullMapCamera.targetTexture : null;
                if (fullMapCamera != null)
                {
                    fullMapCamera.targetTexture = null;
                    EditorUtility.SetDirty(fullMapCamera);
                }

                if (legacyTexture != null)
                {
                    foreach (RawImage image in Object.FindObjectsByType<RawImage>(
                                 FindObjectsInactive.Include))
                    {
                        if (image.texture != legacyTexture) continue;
                        image.texture = bakedMap;
                        EditorUtility.SetDirty(image);
                    }
                }
                EditorUtility.SetDirty(fullMap);
            }

            // Remove the two legacy RenderTexture references. MiniMapWidget assigns the bake in
            // Awake, and MiniMapCamera remains available only as a no-bake fallback.
            if (source != null)
            {
                source.targetTexture = null;
                EditorUtility.SetDirty(source);
            }
            var rawImageProperty = widgetObject.FindProperty("_mapImage");
            var rawImage = rawImageProperty.objectReferenceValue as RawImage;
            if (rawImage != null)
            {
                rawImage.texture = bakedMap;
                EditorUtility.SetDirty(rawImage);
            }

            EditorUtility.SetDirty(widget);
            EditorSceneManager.MarkSceneDirty(widget.gameObject.scene);
            EditorSceneManager.SaveScene(widget.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MiniMapBakeTool] PASS — baked {Resolution}x{Resolution} overhead map to {OutputPath} and removed runtime minimap/full-map world rendering.");
        }

        private static void ReplaceWaterMaterialsForBake(
            Material replacement, Dictionary<Renderer, Material[]> savedMaterials)
        {
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Exclude))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                var replacements = (Material[])materials.Clone();
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null || material.shader == null ||
                        !material.shader.name.StartsWith("NatureManufacture Shaders/Water/"))
                        continue;
                    replacements[i] = replacement;
                    changed = true;
                }

                if (!changed) continue;
                savedMaterials.Add(renderer, materials);
                renderer.sharedMaterials = replacements;
            }
        }

        private static void RemoveErrorShaderPixels(Texture2D texture)
        {
            Color32[] source = texture.GetPixels32();
            Color32[] repaired = (Color32[])source.Clone();
            int width = texture.width;
            int height = texture.height;

            for (int index = 0; index < source.Length; index++)
            {
                if (!IsErrorPink(source[index])) continue;
                int x = index % width;
                int y = index / width;
                bool found = false;

                for (int radius = 1; radius <= 48 && !found; radius++)
                {
                    for (int offset = -radius; offset <= radius && !found; offset++)
                    {
                        found = TryCopy(x + offset, y - radius, width, height, source, repaired, index) ||
                                TryCopy(x + offset, y + radius, width, height, source, repaired, index) ||
                                TryCopy(x - radius, y + offset, width, height, source, repaired, index) ||
                                TryCopy(x + radius, y + offset, width, height, source, repaired, index);
                    }
                }
            }

            texture.SetPixels32(repaired);
            texture.Apply(false, false);
        }

        private static bool TryCopy(
            int x, int y, int width, int height, Color32[] source, Color32[] repaired, int destination)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            Color32 candidate = source[y * width + x];
            if (IsErrorPink(candidate)) return false;
            repaired[destination] = candidate;
            return true;
        }

        private static bool IsErrorPink(Color32 color)
        {
            return color.r >= 240 && color.g <= 16 && color.b >= 240;
        }
    }
}
#endif
