using UnityEngine;

namespace RiftForge
{
    /// <summary>
    /// Serves as the scene startup entry point and connects required gameplay components exactly once.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        private PlayerController playerController;

        [SerializeField]
        private CameraRig cameraRig;

        /// <summary>
        /// Gets whether all required scene dependencies have been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            if (!Initialize())
            {
                Debug.LogError("GameBootstrap requires both PlayerController and CameraRig references.", this);
            }
        }

        /// <summary>
        /// Supplies the scene dependencies used during initialization.
        /// </summary>
        /// <param name="player">The active player controller.</param>
        /// <param name="rig">The camera rig that should follow the player.</param>
        public void Configure(PlayerController player, CameraRig rig)
        {
            playerController = player;
            cameraRig = rig;
        }

        /// <summary>
        /// Initializes the scene once all required dependencies are present.
        /// </summary>
        /// <returns>True when initialization is complete; otherwise false.</returns>
        public bool Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (playerController == null || cameraRig == null)
            {
                return false;
            }

            cameraRig.SetTarget(playerController.transform);
            IsInitialized = true;
            return true;
        }
    }
}
