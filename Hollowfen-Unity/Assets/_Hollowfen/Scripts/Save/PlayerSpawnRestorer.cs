using UnityEngine;

namespace Hollowfen.Save
{
    // Drops the player at the position recorded in the active save slot when the gameplay
    // scene loads (Continue / Load Slot). New games have no recorded transform and spawn
    // at the scene's authored position. Lives on PlayerArmature.
    public class PlayerSpawnRestorer : MonoBehaviour
    {
        private void Start()
        {
            var meta = SaveManager.GetSlotMeta(SaveManager.ActiveSlot);
            if (meta == null || !meta.HasPlayerTransform) return;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = new Vector3(meta.PlayerPosX, meta.PlayerPosY, meta.PlayerPosZ);
            transform.rotation = Quaternion.Euler(0f, meta.PlayerYaw, 0f);
            if (cc != null) cc.enabled = true;
        }
    }
}
