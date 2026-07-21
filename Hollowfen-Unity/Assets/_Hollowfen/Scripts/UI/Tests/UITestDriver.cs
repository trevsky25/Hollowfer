#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Hollowfen.UI.Tests
{
    // Auto-runs a UIManager smoke test on Play in the editor; excluded from player builds.
    public class UITestDriver : MonoBehaviour
    {
        [SerializeField] private float _stepDelay = 0.8f;

        private IEnumerator Start()
        {
            yield return null;

            var mgr = UIManager.Instance;
            if (mgr == null)
            {
                Debug.LogError("[UITest] FAIL: UIManager.Instance is null");
                yield break;
            }
            Debug.Log("[UITest] PASS: UIManager.Instance present");
            Debug.Log($"[UITest] initial HasOpenScreen={mgr.HasOpenScreen} (expect False)");

            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log("[UITest] -> OpenScreen('test-a')");
            mgr.OpenScreen("test-a");
            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log($"[UITest] TopScreen='{mgr.TopScreen?.ScreenId}' (expect test-a)");

            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log("[UITest] -> OpenScreen('test-b')");
            mgr.OpenScreen("test-b");
            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log($"[UITest] TopScreen='{mgr.TopScreen?.ScreenId}' (expect test-b)");

            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log("[UITest] -> Back()");
            mgr.Back();
            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log($"[UITest] TopScreen='{mgr.TopScreen?.ScreenId}' (expect test-a)");

            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log("[UITest] -> Back()");
            mgr.Back();
            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log($"[UITest] HasOpenScreen={mgr.HasOpenScreen} (expect False)");

            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log("[UITest] -> OpenScreen('does-not-exist')  (should warn, not crash)");
            mgr.OpenScreen("does-not-exist");
            yield return new WaitForSecondsRealtime(_stepDelay);
            Debug.Log($"[UITest] HasOpenScreen={mgr.HasOpenScreen} (expect False)");

            Debug.Log("[UITest] === TEST COMPLETE ===");
        }
    }
}
#endif
