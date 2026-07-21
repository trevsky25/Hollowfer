#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Hollowfen
{
    // Dev-only helper: walks a CharacterController in a direction over several frames so
    // physics trigger events fire the same way they do for real player movement. Used by
    // automated play-mode verification through the MCP bridge; excluded from player builds.
    public class EditorTestDriver : MonoBehaviour
    {
        public void Begin(CharacterController cc, Vector3 dir, float speed, float seconds)
        {
            StartCoroutine(Walk(cc, dir, speed, seconds));
        }

        private IEnumerator Walk(CharacterController cc, Vector3 dir, float speed, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && cc != null)
            {
                cc.Move(dir.normalized * speed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
#endif
