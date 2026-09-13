using UnityEngine;
namespace GenjitsuLAB.Data.Samples
{
    /// <summary>Example enemy database asset with embedded records.</summary>
    [CreateAssetMenu(menuName = "GenjitsuLAB/Data/Samples/Enemy Repository")]
    public sealed class EnemyRepository : Repository<EnemyData>
    {
    }
}
