using System;
using Hollowfen.Save;
using UnityEngine;

namespace Hollowfen.GameTime
{
    // The game clock: a day counter plus a 0-24h time-of-day. One real
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
        [SerializeField, Tooltip("Art-directed sky, light, fog, and exposure cycle.")]
        private DayNightLighting _lighting;
        [SerializeField, Tooltip("Clock value for brand-new games.")]
        private float _newGameHour = 14f;
        [SerializeField, Tooltip("Hour at which OnSundown fires.")]
        private float _sundownHour = 19f;
        private const float PlaytimeAutosaveSeconds = 60f;
        private double _totalPlayTimeSeconds;
        private float _playtimeSinceAutosave;

        public int Day { get; private set; } = 1;
        public float Hour { get; private set; } = 14f;
        public bool IsNight => Hour < 5.5f || Hour >= 20.5f;
        public float TotalPlayTimeSeconds => (float)Math.Min(_totalPlayTimeSeconds, float.MaxValue);

        public static event Action<int> OnDayChanged;
        public static event Action OnSundown;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Instance = null;
            OnDayChanged = null;
            OnSundown = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_sun == null) _sun = RenderSettings.sun;
            if (_lighting == null) _lighting = GetComponent<DayNightLighting>();
            if (_lighting == null) _lighting = gameObject.AddComponent<DayNightLighting>();
            _lighting.Initialize(_sun);

            // Hydrate from the active slot; fresh saves start day 1 at the configured hour.
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            _totalPlayTimeSeconds = meta != null && !float.IsNaN(meta.TotalPlayTimeSeconds) &&
                                    !float.IsInfinity(meta.TotalPlayTimeSeconds)
                ? Math.Max(0d, meta.TotalPlayTimeSeconds)
                : 0d;
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
            ApplyLighting();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Application.isFocused)
            {
                // Unscaled time counts reading/dialogue/pause as play, but clamps focus/suspend
                // spikes so leaving the application cannot add hours to a journal.
                float played = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 1f);
                _totalPlayTimeSeconds += played;
                _playtimeSinceAutosave += played;
                if (_playtimeSinceAutosave >= PlaytimeAutosaveSeconds)
                {
                    _playtimeSinceAutosave = 0f;
                    SaveManager.AutoSaveClockAndPlaytime(Day, Hour, TotalPlayTimeSeconds);
                }
            }

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

            ApplyLighting();
        }

        // For sleep/wait mechanics and tests. Fires day events when the day advances.
        public void SetTime(int day, float hour)
        {
            bool dayChanged = day != Day;
            Day = Mathf.Max(1, day);
            Hour = Mathf.Repeat(hour, 24f);
            if (dayChanged) OnDayChanged?.Invoke(Day);
            ApplyLighting();
        }

        /// <summary>
        /// Advances forward through the real clock boundary events. Resting must use this rather
        /// than SetTime so sundown pressure, dawn schedules, crops, and respawns all observe the
        /// same timeline as ordinary play.
        /// </summary>
        public void AdvanceTo(int targetDay, float targetHour, bool saveCheckpoint = true)
        {
            targetDay = Mathf.Max(1, targetDay);
            targetHour = Mathf.Repeat(targetHour, 24f);
            double current = (Day - 1) * 24d + Hour;
            double target = (targetDay - 1) * 24d + targetHour;
            if (target <= current + 0.0001d) return;

            while (true)
            {
                double dayStart = (Day - 1) * 24d;
                double sundown = dayStart + _sundownHour;
                double nextDawn = dayStart + 24d;

                if (current < sundown && sundown <= target)
                {
                    Hour = _sundownHour;
                    current = sundown;
                    OnSundown?.Invoke();
                    continue;
                }

                if (current < nextDawn && nextDawn <= target)
                {
                    Day++;
                    Hour = 0f;
                    current = nextDawn;
                    OnDayChanged?.Invoke(Day);
                    continue;
                }

                break;
            }

            Day = targetDay;
            Hour = targetHour;
            ApplyLighting();
            if (saveCheckpoint) SaveCoordinator.SaveAllWithPlayer();
        }

        public void WriteTo(SaveSlotMeta meta)
        {
            if (meta == null) return;
            meta.GameDay = Day;
            meta.GameHour = Hour;
            meta.TotalPlayTimeSeconds = TotalPlayTimeSeconds;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveManager.AutoSaveClockAndPlaytime(Day, Hour, TotalPlayTimeSeconds);
        }

        private void OnApplicationQuit()
        {
            SaveManager.AutoSaveClockAndPlaytime(Day, Hour, TotalPlayTimeSeconds);
        }

        private void ApplyLighting()
        {
            if (_lighting != null) _lighting.Apply(Hour);
        }
    }
}
