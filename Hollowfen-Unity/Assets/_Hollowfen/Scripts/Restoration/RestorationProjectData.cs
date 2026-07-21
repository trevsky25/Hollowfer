using System;
using UnityEngine;

namespace Hollowfen.Restoration
{
    public enum RestorationStage
    {
        Unavailable = 0,
        Surveyed = 1,
        SuppliesCommitted = 2,
        WorkUnderway = 3,
        Restored = 4,
        Occupied = 5,
    }

    public enum RestorationCondition
    {
        ProjectStage = 0,
        ActiveQuest = 1,
        CompletedQuest = 2,
        FlagSet = 3,
    }

    [Serializable]
    public struct RestorationStageRule
    {
        [SerializeField] private RestorationCondition _condition;
        [SerializeField] private string _valueId;
        [SerializeField] private RestorationStage _stage;

        public RestorationCondition Condition => _condition;
        public string ValueId => _valueId;
        public RestorationStage Stage => _stage;

        public RestorationStageRule(RestorationCondition condition, string valueId, RestorationStage stage)
        {
            _condition = condition;
            _valueId = valueId;
            _stage = stage;
        }
    }

    [Serializable]
    public struct RestorationMilestone
    {
        [SerializeField] private string _labelId;
        [SerializeField] private string _detailId;
        [SerializeField] private RestorationCondition _condition;
        [SerializeField] private string _valueId;
        [SerializeField] private RestorationStage _requiredStage;

        public string LabelId => _labelId;
        public string DetailId => _detailId;
        public RestorationCondition Condition => _condition;
        public string ValueId => _valueId;
        public RestorationStage RequiredStage => _requiredStage;

        public RestorationMilestone(string labelId, string detailId, RestorationCondition condition,
            string valueId, RestorationStage requiredStage = RestorationStage.Unavailable)
        {
            _labelId = labelId;
            _detailId = detailId;
            _condition = condition;
            _valueId = valueId;
            _requiredStage = requiredStage;
        }
    }

    [Serializable]
    public struct RestorationStageCopy
    {
        [SerializeField] private RestorationStage _stage;
        [SerializeField] private string _titleId;
        [SerializeField] private string _bodyId;
        [SerializeField] private string _shortId;

        public RestorationStage Stage => _stage;
        public string TitleId => _titleId;
        public string BodyId => _bodyId;
        public string ShortId => _shortId;

        public RestorationStageCopy(RestorationStage stage, string titleId, string bodyId,
            string shortId = null)
        {
            _stage = stage;
            _titleId = titleId;
            _bodyId = bodyId;
            _shortId = shortId;
        }
    }

    [Serializable]
    public struct RestorationContribution
    {
        [SerializeField] private string _labelId;
        [SerializeField] private string _detailId;
        [SerializeField] private string _fundedFlagId;
        [SerializeField, Min(1)] private int _costCopper;
        [SerializeField] private RestorationStage _availableFromStage;

        public string LabelId => _labelId;
        public string DetailId => _detailId;
        public string FundedFlagId => _fundedFlagId;
        public int CostCopper => Mathf.Max(1, _costCopper);
        public RestorationStage AvailableFromStage => _availableFromStage;

        public RestorationContribution(string labelId, string detailId, string fundedFlagId,
            int costCopper, RestorationStage availableFromStage = RestorationStage.Surveyed)
        {
            _labelId = labelId;
            _detailId = detailId;
            _fundedFlagId = fundedFlagId;
            _costCopper = Mathf.Max(1, costCopper);
            _availableFromStage = availableFromStage;
        }
    }

    [CreateAssetMenu(fileName = "RestorationProject", menuName = "Hollowfen/Restoration/Project")]
    public sealed class RestorationProjectData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _titleId;
        [SerializeField] private string _summaryId;
        [SerializeField] private string _locationId;
        [SerializeField] private string _promptTargetId;
        [SerializeField, Tooltip("Localized description of the permanent gameplay benefit earned at Occupied.")]
        private string _benefitId;
        [SerializeField, Min(0)] private int _villageHopeReward;
        [SerializeField, Min(0)] private int _knowledgeReward;
        [SerializeField] private string _activeQuestId;
        [SerializeField] private string _completedQuestId;
        [SerializeField] private RestorationStageRule[] _stageRules = Array.Empty<RestorationStageRule>();
        [SerializeField] private RestorationStageCopy[] _stageCopy = Array.Empty<RestorationStageCopy>();
        [SerializeField] private RestorationMilestone[] _milestones = Array.Empty<RestorationMilestone>();
        [SerializeField] private RestorationContribution[] _contributions = Array.Empty<RestorationContribution>();
        [SerializeField, Tooltip("Set when every authored contribution has been funded.")]
        private string _contributionsCompleteFlagId;

        public string Id => _id;
        public string TitleId => _titleId;
        public string SummaryId => _summaryId;
        public string LocationId => _locationId;
        public string PromptTargetId => _promptTargetId;
        public string BenefitId => _benefitId;
        public int VillageHopeReward => Mathf.Max(0, _villageHopeReward);
        public int KnowledgeReward => Mathf.Max(0, _knowledgeReward);
        public string ActiveQuestId => _activeQuestId;
        public string CompletedQuestId => _completedQuestId;
        public RestorationStageRule[] StageRules => _stageRules;
        public RestorationStageCopy[] StageCopy => _stageCopy;
        public RestorationMilestone[] Milestones => _milestones;
        public RestorationContribution[] Contributions => _contributions;
        public string ContributionsCompleteFlagId => _contributionsCompleteFlagId;

        public string StageTitleId(RestorationStage stage)
        {
            if (_stageCopy != null)
                foreach (var copy in _stageCopy)
                    if (copy.Stage == stage && !string.IsNullOrWhiteSpace(copy.TitleId)) return copy.TitleId;
            return "restoration.stage." + stage.ToString().ToLowerInvariant() + ".title";
        }

        public string StageBodyId(RestorationStage stage)
        {
            if (_stageCopy != null)
                foreach (var copy in _stageCopy)
                    if (copy.Stage == stage && !string.IsNullOrWhiteSpace(copy.BodyId)) return copy.BodyId;
            return "restoration.stage." + stage.ToString().ToLowerInvariant() + ".body";
        }

        public string StageShortId(RestorationStage stage)
        {
            if (_stageCopy != null)
                foreach (var copy in _stageCopy)
                    if (copy.Stage == stage && !string.IsNullOrWhiteSpace(copy.ShortId)) return copy.ShortId;
            return "restoration.stage." + stage.ToString().ToLowerInvariant() + ".short";
        }
    }
}
