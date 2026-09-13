using UnityEngine;
namespace GenjitsuLAB.Data.Samples
{
    /// <summary>Example enemy record demonstrating typed cross-database keys.</summary>
    public sealed class EnemyData : Data
    {
        [SerializeField] private int m_health = 10;
        [SerializeField] private WeaponReference m_weapon = new WeaponReference();
        /// <summary>Example health value.</summary>
        public int Health => m_health;
        /// <summary>Weapon key selectable through the cached Unity editor dropdown.</summary>
        public WeaponReference Weapon => m_weapon;
    }
}
