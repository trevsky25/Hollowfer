using Hollowfen.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hollowfen.Weather
{
    /// <summary>Camera-local precipitation, shelter detection, and bounded world wind.</summary>
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class WeatherPresentation : MonoBehaviour
    {
        private const float ShelterPollSeconds = .22f;
        private const float ShelterRayDistance = 14f;

        public float OutdoorExposure { get; private set; } = 1f;
        public int MaxRainParticles { get; private set; }
        public bool HasCameraRig => _weatherRoot != null && _rain != null;

        private Camera _camera;
        private Transform _weatherRoot;
        private ParticleSystem _rain;
        private Material _rainMaterial;
        private WindZone _wind;
        private float _targetExposure = 1f;
        private float _nextShelterPoll;
        private int _cameraSearchFrames;
        private readonly Collider[] _shelterOverlaps = new Collider[8];
        private readonly RaycastHit[] _shelterHits = new RaycastHit[12];

        private void Start()
        {
            _wind = GetComponent<WindZone>();
            if (_wind == null) _wind = gameObject.AddComponent<WindZone>();
            _wind.mode = WindZoneMode.Directional;
            _wind.windMain = 0f;
            _wind.windTurbulence = 0f;
            ResolveCamera();
        }

        private void Update()
        {
            if (_camera == null && _cameraSearchFrames++ < 240) ResolveCamera();
            if (_camera == null || WeatherSystem.Instance == null) return;

            if (Time.unscaledTime >= _nextShelterPoll)
            {
                _nextShelterPoll = Time.unscaledTime + ShelterPollSeconds;
                _targetExposure = IsSheltered(_camera.transform.position) ? .06f : 1f;
            }
            OutdoorExposure = Mathf.MoveTowards(OutdoorExposure, _targetExposure,
                Time.unscaledDeltaTime * 2.3f);

            WeatherState state = WeatherSystem.Instance.CurrentState;
            if (_rain != null)
            {
                float motionFactor = GameSettings.ReducedMotion ? .58f : 1f;
                float rate = MaxRainParticles * .92f * state.Precipitation *
                    OutdoorExposure * motionFactor;
                var emission = _rain.emission;
                emission.rateOverTime = rate;
                var main = _rain.main;
                main.startSpeed = Mathf.Lerp(17f, 27f, state.Wind);
                if (rate > .5f && !_rain.isPlaying) _rain.Play();
            }

            if (_wind != null)
            {
                _wind.windMain = Mathf.Lerp(.04f, .78f, state.Wind);
                _wind.windTurbulence = Mathf.Lerp(.02f, .62f, state.Wind);
                _wind.windPulseMagnitude = state.Kind == WeatherKind.Storm ? .75f : .2f;
                _wind.windPulseFrequency = state.Kind == WeatherKind.Storm ? .42f : .18f;
            }
        }

        private void ResolveCamera()
        {
            _camera = Camera.main;
            if (_camera == null) return;
            BuildRainRig();
        }

        private void BuildRainRig()
        {
            if (_weatherRoot != null) Destroy(_weatherRoot.gameObject);
            var root = new GameObject("WeatherCameraRig");
            _weatherRoot = root.transform;
            _weatherRoot.SetParent(_camera.transform, false);
            _weatherRoot.localPosition = new Vector3(0f, 10.5f, 2.5f);

            var rainObject = new GameObject("RainField");
            rainObject.transform.SetParent(_weatherRoot, false);
            _rain = rainObject.AddComponent<ParticleSystem>();
            _rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            MaxRainParticles = QualitySettings.GetQualityLevel() <= 1 ? 420 : 720;

            var main = _rain.main;
            main.loop = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.58f, .86f);
            main.startSpeed = 22f;
            main.startSize = new ParticleSystem.MinMaxCurve(.004f, .009f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.62f, .72f, .78f, .16f), new Color(.78f, .84f, .88f, .34f));
            main.gravityModifier = .32f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxRainParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake = true;

            var emission = _rain.emission;
            emission.rateOverTime = 0f;
            var shape = _rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(25f, 1.2f, 19f);

            var velocity = _rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-23f);
            velocity.x = new ParticleSystem.MinMaxCurve(-.45f);

            var renderer = _rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .025f;
            renderer.lengthScale = .62f;
            renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -1f;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                _rainMaterial = new Material(shader)
                {
                    name = "Weather Rain Runtime",
                    hideFlags = HideFlags.HideAndDontSave,
                    renderQueue = 3000,
                };
                if (_rainMaterial.HasProperty("_BaseColor"))
                    _rainMaterial.SetColor("_BaseColor", new Color(.74f, .82f, .88f, .42f));
                renderer.sharedMaterial = _rainMaterial;
            }
            _rain.Play();
        }

        private bool IsSheltered(Vector3 origin)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, .16f, _shelterOverlaps,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider overlap = _shelterOverlaps[i];
                if (overlap != null && overlap.GetComponentInParent<WeatherShelterVolume>() != null)
                    return true;
            }

            int hitCount = Physics.RaycastNonAlloc(origin + Vector3.up * .08f, Vector3.up,
                _shelterHits, ShelterRayDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Transform cameraTransform = _camera != null ? _camera.transform : null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = _shelterHits[i].collider;
                if (collider == null || collider is TerrainCollider || collider.transform == cameraTransform ||
                    cameraTransform != null && collider.transform.IsChildOf(cameraTransform)) continue;
                if (collider.GetComponentInParent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_rainMaterial != null) Destroy(_rainMaterial);
        }
    }
}
