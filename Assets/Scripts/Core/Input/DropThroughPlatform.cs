using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>
    /// Add this component to platforms that can be dropped through.
    /// Registers with the PlayerController via serialised reference (wired by LevelSceneGenerator).
    /// </summary>
    public class DropThroughPlatform : MonoBehaviour
    {
        [SerializeField] private PlayerController playerController;

        [SerializeField] private Collider2D platformCollider;

        /// <summary>
        /// Inject the player controller reference at runtime (called by LevelSceneGenerator).
        /// </summary>
        public void SetPlayerController(PlayerController controller)
        {
            playerController = controller;
            if (playerController != null)
                playerController.RegisterPlatform(this);
        }

        private void Awake()
        {
            if (platformCollider == null) platformCollider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            // If the reference was already wired (serialised or via SetPlayerController), register.
            if (playerController != null)
                playerController.RegisterPlatform(this);
        }

        private void OnDestroy()
        {
            if (playerController != null)
                playerController.UnregisterPlatform(this);
        }

        public void EnablePlatform()
        {
            if (platformCollider != null)
            {
                platformCollider.enabled = true;
            }
        }

        public void DisablePlatform()
        {
            if (platformCollider != null)
            {
                platformCollider.enabled = false;
            }
        }
    }
}
