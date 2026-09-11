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
        [SerializeField] private Rect m_weaponRecycleArea = new Rect(-6f, -1f, 12f, 11f);
        [SerializeField] private Rect m_pickupRecycleArea = new Rect(-6f, -1f, 12f, 11f);
        [SerializeField] private Rect m_enemyRecycleArea = new Rect(-6f, -1f, 12f, 11f);
        [SerializeField] private Rect m_bulletRecycleArea = new Rect(-6f, -1f, 12f, 11f);
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
        /// Gets the fixed world-space area outside which player weapons are recycled.
        /// </summary>
        public Rect WeaponRecycleArea => m_weaponRecycleArea;

        /// <summary>
        /// Gets the fixed world-space area outside which pickups are recycled.
        /// </summary>
        public Rect PickupRecycleArea => m_pickupRecycleArea;

        /// <summary>
        /// Gets the fixed world-space area outside which enemies are recycled.
        /// </summary>
        public Rect EnemyRecycleArea => m_enemyRecycleArea;

        /// <summary>
        /// Gets the fixed world-space area outside which enemy bullets are recycled.
        /// </summary>
        public Rect BulletRecycleArea => m_bulletRecycleArea;

        /// <summary>
        /// Gets the stage loaded when gameplay starts.
        /// </summary>
        public StageSetting InitialStage => m_initialStage;

        private void OnValidate()
        {
            m_playerMovementArea.width = Mathf.Max(0.01f, m_playerMovementArea.width);
            m_playerMovementArea.height = Mathf.Max(0.01f, m_playerMovementArea.height);

            m_weaponRecycleArea.width = Mathf.Max(0.01f, m_weaponRecycleArea.width);
            m_weaponRecycleArea.height = Mathf.Max(0.01f, m_weaponRecycleArea.height);
            float recycleMinX = Mathf.Min(m_weaponRecycleArea.xMin, m_playerMovementArea.xMin);
            float recycleMinY = Mathf.Min(m_weaponRecycleArea.yMin, m_playerMovementArea.yMin);
            float recycleMaxX = Mathf.Max(m_weaponRecycleArea.xMax, m_playerMovementArea.xMax);
            float recycleMaxY = Mathf.Max(m_weaponRecycleArea.yMax, m_playerMovementArea.yMax);
            m_weaponRecycleArea = Rect.MinMaxRect(recycleMinX, recycleMinY, recycleMaxX, recycleMaxY);

            m_pickupRecycleArea.width = Mathf.Max(0.01f, m_pickupRecycleArea.width);
            m_pickupRecycleArea.height = Mathf.Max(0.01f, m_pickupRecycleArea.height);
            float pickupRecycleMinX = Mathf.Min(m_pickupRecycleArea.xMin, m_playerMovementArea.xMin);
            float pickupRecycleMinY = Mathf.Min(m_pickupRecycleArea.yMin, m_playerMovementArea.yMin);
            float pickupRecycleMaxX = Mathf.Max(m_pickupRecycleArea.xMax, m_playerMovementArea.xMax);
            float pickupRecycleMaxY = Mathf.Max(m_pickupRecycleArea.yMax, m_playerMovementArea.yMax);
            m_pickupRecycleArea = Rect.MinMaxRect(
                pickupRecycleMinX,
                pickupRecycleMinY,
                pickupRecycleMaxX,
                pickupRecycleMaxY);

            m_enemyRecycleArea = EnsureContainsMovementArea(m_enemyRecycleArea);
            m_bulletRecycleArea = EnsureContainsMovementArea(m_bulletRecycleArea);
        }

        private Rect EnsureContainsMovementArea(Rect area)
        {
            area.width = Mathf.Max(0.01f, area.width);
            area.height = Mathf.Max(0.01f, area.height);
            return Rect.MinMaxRect(
                Mathf.Min(area.xMin, m_playerMovementArea.xMin),
                Mathf.Min(area.yMin, m_playerMovementArea.yMin),
                Mathf.Max(area.xMax, m_playerMovementArea.xMax),
                Mathf.Max(area.yMax, m_playerMovementArea.yMax));
        }
    }
}
