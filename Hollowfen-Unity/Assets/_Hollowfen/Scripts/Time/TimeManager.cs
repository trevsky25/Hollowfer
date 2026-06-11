using System;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.GameTime
{
    // The game clock: a day counter plus a 0-24h time-of-day that drives the sun. One real
    // minute is configurable game time (default 20 real minutes per day). The clock runs on
    // Time.deltaTime, so pause/dialogue (timeScale 0) freezes it for free. Day + hour persist
    // in the active save slot. Downstream systems subscribe to OnDayChanged / OnSundown
    // (Voss's deadline, Theo's wagon, Almy's grow beds).
    public class TimeManager : MonoBehaviour
    {
        public static TimeManager Instance { get; private set; }

        [SerializeField, Tooltip("Real minutes for one full in-game day.")]
        private float _minutesPerGameDay = 20f;
        [SerializeField, Tooltip("The scene sun (defaults to RenderSettings.sun).")]
        private Light _sun;
        [SerializeField, Tooltip("Clock value for brand-new games.")]
        private float _newGameHour = 14f;
        [SerializeField, Tooltip("Hour at which OnSundown fires.")]
        private float _sundownHour = 19f;

        public int Day { get; private set; } = 1;
        public float Hour { get; private set; } = 14f;
        public bool IsNight => Hour < 5.5f || Hour >= 20.5f;

        public static event Action<int> OnDayChanged;
        public static event Action OnSundown;

        private float _baseSunIntensity = 1.2f;
        private float _baseAmbient = 1.2f;
        private float _sunYaw = 330f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_sun == null) _sun = RenderSettings.sun;
            if (_sun != null)
            {
                _baseSunIntensity = _sun.intensity;
                _sunYaw = _sun.transform.eulerAngles.y;
            }
            _baseAmbient = RenderSettings.ambientIntensity;

            // Hydrate from the active slot; fresh saves start day 1 at the configured hour.
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            if (meta != null && meta.GameDay > 0)
            {
                Day = meta.GameDay;
                Hour = Mathf.Repeat(meta.GameHour, 24f);
            }
            else
            {
                Day = 1;
                Hour = _newGameHour;
            }
            ApplySun();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            float prev = Hour;
            float hoursPerSecond = 24f / (Mathf.Max(1f, _minutesPerGameDay) * 60f);
            Hour += Time.deltaTime * hoursPerSecond;

            if (prev < _sundownHour && Hour >= _sundownHour)
                OnSundown?.Invoke();

            if (Hour >= 24f)
            {
                Hour -= 24f;
                Day++;
                OnDayChanged?.Invoke(Day);
                // A new dawn is a natural checkpoint.
                SaveCoordinator.SaveAllWithPlayer();
            }

            ApplySun();
        }

        // For sleep/wait mechanics and tests. Fires day events when the day advances.
        public void SetTime(int day, float hour)
        {
            bool dayChanged = day != Day;
            Day = Mathf.Max(1, day);
            Hour = Mathf.Repeat(hour, 24f);
            if (dayChanged) OnDayChanged?.Invoke(Day);
            ApplySun();
        }

        public void WriteTo(SaveSlotMeta meta)
        {
            if (meta == null) return;
            meta.GameDay = Day;
            meta.GameHour = Hour;
        }

        // Sun elevation follows a sine arc: rises ~6h, peaks ~60 deg at noon, sets ~18h.
        // At night the same light dims to a cool moon so shadows never fully vanish.
        private void ApplySun()
        {
            if (_sun == null) return;

            float t = (Hour - 6f) / 12f; // 0 at sunrise, 1 at sunset
            float elevation = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 60f;
            bool day = t >= 0f && t <= 1f;

            if (day)
            {
                float warm = 1f - Mathf.Abs(t - 0.5f) * 2f; // 1 at noon, 0 at rise/set
                _sun.transform.rotation = Quaternion.Euler(Mathf.Max(8f, elevation), _sunYaw, 0f);
                _sun.intensity = Mathf.Lerp(0.45f, _baseSunIntensity, Mathf.SmoothStep(0f, 1f, warm));
                _sun.color = Color.Lerp(new Color(1f, 0.62f, 0.35f), new Color(1f, 0.93f, 0.84f), Mathf.SmoothStep(0f, 1f, warm));
                RenderSettings.ambientIntensity = Mathf.Lerp(0.55f, _baseAmbient, warm);
            }
            else
            {
                // Moonlight: low angle opposite-ish yaw, dim and blue.
                _sun.transform.rotation = Quaternion.Euler(30f, _sunYaw + 180f, 0f);
                _sun.intensity = 0.14f;
                _sun.color = new Color(0.55f, 0.65f, 0.9f);
                RenderSettings.ambientIntensity = 0.35f;
            }
        }
    }
}
