using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenjitsuLAB.Animation
{
    /// <summary>Owns the sprite animation clips stored as sub-assets of one animation asset.</summary>
    [CreateAssetMenu(fileName = "AnimationPack", menuName = "Animation Pack")]
    public sealed class AnimationPack : ScriptableObject
    {
        [SerializeField]
        private List<AnimationClip> m_clips = new();

        /// <summary>Gets the animation clips in deterministic authoring order.</summary>
        public IReadOnlyList<AnimationClip> Clips => m_clips != null
            ? m_clips
            : Array.Empty<AnimationClip>();

        private void OnValidate()
        {
            m_clips ??= new List<AnimationClip>();
        }
    }
}
