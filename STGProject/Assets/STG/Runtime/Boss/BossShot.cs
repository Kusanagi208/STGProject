using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Describes one allocation-free boss projectile launch request.
    /// </summary>
    internal readonly struct BossShot
    {
        internal BossShot(Vector3 position, Vector2 direction)
        {
            Position = position;
            Direction = direction;
        }

        internal Vector3 Position { get; }

        internal Vector2 Direction { get; }
    }
}
