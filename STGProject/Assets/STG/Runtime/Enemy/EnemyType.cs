namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Identifies the prototype behavior used by an enemy prefab.
    /// </summary>
    public enum EnemyType
    {
        /// <summary>Moves straight down until recycled.</summary>
        Straight = 0,
        /// <summary>Stops at a configured Y coordinate and fires aimed bullets.</summary>
        Shooter = 1
    }
}
