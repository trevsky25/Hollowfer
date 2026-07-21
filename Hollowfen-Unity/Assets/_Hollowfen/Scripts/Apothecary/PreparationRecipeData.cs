using System;
using Hollowfen.Data;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    public enum PreparationKind
    {
        Pantry,
        Fieldwork,
        VillageCare,
    }

    [CreateAssetMenu(fileName = "PreparationRecipe_New",
        menuName = "Hollowfen/Apothecary/Preparation Recipe")]
    public sealed class PreparationRecipeData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private PreparationKind _kind;
        [SerializeField] private string _titleId;
        [SerializeField] private string _summaryId;
        [SerializeField] private string _resultId;
        [SerializeField] private string _resultNameId;
        [SerializeField] private string _resultDescriptionId;
        [SerializeField] private MushroomFieldGuideData[] _ingredients =
            Array.Empty<MushroomFieldGuideData>();
        [SerializeField] private int[] _amounts = Array.Empty<int>();
        [SerializeField] private string[] _stepIds = Array.Empty<string>();
        [SerializeField] private string _requiredFlagId;
        [SerializeField, Tooltip("Localized guidance shown while the recipe's story lesson is locked.")]
        private string _unlockHintId;
        [SerializeField, Tooltip("Localized next-use guidance shown after a successful preparation.")]
        private string _resultUseId;
        [SerializeField] private string _completionFlagId;
        [SerializeField, Min(0)] private int _firstCraftKnowledge = 1;

        public string Id => _id;
        public PreparationKind Kind => _kind;
        public string TitleId => _titleId;
        public string SummaryId => _summaryId;
        public string ResultId => _resultId;
        public string ResultNameId => _resultNameId;
        public string ResultDescriptionId => _resultDescriptionId;
        public MushroomFieldGuideData[] Ingredients => _ingredients;
        public int[] Amounts => _amounts;
        public string[] StepIds => _stepIds;
        public string RequiredFlagId => _requiredFlagId;
        public string UnlockHintId => _unlockHintId;
        public string ResultUseId => _resultUseId;
        public string CompletionFlagId => _completionFlagId;
        public int FirstCraftKnowledge => Mathf.Max(0, _firstCraftKnowledge);

        public MushroomFieldGuideData HeroSpecies =>
            _ingredients != null && _ingredients.Length > 0 ? _ingredients[0] : null;

        public bool HasValidIngredients
        {
            get
            {
                if (_ingredients == null || _amounts == null || _ingredients.Length == 0 ||
                    _ingredients.Length != _amounts.Length) return false;
                for (int i = 0; i < _ingredients.Length; i++)
                    if (_ingredients[i] == null || _amounts[i] <= 0) return false;
                return !string.IsNullOrWhiteSpace(_id) && !string.IsNullOrWhiteSpace(_resultId);
            }
        }
    }
}
