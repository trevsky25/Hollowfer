using System.Collections;
using System.Collections.Generic;
using Hollowfen.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Cinematic "living painting" layer for the main menu. Runs on the MainMenuScreen Canvas
    /// (Screen-Space-Overlay), so every effect is UI-native and composites correctly without a
    /// camera / post-processing volume. Adds: drifting warm motes, soft low mist, a painterly
    /// vignette + warm grade, a slow Ken Burns drift on the hero art, and a staggered ink-bleed
    /// reveal of the title block on open. No new art assets — sprites are generated procedurally.
    /// Finds the existing menu elements by name so it needs no serialized wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuCinematics : MonoBehaviour
    {
        [Header("Motes (drifting golden spores)")]
        [SerializeField] private int _moteCount = 34;
        [SerializeField] private Color _moteColor = new Color(1f, 0.88f, 0.60f, 1f); // warm gold spore
        [SerializeField] private float _moteMinSize = 4f;
        [SerializeField] private float _moteMaxSize = 14f;
        [SerializeField] private float _moteDrift = 8f;       // px/sec upward baseline

        [Header("Mist")]
        [SerializeField] private int _mistCount = 5;
        [SerializeField] private Color _mistColor = new Color(0.74f, 0.77f, 0.70f, 0.05f); // pale sage

        [Header("Grade")]
        [SerializeField] private float _vignetteStrength = 0.40f; // softened so the hero reads true-colour

        [Header("Text-side warm gradient")]
        // The warm/gold tint now lives ONLY behind the left text column, cleanly fading out before the
        // hero — so Wren foraging shows the image's true colour (batch-54, Trevor's note).
        [SerializeField] private Color _leftWarmColor = new Color(0.32f, 0.22f, 0.09f, 1f); // warm amber
        [Header("Ken Burns")]
        [SerializeField] private float _kenBurnsScale = 1.06f;
        [SerializeField] private float _kenBurnsSeconds = 48f;
        [SerializeField] private float _kenBurnsDrift = 18f;

        [Header("Reveal")]
        [SerializeField] private bool _playReveal = true;

        private RectTransform _canvasRect;
        private RectTransform _bg;
        private Vector3 _bgBaseScale;
        private Vector2 _bgBasePos;
        private readonly List<Mote> _motes = new List<Mote>();
        private readonly List<Mist> _mist = new List<Mist>();
        private float _t;
        private bool _built;
        private GameObject _moteRoot;
        private GameObject _mistRoot;
        private bool _lastReducedMotion;
        private readonly Dictionary<RectTransform, Vector2> _revealPositions =
            new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> _revealScales =
            new Dictionary<RectTransform, Vector3>();

        private static Sprite _dotSprite;
        private static Sprite _vignetteSprite;
        private static Sprite _leftFadeSprite;

        private class Mote
        {
            public RectTransform rt; public CanvasGroup cg;
            public Vector2 vel; public float phase, swayFreq, swayAmp, baseAlpha, twinkle, size;
            public float w, h;
        }
        private class Mist { public RectTransform rt; public float vx; public float w; }

        private void OnEnable()
        {
            if (_built)
            {
                StopAllCoroutines();
                FinishRevealState();
                ApplyMotionPreference();
                if (_playReveal && !GameSettings.ReducedMotion) StartCoroutine(RevealRoutine());
                return;
            }
            _canvasRect = GetComponent<RectTransform>();
            if (_canvasRect == null) { var c = GetComponentInParent<Canvas>(); if (c != null) _canvasRect = c.GetComponent<RectTransform>(); }
            if (_canvasRect == null) return;
            EnsureSprites();
            Build();
            _built = true;
            ApplyMotionPreference();
            if (_playReveal && !GameSettings.ReducedMotion) StartCoroutine(RevealRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            FinishRevealState();
        }

        private void Build()
        {
            Rect r = _canvasRect.rect;
            float w = r.width, h = r.height;

            // Hero art for Ken Burns
            var bgGo = FindChild(_canvasRect, "BG_WrenImage");
            if (bgGo != null) { _bg = bgGo.GetComponent<RectTransform>(); _bgBaseScale = _bg.localScale; _bgBasePos = _bg.anchoredPosition; }

            // ---- Mist container (just above the hero, below the legibility gradients) ----
            var mistRoot = NewLayer("Cinematic_Mist", 1);
            _mistRoot = mistRoot.gameObject;
            for (int i = 0; i < _mistCount; i++)
            {
                var m = new Mist();
                var go = new GameObject("Mist_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = go.GetComponent<RectTransform>(); rt.SetParent(mistRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                float mw = Random.Range(w * 0.45f, w * 0.8f);
                float mh = mw * Random.Range(0.28f, 0.42f);
                rt.sizeDelta = new Vector2(mw, mh);
                rt.anchoredPosition = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), Random.Range(-h * 0.5f, -h * 0.12f));
                var img = go.GetComponent<Image>(); img.sprite = _dotSprite; img.raycastTarget = false;
                var col = _mistColor; col.a *= Random.Range(0.6f, 1.2f); img.color = col;
                m.rt = rt; m.w = mw; m.vx = Random.Range(3f, 8f) * (Random.value < 0.5f ? -1f : 1f);
                _mist.Add(m);
            }

            // ---- Mote container (above mist) ----
            var moteRoot = NewLayer("Cinematic_Motes", 2);
            _moteRoot = moteRoot.gameObject;
            for (int i = 0; i < _moteCount; i++)
            {
                var go = new GameObject("Mote_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                var rt = go.GetComponent<RectTransform>(); rt.SetParent(moteRoot, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                float size = Random.Range(_moteMinSize, _moteMaxSize);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(Random.Range(-w * 0.5f, w * 0.5f), Random.Range(-h * 0.5f, h * 0.5f));
                var img = go.GetComponent<Image>(); img.sprite = _dotSprite; img.color = _moteColor; img.raycastTarget = false;
                var cg = go.GetComponent<CanvasGroup>();
                var mo = new Mote
                {
                    rt = rt, cg = cg, size = size, w = w, h = h,
                    vel = new Vector2(Random.Range(-4f, 4f), _moteDrift * Random.Range(0.5f, 1.4f)),
                    phase = Random.Range(0f, Mathf.PI * 2f),
                    swayFreq = Random.Range(0.15f, 0.5f),
                    swayAmp = Random.Range(6f, 20f),
                    baseAlpha = Random.Range(0.45f, 1f) * Mathf.Lerp(0.6f, 1f, size / _moteMaxSize),
                    twinkle = Random.Range(0.3f, 0.9f)
                };
                cg.alpha = mo.baseAlpha;
                _motes.Add(mo);
            }

            // ---- Vignette + warm grade (topmost atmosphere, still below the text card) ----
            int textIndex = ChildIndex(_canvasRect, "TextCard");
            int gradeInsert = textIndex >= 0 ? textIndex : _canvasRect.childCount;

            var vg = NewFullScreen("Cinematic_Vignette", gradeInsert);
            var vgImg = vg.GetComponent<Image>(); vgImg.sprite = _vignetteSprite;
            vgImg.color = new Color(0.04f, 0.05f, 0.06f, _vignetteStrength);

            // batch-60: the warm/gold text-side gradient (Cinematic_LeftWarm) was REMOVED at Trevor's
            // request — the hero image reads cleaner at true colour. The neutral dark legibility
            // gradients (scene Overlay_Left/BottomGradient) + the vignette still carry the text.
            // The _leftWarm* fields + _leftFadeSprite are retained (unused) so the band can be
            // restored quickly if wanted.
        }

        private void Update()
        {
            if (!_built) return;
            if (_lastReducedMotion != GameSettings.ReducedMotion) ApplyMotionPreference();
            if (GameSettings.ReducedMotion)
            {
                if (_bg != null)
                {
                    _bg.localScale = _bgBaseScale;
                    _bg.anchoredPosition = _bgBasePos;
                }
                return;
            }
            _t += Time.unscaledDeltaTime;
            float dt = Time.unscaledDeltaTime;

            // Ken Burns — gentle ping-pong scale + drift on the hero
            if (_bg != null)
            {
                float k = (Mathf.Sin(_t / _kenBurnsSeconds * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f; // 0..1
                float s = Mathf.Lerp(1f, _kenBurnsScale, k);
                _bg.localScale = _bgBaseScale * s;
                _bg.anchoredPosition = _bgBasePos + new Vector2(Mathf.Sin(_t * 0.05f) * _kenBurnsDrift, Mathf.Cos(_t * 0.037f) * _kenBurnsDrift * 0.6f);
            }

            // Motes — drift, sway, wrap, twinkle
            for (int i = 0; i < _motes.Count; i++)
            {
                var m = _motes[i];
                Vector2 p = m.rt.anchoredPosition;
                p += m.vel * dt;
                p.x += Mathf.Sin(_t * m.swayFreq + m.phase) * m.swayAmp * dt;
                float hw = m.w * 0.5f + 20f, hh = m.h * 0.5f + 20f;
                if (p.y > hh) { p.y = -hh; p.x = Random.Range(-hw, hw); }
                if (p.x > hw) p.x = -hw; else if (p.x < -hw) p.x = hw;
                m.rt.anchoredPosition = p;
                m.cg.alpha = m.baseAlpha * (0.55f + 0.45f * Mathf.Sin(_t * m.twinkle + m.phase));
            }

            // Mist — slow horizontal drift, wrap
            for (int i = 0; i < _mist.Count; i++)
            {
                var m = _mist[i];
                Vector2 p = m.rt.anchoredPosition;
                p.x += m.vx * dt;
                float bound = _canvasRect.rect.width * 0.5f + m.w * 0.5f;
                if (p.x > bound) p.x = -bound; else if (p.x < -bound) p.x = bound;
                m.rt.anchoredPosition = p;
            }
        }

        private void ApplyMotionPreference()
        {
            _lastReducedMotion = GameSettings.ReducedMotion;
            if (_moteRoot != null) _moteRoot.SetActive(!_lastReducedMotion);
            if (_mistRoot != null) _mistRoot.SetActive(!_lastReducedMotion);
            if (!_lastReducedMotion) return;
            FinishRevealState();
            if (_bg != null)
            {
                _bg.localScale = _bgBaseScale;
                _bg.anchoredPosition = _bgBasePos;
            }
        }

        // ---- Ink-bleed staggered reveal of the title block ----
        private IEnumerator RevealRoutine()
        {
            var card = FindChild(_canvasRect, "TextCard");
            if (card == null) yield break;
            FinishRevealState();
            string[] order = { "Text_Eyebrow", "Text_Title", "Divider", "Text_Subtitle", "Text_Tagline", "Btn_NewGame", "NavRow" };
            var groups = new List<CanvasGroup>();
            var rects = new List<RectTransform>();
            foreach (var n in order)
            {
                var t = FindChild(card, n);
                if (t == null) continue;
                var cg = t.GetComponent<CanvasGroup>(); if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                var rt = t.GetComponent<RectTransform>();
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                groups.Add(cg); rects.Add(rt);
            }
            yield return null; // let layout settle
            // Cache the authored positions only after layout has resolved. Capturing them above
            // this yield records every VerticalLayoutGroup child at its pre-layout origin and an
            // interrupted reveal can then restore the whole menu off the left edge of the canvas.
            foreach (RectTransform rt in rects)
            {
                if (!_revealPositions.ContainsKey(rt)) _revealPositions.Add(rt, rt.anchoredPosition);
                if (!_revealScales.ContainsKey(rt)) _revealScales.Add(rt, rt.localScale);
            }
            float stagger = 0.16f;
            for (int i = 0; i < groups.Count; i++)
            {
                StartCoroutine(FadeRise(groups[i], rects[i], i == 1 ? 1.1f : 0.7f, i == 1)); // title (index 1) gets a longer ink-bleed
                yield return new WaitForSecondsRealtime(stagger);
            }
        }

        private IEnumerator FadeRise(CanvasGroup cg, RectTransform rt, float dur, bool inkBleed)
        {
            Vector2 basePos = _revealPositions.TryGetValue(rt, out Vector2 position)
                ? position : rt.anchoredPosition;
            Vector3 baseScale = _revealScales.TryGetValue(rt, out Vector3 scale)
                ? scale : rt.localScale;
            float rise = 14f;
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(e / dur);
                float ease = 1f - Mathf.Pow(1f - u, 3f); // ease-out cubic
                cg.alpha = ease;
                rt.anchoredPosition = basePos + new Vector2(0f, (1f - ease) * rise);
                if (inkBleed) rt.localScale = baseScale * Mathf.Lerp(1.035f, 1f, ease); // bloom-in
                yield return null;
            }
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
            rt.anchoredPosition = basePos;
            rt.localScale = baseScale;
        }

        // Completes an interrupted reveal so invisible CanvasGroups can never retain input.
        // The Editor-only audit harness also invokes this after deterministic frame stepping.
        private void FinishRevealState()
        {
            if (_canvasRect == null) return;
            var card = FindChild(_canvasRect, "TextCard");
            if (card == null) return;
            string[] order = { "Text_Eyebrow", "Text_Title", "Divider", "Text_Subtitle", "Text_Tagline", "Btn_NewGame", "NavRow" };
            foreach (string name in order)
            {
                RectTransform rt = FindChild(card, name);
                if (rt == null) continue;
                CanvasGroup group = rt.GetComponent<CanvasGroup>();
                if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
                if (_revealPositions.TryGetValue(rt, out Vector2 position))
                    rt.anchoredPosition = position;
                if (_revealScales.TryGetValue(rt, out Vector3 scale))
                    rt.localScale = scale;
            }
        }

        // ---- helpers ----
        private RectTransform NewLayer(string name, int siblingIndex)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(Mathf.Min(siblingIndex, _canvasRect.childCount - 1));
            return rt;
        }

        private RectTransform NewFullScreen(string name, int siblingIndex)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_canvasRect, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>(); img.raycastTarget = false;
            rt.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, _canvasRect.childCount - 1));
            return rt;
        }

        private static RectTransform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            var t = root.Find(name);
            if (t != null) return t as RectTransform;
            foreach (Transform c in root) { var r = FindChild(c, name); if (r != null) return r; }
            return null;
        }

        private static int ChildIndex(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++) if (parent.GetChild(i).name == name) return i;
            return -1;
        }

        private static void EnsureSprites()
        {
            if (_dotSprite == null) _dotSprite = MakeRadial(64, false, 1.6f);
            if (_vignetteSprite == null) _vignetteSprite = MakeRadial(256, true, 2.2f);
            if (_leftFadeSprite == null) _leftFadeSprite = MakeHorizontalFade(256, 1.9f);
        }

        /// Horizontal alpha ramp: opaque at the left edge (x=0) easing to transparent at the right (x=1).
        private static Sprite MakeHorizontalFade(int width, float falloff)
        {
            int h = 4;
            var tex = new Texture2D(width, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[width * h];
            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);           // 0 left .. 1 right
                float a = Mathf.Pow(1f - t, falloff);       // 1 left -> 0 right
                byte b = (byte)(Mathf.Clamp01(a) * 255f);
                for (int y = 0; y < h; y++) px[y * width + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// Radial alpha sprite. invert=false → bright center fading out (mote/mist glow).
        /// invert=true → transparent center darkening to edges (vignette). falloff shapes the curve.
        private static Sprite MakeRadial(int size, bool invert, float falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float maxD = c;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / maxD; // 0 center .. 1 edge
                d = Mathf.Clamp01(d);
                float a = invert ? Mathf.Pow(d, falloff) : Mathf.Pow(1f - d, falloff);
                byte b = (byte)(Mathf.Clamp01(a) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, b);
            }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
