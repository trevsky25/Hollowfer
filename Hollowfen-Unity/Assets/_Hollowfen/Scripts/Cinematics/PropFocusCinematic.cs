using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Cinematics
{
    // Reusable "hero item" camera move. Takes over Camera.main and pushes in on a target transform,
    // holds while the prop does its own hero animation (spin/rock), then glides back to the gameplay
    // camera. Used for the mill-key handoff (batch-52) and the journal reveal — the focus push-ins
    // Trevor asked for. Self-created persistent singleton so it survives the interacted prop
    // deactivating. Mirrors DialogueCinematics' takeover (disable CinemachineBrain, cache the gameplay
    // pose, animate on unscaled time, restore + re-enable). Adds cinematic dressing every prop-focus
    // shares: hides the gameplay HUD and slides in letterbox bars, restoring both at the end.
    public class PropFocusCinematic : MonoBehaviour
    {
        public static PropFocusCinematic Instance { get; private set; }
        public bool IsPlaying { get; private set; }

        private const float LetterboxFraction = 0.12f; // bar height as a fraction of 1080

        private Canvas _lbCanvas;
        private RectTransform _lbTop, _lbBot;
        private bool _lbBuilt;
        private readonly List<CanvasGroup> _hudHidden = new List<CanvasGroup>();

        public static PropFocusCinematic Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("_PropFocusCinematic");
                Instance = go.AddComponent<PropFocusCinematic>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Push in on target, hold, restore. distance = how close (m); heightOffset = camera height vs the
        // target; fov = the tight lens at the hold. onPeak fires when the push-in completes (grant/toast);
        // onDone fires after the restore glide (despawn the prop, resume). Suspends player input, hides the
        // HUD, and slides in letterbox bars for the shot.
        // frameDir (optional): the horizontal direction FROM the target TOWARD the camera to frame from —
        // e.g. a door's face normal for a straight-on shot. Zero = frame from the camera's current side.
        public void Play(Transform target, float distance, float heightOffset, float fov,
                         float pushSeconds, float holdSeconds, float restoreSeconds,
                         Action onPeak = null, Action onDone = null, Vector3 frameDir = default(Vector3))
        {
            if (IsPlaying || target == null || Camera.main == null) { onPeak?.Invoke(); onDone?.Invoke(); return; }
            StartCoroutine(Run(target, distance, heightOffset, fov, pushSeconds, holdSeconds, restoreSeconds, onPeak, onDone, frameDir));
        }

        private IEnumerator Run(Transform target, float distance, float heightOffset, float fov,
                                float pushSeconds, float holdSeconds, float restoreSeconds,
                                Action onPeak, Action onDone, Vector3 frameDir)
        {
            var cam = Camera.main;
            IsPlaying = true;
            Foraging.PlayerInteractor.Suspended = true;
            Foraging.PlayerInteractor.SetPlayerInputEnabled(false);
            HideHud();
            BuildLetterbox();
            _lbCanvas.gameObject.SetActive(true);
            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;
            float startFov = cam.fieldOfView;

            // Frame the target from the side the camera is already on (never crosses to the far side),
            // unless a frameDir is given (e.g. a door's face normal → a straight-on shot). Aim at the
            // VISUAL centre (renderer bounds), not the transform pivot — Meshy pivots sit at the origin.
            Vector3 toCam;
            if (frameDir.sqrMagnitude > 0.01f) { toCam = frameDir; toCam.y = 0f; }
            else { toCam = startPos - Center(target); toCam.y = 0f; }
            if (toCam.sqrMagnitude < 0.0001f) toCam = -cam.transform.forward;
            toCam.Normalize();

            // Push in (letterbox slides in with the move).
            float t = 0f;
            while (t < pushSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / pushSeconds);
                float e = u * u * (3f - 2f * u);
                Vector3 c = Center(target);
                Vector3 endPos = c + toCam * distance + Vector3.up * heightOffset;
                Quaternion endRot = Quaternion.LookRotation(c - endPos, Vector3.up);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, e), Quaternion.Slerp(startRot, endRot, e));
                cam.fieldOfView = Mathf.Lerp(startFov, fov, e);
                SetLetterbox(e);
                yield return null;
            }
            onPeak?.Invoke();

            // Hold, re-aiming each frame so a spinning/rocking prop stays framed.
            float h = 0f;
            while (h < holdSeconds)
            {
                h += Time.unscaledDeltaTime;
                Vector3 c = Center(target);
                Vector3 pos = c + toCam * distance + Vector3.up * heightOffset;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(c - pos, Vector3.up));
                cam.fieldOfView = fov;
                yield return null;
            }

            // Restore to the gameplay pose (letterbox slides out), then hand back to the brain.
            Vector3 fromPos = cam.transform.position;
            Quaternion fromRot = cam.transform.rotation;
            float fromFov = cam.fieldOfView;
            t = 0f;
            while (t < restoreSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / restoreSeconds);
                float e = u * u * (3f - 2f * u);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, startPos, e), Quaternion.Slerp(fromRot, startRot, e));
                cam.fieldOfView = Mathf.Lerp(fromFov, startFov, e);
                SetLetterbox(1f - e);
                yield return null;
            }
            cam.transform.SetPositionAndRotation(startPos, startRot);
            cam.fieldOfView = startFov;
            if (brain != null) brain.enabled = true;
            _lbCanvas.gameObject.SetActive(false);
            ShowHud();
            Foraging.PlayerInteractor.Suspended = false;
            Foraging.PlayerInteractor.SetPlayerInputEnabled(true);
            IsPlaying = false;
            onDone?.Invoke();
        }

        // The prop's visual centre — combined renderer bounds if any, else the transform pivot.
        private static Vector3 Center(Transform target)
        {
            var rends = target.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0) return target.position;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.center;
        }

        // ---- HUD + letterbox dressing ----

        private void HideHud()
        {
            _hudHidden.Clear();
            foreach (var n in new[] { "_HUDCanvas", "_MiniMapCanvas" })
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                _hudHidden.Add(cg);
            }
        }

        private void ShowHud()
        {
            foreach (var cg in _hudHidden) if (cg != null) cg.alpha = 1f;
            _hudHidden.Clear();
        }

        private void BuildLetterbox()
        {
            if (_lbBuilt) return;
            _lbBuilt = true;
            var go = new GameObject("_PropFocusLetterbox", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _lbCanvas = go.AddComponent<Canvas>();
            _lbCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _lbCanvas.sortingOrder = 90;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _lbTop = MakeBar("Top", 1f);
            _lbBot = MakeBar("Bot", 0f);
            _lbCanvas.gameObject.SetActive(false);
        }

        private RectTransform MakeBar(string name, float anchorY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(_lbCanvas.transform, false);
            rt.anchorMin = new Vector2(0f, anchorY); rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = new Vector2(0f, 0f);
            var img = go.GetComponent<Image>(); img.color = Color.black; img.raycastTarget = false;
            return rt;
        }

        private void SetLetterbox(float t)
        {
            if (_lbTop == null) return;
            float hgt = 1080f * LetterboxFraction * Mathf.Clamp01(t);
            _lbTop.sizeDelta = new Vector2(0f, hgt);
            _lbBot.sizeDelta = new Vector2(0f, hgt);
        }
    }
}
