using GenjitsuLAB.Animation;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Represents one animated pickup advanced by the gameplay fixed tick.
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private int m_animationId;
        [SerializeField] private float m_speedPerTick = 0.03f;
        [SerializeField] private Rect m_collisionRect = new Rect(-0.375f, -0.375f, 0.75f, 0.75f);
        [SerializeField] private PickupType m_pickupType = PickupType.Test;

        private AnimationPlayer m_animationPlayer;

        /// <summary>
        /// Gets the gameplay payload emitted when this pickup is collected.
        /// </summary>
        public PickupType PickupType => m_pickupType;

        internal Rect WorldCollisionRect
        {
            get
            {
                Vector3 position = transform.position;
                return new Rect(
                    position.x + m_collisionRect.x,
                    position.y + m_collisionRect.y,
                    m_collisionRect.width,
                    m_collisionRect.height);
            }
        }

        private void Awake()
        {
            if (m_spriteRenderer == null || m_animationPack == null)
            {
                Debug.LogError("Pickup requires a SpriteRenderer and AnimationPack.", this);
                return;
            }

            m_animationPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
            if (!m_animationPlayer.HasAnimation(m_animationId))
            {
                Debug.LogError($"Pickup animation ID {m_animationId} was not found.", this);
            }
        }

        internal void Spawn(Vector3 position)
        {
            transform.position = position;

            if (m_animationPlayer != null)
            {
                m_animationPlayer.ChangeAnim(m_animationId, true);
            }
        }

        internal bool Tick(Rect recycleArea)
        {
            Vector3 position = transform.position;
            position.y -= m_speedPerTick;
            transform.position = position;
            m_animationPlayer?.Tick();

            return position.x >= recycleArea.xMin &&
                   position.x <= recycleArea.xMax &&
                   position.y >= recycleArea.yMin &&
                   position.y <= recycleArea.yMax;
        }

        private void OnValidate()
        {
            m_animationId = Mathf.Max(0, m_animationId);
            m_speedPerTick = Mathf.Max(0f, m_speedPerTick);
            m_collisionRect.width = Mathf.Max(0.01f, m_collisionRect.width);
            m_collisionRect.height = Mathf.Max(0.01f, m_collisionRect.height);
        }
    }
}
