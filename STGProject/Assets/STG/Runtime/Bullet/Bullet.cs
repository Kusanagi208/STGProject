using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Represents one pooled enemy projectile with a fixed launch direction.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        [SerializeField] private Rect m_damageRect = new Rect(-0.1f, -0.1f, 0.2f, 0.2f);
        [Min(0f)]
        [SerializeField] private float m_speedPerTick = 0.08f;

        private Vector2 m_direction;

        internal Rect WorldDamageRect
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

        internal Vector2 Direction => m_direction;

        internal void Spawn(Vector3 position, Vector2 direction)
        {
            transform.position = position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                m_direction = Vector2.down;
                return;
            }

            m_direction = direction.normalized;
        }

        internal bool Tick(Rect recycleArea)
        {
            Vector3 position = transform.position;
            position.x += m_direction.x * m_speedPerTick;
            position.y += m_direction.y * m_speedPerTick;
            transform.position = position;
            return position.x >= recycleArea.xMin &&
                   position.x <= recycleArea.xMax &&
                   position.y >= recycleArea.yMin &&
                   position.y <= recycleArea.yMax;
        }

        private void OnValidate()
        {
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
            m_speedPerTick = Mathf.Max(0f, m_speedPerTick);
        }
    }
}
