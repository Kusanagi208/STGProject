using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Moves one authored scene layer at a fixed parallax multiplier.
    /// </summary>
    public sealed class StageScrollLayer : MonoBehaviour
    {
        [Min(0f)]
        [SerializeField] private float m_multiplier = 1f;

        private Vector3 m_initialPosition;

        /// <summary>Gets this layer's scroll-distance multiplier.</summary>
        public float Multiplier => m_multiplier;

        internal void Initialize()
        {
            m_initialPosition = transform.position;
        }

        internal void SetScrollDistance(float distance)
        {
            transform.position = m_initialPosition + Vector3.down * (distance * m_multiplier);
        }

        private void OnValidate()
        {
            m_multiplier = Mathf.Max(0f, m_multiplier);
        }
    }
}
