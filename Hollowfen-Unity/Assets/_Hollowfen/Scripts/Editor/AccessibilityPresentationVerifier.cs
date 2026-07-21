#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Hollowfen.Settings;
using Hollowfen.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.EditorTools
{
    /// <summary>Runtime proof for readable scaling and reduced-motion focus behavior.</summary>
    public static class AccessibilityPresentationVerifier
    {
        [MenuItem("Hollowfen/Verify/Accessibility Presentation")]
        public static void RunMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("ACCESSIBILITY PRESENTATION — FAIL: enter Play Mode so runtime canvases can be audited.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<AccessibilityVerifierRunner>() != null) return;
            new GameObject("[Accessibility Verifier]").AddComponent<AccessibilityVerifierRunner>();
        }
    }

    public sealed class AccessibilityVerifierRunner : MonoBehaviour
    {
        private const string ScaleKey = "accessibility.interfaceScale";
        private const string MotionKey = "accessibility.reducedMotion";
        private const string CaptionKey = "accessibility.captionBacking";

        private struct PreferenceSnapshot
        {
            public bool HasScale, HasMotion, HasCaption;
            public int Scale, Motion, Caption;
        }

        private IEnumerator Start()
        {
            var snapshot = CapturePreferences();
            GameObject fixture = null;
            GameSettings.InterfaceScaleIndex = 2;
            GameSettings.ReducedMotion = true;
            GameSettings.CaptionBacking = true;
            yield return null;
            yield return null;
            try
            {
                Vector2 expected = new Vector2(1920f, 1080f) / 1.15f;
                Require(Vector2.Distance(AccessibilityPresentationPolicy.ReferenceResolution, expected) < .02f,
                    "115% interface scale resolved to the wrong reference resolution");

                var productionScalers = Resources.FindObjectsOfTypeAll<CanvasScaler>()
                    .Where(s => s != null &&
                                (s.gameObject.scene.IsValid() || s.gameObject.scene.name == "DontDestroyOnLoad") &&
                                s.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    .ToArray();
                Require(productionScalers.Length >= 4, "too few runtime canvases were available for the scale audit");
                foreach (CanvasScaler scaler in productionScalers)
                    Require(Vector2.Distance(scaler.referenceResolution, expected) < .02f,
                        scaler.name + " ignored the 115% interface scale");

                fixture = new GameObject("ReducedMotionFocusFixture", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                fixture.SetActive(false);
                var rect = fixture.GetComponent<RectTransform>();
                var image = fixture.GetComponent<Image>();
                var glowObject = new GameObject("Glow", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                glowObject.transform.SetParent(fixture.transform, false);
                var glow = glowObject.GetComponent<Image>();
                glow.color = new Color(1f, 1f, 1f, .8f);
                var focus = fixture.AddComponent<FocusHighlight>();
                focus.Configure(image, rect, Color.yellow, 1.2f, true, true, false, glow);
                focus.OnSelect((BaseEventData)null);
                Require(Vector3.Distance(rect.localScale, Vector3.one) < .001f,
                    "reduced motion still scaled a focused control");
                Require(glow.color.a > .7f, "reduced motion removed the non-motion focus cue");

                GameSettings.ReducedMotion = false;
                focus.OnDeselect((BaseEventData)null);
                focus.OnSelect((BaseEventData)null);
                Require(Mathf.Abs(rect.localScale.x - 1.2f) < .001f,
                    "standard-motion focus no longer reaches its authored scale");
                Require(GameSettings.CaptionBacking, "caption backing did not persist during the runtime audit");

                Debug.Log("ACCESSIBILITY PRESENTATION — PASS: 115% scaling reached " +
                    productionScalers.Length + " runtime canvases; reduced motion preserves a visible, stable focus cue; caption backing persists.");
            }
            catch (Exception exception)
            {
                Debug.LogError("ACCESSIBILITY PRESENTATION — FAIL: " + exception.Message + "\n" + exception);
            }
            finally
            {
                if (fixture != null) Destroy(fixture);
                RestorePreferences(snapshot);
                AccessibilityPresentationPolicy.RequestRefresh();
                Destroy(gameObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static PreferenceSnapshot CapturePreferences()
        {
            return new PreferenceSnapshot
            {
                HasScale = PlayerPrefs.HasKey(ScaleKey),
                HasMotion = PlayerPrefs.HasKey(MotionKey),
                HasCaption = PlayerPrefs.HasKey(CaptionKey),
                Scale = PlayerPrefs.GetInt(ScaleKey, 0),
                Motion = PlayerPrefs.GetInt(MotionKey, 0),
                Caption = PlayerPrefs.GetInt(CaptionKey, 0),
            };
        }

        private static void RestorePreferences(PreferenceSnapshot snapshot)
        {
            if (snapshot.HasScale) GameSettings.InterfaceScaleIndex = snapshot.Scale;
            else { PlayerPrefs.DeleteKey(ScaleKey); GameSettings.InterfaceScaleIndex = 0; PlayerPrefs.DeleteKey(ScaleKey); }
            if (snapshot.HasMotion) GameSettings.ReducedMotion = snapshot.Motion == 1;
            else { PlayerPrefs.DeleteKey(MotionKey); GameSettings.ReducedMotion = false; PlayerPrefs.DeleteKey(MotionKey); }
            if (snapshot.HasCaption) GameSettings.CaptionBacking = snapshot.Caption == 1;
            else { PlayerPrefs.DeleteKey(CaptionKey); GameSettings.CaptionBacking = false; PlayerPrefs.DeleteKey(CaptionKey); }
        }
    }
}
#endif
