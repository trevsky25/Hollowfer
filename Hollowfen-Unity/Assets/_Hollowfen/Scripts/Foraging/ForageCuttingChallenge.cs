using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Cinematics;
using Hollowfen.Restoration;
using Hollowfen.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Foraging
{
    // Controller-first harvest micro-challenge. The interaction is intentionally recoverable:
    // an imprecise angle stalls the cut and changes feedback, while clean alternating strokes
    // advance it. Inventory/discovery/quest state is committed only after the final stroke.
    public sealed class ForageCuttingChallenge : MonoBehaviour
    {
        public enum CuttingPhase { None, Kneeling, MacroTransition, Cutting, Success, Canceling }

        public const int RequiredStrokes = 6;
        private const string KnifePrefabResource =
            "Wrens Knife/Meshy_AI_Old_Bone_Handled_Knif_0714225928_texture";
        private const string KnifeMaterialResource = "Wrens Knife/WrensKnife";

        public static ForageCuttingChallenge Active { get; private set; }
        public static bool IsActive => Active != null;

        public CuttingPhase Phase { get; private set; }
        public float Progress => _strokeCount / (float)Mathf.Max(1, _requiredStrokes);
        public int StrokeCount => _strokeCount;
        public int StrokeTarget => _requiredStrokes;

        private MushroomNode _node;
        private ForageCuttingHUD _hud;
        private MushroomFocusCamera _focusCamera;
        private CinemachineBrain _brain;
        private bool _previousBrainIgnoreTimeScale;
        private bool _brainCaptured;
        private Transform _cameraAim;
        private Transform _player;
        private Renderer[] _playerRenderers;
        private bool[] _playerRendererStates;
        private Animator _animator;
        private AnimatorUpdateMode _previousAnimatorMode;
        private bool _previousApplyRootMotion;
        private bool _animatorCaptured;
        private Vector3 _cameraOffsetBefore;
        private float _cameraFovBefore;
        private bool _cameraCaptured;
        private GameObject _knifeRig;
        private Transform _knife;
        private LineRenderer _cutGuide;
        private Material _cutGuideMaterial;
        private Vector3 _cutPoint;
        private Vector3 _macroOffset;
        private int _strokeCount;
        private int _requiredStrokes = RequiredStrokes;
        private int _lastStrokeSide;
        private float _lastStrokeAt = -10f;
        private float _lastAngleWarningAt = -10f;
        private float _displayProgress;
        private float _hapticPulseUntil;
        private float _hapticLow;
        private float _hapticHigh;
        private Gamepad _rumblePad;
        private bool _restored;
        private NarrativePresentationSession.Lease _presentationLease;

        public static bool Play(MushroomNode node)
        {
            if (node == null || node.Data == null || IsActive) return false;
            var go = new GameObject("_ForageCuttingChallenge");
            var challenge = go.AddComponent<ForageCuttingChallenge>();
            challenge._node = node;
            challenge._requiredStrokes = RestorationBenefits.CuttingStrokes;
            Active = challenge;
            challenge._presentationLease = NarrativePresentationSession.Acquire(
                challenge,
                NarrativePresentationSession.SlowMotion(0.12f)
                    .With(NarrativePresentationSession.Claim.HideGameplayHud));
            challenge.StartCoroutine(challenge.Run());
            return true;
        }

        private IEnumerator Run()
        {
            FindAndPreparePlayer();
            PrepareCamera();
            BuildHud();

            yield return PlayKneelingShot();
            if (this == null || _node == null) yield break;

            yield return PlayMacroTransition();
            if (this == null || _node == null) yield break;

            yield return RunCuttingInput();
        }

        private void FindAndPreparePlayer()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo == null) return;
            _player = playerGo.transform;
            _playerRenderers = playerGo.GetComponentsInChildren<Renderer>(true);
            _playerRendererStates = new bool[_playerRenderers.Length];
            for (int i = 0; i < _playerRenderers.Length; i++)
                _playerRendererStates[i] = _playerRenderers[i].enabled;
            _animator = playerGo.GetComponentInChildren<Animator>(true);
            if (_animator == null) return;

            _previousAnimatorMode = _animator.updateMode;
            _previousApplyRootMotion = _animator.applyRootMotion;
            _animatorCaptured = true;
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            _animator.applyRootMotion = false;
        }

        private void PrepareCamera()
        {
            var mainCamera = Camera.main;
            _brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
            if (_brain != null)
            {
                _previousBrainIgnoreTimeScale = _brain.IgnoreTimeScale;
                _brainCaptured = true;
                _brain.IgnoreTimeScale = true;
            }

            _focusCamera = MushroomFocusCamera.Instance;
            if (_focusCamera == null) return;
            _focusCamera.EnsureHarvestFocus(_node);
            _focusCamera.IsHarvestCinematicActive = true;
            if (_focusCamera.Follow == null) return;

            _cameraCaptured = true;
            _cameraOffsetBefore = _focusCamera.Follow.FollowOffset;
            _cameraFovBefore = _focusCamera.CurrentFov;

            var aimGo = new GameObject("ForageCameraAim");
            _cameraAim = aimGo.transform;
            Vector3 playerPosition = _player != null
                ? _player.position : _node.transform.position - _node.transform.forward;
            _cameraAim.position = Vector3.Lerp(_node.transform.position, playerPosition, 0.34f)
                + Vector3.up * 0.62f;
            _focusCamera.SetHarvestAim(_cameraAim);
        }

        private void BuildHud()
        {
            var go = new GameObject("ForageCuttingHUD", typeof(RectTransform));
            _hud = go.AddComponent<ForageCuttingHUD>();
            _hud.Build(JournalText.MushroomName(_node.Data), ControllerGlyphs.IsGamepadActive);
        }

        private IEnumerator PlayKneelingShot()
        {
            Phase = CuttingPhase.Kneeling;
            Vector3 toPlayer = GetToPlayerDirection();
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
            Vector3 kneelOffset = toPlayer * 0.45f + side * 2.00f + Vector3.up * 1.02f;

            Quaternion startRotation = _player != null ? _player.rotation : Quaternion.identity;
            Quaternion targetRotation = startRotation;
            if (_player != null)
            {
                Vector3 toNode = _node.transform.position - _player.position;
                toNode.y = 0f;
                if (toNode.sqrMagnitude > 0.001f)
                    targetRotation = Quaternion.LookRotation(toNode.normalized, Vector3.up);
            }

            Vector3 cameraStart = _focusCamera != null && _focusCamera.Follow != null
                ? _focusCamera.Follow.FollowOffset : kneelOffset;
            float fovStart = _focusCamera != null ? _focusCamera.CurrentFov : 40f;
            float t = 0f;
            const float turnDuration = 0.42f;
            while (t < turnDuration)
            {
                float k = Ease(t / turnDuration);
                if (_player != null) _player.rotation = Quaternion.Slerp(startRotation, targetRotation, k);
                SetCamera(Vector3.Lerp(cameraStart, kneelOffset, k), Mathf.Lerp(fovStart, 42f, k));
                if (_hud != null) _hud.RootAlpha = Mathf.Clamp01(t / 0.28f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            SetCamera(kneelOffset, 42f);
            if (_hud != null) _hud.RootAlpha = 1f;

            PlayKneelAnimation();

            // The authored clip is 2.767s. Hold the side composition long enough to read the
            // performance, then cut before the dead tail at the end of the export.
            t = 0f;
            const float kneelRead = 2.20f;
            while (t < kneelRead)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void PlayKneelAnimation()
        {
            if (_animator == null) return;
            int fullPath = Animator.StringToHash("Base Layer.Forage Kneel");
            if (_animator.HasState(0, fullPath))
                _animator.CrossFade(fullPath, 0.12f, 0, 0f);
        }

        private IEnumerator PlayMacroTransition()
        {
            Phase = CuttingPhase.MacroTransition;
            Vector3 toPlayer = GetToPlayerDirection();
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
            _macroOffset = -toPlayer * 0.88f + side * 0.28f + Vector3.up * 0.38f;

            // A quick exposure cut avoids pushing the macro camera through Wren's kneeling mesh.
            // It also makes the switch from authored character performance to tactile knife work
            // feel like an intentional cinematic edit instead of a camera teleport.
            float t = 0f;
            const float fadeOut = 0.16f;
            while (t < fadeOut)
            {
                if (_hud != null) _hud.CurtainAlpha = Ease(t / fadeOut);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_hud != null) _hud.CurtainAlpha = 1f;

            DisablePlayerRenderers();
            BuildKnifeRig();
            Vector3 macroAim = _cutPoint + Vector3.up * 0.035f;
            SetCamera(_macroOffset, 30f);
            if (_cameraAim != null) _cameraAim.position = macroAim;
            if (_focusCamera != null) _focusCamera.InvalidateHarvestState();
            if (_knife != null) _knife.localPosition = new Vector3(-0.18f, 0f, 0f);
            yield return null;

            t = 0f;
            const float reveal = 0.34f;
            while (t < reveal)
            {
                float k = Ease(t / reveal);
                if (_hud != null)
                {
                    _hud.CurtainAlpha = 1f - k;
                    _hud.PanelAlpha = Mathf.Clamp01((k - 0.18f) / 0.82f);
                }
                if (_knife != null)
                    _knife.localPosition = Vector3.Lerp(new Vector3(-0.18f, 0f, 0f),
                        Vector3.zero, k);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_hud != null) _hud.CurtainAlpha = 0f;
            if (_knife != null) _knife.localPosition = Vector3.zero;
            if (_hud != null) _hud.PanelAlpha = 1f;
        }

        private IEnumerator RunCuttingInput()
        {
            Phase = CuttingPhase.Cutting;
            _strokeCount = 0;
            _lastStrokeSide = 0;
            _displayProgress = 0f;

            while (_strokeCount < _requiredStrokes)
            {
                InputSample sample = ReadInput();
                if (sample.Cancel)
                {
                    yield return CancelChallenge();
                    yield break;
                }

                bool braced = sample.BraceAmount >= 0.72f;
                bool level = Mathf.Abs(sample.Saw.y) <= 0.52f;
                bool attempting = Mathf.Abs(sample.Saw.x) >= 0.28f;

                if (_hud != null)
                {
                    _hud.SetDeviceMode(sample.Gamepad);
                    _hud.UpdateInput(sample.Brace, sample.Saw, sample.BraceAmount, braced);
                }

                if (!braced)
                    SetStatus("forage.cut.status.brace", HollowfenPalette.Parchment);
                else if (attempting && !level)
                {
                    SetStatus("forage.cut.status.angle", HollowfenPalette.EdDeadly);
                    if (Time.unscaledTime - _lastAngleWarningAt > 0.40f)
                    {
                        _lastAngleWarningAt = Time.unscaledTime;
                        PulseHaptics(0.24f, 0.42f, 0.07f);
                    }
                }
                else if (_strokeCount == 0)
                    SetStatus("forage.cut.status.ready", HollowfenPalette.GoldGlow);
                else
                    SetStatus("forage.cut.status.rhythm", HollowfenPalette.Sage);

                if (braced && level) TryAcceptStroke(sample.Saw.x);

                AnimateKnife(sample.Saw, braced, level);
                UpdateHaptics(sample, braced, level);

                _displayProgress = Mathf.MoveTowards(_displayProgress, Progress,
                    Time.unscaledDeltaTime * 2.8f);
                if (_hud != null) _hud.SetProgress(_displayProgress, _strokeCount, _requiredStrokes);
                yield return null;
            }

            yield return CompleteChallenge();
        }

        private void TryAcceptStroke(float sawX)
        {
            if (Mathf.Abs(sawX) < 0.64f) return;
            int side = sawX > 0f ? 1 : -1;
            if (side == _lastStrokeSide) return;
            if (Time.unscaledTime - _lastStrokeAt < 0.13f) return;

            _lastStrokeSide = side;
            _lastStrokeAt = Time.unscaledTime;
            _strokeCount++;
            GameplaySfx.KnifeStroke(Progress, side);
            PulseHaptics(0.18f + _strokeCount * 0.025f,
                0.46f + _strokeCount * 0.055f, 0.075f);
        }

        private void AnimateKnife(Vector2 saw, bool braced, bool level)
        {
            if (_knife == null) return;
            float travel = braced ? saw.x * 0.14f : saw.x * 0.045f;
            float verticalError = level ? 0f : saw.y * 0.028f;
            Vector3 target = new Vector3(travel, verticalError, 0f);
            _knife.localPosition = Vector3.Lerp(_knife.localPosition, target,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            _knife.localRotation = Quaternion.Euler(0f, -saw.x * 4f,
                -7f + saw.x * 2.5f + saw.y * 8f);
        }

        private IEnumerator CompleteChallenge()
        {
            Phase = CuttingPhase.Success;
            StopRumble();
            PulseHaptics(0.62f, 1f, 0.22f);
            if (_hud != null)
            {
                _hud.SetProgress(1f, _requiredStrokes, _requiredStrokes);
                _hud.ShowSuccess(JournalText.MushroomName(_node.Data));
            }
            if (_cutGuide != null)
            {
                _cutGuide.startColor = HollowfenPalette.GoldGlow;
                _cutGuide.endColor = HollowfenPalette.GoldGlow;
                _cutGuide.widthMultiplier = 0.012f;
            }

            Vector3 startPosition = _node.transform.position;
            Quaternion startRotation = _node.transform.rotation;
            Vector3 startScale = _node.transform.localScale;
            Vector3 knifeStart = _knife != null ? _knife.localPosition : Vector3.zero;
            float t = 0f;
            const float cutDuration = 0.72f;

            // Commit on the physical release, before visual cleanup. This keeps quest/inventory/save
            // ordering identical to the old harvest path while making cancel-before-cut side-effect free.
            _node.CommitHarvestFromChallenge();

            while (t < cutDuration)
            {
                float k = Ease(t / cutDuration);
                if (_knife != null)
                {
                    float slice = Mathf.Clamp01(k / 0.30f);
                    _knife.localPosition = Vector3.Lerp(knifeStart,
                        new Vector3(0.26f, -0.015f, 0f), Ease(slice));
                    _knife.localRotation = Quaternion.Euler(0f, -7f,
                        Mathf.Lerp(-5f, -13f, slice));
                }

                float lift = Mathf.Clamp01((k - 0.22f) / 0.78f);
                _node.transform.position = startPosition + Vector3.up * (0.16f * Ease(lift));
                _node.transform.rotation = startRotation *
                    Quaternion.AngleAxis(-10f * Ease(lift), Vector3.forward);
                _node.transform.localScale = Vector3.Lerp(startScale,
                    startScale * 0.20f, Ease(lift));

                UpdateSuccessHaptics();
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return FadeHud(1f, 0f, 0.28f);
            yield return RestoreAfterSequence(success: true);
        }

        private IEnumerator CancelChallenge()
        {
            Phase = CuttingPhase.Canceling;
            StopRumble();
            yield return FadeHud(_hud != null ? _hud.RootAlpha : 1f, 0f, 0.18f);
            yield return RestoreAfterSequence(success: false);
        }

        private IEnumerator RestoreAfterSequence(bool success)
        {
            if (_animator != null)
            {
                int idle = Animator.StringToHash("Base Layer.Idle Walk Run Blend");
                if (_animator.HasState(0, idle)) _animator.CrossFade(idle, 0.20f, 0, 0f);
            }

            if (_cameraCaptured && _focusCamera != null)
            {
                Vector3 from = _focusCamera.Follow != null
                    ? _focusCamera.Follow.FollowOffset : _cameraOffsetBefore;
                float fovFrom = _focusCamera.CurrentFov;
                float t = 0f;
                const float duration = 0.34f;
                while (t < duration)
                {
                    float k = Ease(t / duration);
                    SetCamera(Vector3.Lerp(from, _cameraOffsetBefore, k),
                        Mathf.Lerp(fovFrom, _cameraFovBefore, k));
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (!success) PlayerInteractor.DismissCurrent();
            if (_focusCamera != null)
            {
                _focusCamera.ReleaseFocus();
                _focusCamera.IsHarvestCinematicActive = false;
            }

            RestoreGameplayState();
            CleanupPresentation();

            if (success && _node != null) _node.DeactivateAfterChallenge();

            if (Active == this) Active = null;
            Destroy(gameObject);
        }

        private void RestoreGameplayState()
        {
            if (_restored) return;
            _restored = true;
            StopRumble();
            if (_animatorCaptured && _animator != null)
            {
                _animator.updateMode = _previousAnimatorMode;
                _animator.applyRootMotion = _previousApplyRootMotion;
            }
            RestorePlayerRenderers();
            if (_brainCaptured && _brain != null)
                _brain.IgnoreTimeScale = _previousBrainIgnoreTimeScale;
            _presentationLease?.Dispose();
            _presentationLease = null;
        }

        private void CleanupPresentation()
        {
            if (_hud != null) Destroy(_hud.gameObject);
            if (_knifeRig != null) Destroy(_knifeRig);
            if (_cameraAim != null) Destroy(_cameraAim.gameObject);
            if (_cutGuideMaterial != null) Destroy(_cutGuideMaterial);
            _hud = null;
            _knifeRig = null;
            _knife = null;
            _cameraAim = null;
            _cutGuide = null;
            _cutGuideMaterial = null;
        }

        private void BuildKnifeRig()
        {
            Bounds specimen = CalculateBounds(_node.gameObject);
            _cutPoint = new Vector3(specimen.center.x,
                Mathf.Lerp(specimen.min.y, specimen.max.y, 0.15f), specimen.center.z);

            _knifeRig = new GameObject("ForageKnifeRig");
            Vector3 towardCamera = _macroOffset.sqrMagnitude > 0.001f
                ? _macroOffset.normalized : GetToPlayerDirection();
            Vector3 look = -Vector3.ProjectOnPlane(towardCamera, Vector3.up).normalized;
            if (look.sqrMagnitude < 0.001f) look = Vector3.forward;
            _knifeRig.transform.position = _cutPoint + towardCamera * 0.055f;
            _knifeRig.transform.rotation = Quaternion.LookRotation(look, Vector3.up);

            var prefab = Resources.Load<GameObject>(KnifePrefabResource);
            if (prefab != null)
            {
                var knifeGo = Instantiate(prefab, _knifeRig.transform);
                knifeGo.name = "WrensKnife";
                _knife = knifeGo.transform;
                _knife.localPosition = Vector3.zero;
                _knife.localRotation = Quaternion.Euler(0f, 0f, -7f);

                Bounds knifeBounds = CalculateLocalMeshBounds(knifeGo);
                float longest = Mathf.Max(knifeBounds.size.x,
                    Mathf.Max(knifeBounds.size.y, knifeBounds.size.z));
                float desiredLength = Mathf.Clamp(specimen.size.y * 0.72f, 0.34f, 0.48f);
                float scale = longest > 0.0001f ? desiredLength / longest : 0.22f;
                _knife.localScale = Vector3.one * scale;

                var authoredMaterial = Resources.Load<Material>(KnifeMaterialResource);
                if (authoredMaterial != null)
                {
                    foreach (var renderer in knifeGo.GetComponentsInChildren<Renderer>(true))
                    {
                        var materials = renderer.sharedMaterials;
                        for (int i = 0; i < materials.Length; i++) materials[i] = authoredMaterial;
                        renderer.sharedMaterials = materials;
                    }
                }
            }

            var lineGo = new GameObject("CutGuide");
            lineGo.transform.SetParent(_knifeRig.transform, false);
            lineGo.transform.localPosition = new Vector3(0f, 0f, -0.012f);
            _cutGuide = lineGo.AddComponent<LineRenderer>();
            _cutGuide.useWorldSpace = false;
            _cutGuide.positionCount = 2;
            _cutGuide.SetPosition(0, new Vector3(-0.11f, 0f, 0f));
            _cutGuide.SetPosition(1, new Vector3(0.11f, 0f, 0f));
            _cutGuide.widthMultiplier = 0.006f;
            _cutGuide.startColor = new Color(HollowfenPalette.Gold.r,
                HollowfenPalette.Gold.g, HollowfenPalette.Gold.b, 0.52f);
            _cutGuide.endColor = _cutGuide.startColor;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _cutGuideMaterial = new Material(shader);
                if (_cutGuideMaterial.HasProperty("_BaseColor"))
                    _cutGuideMaterial.SetColor("_BaseColor", Color.white);
                else if (_cutGuideMaterial.HasProperty("_Color"))
                    _cutGuideMaterial.SetColor("_Color", Color.white);
                _cutGuide.material = _cutGuideMaterial;
            }
        }

        private void DisablePlayerRenderers()
        {
            if (_playerRenderers == null) return;
            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                if (_playerRenderers[i] != null) _playerRenderers[i].enabled = false;
            }
        }

        private void RestorePlayerRenderers()
        {
            if (_playerRenderers == null || _playerRendererStates == null) return;
            int count = Mathf.Min(_playerRenderers.Length, _playerRendererStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (_playerRenderers[i] != null)
                    _playerRenderers[i].enabled = _playerRendererStates[i];
            }
        }

        // Renderer.bounds can report the FBX's pre-import size during the frame it is instantiated.
        // Mesh-local bounds are deterministic immediately, which matters for this centimeter-authored
        // knife model (0.019 mesh units long after import).
        private static Bounds CalculateLocalMeshBounds(GameObject root)
        {
            bool initialized = false;
            Bounds result = default;
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 corner = new Vector3(x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                    Vector3 point = toRoot.MultiplyPoint3x4(toWorld.MultiplyPoint3x4(corner));
                    if (!initialized)
                    {
                        result = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else result.Encapsulate(point);
                }
            }
            return initialized ? result : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Bounds CalculateBounds(GameObject go)
        {
            var renderers = go != null ? go.GetComponentsInChildren<Renderer>(true) : null;
            if (renderers == null || renderers.Length == 0)
                return new Bounds(go != null ? go.transform.position : Vector3.zero,
                    Vector3.one * 0.4f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private InputSample ReadInput()
        {
            var pad = Gamepad.current;
            if (pad != null && ControllerGlyphs.IsGamepadActive)
            {
                Vector2 brace = pad.leftStick.ReadValue();
                Vector2 saw = pad.rightStick.ReadValue();
                float straightDown = Mathf.Clamp01((-brace.y - 0.18f) / 0.62f);
                float centered = 1f - Mathf.Clamp01(Mathf.Abs(brace.x) / 0.72f);
                return new InputSample
                {
                    Gamepad = true,
                    Brace = brace,
                    Saw = saw,
                    BraceAmount = straightDown * centered,
                    Cancel = pad.buttonEast.wasPressedThisFrame
                };
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return default;
            float braceY = keyboard.sKey.isPressed ? -1f : 0f;
            float sawX = (keyboard.dKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed ? 1f : 0f);
            return new InputSample
            {
                Gamepad = false,
                Brace = new Vector2(0f, braceY),
                Saw = new Vector2(sawX, 0f),
                BraceAmount = keyboard.sKey.isPressed ? 1f : 0f,
                Cancel = keyboard.escapeKey.wasPressedThisFrame
            };
        }

        private void UpdateHaptics(InputSample sample, bool braced, bool level)
        {
            if (!sample.Gamepad)
            {
                StopRumble();
                return;
            }
            var pad = Gamepad.current;
            if (pad == null) return;
            _rumblePad = pad;

            float blade = Mathf.Abs(sample.Saw.x);
            float low = braced ? 0.08f + sample.BraceAmount * 0.10f + blade * 0.05f : 0f;
            float high = braced ? blade * (level ? 0.24f : 0.38f) : 0f;
            high *= 0.78f + Mathf.Sin(Time.unscaledTime * 48f) * 0.22f;
            if (Time.unscaledTime < _hapticPulseUntil)
            {
                low = Mathf.Max(low, _hapticLow);
                high = Mathf.Max(high, _hapticHigh);
            }
            pad.SetMotorSpeeds(Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        private void UpdateSuccessHaptics()
        {
            if (_rumblePad == null) _rumblePad = Gamepad.current;
            if (_rumblePad == null) return;
            if (Time.unscaledTime < _hapticPulseUntil)
                _rumblePad.SetMotorSpeeds(_hapticLow, _hapticHigh);
            else
                _rumblePad.SetMotorSpeeds(0.08f, 0.05f);
        }

        private void PulseHaptics(float low, float high, float duration)
        {
            _hapticLow = Mathf.Clamp01(low);
            _hapticHigh = Mathf.Clamp01(high);
            _hapticPulseUntil = Time.unscaledTime + duration;
            _rumblePad = Gamepad.current;
            if (_rumblePad != null) _rumblePad.SetMotorSpeeds(_hapticLow, _hapticHigh);
        }

        private void StopRumble()
        {
            if (_rumblePad != null) _rumblePad.SetMotorSpeeds(0f, 0f);
            _rumblePad = null;
            _hapticPulseUntil = 0f;
        }

        private void SetStatus(string key, Color color)
        {
            if (_hud != null) _hud.SetStatus(key, color);
        }

        private void SetCamera(Vector3 offset, float fov)
        {
            if (_focusCamera != null) _focusCamera.SetHarvestFraming(offset, fov);
        }

        private Vector3 GetToPlayerDirection()
        {
            Vector3 toPlayer = _player != null
                ? _player.position - _node.transform.position
                : -_node.transform.forward;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.001f) toPlayer = Vector3.back;
            return toPlayer.normalized;
        }

        private IEnumerator FadeHud(float from, float to, float duration)
        {
            if (_hud == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                _hud.RootAlpha = Mathf.Lerp(from, to, Ease(t / Mathf.Max(0.01f, duration)));
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            _hud.RootAlpha = to;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) StopRumble();
        }

        private void OnDestroy()
        {
            StopRumble();
            if (!_restored)
            {
                if (_cameraCaptured && _focusCamera != null)
                    _focusCamera.SetHarvestFraming(_cameraOffsetBefore, _cameraFovBefore);
                if (_focusCamera != null)
                {
                    _focusCamera.ReleaseFocus();
                    _focusCamera.IsHarvestCinematicActive = false;
                }
                RestoreGameplayState();
            }
            CleanupPresentation();
            if (Active == this) Active = null;
        }

        private struct InputSample
        {
            public bool Gamepad;
            public Vector2 Brace;
            public Vector2 Saw;
            public float BraceAmount;
            public bool Cancel;
        }

    }
}
