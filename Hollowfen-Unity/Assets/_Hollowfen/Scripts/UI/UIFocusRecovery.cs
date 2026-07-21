using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hollowfen.UI
{
    /// <summary>
    /// Shared controller-safety contract for runtime-built presentations. Dynamic lists can
    /// destroy the currently selected row, and a mouse click on a non-control can also clear the
    /// EventSystem. A production modal must recover to a useful control instead of silently
    /// becoming keyboard/gamepad-inert.
    /// </summary>
    public static class UIFocusRecovery
    {
        public static bool HasValidFocus(Transform presentation)
        {
            if (presentation == null || EventSystem.current == null) return false;
            GameObject current = EventSystem.current.currentSelectedGameObject;
            if (current == null || !current.activeInHierarchy ||
                !current.transform.IsChildOf(presentation)) return false;
            Selectable selectable = current.GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }

        public static GameObject FirstInteractable(Transform presentation)
        {
            if (presentation == null) return null;
            foreach (Selectable selectable in presentation.GetComponentsInChildren<Selectable>(true))
                if (selectable != null && selectable.gameObject.activeInHierarchy &&
                    selectable.IsInteractable()) return selectable.gameObject;
            return null;
        }

        public static bool RestoreIfLost(Transform presentation, GameObject preferred)
        {
            if (presentation == null || EventSystem.current == null || HasValidFocus(presentation))
                return false;

            GameObject target = IsUsable(preferred, presentation)
                ? preferred
                : FirstInteractable(presentation);
            if (target == null) return false;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
            return EventSystem.current.currentSelectedGameObject == target;
        }

        private static bool IsUsable(GameObject candidate, Transform presentation)
        {
            if (candidate == null || !candidate.activeInHierarchy ||
                !candidate.transform.IsChildOf(presentation)) return false;
            Selectable selectable = candidate.GetComponent<Selectable>();
            return selectable != null && selectable.IsInteractable();
        }
    }
}
