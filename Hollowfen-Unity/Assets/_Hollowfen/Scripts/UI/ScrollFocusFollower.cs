using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    // Watches EventSystem.currentSelectedGameObject and smoothly scrolls a
    // ScrollRect so the focused element stays inside the viewport, with
    // `_padding` of breathing room on the leading edge.
    //
    // Math is done in content-local space (not world) so it's correct under
    // any CanvasScaler / parent scale. Animation uses SmoothDamp so the move
    // eases in and out instead of snapping.
    public class ScrollFocusFollower : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private float _padding = 60f;
        [SerializeField] private float _smoothTime = 0.18f;
        [SerializeField] private float _maxSpeed = 6f; // normalized units per second cap

        private GameObject _last;
        private float _targetNormalized = -1f;
        private float _velocity;

        private void Reset()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        private void Awake()
        {
            if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
        }

        private void LateUpdate()
        {
            var es = EventSystem.current;
            if (es == null || _scrollRect == null || _scrollRect.content == null) return;

            var sel = es.currentSelectedGameObject;
            if (sel != null && sel != _last)
            {
                _last = sel;
                var rt = sel.transform as RectTransform;
                if (rt != null && rt.IsChildOf(_scrollRect.content))
                {
                    float next = ComputeTargetNormalized(rt);
                    if (next >= 0f)
                    {
                        _targetNormalized = next;
                        _velocity = 0f;
                    }
                }
            }

            if (_targetNormalized >= 0f)
            {
                float current = _scrollRect.verticalNormalizedPosition;
                float stepped = Mathf.SmoothDamp(current, _targetNormalized, ref _velocity, _smoothTime, _maxSpeed, Time.unscaledDeltaTime);
                _scrollRect.verticalNormalizedPosition = stepped;
                if (Mathf.Abs(stepped - _targetNormalized) < 0.001f && Mathf.Abs(_velocity) < 0.001f)
                {
                    _scrollRect.verticalNormalizedPosition = _targetNormalized;
                    _targetNormalized = -1f;
                    _velocity = 0f;
                }
            }
        }

        // Returns the desired ScrollRect.verticalNormalizedPosition that brings
        // `target` into the viewport with padding, or -1 if already in view.
        // All distances are taken in content-local units.
        private float ComputeTargetNormalized(RectTransform target)
        {
            var content = _scrollRect.content;
            var viewport = _scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)_scrollRect.transform;
            Canvas.ForceUpdateCanvases();

            float viewportHeight = viewport.rect.height;
            float contentHeight = content.rect.height;
            float scrollable = contentHeight - viewportHeight;
            if (scrollable <= 0.5f) return -1f;

            // Get the target's top-left and bottom-left WORLD positions, then
            // bring them into content-local space.
            var worldCorners = new Vector3[4];
            target.GetWorldCorners(worldCorners);
            // 0 = bottom-left, 1 = top-left
            Vector3 localTopLeft    = content.InverseTransformPoint(worldCorners[1]);
            Vector3 localBottomLeft = content.InverseTransformPoint(worldCorners[0]);

            // Content rect with pivot.y = 1 places yMax=0 at the top, yMin=-h at bottom.
            // So target's "distance from content top" = -localY.
            // (Works for any pivot — see the full derivation below.)
            float contentTopY = content.rect.yMax;
            float targetTopFromTop    = contentTopY - localTopLeft.y;
            float targetBottomFromTop = contentTopY - localBottomLeft.y;

            // Current viewport top in the same "distance from content top" axis.
            float currentTop = (1f - _scrollRect.verticalNormalizedPosition) * scrollable;
            float currentBottom = currentTop + viewportHeight;

            // Target is above current view? scroll up.
            if (targetTopFromTop < currentTop + _padding)
            {
                float newTop = Mathf.Max(0f, targetTopFromTop - _padding);
                return Mathf.Clamp01(1f - newTop / scrollable);
            }
            // Target is below current view? scroll down.
            if (targetBottomFromTop > currentBottom - _padding)
            {
                float newTop = Mathf.Min(scrollable, targetBottomFromTop - viewportHeight + _padding);
                return Mathf.Clamp01(1f - newTop / scrollable);
            }
            // Already in view.
            return -1f;
        }
    }
}
