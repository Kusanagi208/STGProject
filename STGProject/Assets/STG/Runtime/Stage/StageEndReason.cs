namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Identifies the authoritative reason why the current stage stopped.
    /// </summary>
    public enum StageEndReason
    {
        /// <summary>The player used the final remaining life.</summary>
        LivesDepleted = 0,
        /// <summary>Every boss component was destroyed.</summary>
        BossDefeated = 1
    }
}
