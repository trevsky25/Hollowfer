using System;
using System.Collections.Generic;
using System.Linq;
using Hollowfen.Foraging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Cinematics
{
    /// <summary>
    /// Ref-counted ownership for every gameplay presentation resource. Nested screens and
    /// cinematics can finish in any order without restoring time/input/cursor/HUD until the
    /// final owner of that specific resource releases it.
    /// </summary>
    public static class NarrativePresentationSession
    {
        [Flags]
        public enum Claim
        {
            None = 0,
            BlockGameplayShortcuts = 1 << 0,
            GameplayInput = 1 << 1,
            TimeOverride = 1 << 2,
            FreeCursor = 1 << 3,
            HideGameplayHud = 1 << 4,
        }

        public readonly struct Policy
        {
            public Policy(Claim claims, float timeScale = 1f)
            {
                Claims = claims;
                TimeScale = timeScale;
            }

            public Claim Claims { get; }
            public float TimeScale { get; }
            public bool Has(Claim claim) => (Claims & claim) != 0;

            public Policy With(Claim claim) => new Policy(Claims | claim, TimeScale);
        }

        public sealed class Lease : IDisposable
        {
            private int _id;
            internal Lease(int id) => _id = id;

            public void Dispose()
            {
                if (_id == 0) return;
                Release(_id);
                _id = 0;
            }
        }

        private sealed class Entry
        {
            internal UnityEngine.Object Owner;
            internal string Description;
            internal Policy Policy;
        }

        private struct HudState
        {
            internal float Alpha;
            internal bool Interactable;
            internal bool BlocksRaycasts;
        }

        public static readonly Policy InputOnly = new Policy(
            Claim.BlockGameplayShortcuts | Claim.GameplayInput);
        public static readonly Policy InteractiveNoPause = new Policy(
            Claim.BlockGameplayShortcuts | Claim.GameplayInput | Claim.FreeCursor);
        public static readonly Policy Modal = new Policy(
            Claim.BlockGameplayShortcuts | Claim.GameplayInput | Claim.TimeOverride | Claim.FreeCursor, 0f);

        public static Policy SlowMotion(float timeScale) => new Policy(
            Claim.BlockGameplayShortcuts | Claim.GameplayInput | Claim.TimeOverride,
            Mathf.Clamp(timeScale, 0f, 1f));

        private static readonly Dictionary<int, Entry> Active = new Dictionary<int, Entry>();
        private static readonly Dictionary<CanvasGroup, HudState> HiddenHud = new Dictionary<CanvasGroup, HudState>();
        private static int _nextId = 1;
        private static int _blockShortcutsThroughFrame = -1;

        private static bool _inputApplied;
        private static bool _wasSuspended;
        private static bool _wasPlayerInputEnabled = true;
        private static bool _timeApplied;
        private static float _previousTimeScale = 1f;
        private static bool _cursorApplied;
        private static CursorLockMode _previousCursorLock;
        private static bool _previousCursorVisible;
        private static StarterAssets.StarterAssetsInputs _cursorInputs;
        private static bool _previousCursorLocked = true;
        private static bool _previousCursorInputForLook = true;
        private static bool _hudApplied;

        public static bool IsLocked => Active.Values.Any(entry => entry.Policy.Has(Claim.GameplayInput));
        public static int LockCount => Active.Count;
        public static int ActiveOwnerCount => Active.Count;
        public static bool RequiresFreeCursor => Active.Values.Any(entry => entry.Policy.Has(Claim.FreeCursor));
        public static bool BlocksGameplayShortcuts =>
            Active.Values.Any(entry => entry.Policy.Has(Claim.BlockGameplayShortcuts)) ||
            Time.frameCount <= _blockShortcutsThroughFrame;
        public static string[] ActiveOwnerDescriptions => Active.Values.Select(entry => entry.Description).ToArray();

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Active.Clear();
            HiddenHud.Clear();
            _nextId = 1;
            _blockShortcutsThroughFrame = -1;
            _inputApplied = false;
            _timeApplied = false;
            _cursorApplied = false;
            _hudApplied = false;
            _wasSuspended = false;
            _wasPlayerInputEnabled = true;
            _previousTimeScale = 1f;
            _cursorInputs = null;
        }

        // Backward-compatible input-only entry point for narrative owners migrated earlier.
        public static Lease Acquire(UnityEngine.Object owner) => Acquire(owner, InputOnly);

        public static Lease Acquire(UnityEngine.Object owner, Policy policy)
        {
            int id = NextId();
            Active[id] = new Entry
            {
                Owner = owner,
                Description = owner != null ? owner.GetType().Name + "(" + owner.name + ")" : "unknown owner",
                Policy = policy,
            };
            Recompute();
            return new Lease(id);
        }

        public static Lease AcquireIfGameplay(UnityEngine.Object owner, Policy policy)
        {
            var playerInput = UnityEngine.Object.FindAnyObjectByType<PlayerInput>(FindObjectsInactive.Exclude);
            var starterInput = UnityEngine.Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Exclude);
            return playerInput == null && starterInput == null ? null : Acquire(owner, policy);
        }

        private static int NextId()
        {
            while (Active.ContainsKey(_nextId))
            {
                _nextId++;
                if (_nextId == int.MaxValue) _nextId = 1;
            }
            int id = _nextId++;
            if (_nextId == int.MaxValue) _nextId = 1;
            return id;
        }

        private static void Release(int id)
        {
            if (!Active.TryGetValue(id, out var removed)) return;
            bool releasedLastShortcutOwner = removed.Policy.Has(Claim.BlockGameplayShortcuts) &&
                Active.Where(pair => pair.Key != id).All(pair => !pair.Value.Policy.Has(Claim.BlockGameplayShortcuts));
            Active.Remove(id);
            if (releasedLastShortcutOwner) _blockShortcutsThroughFrame = Time.frameCount;
            Recompute();
        }

        private static void Recompute()
        {
            bool wantsInput = Active.Values.Any(entry => entry.Policy.Has(Claim.GameplayInput));
            bool wantsTime = Active.Values.Any(entry => entry.Policy.Has(Claim.TimeOverride));
            bool wantsCursor = Active.Values.Any(entry => entry.Policy.Has(Claim.FreeCursor));
            bool wantsHudHidden = Active.Values.Any(entry => entry.Policy.Has(Claim.HideGameplayHud));

            ApplyInput(wantsInput);
            ApplyTime(wantsTime);
            ApplyCursor(wantsCursor);
            ApplyHud(wantsHudHidden);
        }

        private static void ApplyInput(bool active)
        {
            if (active && !_inputApplied)
            {
                _wasSuspended = PlayerInteractor.Suspended;
                _wasPlayerInputEnabled = ReadPlayerInputEnabled();
                _inputApplied = true;
            }

            if (active)
            {
                PlayerInteractor.Suspended = true;
                PlayerInteractor.SetPlayerInputEnabled(false);
                var starter = UnityEngine.Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Exclude);
                if (starter != null)
                {
                    starter.MoveInput(Vector2.zero);
                    starter.LookInput(Vector2.zero);
                    starter.JumpInput(false);
                    starter.SprintInput(false);
                }
            }
            else if (_inputApplied)
            {
                PlayerInteractor.Suspended = _wasSuspended;
                PlayerInteractor.SetPlayerInputEnabled(_wasPlayerInputEnabled);
                _inputApplied = false;
            }
        }

        private static void ApplyTime(bool active)
        {
            if (active && !_timeApplied)
            {
                _previousTimeScale = Time.timeScale;
                _timeApplied = true;
            }

            if (active)
            {
                float scale = 1f;
                foreach (var entry in Active.Values)
                    if (entry.Policy.Has(Claim.TimeOverride)) scale = Mathf.Min(scale, entry.Policy.TimeScale);
                Time.timeScale = scale;
            }
            else if (_timeApplied)
            {
                Time.timeScale = _previousTimeScale;
                _timeApplied = false;
            }
        }

        private static void ApplyCursor(bool active)
        {
            if (active && !_cursorApplied)
            {
                _previousCursorLock = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                _cursorInputs = UnityEngine.Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Exclude);
                if (_cursorInputs != null)
                {
                    _previousCursorLocked = _cursorInputs.cursorLocked;
                    _previousCursorInputForLook = _cursorInputs.cursorInputForLook;
                }
                _cursorApplied = true;
            }

            if (active)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (_cursorInputs == null)
                    _cursorInputs = UnityEngine.Object.FindAnyObjectByType<StarterAssets.StarterAssetsInputs>(FindObjectsInactive.Exclude);
                if (_cursorInputs != null)
                {
                    _cursorInputs.cursorLocked = false;
                    _cursorInputs.cursorInputForLook = false;
                }
            }
            else if (_cursorApplied)
            {
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                if (_cursorInputs != null)
                {
                    _cursorInputs.cursorLocked = _previousCursorLocked;
                    _cursorInputs.cursorInputForLook = _previousCursorInputForLook;
                }
                _cursorInputs = null;
                _cursorApplied = false;
            }
        }

        private static void ApplyHud(bool active)
        {
            if (active)
            {
                foreach (string name in new[] { "_HUDCanvas", "_MiniMapCanvas" })
                {
                    GameObject go = GameObject.Find(name);
                    if (go == null) continue;
                    var group = go.GetComponent<CanvasGroup>();
                    if (group == null) group = go.AddComponent<CanvasGroup>();
                    if (!HiddenHud.ContainsKey(group))
                    {
                        HiddenHud[group] = new HudState
                        {
                            Alpha = group.alpha,
                            Interactable = group.interactable,
                            BlocksRaycasts = group.blocksRaycasts,
                        };
                    }
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                }
                _hudApplied = true;
            }
            else if (_hudApplied)
            {
                foreach (var pair in HiddenHud)
                {
                    if (pair.Key == null) continue;
                    pair.Key.alpha = pair.Value.Alpha;
                    pair.Key.interactable = pair.Value.Interactable;
                    pair.Key.blocksRaycasts = pair.Value.BlocksRaycasts;
                }
                HiddenHud.Clear();
                _hudApplied = false;
            }
        }

        private static bool ReadPlayerInputEnabled()
        {
            var input = UnityEngine.Object.FindAnyObjectByType<PlayerInput>(FindObjectsInactive.Exclude);
            return input == null || input.enabled;
        }
    }
}
