using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Marks the one authored boss trigger in a scrolling stage.
    /// </summary>
    public sealed class StageBossSpawner : MonoBehaviour
    {
        [SerializeField] private float m_activationY = 9f;

        private bool m_isTriggered;

        /// <summary>Gets whether this marker has crossed its activation line.</summary>
        public bool IsTriggered => m_isTriggered;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets this spawner's authored world-space activation height.</summary>
        internal float ActivationY => m_activationY;
#endif

        internal void Initialize()
        {
            m_isTriggered = false;
        }

        internal bool TryTrigger()
        {
            if (m_isTriggered || transform.position.y > m_activationY)
            {
                return false;
            }

            m_isTriggered = true;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            const float lineHalfWidth = 5f;
            Vector3 left = new Vector3(-lineHalfWidth, m_activationY, transform.position.z);
            Vector3 right = new Vector3(lineHalfWidth, m_activationY, transform.position.z);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down);
        }
    }
}
