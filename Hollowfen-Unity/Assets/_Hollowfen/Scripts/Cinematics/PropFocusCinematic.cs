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
    //
    // batch-62 additions:
    //  - Optional orbital ARC on the push-in (arcDegrees + arcRise) so a hero item is approached on a
    //    sweeping curve from a settling height, not a flat dolly — used for the mill-key handoff.
    //  - HOLD-AT-END mode (holdAtEnd): after the hold the camera STAYS parked on the prop (letterbox up,
    //    HUD hidden, input suspended, brain still off) and fires onDone, instead of gliding back. A later
    //    Restore() call runs the glide-out. Lets the journal reveal dissolve its painted spreads in over
    //    the held book close-up rather than snapping in after a pointless glide back to the room.
    public class PropFocusCinematic : MonoBehaviour
    {
        public static PropFocusCinematic Instance { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsHeld { get; private set; }

        private const float LetterboxFraction = 0.12f; // bar height as a fraction of 1080

        private Canvas _lbCanvas;
        private RectTransform _lbTop, _lbBot;
        private bool _lbBuilt;
        private readonly List<CanvasGroup> _hudHidden = new List<CanvasGroup>();

        // Cached gameplay pose (so a held shot can be restored later, from Restore()).
        private Vector3 _startPos;
        private Quaternion _startRot;
        private float _startFov;
        private CinemachineBrain _brain;
        private float _pendingRestore = 0.55f;
        private NarrativePresentationSession.Lease _inputLease;

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
            _inputLease?.Dispose();
            _inputLease = null;
            if (Instance == this) Instance = null;
        }

        // Push in on target, hold, restore. distance = how close (m); heightOffset = camera height vs the
        // target; fov = the tight lens at the hold. onPeak fires when the push-in completes (grant/toast);
        // onDone fires after the restore glide (despawn the prop, resume). Suspends player input, hides the
        // HUD, and slides in letterbox bars for the shot.
        // frameDir (optional): the horizontal direction FROM the target TOWARD the camera to frame from —
        // e.g. a door's face normal for a straight-on shot. Zero = frame from the camera's current side.
        // arcDegrees (optional): yaw the approach starts swung out by, easing to 0 at the hold — a curved
        // dolly. arcRise (optional): extra height the approach starts raised by, easing to 0 — settles down
        // onto the framing. holdAtEnd (optional): keep the camera parked on the prop after the hold and fire
        // onDone without restoring; call Restore() to glide out later.
        public void Play(Transform target, float distance, float heightOffset, float fov,
                         float pushSeconds, float holdSeconds, float restoreSeconds,
                         Action onPeak = null, Action onDone = null, Vector3 frameDir = default(Vector3),
                         float arcDegrees = 0f, float arcRise = 0f, bool holdAtEnd = false)
        {
            if (IsPlaying || target == null || Camera.main == null) { onPeak?.Invoke(); onDone?.Invoke(); return; }
            StartCoroutine(Run(target, distance, heightOffset, fov, pushSeconds, holdSeconds, restoreSeconds,
                onPeak, onDone, frameDir, arcDegrees, arcRise, holdAtEnd));
        }

        private IEnumerator Run(Transform target, float distance, float heightOffset, float fov,
                                float pushSeconds, float holdSeconds, float restoreSeconds,
                                Action onPeak, Action onDone, Vector3 frameDir,
                                float arcDegrees, float arcRise, bool holdAtEnd)
        {
            var cam = Camera.main;
            IsPlaying = true;
            _pendingRestore = restoreSeconds;
            _inputLease = NarrativePresentationSession.Acquire(this);
            HideHud();
            BuildLetterbox();
            _lbCanvas.gameObject.SetActive(true);
            _brain = cam.GetComponent<CinemachineBrain>();
            if (_brain != null) _brain.enabled = false;

            _startPos = cam.transform.position;
            _startRot = cam.transform.rotation;
            _startFov = cam.fieldOfView;

            // Frame the target from the side the camera is already on (never crosses to the far side),
            // unless a frameDir is given (e.g. a door's face normal → a straight-on shot). Aim at the
            // VISUAL centre (renderer bounds), not the transform pivot — Meshy pivots sit at the origin.
            Vector3 toCam;
            if (frameDir.sqrMagnitude > 0.01f) { toCam = frameDir; toCam.y = 0f; }
            else { toCam = _startPos - Center(target); toCam.y = 0f; }
            if (toCam.sqrMagnitude < 0.0001f) toCam = -cam.transform.forward;
            toCam.Normalize();

            // Push in (letterbox slides in with the move). With an arc, the approach direction starts
            // swung out by arcDegrees and the height starts raised by arcRise, both easing to the final
            // framing — a curved settling move instead of a flat dolly.
            float t = 0f;
            while (t < pushSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / pushSeconds);
                float e = u * u * (3f - 2f * u);
                Vector3 c = Center(target);
                Vector3 dir = arcDegrees != 0f ? Quaternion.AngleAxis(Mathf.Lerp(arcDegrees, 0f, e), Vector3.up) * toCam : toCam;
                float hOff = heightOffset + Mathf.Lerp(arcRise, 0f, e);
                Vector3 endPos = c + dir * distance + Vector3.up * hOff;
                Quaternion endRot = Quaternion.LookRotation(c - endPos, Vector3.up);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(_startPos, endPos, e), Quaternion.Slerp(_startRot, endRot, e));
                cam.fieldOfView = Mathf.Lerp(_startFov, fov, e);
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

            // Hold-at-end: leave the camera parked on the prop and hand off. Restore() finishes it.
            if (holdAtEnd)
            {
                IsHeld = true;
                onDone?.Invoke();
                yield break;
            }

            yield return RestoreRoutine(cam, onDone);
        }

        // Glide the parked camera back to the cached gameplay pose (letterbox slides out, HUD/input/brain
        // restored). Safe to call only after a holdAtEnd Play; otherwise it just fires onDone.
        public void Restore(Action onDone = null)
        {
            if (!IsHeld) { onDone?.Invoke(); return; }
            IsHeld = false;
            StartCoroutine(RestoreRoutine(Camera.main, onDone));
        }

        private IEnumerator RestoreRoutine(Camera cam, Action onDone)
        {
            if (cam == null)
            {
                CleanupAfterRestore();
                onDone?.Invoke();
                yield break;
            }

            Vector3 fromPos = cam.transform.position;
            Quaternion fromRot = cam.transform.rotation;
            float fromFov = cam.fieldOfView;
            float dur = Mathf.Max(0.01f, _pendingRestore);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / dur);
                float e = u * u * (3f - 2f * u);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, _startPos, e), Quaternion.Slerp(fromRot, _startRot, e));
                cam.fieldOfView = Mathf.Lerp(fromFov, _startFov, e);
                SetLetterbox(1f - e);
                yield return null;
            }
            cam.transform.SetPositionAndRotation(_startPos, _startRot);
            cam.fieldOfView = _startFov;
            CleanupAfterRestore();
            onDone?.Invoke();
        }

        private void CleanupAfterRestore()
        {
            if (_brain != null) _brain.enabled = true;
            if (_lbCanvas != null) _lbCanvas.gameObject.SetActive(false);
            ShowHud();
            _inputLease?.Dispose();
            _inputLease = null;
            IsPlaying = false;
            IsHeld = false;
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
                cg.interactable = false;
                cg.blocksRaycasts = false;
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
