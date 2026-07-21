#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Cinematics;
using Hollowfen.Foraging;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.EditorTools
{
    /// <summary>
    /// Runtime regression harness for the centralized presentation-state lease. Run from bare
    /// gameplay, with no screen or cinematic already holding a presentation lease.
    /// </summary>
    public static class PresentationSessionVerifier
    {
        private const string Prefix = "[PresentationSessionVerifier] ";
        private const string MenuPath = "Tools/Hollowfen/Verify Presentation Session Ownership";

        private sealed class HudProbe
        {
            internal GameObject Root;
            internal CanvasGroup Group;
            internal bool CreatedRoot;
            internal bool CreatedGroup;
            internal float Alpha;
            internal bool Interactable;
            internal bool BlocksRaycasts;
        }

        private static readonly List<NarrativePresentationSession.Lease> Leases =
            new List<NarrativePresentationSession.Lease>();
        private static readonly List<GameObject> Owners = new List<GameObject>();
        private static readonly List<HudProbe> HudProbes = new List<HudProbe>();

        private static bool _running;
        private static bool _baselineCaptured;
        private static int _releaseFrame;
        private static float _baselineTimeScale;
        private static bool _baselineSuspended;
        private static PlayerInput _playerInput;
        private static bool _baselinePlayerInputEnabled;
        private static StarterAssets.StarterAssetsInputs _starterInputs;
        private static bool _baselineCursorLocked;
        private static bool _baselineCursorInputForLook;
        private static CursorLockMode _baselineCursorLockState;
        private static bool _baselineCursorVisible;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (_running)
            {
                Debug.LogError(Prefix + "FAIL - a verification run is already active.");
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError(Prefix + "FAIL - enter Play Mode before running this verifier.");
                return;
            }
            if (EditorApplication.isPaused)
            {
                Debug.LogError(Prefix + "FAIL - resume Play Mode before running this verifier.");
                return;
            }
            if (NarrativePresentationSession.ActiveOwnerCount != 0)
            {
                Debug.LogError(Prefix + "FAIL - expected bare gameplay, but active owners were: " +
                    string.Join(", ", NarrativePresentationSession.ActiveOwnerDescriptions));
                return;
            }

            _running = true;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            try
            {
                CaptureBaseline();
                BuildHudProbes();
                RunImmediateAssertions();
                _releaseFrame = Time.frameCount;
                EditorApplication.update += FinishAfterNextFrame;
            }
            catch (Exception exception)
            {
                FinishFailure(exception.Message);
            }
        }

        private static void RunImmediateAssertions()
        {
            GameObject slowOwner = NewOwner("PresentationVerifier_Slow");
            GameObject pointerOwner = NewOwner("PresentationVerifier_Pointer");
            GameObject inputOwner = NewOwner("PresentationVerifier_Input");

            var slowPolicy = NarrativePresentationSession.SlowMotion(0.6f)
                .With(NarrativePresentationSession.Claim.HideGameplayHud);
            var pointerPolicy = NarrativePresentationSession.SlowMotion(0.25f)
                .With(NarrativePresentationSession.Claim.FreeCursor)
                .With(NarrativePresentationSession.Claim.HideGameplayHud);

            var slowLease = NarrativePresentationSession.Acquire(slowOwner, slowPolicy);
            Leases.Add(slowLease);
            Require(NarrativePresentationSession.ActiveOwnerCount == 1,
                "first lease did not register exactly one owner");
            Require(NarrativePresentationSession.IsLocked,
                "gameplay input was not locked by the first lease");
            Require(Approximately(Time.timeScale, 0.6f),
                "first time override was not applied");
            Require(PlayerInteractor.Suspended,
                "PlayerInteractor was not suspended");
            if (_playerInput != null)
                Require(!_playerInput.enabled, "PlayerInput remained enabled");
            Require(NarrativePresentationSession.BlocksGameplayShortcuts,
                "gameplay shortcuts were not blocked");
            Require(HudIsHidden(), "HUD was not hidden by its first owner");

            var pointerLease = NarrativePresentationSession.Acquire(pointerOwner, pointerPolicy);
            Leases.Add(pointerLease);
            Require(NarrativePresentationSession.ActiveOwnerCount == 2,
                "nested lease did not register its owner");
            Require(Approximately(Time.timeScale, 0.25f),
                "nested time overrides did not choose the minimum scale");
            Require(NarrativePresentationSession.RequiresFreeCursor,
                "free-cursor claim was not exposed");
            Require(Cursor.lockState == CursorLockMode.None && Cursor.visible,
                "free-cursor claim was not applied");
            if (_starterInputs != null)
            {
                Require(!_starterInputs.cursorLocked,
                    "StarterAssets cursor lock remained enabled");
                Require(!_starterInputs.cursorInputForLook,
                    "StarterAssets look input remained enabled for the free cursor");
            }

            var inputLease = NarrativePresentationSession.Acquire(
                inputOwner, NarrativePresentationSession.InputOnly);
            Leases.Add(inputLease);
            Require(NarrativePresentationSession.ActiveOwnerCount == 3,
                "third nested lease did not register");
            Require(NarrativePresentationSession.ActiveOwnerDescriptions.Any(
                    description => description.Contains(slowOwner.name)) &&
                NarrativePresentationSession.ActiveOwnerDescriptions.Any(
                    description => description.Contains(pointerOwner.name)) &&
                NarrativePresentationSession.ActiveOwnerDescriptions.Any(
                    description => description.Contains(inputOwner.name)),
                "owner diagnostics omitted an active owner");

            pointerLease.Dispose();
            Require(NarrativePresentationSession.ActiveOwnerCount == 2,
                "out-of-order release removed the wrong number of owners");
            Require(Approximately(Time.timeScale, 0.6f),
                "releasing the minimum override did not reveal the remaining override");
            Require(!NarrativePresentationSession.RequiresFreeCursor,
                "cursor claim survived its final owner");
            RequireCursorRestored();
            Require(HudIsHidden(),
                "releasing one HUD owner revealed the HUD while another remained");
            Require(PlayerInteractor.Suspended,
                "releasing the pointer owner restored input while nested owners remained");

            pointerLease.Dispose();
            Require(NarrativePresentationSession.ActiveOwnerCount == 2,
                "disposing the same lease twice was not idempotent");

            slowLease.Dispose();
            Require(NarrativePresentationSession.ActiveOwnerCount == 1,
                "releasing the slow owner did not preserve the input-only owner");
            Require(Approximately(Time.timeScale, _baselineTimeScale),
                "time did not restore after the final time owner released");
            RequireHudRestored();
            Require(PlayerInteractor.Suspended,
                "input restored before the final input owner released");
            Require(NarrativePresentationSession.BlocksGameplayShortcuts,
                "shortcut blocking ended before the final owner released");

            inputLease.Dispose();
            Require(NarrativePresentationSession.ActiveOwnerCount == 0,
                "final release left an active owner");
            Require(!NarrativePresentationSession.IsLocked,
                "input lock remained after the final owner released");
            RequireInputRestored();
            RequireCursorRestored();
            RequireHudRestored();
            Require(Approximately(Time.timeScale, _baselineTimeScale),
                "final release did not restore the original time scale");
            Require(NarrativePresentationSession.BlocksGameplayShortcuts,
                "same-frame shortcut-release barrier was not held");
        }

        private static void FinishAfterNextFrame()
        {
            if (!_running || Time.frameCount <= _releaseFrame) return;
            EditorApplication.update -= FinishAfterNextFrame;
            try
            {
                Require(!NarrativePresentationSession.BlocksGameplayShortcuts,
                    "shortcut-release barrier survived beyond its release frame");
                Require(NarrativePresentationSession.ActiveOwnerCount == 0,
                    "verification ended with a leaked presentation owner");
                RequireInputRestored();
                RequireCursorRestored();
                RequireHudRestored();
                Require(Approximately(Time.timeScale, _baselineTimeScale),
                    "time scale changed after the release frame");
                FinishSuccess();
            }
            catch (Exception exception)
            {
                FinishFailure(exception.Message);
            }
        }

        private static void CaptureBaseline()
        {
            _baselineTimeScale = Time.timeScale;
            _baselineSuspended = PlayerInteractor.Suspended;
            _baselineCursorLockState = Cursor.lockState;
            _baselineCursorVisible = Cursor.visible;

            GameObject player = null;
            try { player = GameObject.FindGameObjectWithTag("Player"); }
            catch (UnityException) { }
            _playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
            _baselinePlayerInputEnabled = _playerInput == null || _playerInput.enabled;

            _starterInputs = UnityEngine.Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>(
                FindObjectsInactive.Exclude);
            if (_starterInputs != null)
            {
                _baselineCursorLocked = _starterInputs.cursorLocked;
                _baselineCursorInputForLook = _starterInputs.cursorInputForLook;
            }
            _baselineCaptured = true;
        }

        private static void BuildHudProbes()
        {
            AddHudProbe("_HUDCanvas");
            AddHudProbe("_MiniMapCanvas");
            if (HudProbes.Count == 0)
            {
                var root = new GameObject("_HUDCanvas") { hideFlags = HideFlags.HideAndDontSave };
                var group = root.AddComponent<CanvasGroup>();
                HudProbes.Add(new HudProbe
                {
                    Root = root,
                    Group = group,
                    CreatedRoot = true,
                    Alpha = group.alpha,
                    Interactable = group.interactable,
                    BlocksRaycasts = group.blocksRaycasts,
                });
            }
        }

        private static void AddHudProbe(string name)
        {
            GameObject root = GameObject.Find(name);
            if (root == null) return;
            var group = root.GetComponent<CanvasGroup>();
            bool createdGroup = group == null;
            if (group == null) group = root.AddComponent<CanvasGroup>();
            HudProbes.Add(new HudProbe
            {
                Root = root,
                Group = group,
                CreatedGroup = createdGroup,
                Alpha = group.alpha,
                Interactable = group.interactable,
                BlocksRaycasts = group.blocksRaycasts,
            });
        }

        private static GameObject NewOwner(string name)
        {
            var owner = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            Owners.Add(owner);
            return owner;
        }

        private static bool HudIsHidden()
        {
            return HudProbes.Count > 0 && HudProbes.All(probe => probe.Group != null &&
                Approximately(probe.Group.alpha, 0f) && !probe.Group.interactable &&
                !probe.Group.blocksRaycasts);
        }

        private static void RequireHudRestored()
        {
            foreach (var probe in HudProbes)
            {
                Require(probe.Group != null, "a HUD probe was destroyed during verification");
                Require(Approximately(probe.Group.alpha, probe.Alpha) &&
                    probe.Group.interactable == probe.Interactable &&
                    probe.Group.blocksRaycasts == probe.BlocksRaycasts,
                    "HUD state did not restore exactly");
            }
        }

        private static void RequireInputRestored()
        {
            Require(PlayerInteractor.Suspended == _baselineSuspended,
                "PlayerInteractor suspension did not restore");
            if (_playerInput != null)
                Require(_playerInput.enabled == _baselinePlayerInputEnabled,
                    "PlayerInput enabled state did not restore");
        }

        private static void RequireCursorRestored()
        {
            Require(Cursor.lockState == _baselineCursorLockState &&
                Cursor.visible == _baselineCursorVisible,
                "Unity cursor state did not restore");
            if (_starterInputs != null)
            {
                Require(_starterInputs.cursorLocked == _baselineCursorLocked,
                    "StarterAssets cursorLocked did not restore");
                Require(_starterInputs.cursorInputForLook == _baselineCursorInputForLook,
                    "StarterAssets cursorInputForLook did not restore");
            }
        }

        private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= 0.0001f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (_running && state == PlayModeStateChange.ExitingPlayMode)
                FinishFailure("Play Mode exited before the next-frame assertion completed");
        }

        private static void FinishSuccess()
        {
            Cleanup();
            Debug.Log(Prefix + "PASS - nested leases, minimum time override, idempotent disposal, " +
                "input/cursor/HUD restoration, same-frame shortcut blocking, and zero-leak end state.");
        }

        private static void FinishFailure(string message)
        {
            DisposeAllLeases();
            EmergencyRestoreBaseline();
            int leakedOwners = NarrativePresentationSession.ActiveOwnerCount;
            Cleanup();
            Debug.LogError(Prefix + "FAIL - " + message +
                (leakedOwners > 0 ? " (active owners remaining: " + leakedOwners + ")" : string.Empty));
        }

        private static void DisposeAllLeases()
        {
            for (int i = Leases.Count - 1; i >= 0; i--)
                Leases[i]?.Dispose();
        }

        private static void EmergencyRestoreBaseline()
        {
            if (!_baselineCaptured) return;
            Time.timeScale = _baselineTimeScale;
            PlayerInteractor.Suspended = _baselineSuspended;
            if (_playerInput != null) _playerInput.enabled = _baselinePlayerInputEnabled;
            Cursor.lockState = _baselineCursorLockState;
            Cursor.visible = _baselineCursorVisible;
            if (_starterInputs != null)
            {
                _starterInputs.cursorLocked = _baselineCursorLocked;
                _starterInputs.cursorInputForLook = _baselineCursorInputForLook;
            }
            foreach (var probe in HudProbes)
            {
                if (probe.Group == null) continue;
                probe.Group.alpha = probe.Alpha;
                probe.Group.interactable = probe.Interactable;
                probe.Group.blocksRaycasts = probe.BlocksRaycasts;
            }
        }

        private static void Cleanup()
        {
            EditorApplication.update -= FinishAfterNextFrame;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            DisposeAllLeases();
            Leases.Clear();

            foreach (var owner in Owners)
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            Owners.Clear();

            foreach (var probe in HudProbes)
            {
                if (probe.CreatedRoot && probe.Root != null)
                    UnityEngine.Object.DestroyImmediate(probe.Root);
                else if (probe.CreatedGroup && probe.Group != null)
                    UnityEngine.Object.DestroyImmediate(probe.Group);
            }
            HudProbes.Clear();

            _playerInput = null;
            _starterInputs = null;
            _baselineCaptured = false;
            _running = false;
        }
    }
}
#endif
