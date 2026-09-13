using System;
using UnityEngine;
using GenjitsuLAB.Animation;

namespace GenjitsuLAB.STG
{
    public class PlayerController : MonoBehaviour
    {
        private const int k_invalidAnimId = -1;

        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private float m_speed;
        [SerializeField] private Vector2 m_bodySize = new Vector2(1f, 2f);
        [SerializeField] private Rect m_pickupCollisionRect = new Rect(-0.25f, -0.35f, 0.5f, 0.7f);
        [SerializeField] private Rect m_damageRect = new Rect(-0.2f, -0.25f, 0.4f, 0.5f);
        [SerializeField] private int m_idleAnimId;
        [SerializeField] private int m_leftBankingAnimId = 1;
        [SerializeField] private int m_rightBankingAnimId = 2;
        [Range(0f, 1f)]
        [SerializeField] private float m_bankingThreshold = 0.5f;
        [SerializeField] private Transform m_firePoint;
        [SerializeField] private Weapon m_weaponPrefab;
        [Min(1)]
        [SerializeField] private int m_fireIntervalTicks = 10;
        [Min(1)]
        [SerializeField] private int m_weaponPoolCapacity = 32;

        private AnimationPlayer m_animPlayer;
        private int m_resolvedIdleAnimId;
        private int m_resolvedLeftBankingAnimId;
        private int m_resolvedRightBankingAnimId;
        private int m_currentMovementAnimId;
        private ComponentPool<Weapon> m_weaponPool;
        private Weapon[] m_activeWeapons;
        private int m_activeWeaponCount;
        private int m_fireCooldownTicks;
        private Vector3 m_entryTargetPosition;
        private float m_entrySpeedPerTick;
        private bool m_isEntering;
        private bool m_isInitialized;

        /// <summary>
        /// Raised once when the player collects a pickup.
        /// </summary>
        public event Action<PickupType> PickupCollected;

        /// <summary>
        /// Raised once when the player's damage rectangle is hit.
        /// </summary>
        public event Action Destroyed;

        /// <summary>
        /// Gets whether this player has been destroyed for the current stage run.
        /// </summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>Gets whether the player is moving through the protected entry sequence.</summary>
        public bool IsEntering => m_isEntering;

        internal Rect WorldDamageRect => ToWorldRect(m_damageRect);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets the number of active pooled player weapons for runtime diagnostics.</summary>
        internal int ActiveWeaponCount => m_activeWeaponCount;

        /// <summary>Gets the configured player weapon pool capacity for runtime diagnostics.</summary>
        internal int WeaponPoolCapacity => m_weaponPoolCapacity;
#endif

        internal Rect WorldPickupCollisionRect
        {
            get
            {
                Vector3 position = transform.position;
                return new Rect(
                    position.x + m_pickupCollisionRect.x,
                    position.y + m_pickupCollisionRect.y,
                    m_pickupCollisionRect.width,
                    m_pickupCollisionRect.height);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets an active pooled player weapon by its dense runtime index.</summary>
        internal Weapon GetActiveWeapon(int index)
        {
            return m_activeWeapons[index];
        }

        /// <summary>Tries to get the player fire point in world coordinates.</summary>
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
            EnsureAnimationPlayer();
        }

        /// <summary>Initializes the player animation and fixed-capacity weapon pool once.</summary>
        public void Initialize()
        {
            if (m_isInitialized)
            {
                return;
            }

            EnsureAnimationPlayer();
            m_resolvedIdleAnimId = ResolveAnimationId(m_idleAnimId, "Idle", k_invalidAnimId);
            m_resolvedLeftBankingAnimId = ResolveAnimationId(
                m_leftBankingAnimId,
                "Left Banking",
                m_resolvedIdleAnimId);
            m_resolvedRightBankingAnimId = ResolveAnimationId(
                m_rightBankingAnimId,
                "Right Banking",
                m_resolvedIdleAnimId);

            m_currentMovementAnimId = k_invalidAnimId;
            ChangeMovementAnimation(m_resolvedIdleAnimId);
            InitializeWeaponPool();
            IsDestroyed = false;
            m_spriteRenderer.enabled = true;
            m_isInitialized = true;
        }

        public void Tick(GameSceneContext ctx)
        {
            if (IsDestroyed || m_isEntering)
            {
                return;
            }

            Vector2 direction = Vector2.ClampMagnitude(ctx.inputActions.Player.Move.ReadValue<Vector2>(), 1f);
            Move(direction, ctx.gameSetting.PlayerMovementArea);
            UpdateMovementAnimation(direction.x);
            UpdateFire(ctx.inputActions.Player.Fire.IsPressed());
            TickWeapons(ctx.gameSetting.WeaponRecycleArea);

            m_animPlayer.Tick();
        }

        private void OnDestroy()
        {
            PickupCollected = null;
            Destroyed = null;
            m_weaponPool?.Dispose();
            m_weaponPool = null;
            m_activeWeapons = null;
            m_activeWeaponCount = 0;
        }

        private void OnValidate()
        {
            m_idleAnimId = Mathf.Max(0, m_idleAnimId);
            m_leftBankingAnimId = Mathf.Max(0, m_leftBankingAnimId);
            m_rightBankingAnimId = Mathf.Max(0, m_rightBankingAnimId);
            m_bankingThreshold = Mathf.Clamp01(m_bankingThreshold);
            m_pickupCollisionRect.width = Mathf.Max(0.01f, m_pickupCollisionRect.width);
            m_pickupCollisionRect.height = Mathf.Max(0.01f, m_pickupCollisionRect.height);
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
            m_fireIntervalTicks = Mathf.Max(1, m_fireIntervalTicks);
            m_weaponPoolCapacity = Mathf.Max(1, m_weaponPoolCapacity);
        }

        internal void CollectPickup(PickupType pickupType)
        {
            PickupCollected?.Invoke(pickupType);
        }

        internal bool TryConsumeWeaponHit(Rect targetRect)
        {
            for (int index = 0; index < m_activeWeaponCount; index++)
            {
                Weapon weapon = m_activeWeapons[index];
                if (!weapon.WorldDamageRect.Overlaps(targetRect))
                {
                    continue;
                }

                ReturnWeaponAt(index);
                return true;
            }

            return false;
        }

        internal void DestroyByDamage()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;
            m_isEntering = false;
            ReturnAllWeapons();
            m_spriteRenderer.enabled = false;
            Destroyed?.Invoke();
        }

        /// <summary>Begins a protected, fixed-tick entry using the existing player instance.</summary>
        internal void BeginEntry(Vector3 startPosition, Vector3 targetPosition, float speedPerTick)
        {
            ReturnAllWeapons();
            transform.position = startPosition;
            m_entryTargetPosition = targetPosition;
            m_entrySpeedPerTick = Mathf.Max(0f, speedPerTick);
            m_fireCooldownTicks = 0;
            IsDestroyed = false;
            m_isEntering = true;
            m_spriteRenderer.enabled = true;
            m_currentMovementAnimId = m_resolvedIdleAnimId;
            m_animPlayer?.ChangeAnim(m_resolvedIdleAnimId, true);
        }

        /// <summary>Advances the protected entry and reports when the target position is reached.</summary>
        internal bool TickEntry()
        {
            m_animPlayer?.Tick();
            if (!m_isEntering)
            {
                return true;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                m_entryTargetPosition,
                m_entrySpeedPerTick);
            if (transform.position != m_entryTargetPosition)
            {
                return false;
            }

            transform.position = m_entryTargetPosition;
            m_isEntering = false;
            return true;
        }

        /// <summary>Advances player presentation while gameplay remains paused.</summary>
        internal void TickPresentation()
        {
            m_animPlayer?.Tick();
        }

        /// <summary>Returns every active player weapon without rebuilding the pool.</summary>
        internal void ReturnAllWeapons()
        {
            m_weaponPool?.ReturnAll();
            if (m_activeWeapons != null)
            {
                Array.Clear(m_activeWeapons, 0, m_activeWeaponCount);
            }

            m_activeWeaponCount = 0;
        }

        private Rect ToWorldRect(Rect localRect)
        {
            Vector3 position = transform.position;
            return new Rect(
                position.x + localRect.x,
                position.y + localRect.y,
                localRect.width,
                localRect.height);
        }

        private void InitializeWeaponPool()
        {
            if (m_weaponPool != null)
            {
                return;
            }

            if (m_firePoint == null || m_weaponPrefab == null)
            {
                Debug.LogError("PlayerController requires a FirePoint and Weapon prefab. Shooting is disabled.", this);
                return;
            }

            m_weaponPool = new ComponentPool<Weapon>(m_weaponPrefab, m_weaponPoolCapacity, transform.parent);
            m_activeWeapons = new Weapon[m_weaponPoolCapacity];
            m_activeWeaponCount = 0;
            m_fireCooldownTicks = 0;
        }

        private void EnsureAnimationPlayer()
        {
            if (m_animPlayer != null)
            {
                return;
            }

            if (m_spriteRenderer == null || m_animationPack == null)
            {
                Debug.LogError("PlayerController requires a SpriteRenderer and AnimationPack.", this);
                return;
            }

            m_animPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
        }

        private void UpdateFire(bool isPressed)
        {
            if (!isPressed)
            {
                m_fireCooldownTicks = 0;
                return;
            }

            if (m_fireCooldownTicks > 0)
            {
                m_fireCooldownTicks--;
                return;
            }

            TryFireWeapon();
            m_fireCooldownTicks = m_fireIntervalTicks - 1;
        }

        private void TryFireWeapon()
        {
            if (m_weaponPool == null || !m_weaponPool.TryRent(out Weapon weapon))
            {
                return;
            }

            weapon.Spawn(m_firePoint.position);
            m_activeWeapons[m_activeWeaponCount++] = weapon;
        }

        private void TickWeapons(Rect recycleArea)
        {
            int index = 0;
            while (index < m_activeWeaponCount)
            {
                Weapon weapon = m_activeWeapons[index];
                if (weapon.Tick(recycleArea))
                {
                    index++;
                    continue;
                }

                ReturnWeaponAt(index);
            }
        }

        private void ReturnWeaponAt(int index)
        {
            Weapon weapon = m_activeWeapons[index];
            m_weaponPool.Return(weapon);
            int lastIndex = --m_activeWeaponCount;
            m_activeWeapons[index] = m_activeWeapons[lastIndex];
            m_activeWeapons[lastIndex] = null;
        }

        private int ResolveAnimationId(int animId, string animationName, int fallbackAnimId)
        {
            if (m_animPlayer.HasAnimation(animId))
            {
                return animId;
            }

            Debug.LogWarning(
                $"PlayerController {animationName} animation ID {animId} was not found. Using fallback animation.",
                this);
            return fallbackAnimId;
        }

        private void UpdateMovementAnimation(float horizontalInput)
        {
            int targetAnimId = m_resolvedIdleAnimId;
            if (horizontalInput < 0f && horizontalInput <= -m_bankingThreshold)
            {
                targetAnimId = m_resolvedLeftBankingAnimId;
            }
            else if (horizontalInput > 0f && horizontalInput >= m_bankingThreshold)
            {
                targetAnimId = m_resolvedRightBankingAnimId;
            }

            ChangeMovementAnimation(targetAnimId);
        }

        private void ChangeMovementAnimation(int animId)
        {
            if (animId == k_invalidAnimId || animId == m_currentMovementAnimId)
            {
                return;
            }

            m_currentMovementAnimId = animId;
            m_animPlayer.ChangeAnim(animId);
        }

        private void Move(Vector2 direction, Rect movementArea)
        {
            Vector3 position = transform.position;
            Vector2 nextPosition = new Vector2(position.x, position.y) + direction * m_speed;
            nextPosition = ClampPosition(nextPosition, movementArea);
            transform.position = new Vector3(nextPosition.x, nextPosition.y, position.z);
        }

        private Vector2 ClampPosition(Vector2 position, Rect movementArea)
        {
            Vector2 halfBodySize = m_bodySize * 0.5f;
            float minX = movementArea.xMin + halfBodySize.x;
            float maxX = movementArea.xMax - halfBodySize.x;
            float minY = movementArea.yMin + halfBodySize.y;
            float maxY = movementArea.yMax - halfBodySize.y;
            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY));
        }
    }

}
