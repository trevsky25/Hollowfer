using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

namespace Hollowfen.EditorTools
{
    // Generates the controller/UI glyph TMP sprite sheet (batch-48, closes Q11).
    // Procedural, no external art: DualSense-style face buttons (dark chip + Sony shape
    // colors) plus neutral UI marks (✕ ✓) and a coin — drawn with SDF-style AA into a PNG,
    // wrapped in a TMP_SpriteAsset, and registered as TMP's project-wide default sprite
    // asset so any TMP text can use <sprite name="ps_triangle"> etc.
    //
    // Re-run via menu: Hollowfen → Generate Controller Glyphs (idempotent — overwrites).
    public static class ControllerGlyphGenerator
    {
        private const int Cell = 64;
        private const string PngPath = "Assets/_Hollowfen/UI/Glyphs/ControllerGlyphs.png";
        private const string AssetPath = "Assets/_Hollowfen/UI/Glyphs/ControllerGlyphs.asset";

        // Sony face-button shape colors (approximations of the DualSense legends).
        private static readonly Color ChipDark  = new Color(0.13f, 0.13f, 0.15f, 0.95f);
        private static readonly Color CrossBlue = new Color(0.55f, 0.71f, 0.91f, 1f);
        private static readonly Color CircleRed = new Color(0.94f, 0.45f, 0.43f, 1f);
        private static readonly Color SquarePink = new Color(0.91f, 0.63f, 0.78f, 1f);
        private static readonly Color TriGreen  = new Color(0.44f, 0.82f, 0.55f, 1f);
        private static readonly Color Cream     = new Color(0.965f, 0.929f, 0.847f, 1f);
        private static readonly Color CheckSage = new Color(0.49f, 0.77f, 0.54f, 1f);
        private static readonly Color CoinGold  = new Color(0.79f, 0.66f, 0.34f, 1f);

        [MenuItem("Hollowfen/Generate Controller Glyphs")]
        public static void Generate()
        {
            string[] names = { "ps_cross", "ps_circle", "ps_square", "ps_triangle", "ui_x", "ui_check", "coin" };
            int cols = names.Length;
            var tex = new Texture2D(Cell * cols, Cell, TextureFormat.RGBA32, false);
            var px = new Color[Cell * cols * Cell];
            for (int i = 0; i < px.Length; i++) px[i] = Color.clear;
            tex.SetPixels(px);

            DrawChipShape(tex, 0, DrawCross, CrossBlue);
            DrawChipShape(tex, 1, DrawCircle, CircleRed);
            DrawChipShape(tex, 2, DrawSquare, SquarePink);
            DrawChipShape(tex, 3, DrawTriangle, TriGreen);
            DrawShapeOnly(tex, 4, DrawCross, Cream);
            DrawShapeOnly(tex, 5, DrawCheck, CheckSage);
            DrawCoin(tex, 6);
            tex.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(PngPath));
            File.WriteAllBytes(PngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PngPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(PngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);

            // Build (or rebuild) the sprite asset.
            var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(AssetPath);
            bool fresh = spriteAsset == null;
            if (fresh) spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();

            // Stamp the current schema version BEFORE saving — an unversioned asset triggers
            // TMP's legacy-upgrade on load, which rebuilds the tables from the (empty) legacy
            // sprite list and silently wipes ours.
            var verField = typeof(TMP_SpriteAsset).GetField("m_Version",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (verField != null) verField.SetValue(spriteAsset, "1.1.0");

            spriteAsset.spriteSheet = sheet;
            var glyphs = new List<TMP_SpriteGlyph>();
            var chars = new List<TMP_SpriteCharacter>();
            for (int i = 0; i < names.Length; i++)
            {
                // Baseline metrics: sprite sits slightly below cap height so it centers on text.
                var metrics = new GlyphMetrics(Cell, Cell, 0f, Cell * 0.82f, Cell);
                var rect = new GlyphRect(i * Cell, 0, Cell, Cell);
                var glyph = new TMP_SpriteGlyph((uint)i, metrics, rect, 1f, 0);
                glyphs.Add(glyph);
                var ch = new TMP_SpriteCharacter(0xFFFE, glyph) { name = names[i], scale = 1f };
                chars.Add(ch);
            }
            spriteAsset.spriteGlyphTable.Clear();
            spriteAsset.spriteGlyphTable.AddRange(glyphs);
            spriteAsset.spriteCharacterTable.Clear();
            spriteAsset.spriteCharacterTable.AddRange(chars);

            // Point size 64 = cell size → sprites render at the running text's font size.
            var face = spriteAsset.faceInfo;
            face.pointSize = Cell;
            face.scale = 1f;
            face.lineHeight = Cell;
            face.ascentLine = Cell * 0.82f;
            face.descentLine = -Cell * 0.18f;
            spriteAsset.faceInfo = face;

            if (spriteAsset.material == null)
            {
                var mat = new Material(Shader.Find("TextMeshPro/Sprite"));
                mat.mainTexture = sheet;
                mat.name = "ControllerGlyphs Material";
                spriteAsset.material = mat;
                if (!fresh) AssetDatabase.AddObjectToAsset(mat, spriteAsset);
            }
            else spriteAsset.material.mainTexture = sheet;

            if (fresh)
            {
                AssetDatabase.CreateAsset(spriteAsset, AssetPath);
                AssetDatabase.AddObjectToAsset(spriteAsset.material, spriteAsset);
            }
            spriteAsset.UpdateLookupTables();
            EditorUtility.SetDirty(spriteAsset);

            // Register as the project-wide default so <sprite name=...> resolves in ANY TMP text.
            // SerializedObject route — the static setter alone didn't persist to the asset.
            var settings = TMP_Settings.instance;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("m_defaultSpriteAsset");
            if (prop != null) { prop.objectReferenceValue = spriteAsset; so.ApplyModifiedPropertiesWithoutUndo(); }
            TMP_Settings.defaultSpriteAsset = spriteAsset;
            EditorUtility.SetDirty(settings);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ControllerGlyphs] Generated {names.Length} glyphs → {AssetPath}; TMP default sprite asset set.");
        }

        // ------------------------------------------------------------------ drawing

        private delegate float ShapeSdf(float x, float y); // signed distance to shape stroke center

        private static void DrawChipShape(Texture2D tex, int cellIndex, ShapeSdf sdf, Color shape)
        {
            float c = (Cell - 1) * 0.5f;
            Render(tex, cellIndex, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                float rChip = Mathf.Sqrt(dx * dx + dy * dy) - 29f;   // chip disc r=29
                float chipA = Mathf.Clamp01(0.5f - rChip);
                float strokeA = Mathf.Clamp01(2.4f - Mathf.Abs(sdf(dx, dy))); // ~4.8px stroke
                var col = ChipDark; col.a *= chipA;
                return Blend(col, shape, strokeA * chipA);
            });
        }

        private static void DrawShapeOnly(Texture2D tex, int cellIndex, ShapeSdf sdf, Color shape)
        {
            float c = (Cell - 1) * 0.5f;
            Render(tex, cellIndex, (x, y) =>
            {
                float a = Mathf.Clamp01(2.6f - Mathf.Abs(sdf(x - c, y - c)));
                var col = shape; col.a *= a;
                return col;
            });
        }

        private static void DrawCoin(Texture2D tex, int cellIndex)
        {
            float c = (Cell - 1) * 0.5f;
            Render(tex, cellIndex, (x, y) =>
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float discA = Mathf.Clamp01(20f - d);                       // filled disc r~20
                float rimA = Mathf.Clamp01(2.2f - Mathf.Abs(d - 19f));      // bright rim
                float dotA = Mathf.Clamp01(2.2f - Mathf.Abs(d - 9f));       // inner ring detail
                var body = CoinGold * 0.72f; body.a = discA;
                var bright = CoinGold; bright.a = 1f;
                var col = Blend(body, bright, Mathf.Max(rimA, dotA * 0.6f) * discA);
                return col;
            });
        }

        private static void Render(Texture2D tex, int cellIndex, System.Func<int, int, Color> shade)
        {
            int ox = cellIndex * Cell;
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
                tex.SetPixel(ox + x, y, shade(x, y));
        }

        private static Color Blend(Color under, Color over, float t)
        {
            var c = Color.Lerp(under, new Color(over.r, over.g, over.b, 1f), Mathf.Clamp01(t));
            c.a = Mathf.Max(under.a, Mathf.Clamp01(t));
            return c;
        }

        // Shape SDFs — distance from (x,y) [cell-centered coords] to the stroke centerline.
        private static float DrawCross(float x, float y)
        {
            float d1 = SegDist(x, y, -11f, -11f, 11f, 11f);
            float d2 = SegDist(x, y, -11f, 11f, 11f, -11f);
            return Mathf.Min(d1, d2);
        }

        private static float DrawCircle(float x, float y)
        {
            return Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 12.5f);
        }

        private static float DrawSquare(float x, float y)
        {
            float qx = Mathf.Abs(x) - 11f, qy = Mathf.Abs(y) - 11f;
            float outside = Mathf.Sqrt(Mathf.Pow(Mathf.Max(qx, 0f), 2f) + Mathf.Pow(Mathf.Max(qy, 0f), 2f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return Mathf.Abs(outside + inside);
        }

        private static float DrawTriangle(float x, float y)
        {
            // Equilateral triangle, apex up, circumradius 14.5 (visual center-corrected).
            Vector2 a = Pt(90f), b = Pt(210f), cpt = Pt(330f);
            float d = Mathf.Min(SegDist(x, y, a.x, a.y, b.x, b.y),
                     Mathf.Min(SegDist(x, y, b.x, b.y, cpt.x, cpt.y),
                               SegDist(x, y, cpt.x, cpt.y, a.x, a.y)));
            return d;
        }

        private static Vector2 Pt(float deg)
        {
            float r = 14.5f;
            return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad) * r, Mathf.Sin(deg * Mathf.Deg2Rad) * r - 2f);
        }

        private static float DrawCheck(float x, float y)
        {
            float d1 = SegDist(x, y, -12f, 1f, -3f, -9f);
            float d2 = SegDist(x, y, -3f, -9f, 13f, 9f);
            return Mathf.Min(d1, d2);
        }

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float cx = ax + abx * t, cy = ay + aby * t;
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
    }
}
