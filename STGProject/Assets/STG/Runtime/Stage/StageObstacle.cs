using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Defines a rectangular stage obstacle authored under the gameplay scroll layer.
    /// </summary>
    public sealed class StageObstacle : MonoBehaviour
    {
        [SerializeField] private Rect m_damageRect = new Rect(-0.5f, -0.5f, 1f, 1f);

        /// <summary>Gets the obstacle damage rectangle in world coordinates.</summary>
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

        private void OnValidate()
        {
            m_damageRect.width = Mathf.Max(0.01f, m_damageRect.width);
            m_damageRect.height = Mathf.Max(0.01f, m_damageRect.height);
        }

        private void OnDrawGizmosSelected()
        {
            Rect rect = WorldDamageRect;
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(rect.center, rect.size);
        }
    }
}
