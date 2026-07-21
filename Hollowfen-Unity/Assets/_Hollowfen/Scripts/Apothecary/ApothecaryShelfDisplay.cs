using System;
using UnityEngine;

namespace Hollowfen.Apothecary
{
    /// <summary>Uses purchased showcase bottles as a live, save-backed view of prepared stock.</summary>
    [DisallowMultipleComponent]
    public sealed class ApothecaryShelfDisplay : MonoBehaviour
    {
        [SerializeField] private PreparationRecipeData[] _recipes =
            Array.Empty<PreparationRecipeData>();
        [SerializeField] private Transform[] _stockProps = Array.Empty<Transform>();

        private Renderer[][] _renderers;
        private bool[][] _rendererDefaults;

        public int DisplayCount => Mathf.Min(_recipes?.Length ?? 0, _stockProps?.Length ?? 0);

        private void Awake() => Cache();

        private void OnEnable()
        {
            ApothecaryRuntime.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ApothecaryRuntime.OnChanged -= Refresh;
        }

        public void Refresh()
        {
            if (_renderers == null) Cache();
            for (int i = 0; i < DisplayCount; i++)
            {
                bool stocked = _recipes[i] != null &&
                    ApothecaryRuntime.ProductCount(_recipes[i].ResultId) > 0;
                Renderer[] renderers = _renderers[i];
                bool[] defaults = _rendererDefaults[i];
                for (int j = 0; j < renderers.Length; j++)
                    if (renderers[j] != null) renderers[j].enabled = stocked && defaults[j];
            }
        }

        private void Cache()
        {
            int count = DisplayCount;
            _renderers = new Renderer[count][];
            _rendererDefaults = new bool[count][];
            for (int i = 0; i < count; i++)
            {
                _renderers[i] = _stockProps[i] != null
                    ? _stockProps[i].GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
                _rendererDefaults[i] = new bool[_renderers[i].Length];
                for (int j = 0; j < _renderers[i].Length; j++)
                    _rendererDefaults[i][j] = _renderers[i][j] != null &&
                        _renderers[i][j].enabled;
            }
        }
    }
}
