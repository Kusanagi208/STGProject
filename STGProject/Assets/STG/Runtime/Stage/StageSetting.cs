using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Defines the visual content and spawn positions for one gameplay stage.
    /// </summary>
    [CreateAssetMenu(fileName = "StageSettingAsset", menuName = "STG/Stage Setting Asset")]
    public sealed class StageSetting : ScriptableObject
    {
        [SerializeField] private Transform m_backgroundPrefab;
        [SerializeField] private Vector3 m_backgroundPosition;
        [SerializeField] private Vector3 m_playerSpawnPosition = Vector3.up;
        [SerializeField] private Pickup m_pickupPrefab;
        [SerializeField] private Vector3 m_testPickupSpawnPosition = new Vector3(0f, 8f, 0f);
        [Min(1)]
        [SerializeField] private int m_pickupPoolCapacity = 4;

        /// <summary>
        /// Gets the background prefab instantiated for this stage.
        /// </summary>
        public Transform BackgroundPrefab => m_backgroundPrefab;

        /// <summary>
        /// Gets the world-space background spawn position.
        /// </summary>
        public Vector3 BackgroundPosition => m_backgroundPosition;

        /// <summary>
        /// Gets the world-space player spawn position.
        /// </summary>
        public Vector3 PlayerSpawnPosition => m_playerSpawnPosition;

        /// <summary>
        /// Gets the pickup prefab prewarmed for this stage.
        /// </summary>
        public Pickup PickupPrefab => m_pickupPrefab;

        /// <summary>
        /// Gets the fixed prototype pickup spawn position in world space.
        /// </summary>
        public Vector3 TestPickupSpawnPosition => m_testPickupSpawnPosition;

        /// <summary>
        /// Gets the fixed pickup pool capacity for this stage.
        /// </summary>
        public int PickupPoolCapacity => m_pickupPoolCapacity;

        private void OnValidate()
        {
            m_pickupPoolCapacity = Mathf.Max(1, m_pickupPoolCapacity);
        }
    }
}
