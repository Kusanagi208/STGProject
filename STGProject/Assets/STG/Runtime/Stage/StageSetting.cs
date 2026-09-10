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
    }
}
