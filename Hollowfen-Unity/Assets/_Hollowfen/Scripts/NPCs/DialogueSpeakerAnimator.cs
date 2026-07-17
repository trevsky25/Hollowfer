using System;
using Hollowfen.Dialogue;
using UnityEngine;

namespace Hollowfen.NPCs
{
    /// <summary>
    /// Drives an NPC's authored talking state from the speaker currently presented by DialogueScreen.
    /// Attach one per animated speaker; unrelated speakers automatically return this character to idle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueSpeakerAnimator : MonoBehaviour
    {
        [SerializeField] private string _speakerName;
        [SerializeField] private Animator _animator;
        [SerializeField] private string _talkingParameter = "Talking";

        private int _talkingHash;
        private bool _hasTalkingParameter;

        public string SpeakerName => _speakerName;
        public Animator CharacterAnimator => _animator;
        public bool IsTalking => _hasTalkingParameter && _animator != null && _animator.GetBool(_talkingHash);

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
            CacheParameter();
        }

        private void OnEnable()
        {
            DialogueScreen.SpeakerChanged += HandleSpeakerChanged;
        }

        private void OnDisable()
        {
            DialogueScreen.SpeakerChanged -= HandleSpeakerChanged;
            SetTalking(false);
        }

        private void OnValidate()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>(true);
            CacheParameter();
        }

        private void HandleSpeakerChanged(string speaker)
        {
            SetTalking(string.Equals(speaker, _speakerName, StringComparison.OrdinalIgnoreCase));
        }

        private void CacheParameter()
        {
            _talkingHash = Animator.StringToHash(_talkingParameter ?? string.Empty);
            _hasTalkingParameter = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;

            foreach (var parameter in _animator.parameters)
            {
                if (parameter.nameHash != _talkingHash || parameter.type != AnimatorControllerParameterType.Bool)
                    continue;
                _hasTalkingParameter = true;
                break;
            }
        }

        private void SetTalking(bool talking)
        {
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
                return;
            if (!_hasTalkingParameter) CacheParameter();
            if (!_hasTalkingParameter) return;
            _animator.SetBool(_talkingHash, talking);
        }
    }
}
