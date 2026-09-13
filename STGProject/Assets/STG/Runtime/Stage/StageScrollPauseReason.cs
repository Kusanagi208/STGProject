using System;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Identifies independent systems that may pause stage scrolling.
    /// </summary>
    [Flags]
    public enum StageScrollPauseReason
    {
        /// <summary>No pause is active.</summary>
        None = 0,
        /// <summary>The stage has not started scrolling yet.</summary>
        StageStart = 1 << 0,
        /// <summary>A story sequence is holding the scroll.</summary>
        Story = 1 << 1,
        /// <summary>A boss sequence is holding the scroll.</summary>
        Boss = 1 << 2,
        /// <summary>An external system is holding the scroll.</summary>
        External = 1 << 3,
        /// <summary>The player is completing a protected respawn entry.</summary>
        PlayerRespawn = 1 << 4,
        /// <summary>The stage has reached a terminal result.</summary>
        Result = 1 << 5
    }
}
