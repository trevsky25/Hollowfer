using System;
using System.Collections;
using Hollowfen.Items;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Cinematic mill-key handoff (batch-52). When item.mill_key is granted (the end of Bram's key
    // dialogue at the well), present it as a hero item-get: a focus push-in on the slowly-rotating,
    // gently-bobbing key floating in front of Wren, with the KeyItemToast sliding in. Waits for the
    // dialogue camera to finish its restore first so the two takeovers never fight.
    public class MillKeyHandoff : MonoBehaviour
    {
        [SerializeField] private GameObject _keyPrefab;
        [SerializeField] private string _keyItemId = "item.mill_key";
        [SerializeField] private float _forward = 1.05f;      // how far in front of Wren the key floats
        [SerializeField] private float _height = 1.42f;       // chest/eye height
        [SerializeField] private float _camDistance = 0.34f;  // camera framing distance
        [SerializeField] private float _fov = 26f;
        [SerializeField] private float _pushSeconds = 1.1f;
        [SerializeField] private float _holdSeconds = 2.4f;
        [SerializeField] private float _restoreSeconds = 0.7f;
        [SerializeField] private Vector3 _keyTilt = new Vector3(8f, 0f, 14f);  // shank near-horizontal, slight lean
        [SerializeField] private float _rockDegrees = 30f;   // gentle face-on rock (not a full spin)
        [SerializeField] private float _rockSpeed = 1.05f;

        private bool _played;

        private void OnEnable() { KeyItems.OnGranted += HandleGranted; }
        private void OnDisable() { KeyItems.OnGranted -= HandleGranted; }

        private void HandleGranted(string id)
        {
            if (_played || id != _keyItemId) return;
            _played = true;
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // Hold the player still through the brief hand-off (input was just re-enabled as the dialogue closed).
            Foraging.PlayerInteractor.Suspended = true;
            Foraging.PlayerInteractor.SetPlayerInputEnabled(false);

            // Let the dialogue camera hand back first, so we don't fight its restore glide.
            float w = 0f;
            while (w < 2.5f && Dialogue.DialogueCinematics.Instance != null && Dialogue.DialogueCinematics.Instance.IsActive)
            { w += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(0.25f);

            var player = GameObject.FindGameObjectWithTag("Player");
            var cam = Camera.main;
            if (player == null || cam == null || _keyPrefab == null)
            {
                Foraging.PlayerInteractor.Suspended = false;
                Foraging.PlayerInteractor.SetPlayerInputEnabled(true);
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
            focus.Play(key.transform, _camDistance, 0f, _fov, _pushSeconds, _holdSeconds, _restoreSeconds, null, () => done = true);
            while (!done) yield return null;
            spinning = false;
            if (key != null) Destroy(key);
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
