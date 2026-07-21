using UnityEngine;

namespace Hollowfen.Quests
{
    // The mill is a small walk-in room whose vendor furnishing prefab contains dozens of very
    // aggressive close-range LOD thresholds. As the third-person camera crosses the doorway,
    // tables, benches, dishes, and other props can all swap geometry in the same few frames and
    // read as z-fighting. Pin only this interior dressing hierarchy to its authored LOD0 meshes;
    // the mill exterior and the rest of the world retain normal distance-based LOD selection.
    [DisallowMultipleComponent]
    public sealed class MillInteriorRenderStabilizer : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("LOD index held while this interior hierarchy is enabled.")]
        private int _forcedLod;

        private LODGroup[] _groups;

        public int StabilizedGroupCount => _groups != null ? _groups.Length : 0;

        private void OnEnable()
        {
            _groups = GetComponentsInChildren<LODGroup>(false);
            for (int i = 0; i < _groups.Length; i++)
            {
                if (_groups[i] != null && _groups[i].enabled)
                    _groups[i].ForceLOD(_forcedLod);
            }
        }

        private void OnDisable()
        {
            if (_groups == null) return;
            for (int i = 0; i < _groups.Length; i++)
            {
                // Unity disables descendants before notifying this parent component during
                // Play-mode/scene teardown. ForceLOD on an already-disabled group logs an engine
                // error, and there is no state left to restore in that case anyway.
                if (_groups[i] != null && _groups[i].enabled &&
                    _groups[i].gameObject.activeInHierarchy)
                    _groups[i].ForceLOD(-1);
            }
        }
    }
}
