using UnityEngine;
namespace GenjitsuLAB.Data.Samples
{
    /// <summary>Example CSV-authored weapon settings; independent of STG gameplay.</summary>
    public sealed class WeaponData : Data
    {
        [SerializeField] private int m_damage = 1;
        [SerializeField] private float m_speed = 0.1f;
        [SerializeField] private Sprite m_icon;
        /// <summary>Example damage value.</summary>
        public int Damage => m_damage;
        /// <summary>Example movement speed.</summary>
        public float Speed => m_speed;
        /// <summary>Inspector-authored asset reference excluded from CSV.</summary>
        public Sprite Icon => m_icon;
    }
}
