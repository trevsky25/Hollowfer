using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Cinematic "welcome" load screen (batch-38/39). For a NEW GAME (SaveSlotScreen sets
    // NextIsCinematic) it shows the homecoming hero image at the narration's Ken-Burns start-state
    // with a "Chapter One / Homecoming" title, letterbox bars, drifting spore motes, and a discreet
    // loading line — while Scene_Hollowfen loads behind it. UIManager fades it out (FadeOutAndClose)
    // once the in-scene narration is up (same image), so the handoff is image→image seamless.
    // Continue/Load keep the plain rolling-dots text (cinematic root stays hidden).
    public class LoadingScreen : UIScreen
    {
        // Set true by SaveSlotScreen right before a NEW-GAME LoadSceneAndOpen; consumed in OnOpen.
        // NEW GAME → the cinematic welcome + seamless image→narration handoff.
        public static bool NextIsCinematic;
        // CONTINUE / LOAD (batch-50) → the SAME cinematic welcome card, but NO seamless handoff (there's
        // no intro to dissolve into on a load); UIManager just fades the card out to the game.
        public static bool NextIsContinue;

        [SerializeField] private Text _label;
        [SerializeField] private string _baseText = "Traveling to Hollowfen";
        [SerializeField] private float _dotInterval = 0.4f;
        [SerializeField, Tooltip("Homecoming hero image for the cinematic welcome.")]
        private Sprite _heroSprite;
        [SerializeField] private string _welcomeEyebrow = "CHAPTER ONE";
        [SerializeField] private string _welcomeTitle = "Homecoming";
        [SerializeField] private string _returnEyebrow = "RETURNING TO";
        [SerializeField] private string _returnTitle = "Hollowfen";

        // Must match NarrationOverlay's Ken-Burns A-state + letterbox for a seamless handoff.
        private const float KbScaleA = 1.20f;
        private static readonly Vector2 KbPosA = new Vector2(150f, 46f);
        private const float LetterboxHeight = 116f;

        private Coroutine _dotAnim;
        private bool _cineBuilt;
        private bool _isCinematic;
        private RectTransform _cineRoot, _welcomeGroup, _loadingLine;
        private CanvasGroup _welcomeCg;
        private TMP_Text _eyebrowTmp, _titleTmp; // cached so per-open copy survives the build-once guard
        private RectTransform _marqueeStreak;
        private float _marqueeT;

        // Welcome loading states (batch-42, reworked batch-46: no press gate — the card stays a
        // visibly MOVING loading screen: pulsing line + gold marquee + motes; the text flips to
        // "entering Hollowfen" right before the scene-integration stall so the brief hitch reads
        // as arrival, not a freeze).
        private const string LoadingText = "gathering the last light";
        private const string EnteringText = "entering Hollowfen";
        private int _welcomeState; // 0 = loading, 2 = activating/entering

        private readonly List<RectTransform> _motes = new List<RectTransform>();
        private readonly List<CanvasGroup> _moteCg = new List<CanvasGroup>();
        private readonly List<Vector4> _moteData = new List<Vector4>(); // velX, velY, phase, baseAlpha
        private float _cw = 1920f, _ch = 1080f, _mt;
        private static Sprite _dotSprite;
        private static Sprite _vignetteSprite;

        public bool Cinematic => _isCinematic;

        public override void OnOpen()
        {
            base.OnOpen();
            // New game OR continue/load both get the cinematic card (batch-50); only the seamless
            // narration handoff downstream is new-game-only (UIManager keys that off cinematicHandoff).
            bool returning = NextIsContinue && !NextIsCinematic;
            _isCinematic = _heroSprite != null && (NextIsCinematic || NextIsContinue);
            NextIsCinematic = false;
            NextIsContinue = false;

            if (_isCinematic)
            {
                _welcomeState = 0;
                BuildCinematic();
                // Per-open copy (BuildCinematic is guarded build-once, so set the text every open).
                if (_eyebrowTmp != null) _eyebrowTmp.text = returning ? _returnEyebrow : _welcomeEyebrow;
                if (_titleTmp != null) _titleTmp.text = returning ? _returnTitle : _welcomeTitle;
                if (_cineRoot != null) _cineRoot.gameObject.SetActive(true);
                if (_label != null) _label.gameObject.SetActive(false);
                if (CanvasGroup != null) CanvasGroup.alpha = 1f;
                StartCoroutine(PopWelcome());
                if (_loadingLine != null) StartCoroutine(AnimateLoadingLine());
            }
            else
            {
                if (_cineRoot != null) _cineRoot.gameObject.SetActive(false);
                if (_label != null) _label.gameObject.SetActive(true);
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

        // Async load done, activation about to run — flip the line to "entering Hollowfen".
        public void BeginActivation() { _welcomeState = 2; }

        // Cross-fade the whole card out to reveal the (same-image) narration behind it.
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
            Transform parent = canvas != null ? canvas.transform : transform;
            _cw = ((RectTransform)parent).rect.width;
            _ch = ((RectTransform)parent).rect.height;
            if (_cw < 1f) { _cw = 1920f; _ch = 1080f; }

            // Self-contained cinematic root — leaves the plain-mode BG/label untouched.
            var rootGo = new GameObject("CineRoot", typeof(RectTransform));
            _cineRoot = rootGo.GetComponent<RectTransform>();
            _cineRoot.SetParent(parent, false);
            _cineRoot.anchorMin = Vector2.zero; _cineRoot.anchorMax = Vector2.one;
            _cineRoot.offsetMin = Vector2.zero; _cineRoot.offsetMax = Vector2.zero;
            // Own nested canvas so the welcome card always renders ABOVE the game HUD/minimap during a
            // continue/load (batch-50): the loading screen shares UIManager's canvas at sortingOrder 10,
            // which ties with _MiniMapCanvas — the minimap poked through the card's corner. A high
            // overrideSorting order covers everything (still below the FadeOverlay at 32767).
            var cineCanvas = rootGo.AddComponent<Canvas>();
            cineCanvas.overrideSorting = true;
            cineCanvas.sortingOrder = 200;
            Transform root = _cineRoot;

            // Black base (fills the letterbox gaps at other aspects).
            var black = NewImage("Black", root, Color.black);
            Stretch(black.rectTransform);

            // Hero at the narration's Ken-Burns A-state.
            var hero = NewImage("Hero", root, Color.white);
            hero.sprite = _heroSprite; hero.preserveAspect = false;
            var hRT = hero.rectTransform;
            hRT.anchorMin = hRT.anchorMax = new Vector2(0.5f, 0.5f); hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(1920f, 1080f);
            hRT.localScale = Vector3.one * KbScaleA;
            hRT.anchoredPosition = KbPosA;

            // Bottom scrim.
            // Full-screen vignette (dark bottom for the title + dark top for the letterbox blend,
            // clear middle) — full-screen so there's no mid-screen rect edge / cut-off band, and it
            // matches NarrationOverlay for the seamless handoff (batch-40 streak fix).
            EnsureVignette();
            var scrim = NewImage("Scrim", root, Color.black);
            scrim.sprite = _vignetteSprite;
            scrim.type = Image.Type.Simple;
            Stretch(scrim.rectTransform);

            BuildMotes(root);

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
            _eyebrowTmp = eyebrow;
            eyebrow.alignment = TextAlignmentOptions.Center;
            var eRT = eyebrow.rectTransform;
            eRT.anchorMin = new Vector2(0f, 0f); eRT.anchorMax = new Vector2(1f, 0f); eRT.pivot = new Vector2(0.5f, 0f);
            eRT.sizeDelta = new Vector2(0f, 30f); eRT.anchoredPosition = new Vector2(0f, 92f);

            var title = UICanvasUtil.NewHeading("WelcomeTitle", _welcomeGroup, _welcomeTitle, 72f,
                new Color(0.96f, 0.93f, 0.85f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
            _titleTmp = title;
            var tRT = title.rectTransform;
            tRT.anchorMin = new Vector2(0f, 0f); tRT.anchorMax = new Vector2(1f, 0f); tRT.pivot = new Vector2(0.5f, 0f);
            tRT.sizeDelta = new Vector2(0f, 90f); tRT.anchoredPosition = new Vector2(0f, 0f);

            var ll = UICanvasUtil.NewBody("LoadingLine", root, LoadingText, 15f,
                new Color(0.90f, 0.88f, 0.80f, 0.4f), FontStyles.Italic, TextAlignmentOptions.Center);
            _loadingLine = ll.rectTransform;
            _loadingLine.anchorMin = new Vector2(0.5f, 0f); _loadingLine.anchorMax = new Vector2(0.5f, 0f);
            _loadingLine.pivot = new Vector2(0.5f, 0f);
            _loadingLine.sizeDelta = new Vector2(700f, 24f);
            _loadingLine.anchoredPosition = new Vector2(0f, LetterboxHeight * 0.42f);

            // Moving loading marquee (batch-46): a thin ink track with a gold streak sweeping
            // across — an unambiguous "this is loading" motion cue above the loading line.
            // RectMask2D (never Mask — scroll-viewport gotcha) clips the streak to the track.
            var track = NewImage("MarqueeTrack", root, new Color(0.90f, 0.88f, 0.80f, 0.13f));
            var trackRT = track.rectTransform;
            trackRT.anchorMin = new Vector2(0.5f, 0f); trackRT.anchorMax = new Vector2(0.5f, 0f);
            trackRT.pivot = new Vector2(0.5f, 0f);
            trackRT.sizeDelta = new Vector2(320f, 3f);
            trackRT.anchoredPosition = new Vector2(0f, LetterboxHeight * 0.42f + 32f);
            track.gameObject.AddComponent<RectMask2D>();
            var streak = NewImage("Streak", track.transform, new Color(0.965f, 0.812f, 0.475f, 0.9f));
            _marqueeStreak = streak.rectTransform;
            _marqueeStreak.anchorMin = new Vector2(0f, 0f); _marqueeStreak.anchorMax = new Vector2(0f, 1f);
            _marqueeStreak.pivot = new Vector2(0.5f, 0.5f);
            _marqueeStreak.sizeDelta = new Vector2(90f, 0f);
            _marqueeStreak.anchoredPosition = new Vector2(-45f, 0f);
        }

        private void BuildMotes(Transform root)
        {
            EnsureDot();
            var container = new GameObject("Motes", typeof(RectTransform));
            var crt = container.GetComponent<RectTransform>();
            crt.SetParent(root, false);
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            for (int i = 0; i < 26; i++)
            {
                var go = new GameObject("Mote_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                var rt = go.GetComponent<RectTransform>(); rt.SetParent(crt, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                float size = UnityEngine.Random.Range(4f, 12f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-_cw * 0.5f, _cw * 0.5f), UnityEngine.Random.Range(-_ch * 0.5f, _ch * 0.5f));
                var img = go.GetComponent<Image>(); img.sprite = _dotSprite; img.raycastTarget = false; img.color = new Color(1f, 0.88f, 0.6f, 1f);
                var cg = go.GetComponent<CanvasGroup>();
                float baseA = UnityEngine.Random.Range(0.4f, 1f) * Mathf.Lerp(0.6f, 1f, size / 12f);
                cg.alpha = baseA;
                _motes.Add(rt); _moteCg.Add(cg);
                _moteData.Add(new Vector4(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(5f, 12f), UnityEngine.Random.Range(0f, 6.28f), baseA));
            }
        }

        private void Update()
        {
            if (_motes.Count == 0 || _cineRoot == null || !_cineRoot.gameObject.activeInHierarchy) return;
            // Marquee streak sweeps the track continuously (unscaled — the load doesn't touch timeScale).
            if (_marqueeStreak != null)
            {
                _marqueeT += Time.unscaledDeltaTime;
                _marqueeStreak.anchoredPosition = new Vector2(Mathf.Repeat(_marqueeT * 170f, 410f) - 45f, 0f);
            }
            _mt += Time.unscaledDeltaTime; float dt = Time.unscaledDeltaTime;
            float hw = _cw * 0.5f + 20f, hh = _ch * 0.5f + 20f;
            for (int i = 0; i < _motes.Count; i++)
            {
                var d = _moteData[i]; var p = _motes[i].anchoredPosition;
                p.x += (d.x + Mathf.Sin(_mt * 0.4f + d.z) * 10f) * dt; p.y += d.y * dt;
                if (p.y > hh) { p.y = -hh; p.x = UnityEngine.Random.Range(-hw, hw); }
                if (p.x > hw) p.x = -hw; else if (p.x < -hw) p.x = hw;
                _motes[i].anchoredPosition = p;
                _moteCg[i].alpha = d.w * (0.55f + 0.45f * Mathf.Sin(_mt * 0.7f + d.z));
            }
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
            var cg = _loadingLine.GetComponent<CanvasGroup>();
            if (cg == null) cg = _loadingLine.gameObject.AddComponent<CanvasGroup>();
            float t = 0f; int dots = 0; float nextDot = 0f;
            while (true)
            {
                t += Time.unscaledDeltaTime;
                if (_welcomeState == 2)
                {
                    tmp.text = EnteringText;
                    cg.alpha = 0.9f;
                }
                else
                {
                    // Loading: the pulsing "gathering the last light" with cycling dots.
                    cg.alpha = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
                    if (t >= nextDot) { tmp.text = LoadingText + new string('.', dots); dots = (dots + 1) % 4; nextDot = t + _dotInterval; }
                }
                yield return null;
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

        // ---- tiny UI helpers (self-contained, don't touch UICanvasUtil styling) ----
        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void MakeBar(Transform parent, string name, float anchorY)
        {
            var img = NewImage(name, parent, Color.black);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, LetterboxHeight);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void EnsureDot()
        {
            if (_dotSprite != null) return;
            int s = 48; var tex = new Texture2D(s, s, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            float c = (s - 1) * 0.5f; var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float dd = Mathf.Clamp01(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c);
                byte b = (byte)(Mathf.Pow(1f - dd, 1.6f) * 255f);
                px[y * s + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void EnsureVignette()
        {
            if (_vignetteSprite != null) return;
            int h = 256; var tex = new Texture2D(4, h, TextureFormat.RGBA32, false); tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[4 * h];
            for (int y = 0; y < h; y++)
            {
                float fy = y / (float)(h - 1);
                float tb = Mathf.Clamp01((0.42f - fy) / 0.42f); tb = tb * tb * (3f - 2f * tb); // bottom
                float tt = Mathf.Clamp01((fy - 0.84f) / 0.16f); tt = tt * tt * (3f - 2f * tt); // top
                byte b = (byte)(Mathf.Max(tb * 0.88f, tt * 0.70f) * 255f);
                for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            _vignetteSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
