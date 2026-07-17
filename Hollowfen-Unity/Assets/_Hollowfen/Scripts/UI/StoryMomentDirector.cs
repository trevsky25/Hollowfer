using System;
using System.Collections.Generic;
using Hollowfen.Cinematics;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.UI
{
    // Single sequencing boundary for authored quest presentation. It owns the hand-off between
    // an optional world-space focus shot and the painted UI reveal, then restores before callback.
    public sealed class StoryMomentDirector : MonoBehaviour
    {
        private sealed class Request
        {
            public StoryMomentData Moment;
            public Transform Context;
            public Action OnDone;
        }

        public static StoryMomentDirector Instance { get; private set; }

        private readonly Queue<Request> _queue = new Queue<Request>();
        private Request _current;

        public bool IsPresenting => _current != null;
        public StoryMomentData CurrentMoment => _current?.Moment;

        public static StoryMomentDirector Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("_StoryMomentDirector");
                Instance = go.AddComponent<StoryMomentDirector>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Play(StoryMomentData moment, Transform context = null, Action onDone = null)
        {
            if (moment == null) { onDone?.Invoke(); return; }
            _queue.Enqueue(new Request { Moment = moment, Context = context, OnDone = onDone });
            if (_current == null) StartNext();
        }

        public void Skip()
        {
            if (_current == null) return;
            if (NarrationOverlay.Instance != null && NarrationOverlay.Instance.IsShowing)
                NarrationOverlay.Instance.SkipAll();
            else if (PropFocusCinematic.Instance != null && PropFocusCinematic.Instance.IsHeld)
                FinishReveal();
        }

        private void StartNext()
        {
            if (_queue.Count == 0) { _current = null; return; }
            _current = _queue.Dequeue();
            var moment = _current.Moment;

            if (moment.FocusContext && _current.Context != null)
            {
                var focus = PropFocusCinematic.Ensure();
                focus.Play(_current.Context, moment.FocusDistance, moment.FocusHeight, moment.FocusFov,
                    moment.FocusPushSeconds, moment.FocusHoldSeconds, moment.FocusRestoreSeconds,
                    null, ShowReveal, default(Vector3), 0f, 0f, true);
            }
            else
            {
                ShowReveal();
            }
        }

        private void ShowReveal()
        {
            if (_current == null) return;
            var overlay = NarrationOverlay.Instance;
            if (overlay == null)
            {
                var go = new GameObject("_NarrationOverlay");
                overlay = go.AddComponent<NarrationOverlay>();
            }
            overlay.ShowStoryMoment(_current.Moment, FinishReveal);
        }

        private void FinishReveal()
        {
            var focus = PropFocusCinematic.Instance;
            if (focus != null && focus.IsHeld)
                focus.Restore(CompleteCurrent);
            else
                CompleteCurrent();
        }

        private void CompleteCurrent()
        {
            if (_current == null) return;
            var done = _current.OnDone;
            _current = null;
            done?.Invoke();
            if (_current == null) StartNext();
        }
    }
}
