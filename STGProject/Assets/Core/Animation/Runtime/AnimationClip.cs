using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenjitsuLAB.Animation
{
    [System.Serializable]
    public class AnimationElement
    {
        [SerializeField] private Sprite m_sprite;
        [SerializeField] private int m_duration = 1;

        [SerializeField] private List<Rect> m_hitBoxes = new();
        [SerializeField] private List<Rect> m_hurtBoxes = new();

        public Sprite Sprite => m_sprite;
        public int Duration => m_duration;

        public IReadOnlyList<Rect> HitBoxes => m_hitBoxes != null
            ? m_hitBoxes
            : Array.Empty<Rect>();
        public IReadOnlyList<Rect> HurtBoxes => m_hurtBoxes != null
            ? m_hurtBoxes
            : Array.Empty<Rect>();

        public void Validate()
        {
            m_duration = Mathf.Max(1, m_duration);
            m_hitBoxes ??= new List<Rect>();
            m_hurtBoxes ??= new List<Rect>();
        }
    }

    public class AnimationClip : ScriptableObject
    {
        [SerializeField] private int m_animId;
        [SerializeField] private string m_animName;
        [SerializeField] private bool m_isLoop;

        [SerializeField] private List<AnimationElement> m_elements = new();

        [SerializeField] private int m_totalTicks;

        public int AnimId => m_animId;
        public string AnimName => m_animName;
        public bool IsLoop => m_isLoop;
        public int TotalTicks => m_totalTicks;
        public IReadOnlyList<AnimationElement> Elements
        {
            get
            {
                if (m_elements != null)
                {
                    return m_elements;
                }

                return Array.Empty<AnimationElement>();
            }
        }

        private void OnValidate()
        {
            m_animId = Mathf.Max(0, m_animId);
            m_totalTicks = 0;
            if (m_elements == null)
            {
                m_elements = new List<AnimationElement>();
                return;
            }

            foreach (var element in m_elements)
            {
                if (element != null)
                {
                    element.Validate();
                    m_totalTicks += element.Duration;
                }
            }
        }
    }
}
