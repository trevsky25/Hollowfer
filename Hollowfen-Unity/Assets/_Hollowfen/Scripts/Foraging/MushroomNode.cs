using System;
using System.Collections;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.Foraging
{
    [DisallowMultipleComponent]
    public class MushroomNode : MonoBehaviour, IInteractable
    {
        private const string FirstHarvestPrefKey = "forage.firstHarvestSeen";

        [SerializeField] private MushroomFieldGuideData _data;
        [SerializeField] private float _respawnSeconds = 0f;

        [Header("Harvest cinematic")]
        [SerializeField, Tooltip("Camera dolly time at the start (seconds, unscaled).")]
        private float _cinematicDollyDuration = 0.30f;
        [SerializeField, Tooltip("Mushroom lift+spin+shrink time (seconds, unscaled).")]
        private float _cinematicAnimDuration = 0.80f;
        [SerializeField, Tooltip("Camera FollowOffset multiplier at peak dolly. Lower = closer.")]
        private float _cinematicDollyAmount = 0.55f;
        [SerializeField, Tooltip("World-space lift of the mushroom during the animation (meters).")]
        private float _cinematicLiftHeight = 0.25f;
        [SerializeField, Tooltip("Time scale during the animation. 0.5 = half speed slow-mo.")]
        private float _cinematicSlowMo = 0.5f;
        [SerializeField, Tooltip("Spin rate of the mushroom during the animation, deg/sec.")]
        private float _cinematicSpinDegPerSec = 360f;
        [SerializeField, Tooltip("Final scale multiplier at the end (must be > 0 to avoid collapse glitch).")]
        private float _cinematicFinalScale = 0.05f;

        public static event Action<MushroomFieldGuideData> OnAnyHarvested;

        public MushroomFieldGuideData Data => _data;

        public string PromptVerb => "prompt.inspect.verb";
        public string PromptTarget => _data != null ? _data.CommonName : "Mushroom";

        public bool CanInteract(GameObject actor) => _data != null && gameObject.activeInHierarchy;

        // Interact opens the InspectScreen. Forage button there calls BeginHarvest (cinematic + commit).
        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            InspectScreen.Open(this);
        }

        // Synchronous-only commit path (used as the last step of the cinematic OR if a caller
        // ever wants the legacy instant pickup behavior).
        public void Harvest()
        {
            if (_data == null || !gameObject.activeInHierarchy) return;

            Debug.Log($"[Forage] {_data.CommonName} +1");

            OnAnyHarvested?.Invoke(_data);
            MushroomDiscovery.MarkDiscovered(_data.Id);
            InventoryRuntime.Add(_data, 1);

            if (!PlayerPrefs.HasKey(FirstHarvestPrefKey))
            {
                PlayerPrefs.SetInt(FirstHarvestPrefKey, 1);
                PlayerPrefs.Save();
                GameEvents.TriggerAchievement("ACH_FORAGE_FIRST");
            }

            gameObject.SetActive(false);
        }

        // Plays the harvest cinematic, then commits. Called by InspectScreen.OnForageClicked
        // after the screen has hidden (without releasing input/time state — this coroutine owns them).
        public void BeginHarvest()
        {
            if (_data == null || !gameObject.activeInHierarchy) return;
            StartCoroutine(HarvestCinematic());
        }

        private IEnumerator HarvestCinematic()
        {
            // Take ownership of input + time. InspectScreen.HideForCinematic deliberately leaves these
            // in their paused state — we transition to the cinematic's slow-mo, then restore at the end.
            PlayerInteractor.Suspended = true;
            PlayerInteractor.SetPlayerInputEnabled(false);
            Time.timeScale = _cinematicSlowMo;

            var focusCam = MushroomFocusCamera.Instance;
            CinemachineFollowSnapshot snapshot = default;
            if (focusCam != null)
            {
                focusCam.IsHarvestCinematicActive = true;
                snapshot.HasFollow = focusCam.Follow != null;
                snapshot.OriginalOffset = snapshot.HasFollow ? focusCam.Follow.FollowOffset : Vector3.zero;
            }

            // Phase 1: camera dollies in
            float t = 0f;
            Vector3 dollyOffset = snapshot.OriginalOffset * _cinematicDollyAmount;
            while (t < _cinematicDollyDuration && _cinematicDollyDuration > 0f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / _cinematicDollyDuration);
                if (focusCam != null && snapshot.HasFollow)
                    focusCam.Follow.FollowOffset = Vector3.Lerp(snapshot.OriginalOffset, dollyOffset, k);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            if (focusCam != null && snapshot.HasFollow)
                focusCam.Follow.FollowOffset = dollyOffset;

            // Phase 2: mushroom lift + spin + shrink
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            Vector3 endScale = startScale * _cinematicFinalScale;
            t = 0f;
            while (t < _cinematicAnimDuration && _cinematicAnimDuration > 0f)
            {
                float k = t / _cinematicAnimDuration;
                transform.position = startPos + Vector3.up * Mathf.SmoothStep(0f, _cinematicLiftHeight, k);
                transform.Rotate(0f, _cinematicSpinDegPerSec * Time.unscaledDeltaTime, 0f);
                transform.localScale = Vector3.Lerp(startScale, endScale, Mathf.SmoothStep(0f, 1f, k));
                yield return null;
                t += Time.unscaledDeltaTime;
            }

            // Commit harvest (logs, fires events, marks discovery, adds to inventory, fires achievement)
            CommitHarvestState();

            // Restore camera + input + time before the GameObject deactivates (so any coroutine work
            // happens on a still-active host). gameObject.SetActive(false) is the last line.
            if (focusCam != null)
            {
                if (snapshot.HasFollow) focusCam.Follow.FollowOffset = focusCam.BaseOffset;
                focusCam.ReleaseFocus();
                focusCam.IsHarvestCinematicActive = false;
            }
            Time.timeScale = 1f;
            PlayerInteractor.Suspended = false;
            PlayerInteractor.SetPlayerInputEnabled(true);

            gameObject.SetActive(false);
        }

        // Same as Harvest() above but without the SetActive(false) — the coroutine owns deactivation
        // so it can do post-commit cleanup on the still-active host.
        private void CommitHarvestState()
        {
            if (_data == null) return;
            Debug.Log($"[Forage] {_data.CommonName} +1");
            OnAnyHarvested?.Invoke(_data);
            MushroomDiscovery.MarkDiscovered(_data.Id);
            InventoryRuntime.Add(_data, 1);
            if (!PlayerPrefs.HasKey(FirstHarvestPrefKey))
            {
                PlayerPrefs.SetInt(FirstHarvestPrefKey, 1);
                PlayerPrefs.Save();
                GameEvents.TriggerAchievement("ACH_FORAGE_FIRST");
            }
        }

        private struct CinemachineFollowSnapshot
        {
            public bool HasFollow;
            public Vector3 OriginalOffset;
        }
    }
}
