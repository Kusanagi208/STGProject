using GenjitsuLAB.Animation;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Owns health, animation, collision, and firing cadence for one boss part.
    /// </summary>
    public sealed class BossPartController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private int m_operationalAnimationId;
        [SerializeField] private int m_damagedAnimationId = 1;
        [SerializeField] private BossPartType m_partType;
        [Min(1)]
        [SerializeField] private int m_maxHealth = 30;
        [SerializeField] private Rect m_damageRect = new Rect(-0.625f, -0.5f, 1.25f, 1f);
        [SerializeField] private Transform m_firePoint;
        [Min(1)]
        [SerializeField] private int m_firstShotDelayTicks = 30;
        [Min(1)]
        [SerializeField] private int m_fireIntervalTicks = 90;

        private AnimationPlayer m_animationPlayer;
        private int m_fireCooldownTicks;

        /// <summary>Gets which boss component this object represents.</summary>
        public BossPartType PartType => m_partType;

        /// <summary>Gets the configured health restored for each boss spawn.</summary>
        public int MaxHealth => m_maxHealth;

        /// <summary>Gets the current health of this component.</summary>
        public int CurrentHealth { get; private set; }

        /// <summary>Gets whether collision and weapon functions are still active.</summary>
        public bool IsOperational => CurrentHealth > 0;

        /// <summary>Gets this component's damage rectangle in world coordinates.</summary>
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

        private void Awake()
        {
            EnsureAnimationPlayer();
        }

        internal void InitializeForSpawn()
        {
            EnsureAnimationPlayer();
            CurrentHealth = m_maxHealth;
            m_fireCooldownTicks = m_firstShotDelayTicks;
            if (m_spriteRenderer != null)
            {
                m_spriteRenderer.enabled = true;
            }

            m_animationPlayer?.ChangeAnim(m_operationalAnimationId, true);
        }

        internal void TickAnimation()
        {
            m_animationPlayer?.Tick();
        }

        internal bool TakeDamage(int amount)
        {
            if (!IsOperational || amount <= 0)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth > 0)
            {
                return false;
            }

            m_animationPlayer?.ChangeAnim(m_damagedAnimationId, true);
            return true;
        }

        internal bool TryBeginFire(out Vector3 position)
        {
            if (!IsOperational || m_firePoint == null)
            {
                position = default;
                return false;
            }

            if (m_fireCooldownTicks > 0)
            {
                m_fireCooldownTicks--;
                if (m_fireCooldownTicks > 0)
                {
                    position = default;
                    return false;
                }
            }

            position = m_firePoint.position;
            m_fireCooldownTicks = m_fireIntervalTicks;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Tries to get the part fire point in world coordinates.</summary>
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

        private void EnsureAnimationPlayer()
        {
            if (m_animationPlayer != null)
            {
                return;
            }

            if (m_spriteRenderer == null || m_animationPack == null)
            {
                Debug.LogError("BossPartController requires a SpriteRenderer and AnimationPack.", this);
                return;
            }

            m_animationPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
            if (!m_animationPlayer.HasAnimation(m_operationalAnimationId) ||
                !m_animationPlayer.HasAnimation(m_damagedAnimationId))
            {
                Debug.LogError("BossPartController requires operational and damaged animations.", this);
            }
        }

        private void OnValidate()
        {
            m_operationalAnimationId = Mathf.Max(0, m_operationalAnimationId);
            m_damagedAnimationId = Mathf.Max(0, m_damagedAnimationId);
            m_maxHealth = Mathf.Max(1, m_maxHealth);
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
            m_firstShotDelayTicks = Mathf.Max(1, m_firstShotDelayTicks);
            m_fireIntervalTicks = Mathf.Max(1, m_fireIntervalTicks);
        }
    }
}
