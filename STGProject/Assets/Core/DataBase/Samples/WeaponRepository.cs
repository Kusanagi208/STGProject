using UnityEngine;
namespace GenjitsuLAB.Data.Samples
{
    /// <summary>Example weapon database asset with embedded records.</summary>
    [CreateAssetMenu(menuName = "GenjitsuLAB/Data/Samples/Weapon Repository")]
    public sealed class WeaponRepository : Repository<WeaponData>
    {
    }
}
