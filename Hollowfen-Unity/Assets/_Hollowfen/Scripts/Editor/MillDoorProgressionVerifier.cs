#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Hollowfen.Dialogue;
using Hollowfen.Foraging;
using Hollowfen.Items;
using Hollowfen.NPCs;
using Hollowfen.Quests;
using Hollowfen.Save;
using UnityEditor;
using UnityEngine;

namespace Hollowfen.EditorTools
{
    /// <summary>Focused Play Mode coverage for the key-to-mill threshold and resume path.</summary>
    public static class MillDoorProgressionVerifier
    {
        private const string SpeakBramPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act1_02_SpeakBram.asset";
        private const string SearchMillPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act1_03_SearchMill.asset";
        private const string FindJournalPath =
            "Assets/_Hollowfen/Data/Quests/Quest_Act1_04_FindJournal.asset";
        private const string BramPath = "Assets/_Hollowfen/Data/NPCs/NPC_Bram.asset";
        private const string BramIntroPath =
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act1_Homecoming_Bram.asset";
        private const string BramKeyPath =
            "Assets/_Hollowfen/Data/Dialogue/Dialogue_Act1_CrookedPintle_BramKey.asset";
        private const string MillKeyId = "item.mill_key";

        private static readonly BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Hollowfen/Production/Verify Mill Door Progression")]
        public static void VerifyMenu()
        {
            Debug.Log(RunAll());
        }

        public static string RunAll()
        {
            Require(EditorApplication.isPlaying, "run this verifier in Play Mode");

            var locks = UnityEngine.Object.FindObjectsByType<KeyLockedDoor>(FindObjectsInactive.Include);
            Require(locks.Length == 1, "expected exactly one authored KeyLockedDoor");
            KeyLockedDoor millLock = locks[0];
            var serialized = new SerializedObject(millLock);

            string requiredItem = serialized.FindProperty("_requiredItemId").stringValue;
            var completedByDoor = serialized.FindProperty("_completesQuestIfActive")
                .objectReferenceValue as QuestData;
            var door = serialized.FindProperty("_door").objectReferenceValue as GameObject;
            var keyPrefab = serialized.FindProperty("_keyPrefab").objectReferenceValue as GameObject;
            var keyhole = serialized.FindProperty("_keyholeAnchor").objectReferenceValue as Transform;
            Require(requiredItem == MillKeyId, "mill lock requires the wrong key item");
            Require(completedByDoor != null && completedByDoor.Id == "searchMill",
                "mill lock does not complete searchMill");
            Require(door != null && keyPrefab != null && keyhole != null,
                "mill lock is missing its door, key, or keyhole reference");

            var lockTrigger = millLock.GetComponent<SphereCollider>();
            int foragingLayer = LayerMask.NameToLayer("Foraging");
            Require(lockTrigger != null && lockTrigger.enabled && lockTrigger.isTrigger &&
                    millLock.gameObject.layer == foragingLayer,
                "mill lock is not an enabled Foraging-layer trigger");

            var doorCollider = door.GetComponent<Collider>();
            var animation = door.GetComponent<Animation>();
            var demoDoor = door.GetComponent<InfinityPBR.DemoDoor>();
            Require(doorCollider != null && !doorCollider.isTrigger,
                "moving mill-door leaf has no solid collider");
            Require(animation != null && demoDoor != null && demoDoor.open != null,
                "moving mill-door leaf has no authored open animation");
            Require(!animation.isPlaying, "cannot verify while the mill door is already animating");

            var autoOpen = door.GetComponentInParent<InfinityPBR.DemoDoorAutoOpen>();
            var autoTrigger = autoOpen != null ? autoOpen.GetComponent<Collider>() : null;
            Require(autoOpen != null && autoOpen.doors != null && autoOpen.doors.Contains(demoDoor),
                "mill hierarchy no longer exposes the imported auto-opener owning this leaf");
            Require(autoTrigger != null && autoTrigger.isTrigger,
                "imported mill auto-opener has no trigger to suppress");

            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<CharacterController>() : null;
            var interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
            Require(controller != null && interactor != null,
                "player CharacterController/PlayerInteractor is missing");

            string[] originalCompleted = QuestManager.CompletedQuestIds.ToArray();
            string[] originalCards = QuestManager.UnlockedStoryCardIds.ToArray();
            QuestData originalActive = QuestManager.ActiveQuest;
            string[] originalKeys = KeyItems.ToArray();
            var originalScores = new SaveSlotMeta();
            GameScores.WriteTo(originalScores);
            bool originalOpened = GetOpened(millLock);
            Vector3 originalPosition = door.transform.localPosition;
            Quaternion originalRotation = door.transform.localRotation;
            Vector3 originalScale = door.transform.localScale;
            var colliders = door.GetComponentsInChildren<Collider>(true);
            bool[] colliderStates = colliders.Select(collider => collider.enabled).ToArray();
            bool originalAutoOpen = autoOpen.enabled;
            bool originalAutoTrigger = autoTrigger.enabled;
            WrapMode originalAnimationWrap = animation.wrapMode;
            AnimationState openState = animation[demoDoor.open.name];
            Require(openState != null, "open clip has no runtime AnimationState");
            WrapMode originalStateWrap = openState.wrapMode;

            try
            {
                VerifyQuestAndKeyRoute(completedByDoor);
                demoDoor.open.SampleAnimation(door, 0f);
                doorCollider.enabled = true;
                VerifyTwoSidedInteraction(lockTrigger, doorCollider, door.transform, interactor);
                VerifyPassage(doorCollider, door.transform, controller);
                VerifyRuntimeAuthority(millLock, autoOpen, autoTrigger);
                VerifyKeyGate(millLock);
                VerifyCompletedResume(millLock, completedByDoor, door, doorCollider, demoDoor.open);
                VerifyOneShotAnimation(millLock, animation, openState, doorCollider);

                return "MILL DOOR — PASS: Bram grants the persistent key into searchMill, the lock owns the vendor leaf/trigger, both sides can interact, the player capsule clears the opened threshold, the open clip clamps once, and completed saves restore an open collider-free exit";
            }
            finally
            {
                animation.Stop();
                animation.wrapMode = originalAnimationWrap;
                openState.wrapMode = originalStateWrap;
                door.transform.SetLocalPositionAndRotation(originalPosition, originalRotation);
                door.transform.localScale = originalScale;
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
                autoOpen.enabled = originalAutoOpen;
                autoTrigger.enabled = originalAutoTrigger;
                SetOpened(millLock, originalOpened);
                KeyItems.HydrateFrom(originalKeys);
                GameScores.HydrateFrom(originalScores);
                QuestManager.ResetForSlotSwitch();
                QuestManager.HydrateFrom(originalCompleted, originalCards);
                if (originalActive != null) QuestManager.StartQuest(originalActive);
            }
        }

        private static void VerifyQuestAndKeyRoute(QuestData completedByDoor)
        {
            var speakBram = Load<QuestData>(SpeakBramPath);
            var searchMill = Load<QuestData>(SearchMillPath);
            var findJournal = Load<QuestData>(FindJournalPath);
            var bram = Load<NPCData>(BramPath);
            var intro = Load<DialogueData>(BramIntroPath);
            var keyHandoff = Load<DialogueData>(BramKeyPath);

            Require(speakBram.NextQuest == searchMill && completedByDoor == searchMill &&
                    searchMill.NextQuest == findJournal,
                "Act-I quest chain is not speakBram -> searchMill -> findJournal");
            Require(intro.NextDialog == keyHandoff && keyHandoff.GiveItemId == MillKeyId &&
                    keyHandoff.CompleteQuest == speakBram,
                "Bram dialogue does not atomically grant the mill key and finish speakBram");

            GameScores.HydrateFrom(null);
            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(Array.Empty<string>(), Array.Empty<string>());
            QuestManager.StartQuest(speakBram);
            Require(bram.PickActiveQuestDialog() == intro,
                "Bram does not route the active speakBram quest into the key-handoff chain");
        }

        private static void VerifyRuntimeAuthority(KeyLockedDoor millLock,
            InfinityPBR.DemoDoorAutoOpen autoOpen, Collider autoTrigger)
        {
            autoOpen.enabled = true;
            autoTrigger.enabled = true;
            Invoke(millLock, "SuppressVendorAutoOpen");
            Require(!autoOpen.enabled && !autoTrigger.enabled,
                "KeyLockedDoor did not suppress its owning vendor auto-opener and trigger");
        }

        private static void VerifyKeyGate(KeyLockedDoor millLock)
        {
            SetOpened(millLock, false);
            KeyItems.HydrateFrom(null);
            Require(!millLock.CanInteract(null), "mill door is usable without the mill key");
            KeyItems.HydrateFrom(new[] { MillKeyId });
            Require(millLock.CanInteract(null), "persisted mill key does not unlock interaction");
            Require(KeyItems.ToArray().Contains(MillKeyId), "mill key failed hydration round-trip");
        }

        private static void VerifyCompletedResume(KeyLockedDoor millLock, QuestData searchMill,
            GameObject door, Collider doorCollider, AnimationClip openClip)
        {
            openClip.SampleAnimation(door, openClip.length);
            Quaternion expectedOpen = door.transform.localRotation;
            openClip.SampleAnimation(door, 0f);
            doorCollider.enabled = true;
            SetOpened(millLock, false);

            QuestManager.ResetForSlotSwitch();
            QuestManager.HydrateFrom(new[] { searchMill.Id }, Array.Empty<string>());
            Invoke(millLock, "Start");

            Require(GetOpened(millLock), "completed searchMill did not restore the door's opened state");
            Require(!doorCollider.enabled, "completed searchMill restored a blocking door collider");
            Require(Quaternion.Angle(door.transform.localRotation, expectedOpen) < 0.1f,
                "completed searchMill did not restore the final open pose");
            Require(!millLock.CanInteract(null), "restored one-shot lock became interactable again");
        }

        private static void VerifyOneShotAnimation(KeyLockedDoor millLock, Animation animation,
            AnimationState openState, Collider doorCollider)
        {
            animation.Stop();
            doorCollider.enabled = true;
            Invoke(millLock, "OpenDoorVisual", false, false);
            Require(animation.wrapMode == WrapMode.ClampForever &&
                    openState.wrapMode == WrapMode.ClampForever,
                "vendor looping clip is not clamped at runtime");
            Require(!doorCollider.enabled, "opening the leaf left its blocking collider enabled");
            animation.Stop();
        }

        private static void VerifyTwoSidedInteraction(SphereCollider trigger, Collider doorCollider,
            Transform door, PlayerInteractor interactor)
        {
            var serialized = new SerializedObject(interactor);
            float searchRadius = serialized.FindProperty("_searchRadius").floatValue;
            Vector3 searchOffset = serialized.FindProperty("_searchOffset").vector3Value;
            Vector3 forward = HorizontalForward(door);
            Vector3 threshold = new Vector3(doorCollider.bounds.center.x, doorCollider.bounds.min.y,
                doorCollider.bounds.center.z);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 origin = threshold + forward * (1.25f * side) + Vector3.up * searchOffset.y;
                Vector3 closest = trigger.ClosestPoint(origin);
                Require((closest - origin).sqrMagnitude <= searchRadius * searchRadius,
                    side < 0 ? "outside approach cannot reach the mill lock trigger" :
                               "inside approach cannot reach the mill lock trigger");
            }
        }

        private static void VerifyPassage(Collider doorCollider, Transform door,
            CharacterController controller)
        {
            Bounds bounds = doorCollider.bounds;
            Vector3 forward = HorizontalForward(door);
            Vector3 threshold = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float radius = controller.radius * Mathf.Max(controller.transform.lossyScale.x,
                controller.transform.lossyScale.z);
            float height = controller.height * controller.transform.lossyScale.y;
            float clearance = Mathf.Max(0.06f, controller.skinWidth * 2f);

            for (int i = -4; i <= 4; i++)
            {
                Vector3 foot = threshold + forward * (i * 0.1f);
                Vector3 bottom = foot + Vector3.up * (radius + clearance);
                Vector3 top = foot + Vector3.up * (height - radius + clearance);
                var blockers = Physics.OverlapCapsule(bottom, top, radius, ~0,
                        QueryTriggerInteraction.Ignore)
                    .Where(collider => collider != doorCollider && collider.enabled && !collider.isTrigger)
                    .ToArray();
                Require(blockers.Length == 0,
                    "opened threshold blocks the player capsule at " + (i * 0.1f).ToString("F1") +
                    "m: " + string.Join(", ", blockers.Select(Path)));
            }
        }

        private static Vector3 HorizontalForward(Transform transform)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "missing asset " + path);
            return asset;
        }

        private static bool GetOpened(KeyLockedDoor millLock) =>
            (bool)Field("_opened").GetValue(millLock);

        private static void SetOpened(KeyLockedDoor millLock, bool value) =>
            Field("_opened").SetValue(millLock, value);

        private static FieldInfo Field(string name)
        {
            var field = typeof(KeyLockedDoor).GetField(name, PrivateInstance);
            Require(field != null, "KeyLockedDoor field missing: " + name);
            return field;
        }

        private static void Invoke(KeyLockedDoor target, string method, params object[] args)
        {
            var info = typeof(KeyLockedDoor).GetMethod(method, PrivateInstance);
            Require(info != null, "KeyLockedDoor method missing: " + method);
            info.Invoke(target, args);
        }

        private static string Path(Collider collider)
        {
            return AnimationUtility.CalculateTransformPath(collider.transform, null) +
                   " [" + collider.GetType().Name + "]";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[MillDoorProgressionVerifier] " + message);
        }
    }
}
#endif
