using System;
using System.Collections;
using Hollowfen.Cinematics;
using Hollowfen.Items;
using Hollowfen.UI;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Cinematic mill-key handoff (batch-52). When item.mill_key is granted (the end of Bram's key
    // dialogue at the well), present it as a hero item-get: a focus push-in on the slowly-rotating,
    // gently-bobbing key floating in front of Wren, with the KeyItemToast sliding in. It waits for
    // Bram's dialogue and painted transition to finish so the live handoff remains the final beat.
    public class MillKeyHandoff : MonoBehaviour
    {
        [SerializeField] private GameObject _keyPrefab;
        [SerializeField] private string _keyItemId = "item.mill_key";
        [SerializeField] private float _forward = 1.05f;      // how far in front of Wren the key floats
        [SerializeField] private float _height = 1.42f;       // chest/eye height
        [SerializeField] private float _camDistance = 0.32f;  // camera framing distance
        [SerializeField] private float _fov = 24f;            // tighter lens → more compression on the key
        [SerializeField] private float _pushSeconds = 1.35f;  // a touch slower so the arc reads
        [SerializeField] private float _holdSeconds = 2.4f;
        [SerializeField] private float _restoreSeconds = 0.7f;
        [SerializeField] private Vector3 _keyTilt = new Vector3(8f, 0f, 14f);  // shank near-horizontal, slight lean
        [SerializeField] private float _rockDegrees = 30f;   // gentle face-on rock (not a full spin)
        [SerializeField] private float _rockSpeed = 1.05f;

        [Header("Cinematic approach (batch-62)")]
        [SerializeField, Tooltip("Low hero angle: camera height vs the key at the hold. Negative = camera below, looking UP at the key catching the light.")]
        private float _camHeightOffset = -0.07f;
        [SerializeField, Tooltip("Yaw the push-in starts swung out by, easing to 0 — a sweeping curved approach around the key instead of a flat dolly.")]
        private float _arcDegrees = 40f;
        [SerializeField, Tooltip("Extra height the push-in starts raised by, easing to 0 — the camera descends onto the low hero angle.")]
        private float _arcRise = 0.14f;

        private bool _played;
        private NarrativePresentationSession.Lease _handoffLease;

        private void OnEnable() { KeyItems.OnGranted += HandleGranted; }
        private void OnDisable() { KeyItems.OnGranted -= HandleGranted; }

        private void HandleGranted(string id)
        {
            if (_played || id != _keyItemId) return;
            _played = true;
            _handoffLease = NarrativePresentationSession.Acquire(
                this, NarrativePresentationSession.InputOnly);
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // The item is granted before DialogueScreen presents its authored transition moment.
            // Wait for that entire conversation/painting stack to close so this live prop reveal is
            // the final visual beat instead of playing invisibly behind the narration overlay.
            while (PresentationStillOwnsTheScreen()) yield return null;
            yield return new WaitForSecondsRealtime(0.25f);

            var player = GameObject.FindGameObjectWithTag("Player");
            var cam = Camera.main;
            if (player == null || cam == null || _keyPrefab == null)
            {
                ReleaseHandoff();
                yield break;
            }

            Vector3 pos = player.transform.position + player.transform.forward * _forward + Vector3.up * _height;
            // Present the key's flat face toward the camera side (LookRotation puts +Z at the camera),
            // then tilt so the shank sits near-horizontal and the bit reads.
            Vector3 toCam = cam.transform.position - pos; toCam.y = 0f;
            Quaternion baseRot = toCam.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCam.normalized, Vector3.up) * Quaternion.Euler(_keyTilt)
                : Quaternion.Euler(_keyTilt);
            var key = Instantiate(_keyPrefab, pos, baseRot);
            key.name = "_MillKey_Handoff";

            bool spinning = true;
            StartCoroutine(HeroSpin(key.transform, baseRot, () => spinning));

            var focus = Cinematics.PropFocusCinematic.Ensure();
            bool done = false;
            // Sweeping curved approach that descends onto a low hero angle (arcDegrees/arcRise), holding on
            // the slowly-rocking key — the "amazing angle" pass Trevor asked for (batch-62).
            focus.Play(key.transform, _camDistance, _camHeightOffset, _fov, _pushSeconds, _holdSeconds, _restoreSeconds,
                null, () => done = true, default(Vector3), _arcDegrees, _arcRise, false);
            while (!done) yield return null;
            spinning = false;
            if (key != null) Destroy(key);
            ReleaseHandoff();
        }

        private static bool PresentationStillOwnsTheScreen()
        {
            if (Dialogue.DialogueScreen.Instance != null && Dialogue.DialogueScreen.Instance.IsOpen)
                return true;
            if (Dialogue.DialogueCinematics.Instance != null && Dialogue.DialogueCinematics.Instance.IsActive)
                return true;
            if (StoryMomentDirector.Instance != null && StoryMomentDirector.Instance.IsPresenting)
                return true;
            if (NarrationOverlay.Instance != null && NarrationOverlay.Instance.IsShowing)
                return true;

            var focus = PropFocusCinematic.Instance;
            return focus != null && (focus.IsPlaying || focus.IsHeld);
        }

        private void OnDestroy() => ReleaseHandoff();

        private void ReleaseHandoff()
        {
            _handoffLease?.Dispose();
            _handoffLease = null;
        }

        // Gentle face-on rock (not a full spin) so the key stays presented to the camera while turning
        // just enough to catch the light, with a soft vertical bob.
        private IEnumerator HeroSpin(Transform t, Quaternion baseRot, Func<bool> alive)
        {
            float baseY = t.position.y;
            float e = 0f;
            while (alive() && t != null)
            {
                e += Time.unscaledDeltaTime;
                float yaw = Mathf.Sin(e * _rockSpeed) * _rockDegrees;
                t.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * baseRot;
                var p = t.position; p.y = baseY + Mathf.Sin(e * 1.6f) * 0.015f; t.position = p;
                yield return null;
            }
        }
    }
}
