using System;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Coordinates the three-part prototype boss and emits allocation-free shot requests.
    /// </summary>
    public sealed class BossController : MonoBehaviour
    {
        private static readonly Vector2 s_centerLeftDirection = new Vector2(-0.42261827f, -0.9063078f);
        private static readonly Vector2 s_centerDirection = Vector2.down;
        private static readonly Vector2 s_centerRightDirection = new Vector2(0.42261827f, -0.9063078f);

        [SerializeField] private BossPartController m_leftPart;
        [SerializeField] private BossPartController m_centerPart;
        [SerializeField] private BossPartController m_rightPart;
        [Min(0f)]
        [SerializeField] private float m_entrySpeedPerTick = 0.03f;
        [SerializeField] private float m_stopY = 7f;

        private bool m_hasReachedStop;
        private bool m_defeatRaised;

        /// <summary>Raised once after all three boss components are destroyed.</summary>
        public event Action Defeated;

        /// <summary>Gets whether the boss has reached its stationary combat position.</summary>
        public bool HasReachedStop => m_hasReachedStop;

        /// <summary>Gets whether all three components have been destroyed.</summary>
        public bool IsDefeated => m_defeatRaised;

        internal BossPartController LeftPart => m_leftPart;

        internal BossPartController CenterPart => m_centerPart;

        internal BossPartController RightPart => m_rightPart;

        internal void Spawn(Vector3 position)
        {
            transform.position = position;
            m_hasReachedStop = position.y <= m_stopY;
            m_defeatRaised = false;
            m_leftPart.InitializeForSpawn();
            m_centerPart.InitializeForSpawn();
            m_rightPart.InitializeForSpawn();
        }

        internal void TickMovementAndAnimation()
        {
            if (!m_hasReachedStop)
            {
                Vector3 position = transform.position;
                position.y = Mathf.Max(position.y - m_entrySpeedPerTick, m_stopY);
                m_hasReachedStop = position.y <= m_stopY;
                transform.position = position;
            }

            m_leftPart.TickAnimation();
            m_centerPart.TickAnimation();
            m_rightPart.TickAnimation();
        }

        internal void ResolveWeaponHits(PlayerController player)
        {
            if (player.IsDestroyed || m_defeatRaised)
            {
                return;
            }

            ResolveWeaponHits(player, m_leftPart);
            ResolveWeaponHits(player, m_centerPart);
            ResolveWeaponHits(player, m_rightPart);
            RaiseDefeatedIfNeeded();
        }

        internal bool OverlapsOperationalPart(Rect playerRect)
        {
            return IsPartOverlapping(m_leftPart, playerRect) ||
                   IsPartOverlapping(m_centerPart, playerRect) ||
                   IsPartOverlapping(m_rightPart, playerRect);
        }

        internal int CollectShots(Vector2 playerPosition, BossShot[] shots)
        {
            if (!m_hasReachedStop || m_defeatRaised || shots == null || shots.Length < 5)
            {
                return 0;
            }

            int count = 0;
            if (m_leftPart.TryBeginFire(out Vector3 leftPosition))
            {
                shots[count++] = new BossShot(leftPosition, GetAimedDirection(leftPosition, playerPosition));
            }

            if (m_rightPart.TryBeginFire(out Vector3 rightPosition))
            {
                shots[count++] = new BossShot(rightPosition, GetAimedDirection(rightPosition, playerPosition));
            }

            if (m_centerPart.TryBeginFire(out Vector3 centerPosition))
            {
                shots[count++] = new BossShot(centerPosition, s_centerLeftDirection);
                shots[count++] = new BossShot(centerPosition, s_centerDirection);
                shots[count++] = new BossShot(centerPosition, s_centerRightDirection);
            }

            return count;
        }

        private void OnDestroy()
        {
            Defeated = null;
        }

        private void OnValidate()
        {
            m_entrySpeedPerTick = Mathf.Max(0f, m_entrySpeedPerTick);
        }

        private static bool IsPartOverlapping(BossPartController part, Rect playerRect)
        {
            return part.IsOperational && playerRect.Overlaps(part.WorldDamageRect);
        }

        private static Vector2 GetAimedDirection(Vector3 firePosition, Vector2 playerPosition)
        {
            Vector2 direction = playerPosition - (Vector2)firePosition;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector2.down;
            }

            direction.Normalize();
            return direction;
        }

        private static void ResolveWeaponHits(PlayerController player, BossPartController part)
        {
            while (part.IsOperational && player.TryConsumeWeaponHit(part.WorldDamageRect))
            {
                part.TakeDamage(1);
            }
        }

        private void RaiseDefeatedIfNeeded()
        {
            if (m_defeatRaised ||
                m_leftPart.IsOperational ||
                m_centerPart.IsOperational ||
                m_rightPart.IsOperational)
            {
                return;
            }

            m_defeatRaised = true;
            Defeated?.Invoke();
        }
    }
}
