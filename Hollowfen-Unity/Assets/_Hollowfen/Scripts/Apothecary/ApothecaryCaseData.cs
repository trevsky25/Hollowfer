using System;
using Hollowfen.Data;
using Hollowfen.Dialogue;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    public enum ApothecaryCaseGrade
    {
        Mistaken = 0,
        Supportive = 1,
        Careful = 2,
    }

    [Serializable]
    public struct ApothecaryCaseClue
    {
        public string id;
    }

    [Serializable]
    public struct ApothecaryCaseInterview
    {
        public string id;
    }

    [Serializable]
    public struct ApothecaryCaseDecision
    {
        public string id;
        public PreparationRecipeData preparation;
        public ApothecaryCaseGrade grade;
        [Min(1)] public int followUpDays;
        [Min(0)] public int villageHope;
        [Min(0)] public int knowledge;
        public int relationshipDelta;
        public int mentorBondDelta;
        public string memoryId;
    }

    /// <summary>
    /// One fictional village-care case. Text is resolved from
    /// apothecary.case.&lt;case id&gt;.* so the asset remains a compact gameplay contract.
    /// </summary>
    [CreateAssetMenu(fileName = "ApothecaryCase_New",
        menuName = "Hollowfen/Apothecary/Patient Case")]
    public sealed class ApothecaryCaseData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _patientNpcId;
        [SerializeField] private CharacterProfileData _patientProfile;
        [SerializeField] private string _mentorNpcId = "edda";
        [SerializeField] private string _requiredFlagId = "apothecary_story_complete";
        [SerializeField] private ApothecaryCaseData _requiresResolvedCase;
        [SerializeField, Min(0)] private int _unlockDelayDays;
        [SerializeField] private ApothecaryCaseClue[] _clues = Array.Empty<ApothecaryCaseClue>();
        [SerializeField] private ApothecaryCaseInterview[] _interviews =
            Array.Empty<ApothecaryCaseInterview>();
        [SerializeField] private ApothecaryCaseDecision[] _decisions =
            Array.Empty<ApothecaryCaseDecision>();
        [SerializeField] private DialogueData _intakeDialogue;
        [SerializeField] private DialogueData _followUpDialogue;

        public string Id => _id;
        public string PatientNpcId => _patientNpcId;
        public CharacterProfileData PatientProfile => _patientProfile;
        public string MentorNpcId => _mentorNpcId;
        public string RequiredFlagId => _requiredFlagId;
        public ApothecaryCaseData RequiresResolvedCase => _requiresResolvedCase;
        public int UnlockDelayDays => Mathf.Max(0, _unlockDelayDays);
        public ApothecaryCaseClue[] Clues => _clues;
        public ApothecaryCaseInterview[] Interviews => _interviews;
        public ApothecaryCaseDecision[] Decisions => _decisions;
        public DialogueData IntakeDialogue => _intakeDialogue;
        public DialogueData FollowUpDialogue => _followUpDialogue;

        public string TextId(string suffix) => "apothecary.case." + _id + "." + suffix;
        public string ActiveFlagId => "apothecary_case_active_" + _id;
        public string TreatedFlagId => "apothecary_case_treated_" + _id;
        public string ResolvedFlagId => "apothecary_case_resolved_" + _id;

        public bool HasValidStructure
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_patientNpcId) ||
                    _patientProfile == null || _clues == null || _clues.Length == 0 ||
                    _clues.Length > 30 || _interviews == null || _interviews.Length == 0 ||
                    _interviews.Length > 30 || _decisions == null || _decisions.Length < 2)
                    return false;
                foreach (var clue in _clues)
                    if (string.IsNullOrWhiteSpace(clue.id)) return false;
                foreach (var interview in _interviews)
                    if (string.IsNullOrWhiteSpace(interview.id)) return false;
                foreach (var decision in _decisions)
                    if (string.IsNullOrWhiteSpace(decision.id) || decision.preparation == null ||
                        string.IsNullOrWhiteSpace(decision.memoryId)) return false;
                return true;
            }
        }
    }
}
