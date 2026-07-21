using System.Collections;
using Hollowfen.Audio;
using Hollowfen.Cinematics;
using Hollowfen.GameTime;
using Hollowfen.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowfen.Restoration
{
    /// <summary>
    /// Queues a one-time, story-triggered world reveal after an overnight restoration promotion.
    /// Save hydration never raises DayFlagScheduler.FlagPromoted, so loading an established village
    /// restores its presentation quietly instead of replaying yesterday's cinematic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RestorationRevealDirector : MonoBehaviour
    {
        [SerializeField] private RestorationProjectData _project;
        [SerializeField] private Transform _focusTarget;
        [SerializeField] private string _promotionFlagId = "cottages_reopened_2";
        [SerializeField] private RestorationStage _minimumStage = RestorationStage.Restored;
        [SerializeField] private string _eyebrowId = "restoration.reveal.eyebrow";
        [SerializeField] private string _titleId = "restoration.reveal.title";
        [SerializeField] private string _bodyId = "restoration.reveal.body";
        [SerializeField, Min(0f)] private float _settleDelay = .45f;
        [SerializeField, Min(.1f)] private float _cameraDistance = 7.5f;
        [SerializeField] private float _cameraHeight = 1.8f;
        [SerializeField, Range(30f, 70f)] private float _cameraFov = 46f;
        [SerializeField, Tooltip("Horizontal direction from the cottage toward the reveal camera. An authored direction keeps trees and the player's resting camera from obscuring the house.")]
        private Vector3 _frameDirection = new Vector3(-.44f, 0f, -.90f);

        public bool IsPending => _waitRoutine != null;
        public bool IsPresenting { get; private set; }
        public int QueuedCount { get; private set; }
        public int LastPromotionDay { get; private set; }
        public RestorationProjectData Project => _project;
        public Transform FocusTarget => _focusTarget;
        public Vector3 FrameDirection => _frameDirection;

        private Coroutine _waitRoutine;
        private Coroutine _captionRoutine;
        private GameObject _captionRoot;

        private void OnEnable() => DayFlagScheduler.FlagPromoted += HandleFlagPromoted;

        private void OnDisable()
        {
            DayFlagScheduler.FlagPromoted -= HandleFlagPromoted;
            CancelPending();
        }

        private void OnDestroy() => DestroyCaption();

        private void HandleFlagPromoted(int day, string _, string thenFlag)
        {
            if (string.IsNullOrEmpty(thenFlag) || thenFlag != _promotionFlagId) return;
            if (_project == null || RestorationProjects.GetStage(_project) < _minimumStage) return;
            LastPromotionDay = day;
            QueueReveal(false);
        }

        /// <summary>Presentation/debug seam for visual QA; production uses the overnight flag event.</summary>
        public void PreviewReveal(bool skipWait = false) => QueueReveal(skipWait);

        /// <summary>Stops a queued reveal before camera ownership begins; used by focused verification.</summary>
        public void CancelPending()
        {
            if (_waitRoutine == null || IsPresenting) return;
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }

        private void QueueReveal(bool skipWait)
        {
            if (_waitRoutine != null || IsPresenting || _focusTarget == null) return;
            QueuedCount++;
            _waitRoutine = StartCoroutine(WaitThenReveal(skipWait));
        }

        private IEnumerator WaitThenReveal(bool skipWait)
        {
            while (!skipWait && !PresentationLaneIsClear()) yield return null;

            float elapsed = skipWait ? _settleDelay : 0f;
            while (elapsed < _settleDelay)
            {
                if (!PresentationLaneIsClear())
                {
                    elapsed = 0f;
                    yield return null;
                    continue;
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _waitRoutine = null;
            BeginReveal();
        }

        private bool PresentationLaneIsClear()
        {
            if (NarrativePresentationSession.ActiveOwnerCount > 0) return false;
            if (UIManager.Instance != null &&
                (UIManager.Instance.HasOpenScreen || UIManager.Instance.IsTransitioning)) return false;
            var focus = PropFocusCinematic.Instance;
            return focus == null || (!focus.IsPlaying && !focus.IsHeld);
        }

        private void BeginReveal()
        {
            if (_focusTarget == null || IsPresenting) return;
            IsPresenting = true;
            PropFocusCinematic.Ensure().Play(
                _focusTarget, _cameraDistance, _cameraHeight, _cameraFov,
                1.05f, 2.15f, .80f,
                ShowCaption, CompleteReveal,
                _frameDirection, 9f, .35f);
        }

        private void ShowCaption()
        {
            GameplaySfx.QuestComplete();
            if (_captionRoutine != null) StopCoroutine(_captionRoutine);
            _captionRoutine = StartCoroutine(CaptionRoutine());
        }

        private IEnumerator CaptionRoutine()
        {
            var group = BuildCaption();
            yield return Fade(group, 0f, 1f, .28f);
            yield return new WaitForSecondsRealtime(2.15f);
            yield return Fade(group, 1f, 0f, .42f);
            DestroyCaption();
        }

        private CanvasGroup BuildCaption()
        {
            DestroyCaption();
            _captionRoot = new GameObject("_RestorationRevealCaption", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            _captionRoot.transform.SetParent(transform, false);
            var canvas = _captionRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 96;
            _captionRoot.GetComponent<CanvasScaler>().Init1080();
            _captionRoot.GetComponent<GraphicRaycaster>().enabled = false;
            var group = _captionRoot.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var panelObject = UICanvasUtil.NewImage("RevealPanel", _captionRoot.transform,
                new Color(HollowfenPalette.InkSoft.r, HollowfenPalette.InkSoft.g,
                    HollowfenPalette.InkSoft.b, .94f), false);
            var panel = panelObject.GetComponent<Image>();
            panel.sprite = UICanvasUtil.RoundedRect(18);
            panel.type = Image.Type.Sliced;
            UICanvasUtil.SetRect((RectTransform)panelObject.transform, new Vector2(.5f, 0f),
                new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(780f, 146f),
                new Vector2(0f, 175f));

            var line = UICanvasUtil.NewImage("GoldRule", panelObject.transform,
                new Color(HollowfenPalette.Gold.r, HollowfenPalette.Gold.g,
                    HollowfenPalette.Gold.b, .72f), false);
            UICanvasUtil.SetRect((RectTransform)line.transform, new Vector2(.08f, 1f),
                new Vector2(.92f, 1f), new Vector2(.5f, 1f), new Vector2(0f, 2f), Vector2.zero);

            var eyebrow = UICanvasUtil.NewEyebrow("Eyebrow", panelObject.transform,
                Localization.Get(_eyebrowId), 13f, HollowfenPalette.Gold,
                TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(eyebrow.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), new Vector2(-44f, 20f), new Vector2(0f, -18f));

            var title = UICanvasUtil.NewHeading("Title", panelObject.transform,
                Localization.Get(_titleId), 34f, HollowfenPalette.Cream,
                FontStyles.Normal, TextAlignmentOptions.Center);
            UICanvasUtil.SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(.5f, 1f), new Vector2(-48f, 42f), new Vector2(0f, -43f));

            var body = UICanvasUtil.NewBody("Body", panelObject.transform,
                Localization.Get(_bodyId), 16f,
                new Color(HollowfenPalette.Parchment.r, HollowfenPalette.Parchment.g,
                    HollowfenPalette.Parchment.b, .82f), FontStyles.Italic,
                TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.NoWrap;
            UICanvasUtil.SetRect(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(.5f, 0f), new Vector2(-60f, 30f), new Vector2(0f, 17f));
            return group;
        }

        private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(.05f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                if (group != null) group.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }
            if (group != null) group.alpha = to;
        }

        private void CompleteReveal()
        {
            IsPresenting = false;
            if (_captionRoot == null) _captionRoutine = null;
        }

        private void DestroyCaption()
        {
            if (_captionRoot != null) Destroy(_captionRoot);
            _captionRoot = null;
            _captionRoutine = null;
        }
    }
}
