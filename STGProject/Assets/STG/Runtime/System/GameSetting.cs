using UnityEngine;
using UnityEngine.Serialization;


namespace GenjitsuLAB.STG
{
    [CreateAssetMenu(fileName = "GameSettingAsset", menuName = "STG/Game Setting Asset")]
    public class GameSetting : ScriptableObject
    {
        [FormerlySerializedAs("playerPrefab")]
        [SerializeField] private PlayerController m_playerPrefab;
        [SerializeField] private Rect m_playerMovementArea = new Rect(-5f, 0f, 10f, 9f);
        [SerializeField] private StageSetting m_initialStage;

        /// <summary>
        /// Gets the player prefab shared by all stages.
        /// </summary>
        public PlayerController PlayerPrefab => m_playerPrefab;

        /// <summary>
        /// Gets the fixed world-space area in which the complete player body may move.
        /// </summary>
        public Rect PlayerMovementArea => m_playerMovementArea;

        /// <summary>
        /// Gets the stage loaded when gameplay starts.
        /// </summary>
        public StageSetting InitialStage => m_initialStage;

        private void OnValidate()
        {
            m_playerMovementArea.width = Mathf.Max(0.01f, m_playerMovementArea.width);
            m_playerMovementArea.height = Mathf.Max(0.01f, m_playerMovementArea.height);
        }
    }
}
