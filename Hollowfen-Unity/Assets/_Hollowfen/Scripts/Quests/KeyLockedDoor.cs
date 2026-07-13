using System.Collections;
using Hollowfen.Foraging;
using Hollowfen.Items;
using UnityEngine;

namespace Hollowfen.Quests
{
    // Locked door gating a story beat (Father's Mill). Becomes interactable once the player
    // carries the required key item; on unlock it plays the pack door's open animation, drops
    // the door collider so the doorway is passable, and completes the configured quest.
    // Same interaction convention as MushroomNode: trigger SphereCollider on the Foraging layer.
    //
    // With a key prefab + keyhole anchor set (batch-52b), the unlock is cinematic: PropFocusCinematic
    // frames the keyhole, the mill key turns in the lock, then the door swings open (the key is
    // parented to the door leaf, so it swings away with the door revealing the interior).
    [DisallowMultipleComponent]
    public class KeyLockedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _requiredItemId = "item.mill_key";
        [SerializeField, Tooltip("Quest completed on unlock if currently active.")]
        private QuestData _completesQuestIfActive;
        [SerializeField] private string _promptVerbId = "prompt.door.unlock";
        [SerializeField, Tooltip("Localization id for the door's display name.")]
        private string _promptTargetId = "loc.fathers_mill.name";
        [SerializeField, Tooltip("The pack Door object carrying DemoDoor + Animation + BoxCollider.")]
        private GameObject _door;

        [Header("Cinematic unlock (optional)")]
        [SerializeField, Tooltip("Key model shown turning in the lock. Null = instant unlock (no cinematic).")]
        private GameObject _keyPrefab;
        [SerializeField, Tooltip("Fixed anchor at the keyhole (child of this lock, NOT the swinging door). The camera holds here; the key spawns matching its pose.")]
        private Transform _keyholeAnchor;
        [SerializeField] private float _camDistance = 0.85f;
        [SerializeField] private float _fov = 33f;
        [SerializeField] private float _pushSeconds = 1.1f;
        [SerializeField] private float _holdSeconds = 2.8f;
        [SerializeField] private float _restoreSeconds = 0.7f;
        [SerializeField, Tooltip("Local axis the key turns around (its shaft) and how far — the unlock twist.")]
        private Vector3 _turnAxis = Vector3.right;
        [SerializeField] private float _turnDegrees = 95f;
        [SerializeField] private float _turnSeconds = 0.55f;
        [SerializeField, Tooltip("Extra scale on the in-lock key so it reads on a keyhole-less door.")]
        private float _keyScale = 1.5f;

        private bool _opened;

        public string PromptVerb => _promptVerbId;
        public string PromptTarget => Localization.Get(_promptTargetId);

        public bool CanInteract(GameObject actor) => !_opened && KeyItems.Has(_requiredItemId);

        public void Interact(GameObject actor)
        {
            if (!CanInteract(actor)) return;
            _opened = true;

            if (_keyPrefab != null && _keyholeAnchor != null && _door != null
                && Cinematics.PropFocusCinematic.Ensure() != null && !Cinematics.PropFocusCinematic.Instance.IsPlaying)
            {
                StartCoroutine(UnlockCinematic());
                return;
            }

            OpenDoorVisual();
            CompleteQuestIfActive();
        }

        private IEnumerator UnlockCinematic()
        {
            // Key parented to the door leaf so it swings away with the door; camera holds on the fixed
            // keyhole anchor (child of this lock), framed straight-on along the door's face normal so the
            // studded door reads flat and the interior reveals cleanly as it swings.
            var key = Instantiate(_keyPrefab, _keyholeAnchor.position, _keyholeAnchor.rotation, _door.transform);
            key.name = "_MillKey_InLock";
            key.transform.localScale *= _keyScale;

            var focus = Cinematics.PropFocusCinematic.Instance;
            bool peaked = false, done = false;
            focus.Play(_keyholeAnchor, _camDistance, 0f, _fov, _pushSeconds, _holdSeconds, _restoreSeconds,
                () => peaked = true, () => done = true, _door.transform.forward);

            while (!peaked) yield return null;
            yield return TurnKey(key.transform, _turnAxis, _turnDegrees, _turnSeconds);
            yield return new WaitForSecondsRealtime(0.2f);
            OpenDoorVisual();

            while (!done) yield return null;
            if (key != null) Destroy(key);
            CompleteQuestIfActive();
        }

        private static IEnumerator TurnKey(Transform key, Vector3 localAxis, float degrees, float seconds)
        {
            Quaternion from = key.localRotation;
            Quaternion to = from * Quaternion.AngleAxis(degrees, localAxis);
            float t = 0f;
            while (t < seconds && key != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float e = u * u * (3f - 2f * u);
                key.localRotation = Quaternion.Slerp(from, to, e);
                yield return null;
            }
            if (key != null) key.localRotation = to;
        }

        private void OpenDoorVisual()
        {
            if (_door == null) return;
            var demo = _door.GetComponent<InfinityPBR.DemoDoor>();
            bool animated = false;
            if (demo != null && demo.open != null)
            {
                demo.Open();
                animated = true;
            }
            var col = _door.GetComponent<Collider>();
            if (col != null) col.enabled = false;
            // No open clip on this prefab variant — swing it out of the way so the
            // doorway still reads as opened.
            if (!animated) _door.transform.Rotate(0f, 110f, 0f, Space.Self);
        }

        private void CompleteQuestIfActive()
        {
            if (_completesQuestIfActive != null && QuestManager.IsActive(_completesQuestIfActive.Id))
                QuestManager.CompleteQuest(_completesQuestIfActive.Id);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1.2f, 0.4f);
        }
    }
}
