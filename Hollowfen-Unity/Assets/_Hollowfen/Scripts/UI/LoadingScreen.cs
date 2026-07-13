using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Cinematic "welcome" load screen (batch-38). For a new game it shows the homecoming hero
    // image at the narration's Ken-Burns start-state with a "Chapter One / Homecoming" welcome
    // title that pops up, letterbox bars, and a discreet loading line — while Scene_Hollowfen loads
    // behind it. When the in-scene cinematic narration is up (same image), UIManager fades this out
    // (FadeOutAndClose) so the handoff is image→image seamless. Continue/Load keep the plain text.
    public class LoadingScreen : UIScreen
    {
        [SerializeField] private Text _label;
        [SerializeField] private string _baseText = "Traveling to Hollowfen";
        [SerializeField] private float _dotInterval = 0.4f;
        [SerializeField, Tooltip("Homecoming hero image for the cinematic welcome. Null → plain text load.")]
        private Sprite _heroSprite;
        [SerializeField] private string _welcomeEyebrow = "CHAPTER ONE";
        [SerializeField] private string _welcomeTitle = "Homecoming";

        // Must match NarrationOverlay's Ken-Burns A-state + letterbox for a seamless handoff.
        private const float KbScaleA = 1.20f;
        private static readonly Vector2 KbPosA = new Vector2(150f, 46f);
        private const float LetterboxHeight = 116f;

        private Coroutine _dotAnim;
        private bool _cineBuilt;
        private RectTransform _hero, _welcomeGroup, _loadingLine;
        private CanvasGroup _welcomeCg;

        public bool Cinematic => _heroSprite != null;

        public override void OnOpen()
        {
            base.OnOpen();
            if (Cinematic)
            {
                BuildCinematic();
                if (_label != null) _label.gameObject.SetActive(false);
                if (CanvasGroup != null) CanvasGroup.alpha = 1f;
                StartCoroutine(PopWelcome());
                if (_loadingLine != null) StartCoroutine(AnimateLoadingLine());
            }
            else
            {
                if (_dotAnim != null) StopCoroutine(_dotAnim);
                _dotAnim = StartCoroutine(AnimateDots());
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            if (_dotAnim != null) { StopCoroutine(_dotAnim); _dotAnim = null; }
            if (_label != null) _label.text = _baseText;
            if (CanvasGroup != null) CanvasGroup.alpha = 1f;
        }

        // Cross-fade the whole welcome card out to reveal the (same-image) narration behind it.
        public void FadeOutAndClose(float seconds, Action onDone)
        {
            StartCoroutine(FadeOutRoutine(seconds, onDone));
        }

        private IEnumerator FadeOutRoutine(float seconds, Action onDone)
        {
            var cg = CanvasGroup;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                if (cg != null) cg.alpha = 1f - Mathf.Clamp01(t / seconds);
                yield return null;
            }
            if (cg != null) cg.alpha = 0f;
            onDone?.Invoke();
        }

        private void BuildCinematic()
        {
            if (_cineBuilt) return;
            _cineBuilt = true;

            var canvas = GetComponentInChildren<Canvas>();
            Transform root = canvas != null ? canvas.transform : transform;

            // Hide the legacy plain-text labels — the cinematic welcome replaces them.
            var oldEyebrow = root.Find("Eyebrow"); if (oldEyebrow != null) oldEyebrow.gameObject.SetActive(false);
            var oldLabel = root.Find("LoadingLabel"); if (oldLabel != null) oldLabel.gameObject.SetActive(false);

            // Reuse the existing background image if present; else make one.
            var bg = root.Find("BG_Wren");
            Image heroImg = bg != null ? bg.GetComponent<Image>() : UICanvasUtil.NewImage("Hero", root, Color.white, false).GetComponent<Image>();
            heroImg.sprite = _heroSprite;
            heroImg.color = Color.white;
            heroImg.preserveAspect = false;
            _hero = heroImg.rectTransform;
            _hero.anchorMin = _hero.anchorMax = new Vector2(0.5f, 0.5f);
            _hero.pivot = new Vector2(0.5f, 0.5f);
            _hero.sizeDelta = new Vector2(1920f, 1080f);
            _hero.localScale = Vector3.one * KbScaleA;
            _hero.anchoredPosition = KbPosA;
            _hero.SetAsFirstSibling();

            // Bottom scrim (reuse if present) so the welcome title reads.
            var scrimT = root.Find("Scrim");
            Image scrim = scrimT != null ? scrimT.GetComponent<Image>() : UICanvasUtil.NewImage("Scrim", root, Color.black, false).GetComponent<Image>();
            var sRT = scrim.rectTransform;
            sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f); sRT.pivot = new Vector2(0.5f, 0f);
            sRT.sizeDelta = new Vector2(0f, 620f); sRT.anchoredPosition = Vector2.zero;
            scrim.color = new Color(0f, 0f, 0f, 0.82f);
            scrim.transform.SetSiblingIndex(1);

            // Letterbox bars (match the narration).
            MakeBar(root, "LB_Top", 1f);
            MakeBar(root, "LB_Bot", 0f);

            // Welcome title block (Georgia), lower third, pops up.
            var wg = new GameObject("WelcomeGroup", typeof(RectTransform), typeof(CanvasGroup));
            _welcomeGroup = wg.GetComponent<RectTransform>();
            _welcomeGroup.SetParent(root, false);
            _welcomeGroup.anchorMin = new Vector2(0.5f, 0f); _welcomeGroup.anchorMax = new Vector2(0.5f, 0f);
            _welcomeGroup.pivot = new Vector2(0.5f, 0f);
            _welcomeGroup.sizeDelta = new Vector2(1200f, 260f);
            _welcomeGroup.anchoredPosition = new Vector2(0f, LetterboxHeight + 46f);
            _welcomeCg = wg.GetComponent<CanvasGroup>();

            var eyebrow = UICanvasUtil.NewEyebrow("WelcomeEyebrow", _welcomeGroup, _welcomeEyebrow, 24f,
                new Color(0.78f, 0.66f, 0.42f, 1f));
            var eRT = eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0.5f, 0f); eRT.anchorMax = new Vector2(1f, 0f); eRT.pivot = new Vector2(0.5f, 0f);
            eRT.sizeDelta = new Vector2(0f, 30f); eRT.anchoredPosition = new Vector2(0f, 92f);

            var title = UICanvasUtil.NewHeading("WelcomeTitle", _welcomeGroup, _welcomeTitle, 72f,
                new Color(0.96f, 0.93f, 0.85f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0.5f, 0f); tRT.anchorMax = new Vector2(1f, 0f); tRT.pivot = new Vector2(0.5f, 0f);
            tRT.sizeDelta = new Vector2(0f, 90f); tRT.anchoredPosition = new Vector2(0f, 0f);

            // Discreet loading line at the very bottom.
            var ll = UICanvasUtil.NewBody("LoadingLine", root, "gathering the last light", 15f,
                new Color(0.90f, 0.88f, 0.80f, 0.4f), FontStyles.Italic, TextAlignmentOptions.Center);
            _loadingLine = ll.rectTransform;
            _loadingLine.anchorMin = new Vector2(0.5f, 0f); _loadingLine.anchorMax = new Vector2(0.5f, 0f);
            _loadingLine.pivot = new Vector2(0.5f, 0f);
            _loadingLine.sizeDelta = new Vector2(700f, 24f);
            _loadingLine.anchoredPosition = new Vector2(0f, LetterboxHeight * 0.42f);
        }

        private static void MakeBar(Transform parent, string name, float anchorY)
        {
            var img = UICanvasUtil.NewImage(name, parent, Color.black, false).GetComponent<Image>();
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, LetterboxHeight);
            rt.anchoredPosition = Vector2.zero;
        }

        private IEnumerator PopWelcome()
        {
            if (_welcomeCg == null) yield break;
            _welcomeCg.alpha = 0f;
            _welcomeGroup.localScale = Vector3.one * 1.04f;
            yield return new WaitForSecondsRealtime(0.35f);
            float dur = 0.9f, t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                _welcomeCg.alpha = ease;
                _welcomeGroup.localScale = Vector3.one * Mathf.Lerp(1.04f, 1f, ease);
                yield return null;
            }
            _welcomeCg.alpha = 1f; _welcomeGroup.localScale = Vector3.one;
        }

        private IEnumerator AnimateLoadingLine()
        {
            var tmp = _loadingLine != null ? _loadingLine.GetComponent<TMP_Text>() : null;
            if (tmp == null) yield break;
            string baseTxt = tmp.text; int dots = 0;
            while (true)
            {
                tmp.text = baseTxt + new string('.', dots);
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(_dotInterval);
            }
        }

        private IEnumerator AnimateDots()
        {
            int dots = 0;
            while (true)
            {
                if (_label != null) _label.text = _baseText + new string('.', dots);
                dots = (dots + 1) % 4;
                yield return new WaitForSecondsRealtime(_dotInterval);
            }
        }
    }
}
