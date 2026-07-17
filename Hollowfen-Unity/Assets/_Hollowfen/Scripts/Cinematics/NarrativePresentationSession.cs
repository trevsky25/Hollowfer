using System;
using System.Collections.Generic;
using Hollowfen.Foraging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hollowfen.Cinematics
{
    // Ref-counted ownership for nested narrative presentation. Dialogue, painted narration and
    // prop focus can overlap without whichever one finishes first accidentally restoring control.
    public static class NarrativePresentationSession
    {
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

        private static readonly HashSet<int> Active = new HashSet<int>();
        private static int _nextId = 1;
        private static bool _wasSuspended;
        private static bool _wasPlayerInputEnabled = true;

        public static bool IsLocked => Active.Count > 0;
        public static int LockCount => Active.Count;

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
#endif
        private static void ResetOnLoad()
        {
            Active.Clear();
            _nextId = 1;
            _wasSuspended = false;
            _wasPlayerInputEnabled = true;
        }

        public static Lease Acquire(UnityEngine.Object owner)
        {
            if (Active.Count == 0)
            {
                _wasSuspended = PlayerInteractor.Suspended;
                _wasPlayerInputEnabled = ReadPlayerInputEnabled();
                PlayerInteractor.Suspended = true;
                PlayerInteractor.SetPlayerInputEnabled(false);
            }

            int id = _nextId++;
            if (_nextId == int.MaxValue) _nextId = 1;
            Active.Add(id);
            return new Lease(id);
        }

        private static void Release(int id)
        {
            if (!Active.Remove(id) || Active.Count > 0) return;
            PlayerInteractor.Suspended = _wasSuspended;
            PlayerInteractor.SetPlayerInputEnabled(_wasPlayerInputEnabled);
        }

        private static bool ReadPlayerInputEnabled()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return true;
            var input = player.GetComponent<PlayerInput>();
            return input == null || input.enabled;
        }
    }
}
