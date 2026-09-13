using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Defines scene content and spawn settings for one gameplay stage.
    /// </summary>
    [CreateAssetMenu(fileName = "StageSettingAsset", menuName = "STG/Stage Setting Asset")]
    public sealed class StageSetting : ScriptableObject
    {
        [SerializeField] private string m_sceneName = "Stage01";
        [SerializeField] private Vector3 m_playerEntryPosition = new Vector3(0f, -1.5f, 0f);
        [SerializeField] private Vector3 m_playerSpawnPosition = Vector3.up;
        [Min(0.001f)]
        [SerializeField] private float m_playerEntrySpeedPerTick = 0.03f;
        [Min(1)]
        [SerializeField] private int m_startNoticeTicks = 60;
        [SerializeField] private Pickup m_pickupPrefab;
        [SerializeField] private Vector3 m_testPickupSpawnPosition = new Vector3(0f, 8f, 0f);
        [Min(1)]
        [SerializeField] private int m_pickupPoolCapacity = 4;
        [SerializeField] private EnemyController m_straightEnemyPrefab;
        [SerializeField] private EnemyController m_shooterEnemyPrefab;
        [SerializeField] private Bullet m_bulletPrefab;
        [SerializeField] private BossController m_bossPrefab;
        [Min(1)]
        [SerializeField] private int m_straightEnemyPoolCapacity = 8;
        [Min(1)]
        [SerializeField] private int m_shooterEnemyPoolCapacity = 4;
        [Min(1)]
        [SerializeField] private int m_bulletPoolCapacity = 32;

        /// <summary>
        /// Gets the additive Unity scene name containing this stage.
        /// </summary>
        public string SceneName => m_sceneName;

        /// <summary>
        /// Gets the world-space position from which the player enters the visible playfield.
        /// </summary>
        public Vector3 PlayerEntryPosition => m_playerEntryPosition;

        /// <summary>
        /// Gets the world-space player spawn position.
        /// </summary>
        public Vector3 PlayerSpawnPosition => m_playerSpawnPosition;

        /// <summary>Gets the fixed distance moved by the player on each entry tick.</summary>
        public float PlayerEntrySpeedPerTick => m_playerEntrySpeedPerTick;

        /// <summary>Gets how many gameplay ticks the START notification remains visible.</summary>
        public int StartNoticeTicks => m_startNoticeTicks;

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

        /// <summary>Gets the straight-moving enemy prefab for this stage.</summary>
        public EnemyController StraightEnemyPrefab => m_straightEnemyPrefab;

        /// <summary>Gets the aimed-shooter enemy prefab for this stage.</summary>
        public EnemyController ShooterEnemyPrefab => m_shooterEnemyPrefab;

        /// <summary>Gets the enemy projectile prefab for this stage.</summary>
        public Bullet BulletPrefab => m_bulletPrefab;

        /// <summary>Gets the single level-scoped boss prefab for this stage.</summary>
        public BossController BossPrefab => m_bossPrefab;

        /// <summary>Gets the fixed Straight enemy pool capacity.</summary>
        public int StraightEnemyPoolCapacity => m_straightEnemyPoolCapacity;

        /// <summary>Gets the fixed Shooter enemy pool capacity.</summary>
        public int ShooterEnemyPoolCapacity => m_shooterEnemyPoolCapacity;

        /// <summary>Gets the fixed enemy bullet pool capacity.</summary>
        public int BulletPoolCapacity => m_bulletPoolCapacity;

        private void OnValidate()
        {
            m_playerEntrySpeedPerTick = Mathf.Max(0.001f, m_playerEntrySpeedPerTick);
            m_startNoticeTicks = Mathf.Max(1, m_startNoticeTicks);
            m_pickupPoolCapacity = Mathf.Max(1, m_pickupPoolCapacity);
            m_straightEnemyPoolCapacity = Mathf.Max(1, m_straightEnemyPoolCapacity);
            m_shooterEnemyPoolCapacity = Mathf.Max(1, m_shooterEnemyPoolCapacity);
            m_bulletPoolCapacity = Mathf.Max(1, m_bulletPoolCapacity);
        }
    }
}
