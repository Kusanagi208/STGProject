using System;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Owns deterministic fixed-tick scrolling and authored stage markers.
    /// </summary>
    public sealed class StageController : MonoBehaviour
    {
        [Min(0f)]
        [SerializeField] private float m_scrollSpeedPerTick = 0.025f;
        [Min(0f)]
        [SerializeField] private float m_scrollLength = 90f;
        [SerializeField] private bool m_startPaused;
        [SerializeField] private StageScrollLayer m_farBackground;
        [SerializeField] private StageScrollLayer m_midBackground;
        [SerializeField] private StageScrollLayer m_gameplay;
        [SerializeField] private StageScrollLayer m_foreground;

        private StageScrollLayer[] m_layers;
        private StageObstacle[] m_obstacles;
        private StageEnemySpawner[] m_spawners;
        private StageScrollPauseReason m_pauseReasons;
        private bool m_completionRaised;

        /// <summary>Raised once when the configured scroll length is reached.</summary>
        public event Action Completed;

        /// <summary>Raised once for each enemy spawner crossing its activation line.</summary>
        public event Action<StageEnemySpawner> SpawnerTriggered;

        /// <summary>Gets whether at least one scroll pause reason is active.</summary>
        public bool IsPaused => m_pauseReasons != StageScrollPauseReason.None;

        /// <summary>Gets whether the configured scroll distance has been reached.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Gets the current gameplay-layer scroll distance.</summary>
        public float ScrollDistance { get; private set; }

        /// <summary>Adds an independent reason that pauses stage scrolling.</summary>
        public void Pause(StageScrollPauseReason reason)
        {
            m_pauseReasons |= reason;
        }

        /// <summary>Removes one independent scroll pause reason.</summary>
        public void Resume(StageScrollPauseReason reason)
        {
            m_pauseReasons &= ~reason;
        }

        internal StageObstacle[] Obstacles => m_obstacles;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets the authored stage spawners for runtime diagnostics.</summary>
        internal StageEnemySpawner[] Spawners => m_spawners;
#endif

        internal void Initialize()
        {
            m_layers = new[] { m_farBackground, m_midBackground, m_gameplay, m_foreground };
            for (int index = 0; index < m_layers.Length; index++)
            {
                m_layers[index].Initialize();
            }

            m_obstacles = m_gameplay.GetComponentsInChildren<StageObstacle>(true);
            m_spawners = m_gameplay.GetComponentsInChildren<StageEnemySpawner>(true);
            for (int index = 0; index < m_spawners.Length; index++)
            {
                m_spawners[index].Initialize();
            }

            ScrollDistance = 0f;
            IsComplete = m_scrollLength <= 0f;
            m_completionRaised = false;
            m_pauseReasons = m_startPaused ? StageScrollPauseReason.StageStart : StageScrollPauseReason.None;
            ApplyLayerPositions();
        }

        internal void TickScroll()
        {
            if (IsPaused || IsComplete)
            {
                RaiseCompletionIfNeeded();
                return;
            }

            ScrollDistance = Mathf.Min(ScrollDistance + m_scrollSpeedPerTick, m_scrollLength);
            ApplyLayerPositions();
            if (ScrollDistance >= m_scrollLength)
            {
                IsComplete = true;
                RaiseCompletionIfNeeded();
            }
        }

        internal void TickSpawners()
        {
            if (IsPaused)
            {
                return;
            }

            for (int index = 0; index < m_spawners.Length; index++)
            {
                StageEnemySpawner spawner = m_spawners[index];
                if (spawner.TryTrigger())
                {
                    SpawnerTriggered?.Invoke(spawner);
                }
            }
        }

        private void ApplyLayerPositions()
        {
            for (int index = 0; index < m_layers.Length; index++)
            {
                m_layers[index].SetScrollDistance(ScrollDistance);
            }
        }

        private void RaiseCompletionIfNeeded()
        {
            if (!IsComplete || m_completionRaised)
            {
                return;
            }

            m_completionRaised = true;
            Completed?.Invoke();
        }

        private void OnValidate()
        {
            m_scrollSpeedPerTick = Mathf.Max(0f, m_scrollSpeedPerTick);
            m_scrollLength = Mathf.Max(0f, m_scrollLength);
        }
    }
}
