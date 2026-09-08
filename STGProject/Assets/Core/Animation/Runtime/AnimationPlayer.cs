using System;
using System.Collections.Generic;
using UnityEngine;
using GenjitsuLAB.Core;

namespace GenjitsuLAB.Animation
{
    public class AnimationPlayer
    {
        private SpriteRenderer m_spriteRenderer;
        private AnimationClip m_currentAnim;
        private int m_currentElement;
        private int m_currentElementTime;
        private int m_animationElapsedTime;
        private bool m_hasLooped;
        private bool m_isElementStartTick;
        private bool m_isPaused;
        private bool m_isPlaying;
        private readonly Dictionary<int, AnimationClip> m_animations = new();

        public bool IsPause { get => m_isPaused; set => m_isPaused = value; }
        public bool IsPlaying => m_isPlaying;
        public AnimationClip CurrentAnim => m_currentAnim;

        /// <summary>
        /// 當前動畫影格
        /// </summary>
        public int AnimElem => m_currentAnim == null ? 0 : m_currentElement + 1;

        /// <summary>
        /// 當前動畫經過時間
        /// </summary>
        public int AnimationElapsedTime => m_animationElapsedTime;
        
        /// <summary>
        /// 當前動畫相對時間
        /// </summary>
        public int AnimTime => m_currentAnim == null
            ? 0
            : Mathf.Min(0, m_animationElapsedTime - GetTotalDuration(m_currentAnim));

        /// <summary>
        /// 當前動畫影格經過時間
        /// </summary>
        public int CurrentElementTime => m_currentElementTime;
        public int AnimId => m_currentAnim?.AnimId ?? -1;

        /// <summary>
        /// 影格開始
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public bool IsAnimElemStart(int element)
        {
            return !m_hasLooped && m_currentAnim != null &&
                   m_currentElement + 1 == element && m_isElementStartTick;
        }

        /// <summary>
        /// 檢查特定動畫編號
        /// </summary>
        /// <param name="animationId"></param>
        /// <returns></returns>
        public bool HasAnimation(int animationId) => m_animations.ContainsKey(animationId);

        public AnimationPlayer(SpriteRenderer spriteRenderer, IEnumerable<AnimationClip> animations)
        {
            m_spriteRenderer = spriteRenderer;

            if (animations == null)
            {
                return;
            }

            foreach (var clip in animations)
            {
                if (clip == null)
                {
                    continue;
                }

                if (!m_animations.TryAdd(clip.AnimId, clip))
                {
                    DebugLogger.LogWarning($"[Animation Player] Duplicate animation id: {clip.AnimId}");
                }
            }
        }

        private bool Play(AnimationClip clip)
        {
            if (clip == null || clip.Elements.Count == 0)
            {
                DebugLogger.LogWarning($"[Animation Player] Animation has no elements: {clip?.name ?? "null"}");
                return false;
            }

            m_currentAnim = clip;
            m_currentElementTime = 0;
            m_currentElement = 0;
            m_animationElapsedTime = 0;
            m_hasLooped = false;
            m_isElementStartTick = true;
            m_isPlaying = true;

            ApplyElement();
            return true;
        }

        public void Tick()
        {
            if (!m_isPlaying || m_currentAnim == null)
            {
                m_isElementStartTick = false;
                return;
            }

            m_isElementStartTick = false;
            if (m_isPaused)
            {
                return;
            }

            m_currentElementTime++;
            m_animationElapsedTime++;
            var element = m_currentAnim.Elements[m_currentElement];
            int duration = element == null ? 1 : Mathf.Max(1, element.Duration);

            if (m_currentElementTime >= duration)
            {
                m_currentElementTime = 0;
                m_currentElement++;

                if (m_currentElement >= m_currentAnim.Elements.Count)
                {
                    if (m_currentAnim.IsLoop)
                    {
                        m_currentElement = 0;
                        m_animationElapsedTime = 0;
                        m_hasLooped = true;
                    }
                    else
                    {
                        m_currentElement = m_currentAnim.Elements.Count - 1;
                        m_isPlaying = false;
                    }
                }
                else
                {
                    m_isElementStartTick = true;
                }

                ApplyElement();
            }
        }

        private void ApplyElement()
        {
            if (m_spriteRenderer == null)
            {
                return;
            }

            var frame = m_currentAnim.Elements[m_currentElement];
            m_spriteRenderer.sprite = frame?.Sprite;
        }

        public IReadOnlyList<Rect> GetHitBox()
        {
            if (m_currentAnim == null || m_currentElement < 0 || m_currentElement >= m_currentAnim.Elements.Count)
            {
                return Array.Empty<Rect>();
            }

            var element = m_currentAnim.Elements[m_currentElement];
            if (element?.HitBoxes != null)
            {
                return element.HitBoxes;
            }

            return Array.Empty<Rect>();
        }

        public IReadOnlyList<Rect> GetHurtBox()
        {
            if (m_currentAnim == null || m_currentElement < 0 || m_currentElement >= m_currentAnim.Elements.Count)
            {
                return Array.Empty<Rect>();
            }

            var element = m_currentAnim.Elements[m_currentElement];
            if (element?.HurtBoxes != null)
            {
                return element.HurtBoxes;
            }

            return Array.Empty<Rect>();
        }

        public void Reset()
        {
            m_currentAnim = null;
            m_currentElement = 0;
            m_currentElementTime = 0;
            m_animationElapsedTime = 0;
            m_hasLooped = false;
            m_isElementStartTick = false;
            m_isPaused = false;
            m_isPlaying = false;
        }

        public bool ChangeAnim(int animId, bool restart = false)
        {
            if (!m_animations.TryGetValue(animId, out var clip))
            {
                DebugLogger.LogWarning($"[Animation Player] Animation id not found: {animId}");
                return false;
            }

            return ChangeAnim(clip, restart);
        }

        public bool ChangeAnim(int animId, int element, bool restart = false)
        {
            if (!m_animations.TryGetValue(animId, out AnimationClip clip))
            {
                DebugLogger.LogWarning($"[Animation Player] Animation id not found: {animId}");
                return false;
            }

            return ChangeAnim(clip, element, restart);
        }

        public bool ChangeAnim(AnimationClip clip, bool restart = false)
        {
            if (clip == null)
            {
                DebugLogger.LogWarning("[Animation Player] Animation clip is null.");
                return false;
            }

            if (!m_animations.TryGetValue(clip.AnimId, out var registeredClip) || registeredClip != clip)
            {
                DebugLogger.LogWarning($"[Animation Player] Animation is not registered: {clip.name}");
                return false;
            }

            if (!restart && m_currentAnim == clip)
            {
                return true;
            }

            return Play(clip);
        }

        public bool ChangeAnim(AnimationClip clip, int element, bool restart = false)
        {
            if (!ChangeAnim(clip, restart))
            {
                return false;
            }

            int elementIndex = Mathf.Clamp(element - 1, 0, clip.Elements.Count - 1);
            m_currentElement = elementIndex;
            m_currentElementTime = 0;
            m_animationElapsedTime = GetElementStartTime(clip, elementIndex);
            m_hasLooped = false;
            m_isElementStartTick = true;
            m_isPlaying = true;
            ApplyElement();
            return true;
        }

        public bool ChangeForeignAnim(AnimationClip clip, int element)
        {
            if (clip == null || clip.Elements.Count == 0)
            {
                return false;
            }
            m_currentAnim = clip;
            m_currentElement = Mathf.Clamp(element - 1, 0, clip.Elements.Count - 1);
            m_currentElementTime = 0;
            m_animationElapsedTime = GetElementStartTime(clip, m_currentElement);
            m_hasLooped = false;
            m_isElementStartTick = true;
            m_isPlaying = true;
            ApplyElement();
            return true;
        }

        public bool ChangeForeignAnim(AnimationClip clip)
        {
            if (clip == null || clip.Elements.Count == 0)
            {
                return false;
            }
            return m_currentAnim == clip || ChangeForeignAnim(clip, 1);
        }

        public int GetElementTime(int element)
        {
            if (m_currentAnim == null)
            {
                return int.MinValue;
            }

            int elementIndex = element - 1;
            if (elementIndex < 0 || elementIndex >= m_currentAnim.Elements.Count)
            {
                return int.MinValue;
            }

            return m_animationElapsedTime - GetElementStartTime(m_currentAnim, elementIndex);
        }

        private static int GetElementStartTime(AnimationClip clip, int elementIndex)
        {
            int result = 0;
            for (int i = 0; i < elementIndex; i++)
            {
                AnimationElement element = clip.Elements[i];
                result += element == null ? 1 : Mathf.Max(1, element.Duration);
            }
            return result;
        }

        private static int GetTotalDuration(AnimationClip clip)
        {
            int result = 0;
            for (int i = 0; i < clip.Elements.Count; i++)
            {
                AnimationElement element = clip.Elements[i];
                result += element == null ? 1 : Mathf.Max(1, element.Duration);
            }
            return result;
        }
    }
}
