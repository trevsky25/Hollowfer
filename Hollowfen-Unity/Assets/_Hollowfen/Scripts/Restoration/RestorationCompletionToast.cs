using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Settings;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Restoration
{
    /// <summary>Non-blocking first-use confirmation for a project and its permanent benefit.</summary>
    public sealed class RestorationCompletionToast : MonoBehaviour
    {
        private static RestorationCompletionToast _instance;
        private CanvasGroup _group;
        private RectTransform _panel;
        private TMP_Text _title;
        private TMP_Text _benefit;
        private RectTransform _check;
        private Coroutine _routine;

        public static void Show(RestorationProjectData project)
        {
            if (project == null) return;
            if (_instance == null)
            {
                var host = new GameObject("_RestorationCompletionToast", typeof(RectTransform));
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<RestorationCompletionToast>();
                _instance.Build();
            }
            _instance.Present(project);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // This is a non-blocking gameplay notification, not a modal. Keep it above the
            // HUD but below every full-screen interaction so it can never cover a decision.
            canvas.sortingOrder = 18;
            gameObject.AddComponent<CanvasScaler>().Init1080();
            gameObject.AddComponent<GraphicRaycaster>().enabled = false;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _panel = UICanvasUtil.NewRect("Panel", transform);
            UICanvasUtil.SetRect(_panel, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(.5f, 0f), new Vector2(720f, 122f), new Vector2(0f, 130f));
            UICanvasUtil.MakeRoundedPanel(_panel,
                new Color(HollowfenPalette.InkSoft.r, HollowfenPalette.InkSoft.g,
                    HollowfenPalette.InkSoft.b, .97f), 20, .2f);

            var checkSurface = UICanvasUtil.NewImage("Check", _panel,
                HollowfenPalette.Gold, false).GetComponent<Image>();
            checkSurface.sprite = UICanvasUtil.Circle(48);
            _check = checkSurface.rectTransform;
            UICanvasUtil.SetRect(_check, new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(.5f, .5f), new Vector2(58f, 58f), new Vector2(50f, 0f));
            var glyph = UICanvasUtil.NewBody("Glyph", _check, "<sprite name=\"ui_check\">", 30f,
                HollowfenPalette.InkDeep, FontStyles.Bold, TextAlignmentOptions.Center);
            UICanvasUtil.Stretch(glyph.rectTransform);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", _panel,
                Localization.Get("restoration.completed.eyebrow"), 18f,
                HollowfenPalette.Gold, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(-142f, 20f), new Vector2(96f, -18f));
            _title = UICanvasUtil.NewHeading("Title", _panel, "", 30f,
                HollowfenPalette.Cream, FontStyles.Normal, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(-142f, 38f), new Vector2(96f, -42f));
            _benefit = UICanvasUtil.NewBody("Benefit", _panel, "", 18f,
                HollowfenPalette.Moss, FontStyles.Italic, TextAlignmentOptions.Left);
            UICanvasUtil.SetRect(_benefit.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(-142f, 24f), new Vector2(96f, 18f));
        }

        private void Present(RestorationProjectData project)
        {
            _title.text = Localization.Get(project.TitleId);
            _benefit.text = string.IsNullOrWhiteSpace(project.BenefitId) ? "" :
                string.Format(Localization.Get("restoration.completed.benefit"),
                    Localization.Get(project.BenefitId));
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PresentRoutine());
        }

        private IEnumerator PresentRoutine()
        {
            GameplaySfx.QuestComplete();
            if (GameSettings.ReducedMotion)
            {
                _group.alpha = 1f;
                _panel.anchoredPosition = new Vector2(0f, 130f);
                _check.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(3.1f);
                _group.alpha = 0f;
                _routine = null;
                yield break;
            }
            _group.alpha = 0f;
            _panel.anchoredPosition = new Vector2(0f, 104f);
            _check.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < .38f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .38f));
                _group.alpha = t;
                _panel.anchoredPosition = Vector2.Lerp(new Vector2(0f, 104f),
                    new Vector2(0f, 130f), t);
                _check.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f,
                    1f - Mathf.Pow(1f - Mathf.Clamp01(t * 1.15f), 3f));
                yield return null;
            }
            _group.alpha = 1f;
            _check.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(3.1f);
            elapsed = 0f;
            while (elapsed < .35f)
            {
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / .35f);
                yield return null;
            }
            _group.alpha = 0f;
            _routine = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
