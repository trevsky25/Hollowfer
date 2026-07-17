using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Hollowfen.Audio
{
    /// <summary>
    /// Mixer-routed gameplay feedback library. Cues are synthesized once at runtime so the
    /// interaction layer stays complete while the final recorded Foley pass is still pending.
    /// </summary>
    public static class GameplaySfx
    {
        public enum Cue
        {
            KnifeLeft,
            KnifeRight,
            ForageCollected,
            DeliveryComplete,
            CoinsEarned,
            CoinsSpent,
            ItemAcquired,
            QuestComplete,
            KeyTurn,
            DoorOpen,
            Rest,
            Plant,
            CropMature,
        }

        private const int Rate = 48000;
        private static readonly AudioClip[] Clips = new AudioClip[Enum.GetValues(typeof(Cue)).Length];
        private static AudioSource _source;
        private static AudioMixerGroup _output;

        public static Cue? LastCue { get; private set; }
        public static int CueCount { get; private set; }
        public static int SampleRate => Rate;
        public static AudioMixerGroup Output => _output;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            _source = null;
            _output = null;
            LastCue = null;
            CueCount = 0;
            Array.Clear(Clips, 0, Clips.Length);
        }

        public static void SetOutput(AudioMixerGroup group)
        {
            _output = group;
            if (_source != null) _source.outputAudioMixerGroup = group;
        }

        public static void KnifeStroke(float progress, int side) =>
            Play(side < 0 ? Cue.KnifeLeft : Cue.KnifeRight, Mathf.Lerp(.42f, .58f, progress));

        public static void ForageCollected() => Play(Cue.ForageCollected, .72f);
        public static void DeliveryComplete() => Play(Cue.DeliveryComplete, .78f);
        public static void CoinsEarned() => Play(Cue.CoinsEarned, .66f);
        public static void CoinsSpent() => Play(Cue.CoinsSpent, .54f);
        public static void ItemAcquired() => Play(Cue.ItemAcquired, .68f);
        public static void QuestComplete() => Play(Cue.QuestComplete, .72f);
        public static void KeyTurn() => Play(Cue.KeyTurn, .72f);
        public static void DoorOpen() => Play(Cue.DoorOpen, .70f);
        public static void Rest() => Play(Cue.Rest, .65f);
        public static void Plant() => Play(Cue.Plant, .66f);
        public static void CropMature() => Play(Cue.CropMature, .62f);

        public static AudioClip GetClip(Cue cue)
        {
            int index = (int)cue;
            if (Clips[index] == null) Clips[index] = Build(cue);
            return Clips[index];
        }

        public static void Play(Cue cue, float volume = 1f)
        {
            EnsureSource();
            if (_source == null) return;
            LastCue = cue;
            CueCount++;
            _source.PlayOneShot(GetClip(cue), Mathf.Clamp01(volume));
        }

        private static void EnsureSource()
        {
            if (_source != null) return;
            var host = new GameObject("_GameplaySfx");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.priority = 24; // below dialogue, above music and ambience
            _source.ignoreListenerPause = true;
            _source.outputAudioMixerGroup = _output;
        }

        private static AudioClip Build(Cue cue)
        {
            switch (cue)
            {
                case Cue.KnifeLeft: return BuildKnife("KnifeStrokeLeft", 1250f, 190f, 11);
                case Cue.KnifeRight: return BuildKnife("KnifeStrokeRight", 1420f, 215f, 23);
                case Cue.ForageCollected: return BuildForage();
                case Cue.DeliveryComplete: return BuildDelivery();
                case Cue.CoinsEarned: return BuildCoins("CoinsEarned", true);
                case Cue.CoinsSpent: return BuildCoins("CoinsSpent", false);
                case Cue.ItemAcquired: return BuildChime("ItemAcquired", 493.88f, 739.99f, .28f);
                case Cue.QuestComplete: return BuildChime("QuestComplete", 392f, 659.25f, .38f);
                case Cue.KeyTurn: return BuildKeyTurn();
                case Cue.DoorOpen: return BuildDoor();
                case Cue.Rest: return BuildRest();
                case Cue.Plant: return BuildPlant();
                case Cue.CropMature: return BuildChime("CropMature", 329.63f, 523.25f, .34f);
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
        }

        private static AudioClip BuildKnife(string name, float scrapeFrequency, float bodyFrequency, int seed)
        {
            const float seconds = .12f;
            var data = Buffer(seconds);
            var random = new System.Random(seed);
            double phase = 0d;
            float previousNoise = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                phase += 2d * Math.PI * Mathf.Lerp(scrapeFrequency, scrapeFrequency * .72f, u) / Rate;
                float noise = (float)(random.NextDouble() * 2d - 1d);
                previousNoise = Mathf.Lerp(previousNoise, noise, .22f);
                float scrape = (Mathf.Sin((float)phase) * .38f + previousNoise * .62f) * Mathf.Exp(-t * 22f);
                float body = Mathf.Sin(2f * Mathf.PI * bodyFrequency * t) * Mathf.Exp(-t * 38f) * .32f;
                data[i] = (scrape + body) * Attack(t, .004f) * Release(u, .16f) * .42f;
            }
            return Make(name, data);
        }

        private static AudioClip BuildForage()
        {
            const float seconds = .34f;
            var data = Buffer(seconds);
            var random = new System.Random(47);
            float filtered = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                float noise = (float)(random.NextDouble() * 2d - 1d);
                filtered = Mathf.Lerp(filtered, noise, .07f);
                float pluck = filtered * Mathf.Exp(-t * 18f) * .34f;
                float lift = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 520f, u) * t) * Mathf.Exp(-t * 9f) * .23f;
                float bell = t > .07f ? Mathf.Sin(2f * Mathf.PI * 783.99f * (t - .07f)) * Mathf.Exp(-(t - .07f) * 12f) * .18f : 0f;
                data[i] = (pluck + lift + bell) * Attack(t, .005f) * Release(u, .12f);
            }
            return Make("ForageCollected", data);
        }

        private static AudioClip BuildDelivery()
        {
            const float seconds = .46f;
            var data = Buffer(seconds);
            var random = new System.Random(71);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float basket = (float)(random.NextDouble() * 2d - 1d) * Mathf.Exp(-t * 28f) * .10f;
                float thump = Mathf.Sin(2f * Mathf.PI * 128f * t) * Mathf.Exp(-t * 22f) * .22f;
                float first = ToneAfter(t, .08f, 523.25f, 12f, .20f);
                float second = ToneAfter(t, .19f, 659.25f, 10f, .24f);
                data[i] = (basket + thump + first + second) * Attack(t, .004f) * Release(t / seconds, .08f);
            }
            return Make("DeliveryComplete", data);
        }

        private static AudioClip BuildCoins(string name, bool rising)
        {
            const float seconds = .30f;
            var data = Buffer(seconds);
            float a = rising ? 880f : 1046.5f;
            float b = rising ? 1174.66f : 783.99f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float first = ToneAfter(t, 0f, a, 25f, .26f) + ToneAfter(t, 0f, a * 2.37f, 31f, .08f);
                float second = ToneAfter(t, .085f, b, 20f, .28f) + ToneAfter(t, .085f, b * 2.19f, 29f, .07f);
                data[i] = (first + second) * Attack(t, .002f) * Release(t / seconds, .07f);
            }
            return Make(name, data);
        }

        private static AudioClip BuildChime(string name, float low, float high, float seconds)
        {
            var data = Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float root = ToneAfter(t, 0f, low, 9f, .22f);
                float crown = ToneAfter(t, .085f, high, 7f, .24f);
                float air = ToneAfter(t, .085f, high * 2.01f, 12f, .055f);
                data[i] = (root + crown + air) * Attack(t, .006f) * Release(t / seconds, .10f);
            }
            return Make(name, data);
        }

        private static AudioClip BuildKeyTurn()
        {
            const float seconds = .33f;
            var data = Buffer(seconds);
            var random = new System.Random(103);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float scrape = (float)(random.NextDouble() * 2d - 1d) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .22f)) * .08f;
                float metal = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(760f, 570f, t / seconds) * t) * Mathf.Exp(-t * 8f) * .13f;
                float click = ToneAfter(t, .20f, 2100f, 70f, .20f) + ToneAfter(t, .20f, 230f, 35f, .24f);
                data[i] = (scrape + metal + click) * Attack(t, .004f) * Release(t / seconds, .07f);
            }
            return Make("KeyTurn", data);
        }

        private static AudioClip BuildDoor()
        {
            const float seconds = .74f;
            var data = Buffer(seconds);
            var random = new System.Random(151);
            float filtered = 0f;
            double phase = 0d;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                float noise = (float)(random.NextDouble() * 2d - 1d);
                filtered = Mathf.Lerp(filtered, noise, .025f);
                phase += 2d * Math.PI * Mathf.Lerp(118f, 72f, u) / Rate;
                float groan = Mathf.Sin((float)phase) * (1f + .22f * Mathf.Sin(2f * Mathf.PI * 6.2f * t)) * .20f;
                data[i] = (groan + filtered * .32f) * Attack(t, .035f) * Release(u, .18f) * .72f;
            }
            return Make("DoorOpen", data);
        }

        private static AudioClip BuildRest()
        {
            const float seconds = .80f;
            var data = Buffer(seconds);
            var random = new System.Random(191);
            float air = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                air = Mathf.Lerp(air, (float)(random.NextDouble() * 2d - 1d), .018f);
                float low = Mathf.Sin(2f * Mathf.PI * 164.81f * t) * Mathf.Sin(Mathf.PI * u) * .12f;
                float chime = ToneAfter(t, .18f, 493.88f, 4.5f, .16f) + ToneAfter(t, .28f, 659.25f, 4.2f, .13f);
                data[i] = (air * .07f + low + chime) * Attack(t, .04f) * Release(u, .16f);
            }
            return Make("Rest", data);
        }

        private static AudioClip BuildPlant()
        {
            const float seconds = .42f;
            var data = Buffer(seconds);
            var random = new System.Random(223);
            float soil = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float u = t / seconds;
                soil = Mathf.Lerp(soil, (float)(random.NextDouble() * 2d - 1d), .05f);
                float press = Mathf.Sin(2f * Mathf.PI * 92f * t) * Mathf.Exp(-t * 16f) * .20f;
                float root = ToneAfter(t, .12f, 293.66f, 9f, .14f);
                data[i] = (soil * Mathf.Exp(-t * 10f) * .23f + press + root) * Attack(t, .008f) * Release(u, .14f);
            }
            return Make("Plant", data);
        }

        private static float ToneAfter(float time, float delay, float frequency, float decay, float gain)
        {
            if (time < delay) return 0f;
            float local = time - delay;
            return Mathf.Sin(2f * Mathf.PI * frequency * local) * Mathf.Exp(-local * decay) * gain;
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(Rate * seconds)];
        private static float Attack(float time, float seconds) => Mathf.Clamp01(time / Mathf.Max(.0001f, seconds));
        private static float Release(float normalizedTime, float fraction) =>
            1f - Mathf.Clamp01((normalizedTime - (1f - fraction)) / Mathf.Max(.001f, fraction));

        private static AudioClip Make(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -.95f, .95f);
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
