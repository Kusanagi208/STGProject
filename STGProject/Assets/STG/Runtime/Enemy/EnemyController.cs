using GenjitsuLAB.Animation;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Represents one pooled prototype enemy advanced by the gameplay fixed tick.
    /// </summary>
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private int m_animationId;
        [SerializeField] private EnemyType m_enemyType;
        [SerializeField] private Rect m_damageRect = new Rect(-0.35f, -0.35f, 0.7f, 0.7f);
        [Min(0f)]
        [SerializeField] private float m_speedPerTick = 0.04f;
        [SerializeField] private float m_shooterStopY = 7f;
        [SerializeField] private Transform m_firePoint;
        [Min(1)]
        [SerializeField] private int m_firstShotDelayTicks = 30;
        [Min(1)]
        [SerializeField] private int m_fireIntervalTicks = 90;

        private int m_fireCooldownTicks;
        private bool m_hasReachedStop;
        private AnimationPlayer m_animationPlayer;

        /// <summary>Gets the behavior type configured by this enemy prefab.</summary>
        public EnemyType EnemyType => m_enemyType;

        /// <summary>Gets this enemy's damage rectangle in world coordinates.</summary>
        public Rect WorldDamageRect
        {
            get
            {
                Vector3 position = transform.position;
                return new Rect(
                    position.x + m_damageRect.x,
                    position.y + m_damageRect.y,
                    m_damageRect.width,
                    m_damageRect.height);
            }
        }

        /// <summary>Gets whether this enemy has received its one lethal hit.</summary>
        public bool IsDestroyed { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Tries to get this enemy's fire point in world coordinates.</summary>
        internal bool TryGetFirePointPosition(out Vector3 position)
        {
            if (m_firePoint == null)
            {
                position = default;
                return false;
            }

            position = m_firePoint.position;
            return true;
        }
#endif

        private void Awake()
        {
            if (m_spriteRenderer == null || m_animationPack == null)
            {
                Debug.LogError("EnemyController requires a SpriteRenderer and AnimationPack.", this);
                return;
            }

            m_animationPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
            if (!m_animationPlayer.HasAnimation(m_animationId))
            {
                Debug.LogError($"EnemyController animation ID {m_animationId} was not found.", this);
            }
        }

        internal void Spawn(Vector3 position)
        {
            transform.position = position;
            IsDestroyed = false;
            m_spriteRenderer.enabled = true;
            m_hasReachedStop = m_enemyType == GenjitsuLAB.STG.EnemyType.Shooter &&
                               position.y <= m_shooterStopY;
            m_fireCooldownTicks = m_firstShotDelayTicks;
            m_animationPlayer?.ChangeAnim(m_animationId, true);
        }

        internal bool TickMovement(Rect recycleArea)
        {
            Vector3 position = transform.position;
            if (m_enemyType == GenjitsuLAB.STG.EnemyType.Shooter)
            {
                if (!m_hasReachedStop)
                {
                    position.y = Mathf.Max(position.y - m_speedPerTick, m_shooterStopY);
                    m_hasReachedStop = position.y <= m_shooterStopY;
                    transform.position = position;
                }
            }
            else
            {
                position.y -= m_speedPerTick;
                transform.position = position;
            }

            m_animationPlayer?.Tick();
            return position.x >= recycleArea.xMin &&
                   position.x <= recycleArea.xMax &&
                   position.y >= recycleArea.yMin &&
                   position.y <= recycleArea.yMax;
        }

        internal bool TryGetFire(Vector2 playerPosition, out Vector3 firePosition, out Vector2 direction)
        {
            if (IsDestroyed ||
                m_enemyType != GenjitsuLAB.STG.EnemyType.Shooter ||
                !m_hasReachedStop ||
                m_firePoint == null)
            {
                firePosition = default;
                direction = default;
                return false;
            }

            if (m_fireCooldownTicks > 0)
            {
                m_fireCooldownTicks--;
                if (m_fireCooldownTicks > 0)
                {
                    firePosition = default;
                    direction = default;
                    return false;
                }
            }

            firePosition = m_firePoint.position;
            direction = playerPosition - (Vector2)firePosition;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.down;
            }
            else
            {
                direction.Normalize();
            }

            m_fireCooldownTicks = m_fireIntervalTicks;
            return true;
        }

        internal void DestroyByDamage()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            m_spriteRenderer.enabled = false;
        }

        private void OnValidate()
        {
            m_animationId = Mathf.Max(0, m_animationId);
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
            m_speedPerTick = Mathf.Max(0f, m_speedPerTick);
            m_firstShotDelayTicks = Mathf.Max(1, m_firstShotDelayTicks);
            m_fireIntervalTicks = Mathf.Max(1, m_fireIntervalTicks);
        }
    }
}
