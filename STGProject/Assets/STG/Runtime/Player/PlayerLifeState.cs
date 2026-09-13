using System;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Owns the remaining player-life count for the current game session.
    /// </summary>
    public sealed class PlayerLifeState
    {
        private const int k_maximumSupportedLives = 99;

        private int m_currentLives;

        /// <summary>
        /// Creates a bounded player-life state.
        /// </summary>
        /// <param name="initialLives">Initial number of lives, including the active player.</param>
        /// <param name="maximumLives">Maximum number of lives that may be held.</param>
        public PlayerLifeState(int initialLives, int maximumLives)
        {
            MaximumLives = Math.Min(k_maximumSupportedLives, Math.Max(1, maximumLives));
            m_currentLives = Math.Max(0, Math.Min(initialLives, MaximumLives));
        }

        /// <summary>
        /// Gets the current number of lives, including the active player.
        /// </summary>
        public int CurrentLives => m_currentLives;

        /// <summary>
        /// Gets the maximum number of lives that may be held.
        /// </summary>
        public int MaximumLives { get; }

        /// <summary>
        /// Raised after the current life count changes.
        /// </summary>
        public event Action<int> LivesChanged;

        /// <summary>
        /// Attempts to add a positive number of lives without exceeding the configured maximum.
        /// </summary>
        /// <param name="amount">Positive number of lives to add.</param>
        /// <returns><see langword="true"/> when the count changed; otherwise <see langword="false"/>.</returns>
        public bool TryAddLives(int amount)
        {
            if (amount <= 0 || m_currentLives >= MaximumLives)
            {
                return false;
            }

            int availableCapacity = MaximumLives - m_currentLives;
            m_currentLives += Math.Min(amount, availableCapacity);
            LivesChanged?.Invoke(m_currentLives);
            return true;
        }

        /// <summary>
        /// Attempts to consume one life from the current session.
        /// </summary>
        /// <returns><see langword="true"/> when one life was consumed; otherwise <see langword="false"/>.</returns>
        public bool TryConsumeLife()
        {
            if (m_currentLives <= 0)
            {
                return false;
            }

            m_currentLives--;
            LivesChanged?.Invoke(m_currentLives);
            return true;
        }
    }
}
