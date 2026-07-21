using System;
using System.Collections;
using System.Collections.Generic;
using Hollowfen.NPCs;
using Unity.Cinemachine;
using UnityEngine;

namespace Hollowfen.Dialogue
{
    // Procedural dialogue camera director (batch-45, Cinematic Pass #3). While a conversation
    // plays, this owns Camera.main and shoots it like a scene: an establishing two-shot, then
    // over-the-shoulder frames that GLIDE between speakers (a pan/dolly move, not a cut), a slow
    // push-in across every line, tighter singles on `isCloseup` beats, a pull-back two-shot while
    // choices are up, and a handheld breath over everything. All framing is computed from the two
    // speakers' head positions — no per-dialogue authoring.
    //
    // Film grammar kept: one side of the action axis is chosen at Begin (180° rule), so
    // reverse shots never flip eyelines.
    //
    // Dialogue freezes timeScale, so the director runs on unscaled time and flips both speakers'
    // Animators to UnscaledTime for the scene (restored at End) — Bram keeps breathing in closeups.
    // The CinemachineBrain is disabled while the director owns the camera; End() glides back to the
    // cached gameplay pose and re-enables it, so the handoff is seamless in both directions.
    public class DialogueCinematics : MonoBehaviour
    {
        public static DialogueCinematics Instance { get; private set; }

        // True while the director owns Camera.main (incl. the End() restore glide). Other camera
        // takeovers (PropFocusCinematic) wait on this so they don't fight the dialogue camera.
        public bool IsActive => _active;

        private const float GlideSeconds = 1.1f;    // deliberate shot-change camera move (mode change only)
        private const float FavorGlideSeconds = 0.7f; // gentle favor pan within the two-shot
        private const float RestoreSeconds = 0.55f; // End() glide back to gameplay
        private const float LongLineSeconds = 4.2f; // a line this long earns a committed single
        private const float PushInFraction = 0.028f; // per-line dolly toward the speaker (damped)
        private const float SwayPos = 0.008f;       // handheld breath amplitude (m, damped)
        private const float SwayDeg = 0.15f;        // handheld breath amplitude (deg, damped)
        private const float ParticipantRadius = 18f; // nearby staged NPCs eligible for authored lines

        // Coverage mode — hold a wide/favor frame through rapid exchanges, commit to a single only
        // for lines that earn it. Re-gliding on every speaker change is what made batch-45 whip.
        private const int ModeTwo = 0;
        private const int ModeSingle = 1;
        private int _shotMode;

        private Camera _cam;
        private CinemachineBrain _brain;
        private bool _active;

        private Transform _player;
        private Transform _primaryNpc;
        private Transform _npc;
        private Vector3 _headA;   // player head (Wren)
        private Vector3 _headB;   // npc head
        private Vector3 _side;    // 180°-rule side vector, fixed at Begin

        private struct Shot { public Vector3 Pos; public Quaternion Rot; public float Fov; }
        private Shot _from, _to;
        private Shot _restore;
        private float _glideT;      // seconds into the current glide
        private float _glideLen;
        private float _pushT;       // seconds into the current line's push
        private float _pushLen;
        private Vector3 _pushDir;
        private float _swaySeed;
        private bool _restoring;
        private bool _establishing;
        private bool _propHandoffActive;
        private string _lastSpeaker;
        private int _samePeakerRuns;

        private readonly List<Animator> _retimed = new List<Animator>();
        private readonly List<AnimatorUpdateMode> _retimedPrev = new List<AnimatorUpdateMode>();
        private readonly Dictionary<string, Transform> _speakerAnchors =
            new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private Coroutine _propHandoffCo;
        private GameObject _handoffProp;
        private Action _handoffDone;

        // Exposed for focused Play Mode verification of multi-character conversation staging.
        public Transform CurrentNpcAnchor => _npc;
        public bool IsPropHandoffActive => _propHandoffActive;
        public GameObject CurrentHandoffProp => _handoffProp;

        public static DialogueCinematics Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("_DialogueCinematics");
                Instance = go.AddComponent<DialogueCinematics>();
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
            CancelPropHandoff(false);
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ lifecycle

        public void Begin(Transform npcAnchor)
        {
            _cam = Camera.main;
            if (_cam == null) return;

            var playerGo = GameObject.FindGameObjectWithTag("Player");
            _player = playerGo != null ? playerGo.transform : null;
            _npc = npcAnchor;
            if (_player == null && _npc == null) return;
            if (_player == null) _player = _npc;
            if (_npc == null) _npc = _player;
            _primaryNpc = _npc;
            IndexNearbyParticipants();

            _headA = HeadOf(_player);
            _headB = HeadOf(_npc);

            // One side of the action axis for the whole scene (180° rule). Prefer the side the
            // camera is already on, so the establishing glide is short and never crosses the line.
            Vector3 axis = _headB - _headA; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = _npc.forward;
            axis.Normalize();
            _side = Vector3.Cross(Vector3.up, axis);
            Vector3 mid = (_headA + _headB) * 0.5f;
            if (Vector3.Dot(_cam.transform.position - mid, _side) < 0f) _side = -_side;

            _restore = new Shot { Pos = _cam.transform.position, Rot = _cam.transform.rotation, Fov = _cam.fieldOfView };
            _brain = _cam.GetComponent<CinemachineBrain>();
            if (_brain != null) _brain.enabled = false;

            RetimeAnimators();

            _active = true;
            _restoring = false;
            _lastSpeaker = null;
            _samePeakerRuns = 0;
            _shotMode = ModeTwo;
            _swaySeed = (_headA.x + _headB.z) * 10f; // stable per-scene, no RNG

            // CUT to the establishing two-shot — scene grammar: a conversation opens on a cut,
            // then the first line GLIDES from this frame into its speaker (see OnLine).
            var two = TwoShot();
            _from = two; _to = two;
            _glideT = 0f; _glideLen = 0.01f;
            _cam.transform.SetPositionAndRotation(two.Pos, two.Rot);
            _cam.fieldOfView = two.Fov;
            _establishing = true;
            StartPush(mid + Vector3.up * 0.1f, 5f);
        }

        // Called per line by DialogueScreen. Wren frames the player; a named nearby participant
        // frames that live NPC, falling back to the interaction anchor when no staged actor matches.
        // estSeconds = VO length or a text-length estimate.
        public void OnLine(string speaker, bool closeup, float estSeconds)
        {
            if (!_active) return;
            bool wren = string.Equals(speaker, "Wren", StringComparison.OrdinalIgnoreCase);
            if (!wren) SelectSpeakerAnchor(speaker);
            RefreshHeads();
            Vector3 subjHead = wren ? _headA : _headB;

            bool sameSpeaker = speaker == _lastSpeaker;
            _samePeakerRuns = sameSpeaker ? _samePeakerRuns + 1 : 0;
            _lastSpeaker = speaker;

            // Short/rapid lines hold a loose two-shot; only long lines or closeup beats earn a single.
            bool wantSingle = closeup || estSeconds >= LongLineSeconds;

            if (!wantSingle)
            {
                // Favor two-shot: both stay in frame, the framing leans toward the talker. If we're
                // already wide, this is a gentle favor pan (or a hold on the same speaker) — never a
                // full over-the-shoulder whip. That's the fix for the back-and-forth seasickness.
                Shot favor = FavorShot(wren);
                _from = CurrentShot();
                _to = favor;
                _glideT = 0f;
                if (_establishing) _glideLen = GlideSeconds * 1.6f;      // first line eases in, readable
                else if (_shotMode == ModeTwo) _glideLen = sameSpeaker ? 0.001f : FavorGlideSeconds;
                else _glideLen = GlideSeconds;                            // pulling back from a single
                _shotMode = ModeTwo;
            }
            else
            {
                Shot target = closeup ? Closeup(wren) : OverShoulder(wren);
                if (sameSpeaker && _shotMode == ModeSingle && !closeup)
                {
                    // Same voice, still a single — creep closer instead of re-gliding.
                    Vector3 toSubj = (subjHead - target.Pos).normalized;
                    target.Pos += toSubj * Mathf.Min(0.10f * _samePeakerRuns, 0.28f);
                    target.Fov = Mathf.Max(target.Fov - 1.5f * _samePeakerRuns, 28f);
                }
                _from = CurrentShot();
                _to = target;
                _glideT = 0f;
                _glideLen = _establishing ? GlideSeconds * 1.6f
                          : (sameSpeaker && _shotMode == ModeSingle) ? GlideSeconds * 0.7f
                          : GlideSeconds;
                _shotMode = ModeSingle;
            }
            _establishing = false;
            StartPush(subjHead, Mathf.Max(2.5f, estSeconds));
        }

        // Choices up: settle back to the two-shot so both parties share the frame while the player decides.
        public void OnChoices()
        {
            if (!_active) return;
            RefreshHeads();
            _from = CurrentShot();
            _to = TwoShot();
            _glideT = 0f; _glideLen = GlideSeconds;
            _shotMode = ModeTwo;
            StartPush((_headA + _headB) * 0.5f, 30f); // barely-perceptible drift while idle in choices
        }

        // Data-authored live transfer used by Marra's first-basket scene. It stays inside the
        // dialogue camera owner (rather than starting a second camera system), tracks the live
        // humanoid hands when available, and uses the canonical journal-preview prefab as a safe
        // presentation model. The callback resumes the line that follows the visual beat.
        public bool PlayMushroomHandoff(GameObject prefab, string recipientSpeaker,
            float presentationHeight, Action onDone)
        {
            if (!_active || _restoring || _cam == null || _player == null || prefab == null ||
                _propHandoffActive)
                return false;

            if (!string.IsNullOrWhiteSpace(recipientSpeaker) &&
                _speakerAnchors.TryGetValue(recipientSpeaker, out Transform recipient) && recipient != null)
                _npc = recipient;
            if (_npc == null || _npc == _player) return false;

            CollectAnimators(_npc);
            RefreshHeads();
            _propHandoffActive = true;
            _handoffDone = onDone;
            _propHandoffCo = StartCoroutine(MushroomHandoffRoutine(prefab,
                Mathf.Clamp(presentationHeight, 0.12f, 0.50f)));
            return true;
        }

        private IEnumerator MushroomHandoffRoutine(GameObject prefab, float presentationHeight)
        {
            _handoffProp = Instantiate(prefab);
            _handoffProp.name = "_Dialogue_MushroomHandoff";
            MakePresentationSafe(_handoffProp);
            NormalizeHeight(_handoffProp.transform, presentationHeight);
            AddPropFillLight(_handoffProp.transform);

            Vector3 start = HandoffPoint(_player, _npc.position, true);
            Vector3 end = HandoffPoint(_npc, _player.position, false);
            _handoffProp.transform.position = start;
            Quaternion startRot = HandoffRotation(start, -10f);
            Quaternion endRot = HandoffRotation(end, 9f);
            _handoffProp.transform.rotation = startRot;

            Vector3 mid = (start + end) * 0.5f + Vector3.up * 0.08f;
            // A committed chest-and-hands insert, not another full-body dialogue two-shot. The
            // model crosses frame centre while both giver and receiver hands remain at the edges.
            Vector3 cameraPos = mid + _side * 1.90f + Vector3.up * 0.18f;
            _from = CurrentShot();
            _to = Frame(cameraPos, mid + Vector3.up * 0.02f, 28f, mid);
            _glideT = 0f;
            _glideLen = 0.58f;
            _pushLen = 0f;
            _shotMode = ModeTwo;

            // First let the camera find Wren's hand and the species silhouette.
            float elapsed = 0f;
            const float settleSeconds = 0.72f;
            while (elapsed < settleSeconds && _propHandoffActive)
            {
                elapsed += Time.unscaledDeltaTime;
                start = HandoffPoint(_player, _npc.position, true);
                _handoffProp.transform.SetPositionAndRotation(start, HandoffRotation(start, -10f));
                yield return null;
            }

            // Carry the real model along a visible hand-to-hand arc. The slow middle makes the cap,
            // ridges, and gold stem readable without turning the transfer into an item-get hover.
            elapsed = 0f;
            const float transferSeconds = 1.22f;
            while (elapsed < transferSeconds && _propHandoffActive)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / transferSeconds);
                float smooth = u * u * (3f - 2f * u);
                start = HandoffPoint(_player, _npc.position, true);
                end = HandoffPoint(_npc, _player.position, false);
                Vector3 pos = Vector3.Lerp(start, end, smooth) +
                              Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.16f);
                startRot = HandoffRotation(start, -10f);
                endRot = HandoffRotation(end, 9f);
                _handoffProp.transform.SetPositionAndRotation(pos,
                    Quaternion.Slerp(startRot, endRot, smooth));
                yield return null;
            }

            // Marra owns the mushroom for the final hold; her identification line follows after
            // this callback, turning the spoken "Goldfoots" into the payoff rather than a subtitle.
            elapsed = 0f;
            const float receiveHoldSeconds = 0.62f;
            while (elapsed < receiveHoldSeconds && _propHandoffActive)
            {
                elapsed += Time.unscaledDeltaTime;
                end = HandoffPoint(_npc, _player.position, false);
                float inspect = Mathf.Sin(elapsed * 3.8f) * 4f;
                _handoffProp.transform.SetPositionAndRotation(end, HandoffRotation(end, 9f + inspect));
                yield return null;
            }

            CompletePropHandoff();
        }

        private Quaternion HandoffRotation(Vector3 position, float roll)
        {
            Vector3 toCamera = _cam != null ? _cam.transform.position - position : _side;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.001f) toCamera = _side;
            return Quaternion.LookRotation(toCamera.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }

        private static Vector3 HandoffPoint(Transform owner, Vector3 otherPosition, bool giver)
        {
            if (owner == null) return otherPosition + Vector3.up * 1.15f;

            Vector3 fallback = HeadOf(owner) - Vector3.up * (giver ? 0.64f : 0.72f);
            Vector3 towardOther = otherPosition - owner.position;
            towardOther.y = 0f;
            if (towardOther.sqrMagnitude > 0.001f)
                fallback += towardOther.normalized * (giver ? 0.24f : 0.19f);

            Transform left = null;
            Transform right = null;
            foreach (Animator animator in owner.GetComponentsInChildren<Animator>())
            {
                if (!animator.isHuman || animator.avatar == null || !animator.avatar.isValid) continue;
                left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (left != null || right != null) break;
            }

            if (left == null && right == null) return fallback;
            if (left == null) return right.position;
            if (right == null) return left.position;
            return (left.position - otherPosition).sqrMagnitude <= (right.position - otherPosition).sqrMagnitude
                ? left.position
                : right.position;
        }

        private static void NormalizeHeight(Transform root, float desiredHeight)
        {
            if (root == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y <= 0.0001f) return;
            root.localScale *= desiredHeight / bounds.size.y;
        }

        private static void MakePresentationSafe(GameObject prop)
        {
            foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in prop.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }
            foreach (Hollowfen.Foraging.MushroomNode node in
                     prop.GetComponentsInChildren<Hollowfen.Foraging.MushroomNode>(true))
                node.enabled = false;
        }

        private static void AddPropFillLight(Transform prop)
        {
            if (prop == null) return;
            var lightObject = new GameObject("CinematicFill");
            lightObject.transform.SetParent(prop, false);
            lightObject.transform.localPosition = new Vector3(-0.18f, 0.22f, 0.20f);
            var fill = lightObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.72f, 0.42f);
            fill.intensity = 0.85f;
            fill.range = 1.25f;
            fill.shadows = LightShadows.None;
        }

        private void CompletePropHandoff()
        {
            if (_handoffProp != null) Destroy(_handoffProp);
            _handoffProp = null;
            _propHandoffCo = null;
            _propHandoffActive = false;
            Action done = _handoffDone;
            _handoffDone = null;
            done?.Invoke();
        }

        private void CancelPropHandoff(bool invokeCallback)
        {
            if (_propHandoffCo != null) StopCoroutine(_propHandoffCo);
            _propHandoffCo = null;
            if (_handoffProp != null) Destroy(_handoffProp);
            _handoffProp = null;
            _propHandoffActive = false;
            Action done = _handoffDone;
            _handoffDone = null;
            if (invokeCallback) done?.Invoke();
        }

        public void End()
        {
            if (!_active) return;
            CancelPropHandoff(false);
            _from = CurrentShot();
            _to = _restore;
            _glideT = 0f; _glideLen = RestoreSeconds;
            _pushLen = 0f;
            _restoring = true;
        }

        private void FinishRestore()
        {
            _active = false;
            _restoring = false;
            if (_cam != null)
            {
                _cam.transform.SetPositionAndRotation(_restore.Pos, _restore.Rot);
                _cam.fieldOfView = _restore.Fov;
            }
            if (_brain != null) _brain.enabled = true;
            RestoreAnimators();
            _speakerAnchors.Clear();
            _primaryNpc = null;
        }

        // ------------------------------------------------------------------ shots

        private Shot CurrentShot()
        {
            return new Shot { Pos = _cam.transform.position, Rot = _cam.transform.rotation, Fov = _cam.fieldOfView };
        }

        // Wide lateral frame of both characters; camera on the scene's chosen side.
        private Shot TwoShot()
        {
            Vector3 mid = (_headA + _headB) * 0.5f;
            float gap = Mathf.Max(1.2f, Vector3.Distance(_headA, _headB));
            Vector3 pos = mid + _side * (gap * 1.35f + 1.15f) + Vector3.up * 0.12f;
            Vector3 look = mid - Vector3.up * 0.18f;
            return Frame(pos, look, 40f, mid);
        }

        // Loose two-shot that FAVORS the speaker: same camera side as the two-shot but the look target
        // pans toward the talker and the lens tightens a touch. Both characters stay in frame, so a
        // rapid exchange reads as a held frame gently leaning back and forth — not a whipping single.
        private Shot FavorShot(bool wrenSpeaks)
        {
            Vector3 speaker = wrenSpeaks ? _headA : _headB;
            Vector3 mid = (_headA + _headB) * 0.5f;
            float gap = Mathf.Max(1.2f, Vector3.Distance(_headA, _headB));
            Vector3 pos = mid + _side * (gap * 1.25f + 1.05f) + Vector3.up * 0.11f;
            Vector3 look = Vector3.Lerp(mid, speaker, 0.34f) - Vector3.up * 0.16f;
            return Frame(pos, look, 37f, mid);
        }

        // Classic over-the-shoulder: camera just behind and beside the LISTENER, framing the speaker.
        // wrenSpeaks=true → camera over the NPC's shoulder looking at Wren. Offsets scale with the
        // listener's shoulder radius so a broad frame (Bram) doesn't swallow half the screen.
        private Shot OverShoulder(bool wrenSpeaks)
        {
            Vector3 speaker = wrenSpeaks ? _headA : _headB;
            Vector3 listener = wrenSpeaks ? _headB : _headA;
            Transform listenerT = wrenSpeaks ? _npc : _player;
            Vector3 axis = speaker - listener; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.forward;
            axis.Normalize();

            float r = ShoulderRadius(listenerT);
            Vector3 pos = listener - axis * (0.28f + r * 0.30f) + _side * (0.34f + r * 0.62f);
            pos.y = listener.y + 0.04f;
            Vector3 look = speaker - Vector3.up * 0.22f; // aim low → face rides the upper third
            return Frame(pos, look, 34f, speaker);
        }

        // Half-width of the character in the ground plane — how much frame their shoulder will eat.
        private static float ShoulderRadius(Transform t)
        {
            var smr = t != null ? t.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (smr == null) return 0.45f;
            var e = smr.bounds.extents;
            return Mathf.Clamp((e.x + e.z) * 0.5f, 0.40f, 0.95f);
        }

        // Tight single on the speaker for isCloseup beats — nearer, narrower, slightly off-axis.
        // Distance scales with the character's bulk so a broad frame (Bram) still keeps his
        // whole face inside the letterboxed safe area.
        private Shot Closeup(bool wrenSpeaks)
        {
            Vector3 speaker = wrenSpeaks ? _headA : _headB;
            Vector3 listener = wrenSpeaks ? _headB : _headA;
            Transform speakerT = wrenSpeaks ? _player : _npc;
            Vector3 axis = speaker - listener; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = Vector3.forward;
            axis.Normalize();

            float r = ShoulderRadius(speakerT);
            Vector3 pos = speaker - axis * (1.30f + r * 0.60f) + _side * 0.32f;
            pos.y = speaker.y;
            Vector3 look = speaker - Vector3.up * 0.10f;
            return Frame(pos, look, 29f, speaker);
        }

        // Build a shot, pulling the camera in if world geometry blocks the subject.
        private Shot Frame(Vector3 pos, Vector3 look, float fov, Vector3 subject)
        {
            Vector3 fromSubject = pos - subject;
            float dist = fromSubject.magnitude;
            if (dist > 0.05f && Physics.Raycast(subject, fromSubject / dist, out var hit, dist,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                // Ignore hits on the speakers themselves (their own colliders sit on the ray).
                bool onCast = _player != null && hit.transform.IsChildOf(_player.root);
                bool onNpc = _npc != null && hit.transform.IsChildOf(_npc.root);
                if (!onCast && !onNpc)
                    pos = subject + fromSubject / dist * (hit.distance * 0.88f);
            }
            return new Shot { Pos = pos, Rot = Quaternion.LookRotation(look - pos, Vector3.up), Fov = fov };
        }

        private void StartPush(Vector3 towards, float seconds)
        {
            _pushT = 0f;
            _pushLen = seconds;
            _pushDir = (towards - _to.Pos).normalized * (Vector3.Distance(towards, _to.Pos) * PushInFraction);
        }

        // ------------------------------------------------------------------ drive

        private void LateUpdate()
        {
            if (!_active || _cam == null) return;
            float dt = Time.unscaledDeltaTime;

            // Glide between shots (smoothstep — an eased camera MOVE, reads as a pan/dolly).
            float u = _glideLen > 0.001f ? Mathf.Clamp01((_glideT += dt) / _glideLen) : 1f;
            float e = u * u * (3f - 2f * u);
            Vector3 pos = Vector3.Lerp(_from.Pos, _to.Pos, e);
            Quaternion rot = Quaternion.Slerp(_from.Rot, _to.Rot, e);
            float fov = Mathf.Lerp(_from.Fov, _to.Fov, e);

            if (_restoring)
            {
                _cam.transform.SetPositionAndRotation(pos, rot);
                _cam.fieldOfView = fov;
                if (u >= 1f) FinishRestore();
                return;
            }

            // Slow push-in over the line, eased so it never reads mechanical.
            if (_pushLen > 0.001f)
            {
                float p = Mathf.Clamp01((_pushT += dt) / _pushLen);
                pos += _pushDir * (p * p * (3f - 2f * p));
            }

            // Handheld breath — low-frequency Perlin sway on position + rotation.
            float t = Time.unscaledTime * 0.32f;
            Vector3 sway = new Vector3(
                (Mathf.PerlinNoise(_swaySeed, t) - 0.5f),
                (Mathf.PerlinNoise(_swaySeed + 11.3f, t) - 0.5f),
                0f) * (2f * SwayPos);
            float yaw = (Mathf.PerlinNoise(_swaySeed + 23.7f, t) - 0.5f) * 2f * SwayDeg;
            float pitch = (Mathf.PerlinNoise(_swaySeed + 37.1f, t) - 0.5f) * 2f * SwayDeg;

            _cam.transform.SetPositionAndRotation(
                pos + rot * sway,
                rot * Quaternion.Euler(pitch, yaw, 0f));
            _cam.fieldOfView = fov;
        }

        // ------------------------------------------------------------------ helpers

        private void RefreshHeads()
        {
            if (_player != null) _headA = HeadOf(_player);
            if (_npc != null) _headB = HeadOf(_npc);
        }

        private void IndexNearbyParticipants()
        {
            _speakerAnchors.Clear();
            RegisterParticipant("Wren", _player);
            RegisterNpc(_primaryNpc != null ? _primaryNpc.GetComponentInParent<NPCInteractable>() : null);

            if (_primaryNpc == null) return;
            float radiusSqr = ParticipantRadius * ParticipantRadius;
            foreach (var candidate in UnityEngine.Object.FindObjectsByType<NPCInteractable>(
                         FindObjectsInactive.Exclude))
            {
                if (candidate == null || candidate.Data == null) continue;
                if ((candidate.transform.position - _primaryNpc.position).sqrMagnitude > radiusSqr) continue;
                RegisterNpc(candidate);
            }

            // A speaker animator can identify a staged actor even when it is not interactable.
            foreach (var driver in UnityEngine.Object.FindObjectsByType<DialogueSpeakerAnimator>(
                         FindObjectsInactive.Exclude))
            {
                if (driver == null || string.IsNullOrWhiteSpace(driver.SpeakerName)) continue;
                if ((driver.transform.position - _primaryNpc.position).sqrMagnitude > radiusSqr) continue;
                RegisterParticipant(driver.SpeakerName, driver.transform);
            }
        }

        private void RegisterNpc(NPCInteractable npc)
        {
            if (npc == null || npc.Data == null) return;
            RegisterParticipant(Hollowfen.Localization.Get(npc.Data.DisplayNameId), npc.transform);
        }

        private void RegisterParticipant(string speaker, Transform anchor)
        {
            if (anchor == null || string.IsNullOrWhiteSpace(speaker)) return;
            _speakerAnchors[speaker] = anchor;
        }

        private void SelectSpeakerAnchor(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker) ||
                !_speakerAnchors.TryGetValue(speaker, out Transform anchor) || anchor == null)
                return;
            if (_npc == anchor) return;
            _npc = anchor;
            CollectAnimators(_npc);
        }

        private static Vector3 HeadOf(Transform t)
        {
            var smr = t.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                var b = smr.bounds;
                return new Vector3(b.center.x, b.max.y - b.size.y * 0.12f, b.center.z);
            }
            return t.position + Vector3.up * 1.6f;
        }

        // Both speakers keep animating through the dialogue's timeScale=0 freeze.
        private void RetimeAnimators()
        {
            _retimed.Clear();
            _retimedPrev.Clear();
            foreach (Transform participant in _speakerAnchors.Values)
                CollectAnimators(participant);
        }

        private void CollectAnimators(Transform root)
        {
            if (root == null) return;
            foreach (var a in root.GetComponentsInChildren<Animator>())
            {
                if (!a.enabled || _retimed.Contains(a)) continue;
                _retimed.Add(a);
                _retimedPrev.Add(a.updateMode);
                a.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        private void RestoreAnimators()
        {
            for (int i = 0; i < _retimed.Count; i++)
                if (_retimed[i] != null) _retimed[i].updateMode = _retimedPrev[i];
            _retimed.Clear();
            _retimedPrev.Clear();
        }
    }
}
