using System.Collections.Generic;
using System.Text;
using Hollowfen.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Runtime UI lint for the code-built screens. Run it while a presentation is visible;
    /// it catches the invisible blockers, missing focus, undersized hit targets, clipped text,
    /// inconsistent canvas scaling, and leaked pause/cutting shadows or resting button fills that
    /// are easy to miss in a purely visual review.
    /// </summary>
    public static class ProductionUIVerifier
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float MinimumReadablePixels = 9f;
        private const float MinimumHitPixels = 32f;

        [MenuItem("Tools/Hollowfen/Verify Active UI Presentation")]
        public static void VerifyActivePresentation()
        {
            string report = VerifyActiveForAutomation();
            if (report.StartsWith("PASS")) Debug.Log(report);
            else Debug.LogError(report);
        }

        public static string VerifyActiveForAutomation()
        {
            var critical = new List<string>();
            var warnings = new List<string>();

            VerifyCanvases(critical);
            VerifyCanvasGroups(critical);
            VerifyText(critical, warnings);
            VerifyFocus(critical);
            VerifyHitTargets(warnings);
            VerifyMissingComponents(critical);
            VerifyPausePresentation(critical);
            VerifyCuttingPresentation(critical);

            var sb = new StringBuilder();
            sb.Append(critical.Count == 0 ? "PASS" : "FAIL");
            sb.Append(" · active UI presentation · ");
            sb.Append(critical.Count).Append(" critical · ");
            sb.Append(warnings.Count).Append(" advisory");
            foreach (string issue in critical) sb.Append("\nERROR · ").Append(issue);
            foreach (string issue in warnings) sb.Append("\nNOTE  · ").Append(issue);
            return sb.ToString();
        }

        private static void VerifyCanvases(List<string> critical)
        {
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!IsLive(canvas) || !canvas.enabled || !canvas.gameObject.activeInHierarchy ||
                    !canvas.isRootCanvas) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                var scaler = canvas.GetComponent<CanvasScaler>();
                // FadeOverlay only stretches a solid-color image and intentionally inherits no
                // reference-space layout; every authored screen/HUD canvas should be standardized.
                if (scaler == null)
                {
                    if (canvas.name != "FadeOverlay")
                        critical.Add(Path(canvas.transform) + " has no CanvasScaler.");
                    continue;
                }

                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    Vector2.Distance(scaler.referenceResolution,
                        new Vector2(ReferenceWidth, ReferenceHeight)) > 0.1f ||
                    Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) > 0.01f)
                {
                    critical.Add(Path(canvas.transform) +
                        " does not use the shared 1920×1080 / 0.5 scaling contract.");
                }
            }
        }

        private static void VerifyCanvasGroups(List<string> critical)
        {
            foreach (CanvasGroup group in Resources.FindObjectsOfTypeAll<CanvasGroup>())
            {
                if (!IsLive(group) || !group.gameObject.activeInHierarchy) continue;
                if (group.alpha <= 0.01f && group.blocksRaycasts)
                    critical.Add(Path(group.transform) +
                        " is invisible but still blocks pointer input.");
            }
        }

        private static void VerifyText(List<string> critical, List<string> warnings)
        {
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                if (!IsVisible(text)) continue;
                if (text.font == null)
                {
                    critical.Add(Path(text.transform) + " has no TMP font asset.");
                    continue;
                }

                text.ForceMeshUpdate();
                bool clipsByDesign = text.overflowMode == TextOverflowModes.Truncate ||
                                     text.overflowMode == TextOverflowModes.Ellipsis ||
                                     text.overflowMode == TextOverflowModes.Masking;
                if (clipsByDesign && text.isTextOverflowing)
                    critical.Add(Path(text.transform) + " clips visible copy: “" +
                        Compact(text.text) + "”.");

                Canvas canvas = text.GetComponentInParent<Canvas>();
                float pixels = text.fontSize * ScreenScale(text.rectTransform, canvas).y;
                if (pixels < MinimumReadablePixels && !IsDecorativeMicrocopy(text))
                    warnings.Add(Path(text.transform) + " renders at " +
                        pixels.ToString("0.0") + " px: “" + Compact(text.text) + "”.");
            }
        }

        private static void VerifyFocus(List<string> critical)
        {
            UIManager manager = UIManager.Instance;
            UIScreen top = manager != null ? manager.TopScreen : null;
            if (top == null) return;
            if (!top.gameObject.activeInHierarchy)
                critical.Add("UIManager top screen “" + top.ScreenId + "” is inactive.");

            // Loading and purely cinematic presentations intentionally contain no controls.
            // Requiring a default selection there creates a false failure; focus is only a
            // contract when the active screen actually exposes an interactable selectable.
            bool hasInteractable = false;
            foreach (Selectable candidate in top.GetComponentsInChildren<Selectable>(true))
            {
                if (candidate.gameObject.activeInHierarchy && candidate.IsInteractable())
                {
                    hasInteractable = true;
                    break;
                }
            }
            if (!hasInteractable) return;

            GameObject fallback = top.DefaultSelected;
            if (fallback == null)
            {
                critical.Add("Top screen “" + top.ScreenId + "” has no default focus target.");
                return;
            }

            Selectable selectable = fallback.GetComponent<Selectable>();
            if (!fallback.activeInHierarchy || selectable == null || !selectable.IsInteractable())
                critical.Add("Top screen “" + top.ScreenId + "” has an invalid default focus target.");

            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
                critical.Add("Top screen “" + top.ScreenId + "” has no current EventSystem focus.");
        }

        private static void VerifyHitTargets(List<string> warnings)
        {
            foreach (Selectable selectable in Resources.FindObjectsOfTypeAll<Selectable>())
            {
                if (!IsLive(selectable) || !selectable.gameObject.activeInHierarchy ||
                    !selectable.IsInteractable()) continue;
                var rt = selectable.transform as RectTransform;
                Canvas canvas = selectable.GetComponentInParent<Canvas>();
                if (rt == null || canvas == null) continue;
                Vector2 pixels = Vector2.Scale(rt.rect.size, ScreenScale(rt, canvas));
                if (pixels.x < MinimumHitPixels || pixels.y < MinimumHitPixels)
                    warnings.Add(Path(selectable.transform) + " hit target is " +
                        pixels.x.ToString("0") + "×" + pixels.y.ToString("0") + " px.");
            }
        }

        private static Vector2 ScreenScale(RectTransform rect, Canvas canvas)
        {
            if (rect == null || canvas == null || canvas.rootCanvas == null)
                return Vector2.one;

            Canvas root = canvas.rootCanvas;
            Transform rootTransform = root.transform;
            float rootX = Mathf.Abs(rootTransform.lossyScale.x);
            float rootY = Mathf.Abs(rootTransform.lossyScale.y);
            float relativeX = rootX > 0.0001f ? Mathf.Abs(rect.lossyScale.x) / rootX : 1f;
            float relativeY = rootY > 0.0001f ? Mathf.Abs(rect.lossyScale.y) / rootY : 1f;

            // scaleFactor converts root-canvas reference units to pixels. The relative term
            // adds only presentation/local scaling below the root, avoiding the previous
            // double-count of the root Canvas transform.
            return new Vector2(root.scaleFactor * relativeX, root.scaleFactor * relativeY);
        }

        private static void VerifyMissingComponents(List<string> critical)
        {
            // Missing components matter here when they are attached to UI. Walking every scene
            // Transform also traverses hundreds of thousands of terrain/detail instances in the
            // village and turns a presentation lint into a multi-second hitch.
            var uiTransforms = new HashSet<Transform>();
            foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!IsLive(canvas)) continue;
                for (Transform parent = canvas.transform; parent != null; parent = parent.parent)
                    uiTransforms.Add(parent);
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                    uiTransforms.Add(child);
            }

            foreach (Transform transform in uiTransforms)
            {
                Component[] components = transform.GetComponents<Component>();
                foreach (Component component in components)
                    if (component == null)
                        critical.Add(Path(transform) + " contains a missing script component.");
            }
        }

        private static void VerifyCuttingPresentation(List<string> critical)
        {
            GameObject presentation = GameObject.Find("CuttingPanelPresentation");
            if (presentation == null) return;
            CanvasGroup group = presentation.GetComponent<CanvasGroup>();
            if (group == null)
                critical.Add("CuttingPanelPresentation has no CanvasGroup.");

            Transform hud = presentation.transform.parent;
            if (hud == null) return;
            foreach (Transform child in hud.GetComponentsInChildren<Transform>(true))
            {
                if (child == presentation.transform ||
                    child.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!child.IsChildOf(presentation.transform))
                    critical.Add(Path(child) +
                        " is outside CuttingPanelPresentation and can leak into the kneeling shot.");
            }
        }

        private static void VerifyPausePresentation(List<string> critical)
        {
            GameObject presentation = GameObject.Find("PausePresentation");
            if (presentation == null) return;

            foreach (Transform child in presentation.transform)
            {
                if (child.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    critical.Add(Path(child) +
                        " is a detached pause-card shadow and can render as a blurred header band.");
            }

            foreach (Button button in presentation.GetComponentsInChildren<Button>(true))
            {
                Image restingFill = button.GetComponent<Image>();
                if (restingFill != null && restingFill.color.a > 0.01f)
                    critical.Add(Path(button.transform) +
                        " has an opaque resting fill; pause selection visuals must come only from FocusGlow.");
            }
        }

        private static bool IsLive(Component component)
        {
            return component != null && component.gameObject.scene.IsValid();
        }

        private static bool IsVisible(TMP_Text text)
        {
            if (!IsLive(text) || !text.enabled || !text.gameObject.activeInHierarchy ||
                text.color.a <= 0.01f || string.IsNullOrWhiteSpace(text.text)) return false;
            float alpha = 1f;
            foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>(true))
                alpha *= group.alpha;
            return alpha * text.color.a > 0.03f;
        }

        private static bool IsDecorativeMicrocopy(TMP_Text text)
        {
            string name = text.name.ToLowerInvariant();
            return name.Contains("eyebrow") || name.Contains("edition") ||
                   name.Contains("counter") || name.Contains("credit");
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
            return value.Length <= 80 ? value : value.Substring(0, 77) + "…";
        }

        private static string Path(Transform transform)
        {
            if (transform == null) return "<null>";
            var parts = new List<string>();
            for (Transform cursor = transform; cursor != null; cursor = cursor.parent)
                parts.Add(cursor.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
