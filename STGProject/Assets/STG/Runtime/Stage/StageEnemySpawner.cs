using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Marks an authored enemy-wave trigger in a scrolling stage.
    /// </summary>
    public sealed class StageEnemySpawner : MonoBehaviour
    {
        [SerializeField] private float m_activationY = 9f;
        [SerializeField] private EnemyType m_enemyType;

        private bool m_isTriggered;

        /// <summary>Gets whether this marker has crossed its activation line.</summary>
        public bool IsTriggered => m_isTriggered;

        /// <summary>Gets the enemy behavior spawned by this marker.</summary>
        public EnemyType EnemyType => m_enemyType;

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
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(left, right);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.down);
        }
    }
}
