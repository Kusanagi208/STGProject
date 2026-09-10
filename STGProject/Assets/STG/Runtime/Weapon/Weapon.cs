using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Represents a pooled player projectile advanced by the gameplay fixed tick.
    /// </summary>
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private float m_speedPerTick = 0.25f;

        internal void Spawn(Vector3 position)
        {
            transform.position = position;
        }

        internal bool Tick(Rect recycleArea)
        {
            Vector3 position = transform.position;
            position.y += m_speedPerTick;
            transform.position = position;

            return position.x >= recycleArea.xMin &&
                   position.x <= recycleArea.xMax &&
                   position.y >= recycleArea.yMin &&
                   position.y <= recycleArea.yMax;
        }

        private void OnValidate()
        {
            m_speedPerTick = Mathf.Max(0f, m_speedPerTick);
        }
    }
}
