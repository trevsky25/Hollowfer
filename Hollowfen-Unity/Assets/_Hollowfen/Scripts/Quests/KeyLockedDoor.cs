using System.Collections;
using Hollowfen.Audio;
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
        [Header("Key blocking")]
        [SerializeField, Tooltip("How far in front of the keyhole the guided approach begins.")]
        private float _approachDistance = 0.18f;
        [SerializeField, Tooltip("Sideways offset at the beginning of the approach, in door-local metres.")]
        private float _approachLateralOffset = 0.08f;
        [SerializeField, Tooltip("Vertical offset at the beginning of the approach, in metres.")]
        private float _approachVerticalOffset = -0.055f;
        [SerializeField] private float _approachSeconds = 0.5f;
        [SerializeField] private float _insertSeconds = 0.34f;
        [SerializeField, Tooltip("Leaves the key's centre just proud of the door after insertion. The tip still passes into the lock.")]
        private float _seatedOffset = 0.012f;
        [SerializeField, Tooltip("Fine alignment in the straight-on lock shot. Positive values move the whole insertion route screen-right so the shaft meets the visible keyhole.")]
        private float _keyholeScreenRightOffset = 0.025f;
        [SerializeField, Tooltip("Local axis the key turns around (its shaft) and how far — the unlock twist.")]
        private Vector3 _turnAxis = Vector3.right;
        [SerializeField] private float _turnDegrees = 95f;
        [SerializeField] private float _turnSeconds = 0.55f;
        [SerializeField, Tooltip("Extra scale on the in-lock key so it reads on a keyhole-less door.")]
        private float _keyScale = 1.5f;

        private bool _opened;

        public string PromptVerb => _promptVerbId;
        public string PromptTarget => Localization.Get(_promptTargetId);

        private void Awake()
        {
            SuppressVendorAutoOpen();
        }

        private void Start()
        {
            // Player position and completed quests persist independently of scene objects. A save
            // made inside the mill therefore reloads on the far side of this authored door. Mirror
            // the completed unlock immediately so Continue never closes the threshold around Wren
            // or asks her to replay a one-shot key cinematic.
            if (_door != null && _completesQuestIfActive != null &&
                QuestManager.IsCompleted(_completesQuestIfActive.Id))
            {
                _opened = true;
                OpenDoorVisual(true, false);
            }
        }

        private void SuppressVendorAutoOpen()
        {
            if (_door == null) return;
            var controlledDoor = _door.GetComponent<InfinityPBR.DemoDoor>();
            var autoOpen = _door.GetComponentInParent<InfinityPBR.DemoDoorAutoOpen>();
            if (controlledDoor == null || autoOpen == null || autoOpen.doors == null) return;

            bool ownsDoor = false;
            for (int i = 0; i < autoOpen.doors.Length; i++)
            {
                if (autoOpen.doors[i] != controlledDoor) continue;
                ownsDoor = true;
                break;
            }
            if (!ownsDoor) return;

            // The imported demo trigger ignores its serialized layer mask and opens for every
            // valid Unity layer. This lock is the authoritative controller for this one leaf.
            autoOpen.enabled = false;
            var trigger = autoOpen.GetComponent<Collider>();
            if (trigger != null && trigger.isTrigger) trigger.enabled = false;
        }

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
            // Keep the camera on the fixed keyhole anchor, but animate the key from a separate motion
            // pivot. The key mesh's shaft is its local +X axis; the old spawn copied the anchor rotation,
            // which laid that shaft sideways across the door. Here +X points INTO the lock, local +Z is
            // upright, and the prefab's authored rotation is preserved under the pivot.
            Vector3 outward = _door.transform.forward.normalized;
            Vector3 doorUp = _door.transform.up.normalized;
            Vector3 doorRight = _door.transform.right.normalized;

            var pivotObject = new GameObject("_MillKey_InLockPivot");
            var pivot = pivotObject.transform;
            pivot.SetParent(transform, true);
            pivot.rotation = Quaternion.LookRotation(doorUp, -doorRight);

            var key = Instantiate(_keyPrefab, pivot, false);
            key.name = "_MillKey_InLock";
            key.transform.localScale *= _keyScale;

            // Place the pre-insert pose from the actual rendered tip rather than a hand-entered model
            // offset. This keeps the route valid if the key art or its presentation scale changes.
            float tipReach = Mathf.Max(0.02f, PositiveShaftExtent(pivot, key));
            // The cinematic camera frames from door.forward and therefore sees -doorRight as
            // screen-right. Keep this correction on the entire route (not just the last frame) so
            // the key never makes a sideways snap as it seats in the visible keyhole.
            Vector3 keyholeAlignment = -doorRight * _keyholeScreenRightOffset;
            Vector3 seatedPosition = _keyholeAnchor.position + keyholeAlignment
                                     + outward * Mathf.Max(0f, _seatedOffset);
            Vector3 preInsertPosition = _keyholeAnchor.position + keyholeAlignment + outward * tipReach;
            Vector3 approachPosition = preInsertPosition
                                       + outward * Mathf.Max(0f, _approachDistance)
                                       + doorRight * _approachLateralOffset
                                       + doorUp * _approachVerticalOffset;
            pivot.position = approachPosition;

            var focus = Cinematics.PropFocusCinematic.Instance;
            bool peaked = false, done = false;
            focus.Play(_keyholeAnchor, _camDistance, 0f, _fov, _pushSeconds, _holdSeconds, _restoreSeconds,
                () => peaked = true, () => done = true, _door.transform.forward);

            while (!peaked) yield return null;
            yield return MoveKey(pivot, approachPosition, preInsertPosition,
                Mathf.Max(0.01f, _approachSeconds), doorUp, 0.018f);
            yield return MoveKey(pivot, preInsertPosition, seatedPosition,
                Mathf.Max(0.01f, _insertSeconds), doorUp, 0f);

            GameplaySfx.KeyTurn();
            yield return TurnKey(pivot, _turnAxis, _turnDegrees, Mathf.Max(0.01f, _turnSeconds));
            yield return new WaitForSecondsRealtime(0.2f);

            // Only attach the rig to the moving leaf once the key is seated and turned. Until this
            // moment its route is measured in the fixed door-frame space; afterwards it swings away
            // with the door exactly as the original cinematic intended.
            pivot.SetParent(_door.transform, true);
            OpenDoorVisual();

            while (!done) yield return null;
            if (pivotObject != null) Destroy(pivotObject);
            CompleteQuestIfActive();
        }

        private static IEnumerator MoveKey(Transform keyPivot, Vector3 from, Vector3 to,
                                           float seconds, Vector3 arcUp, float arcHeight)
        {
            float t = 0f;
            while (t < seconds && keyPivot != null)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                float e = u * u * (3f - 2f * u);
                float arc = Mathf.Sin(e * Mathf.PI) * arcHeight;
                keyPivot.position = Vector3.LerpUnclamped(from, to, e) + arcUp * arc;
                yield return null;
            }
            if (keyPivot != null) keyPivot.position = to;
        }

        // Furthest rendered point along the motion pivot's +X (the key tip / insertion axis).
        // Renderer world bounds are intentionally not used: their axis-aligned expansion would make
        // this value camera/door-rotation dependent. Mesh bounds transformed into pivot space remain
        // stable and are cheap to evaluate for this one-shot cinematic.
        private static float PositiveShaftExtent(Transform pivot, GameObject key)
        {
            float maximum = 0f;
            bool found = false;
            var filters = key.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                AccumulatePositiveX(pivot, filters[i].transform, mesh.bounds, ref maximum);
                found = true;
            }

            var skinned = key.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length; i++)
            {
                AccumulatePositiveX(pivot, skinned[i].transform, skinned[i].localBounds, ref maximum);
                found = true;
            }
            return found ? maximum : 0.08f;
        }

        private static void AccumulatePositiveX(Transform pivot, Transform meshTransform, Bounds bounds,
                                                ref float maximum)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                var corner = new Vector3(x == 0 ? min.x : max.x,
                                         y == 0 ? min.y : max.y,
                                         z == 0 ? min.z : max.z);
                float localX = pivot.InverseTransformPoint(meshTransform.TransformPoint(corner)).x;
                maximum = Mathf.Max(maximum, localX);
            }
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

        private void OpenDoorVisual(bool instant = false, bool playFeedback = true)
        {
            if (_door == null) return;
            if (playFeedback) GameplaySfx.DoorOpen();

            var demo = _door.GetComponent<InfinityPBR.DemoDoor>();
            var animation = _door.GetComponent<Animation>();
            bool animated = false;
            if (demo != null && demo.open != null)
            {
                if (instant)
                {
                    if (animation != null) animation.Stop();
                    demo.open.SampleAnimation(_door, demo.open.length);
                    animated = true;
                }
                else if (animation != null)
                {
                    // This vendor clip is imported as looping and all three wrap settings default
                    // to the clip. Clamp the runtime state so the leaf reaches its final pose once
                    // instead of cycling back across the doorway.
                    animation.wrapMode = WrapMode.ClampForever;
                    var state = animation[demo.open.name];
                    if (state != null) state.wrapMode = WrapMode.ClampForever;
                    demo.Open();
                    animated = true;
                }
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
