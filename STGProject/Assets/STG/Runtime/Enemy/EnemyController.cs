using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Represents one pooled prototype enemy advanced by the gameplay fixed tick.
    /// </summary>
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
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

        internal void Spawn(Vector3 position)
        {
            transform.position = position;
            IsDestroyed = false;
            m_spriteRenderer.enabled = true;
            m_hasReachedStop = m_enemyType == GenjitsuLAB.STG.EnemyType.Shooter &&
                               position.y <= m_shooterStopY;
            m_fireCooldownTicks = m_firstShotDelayTicks;
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
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
            m_speedPerTick = Mathf.Max(0f, m_speedPerTick);
            m_firstShotDelayTicks = Mathf.Max(1, m_firstShotDelayTicks);
            m_fireIntervalTicks = Mathf.Max(1, m_fireIntervalTicks);
        }
    }
}
