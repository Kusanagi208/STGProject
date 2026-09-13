using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Presents the session life count in the persistent gameplay Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLifeHud : MonoBehaviour
    {
        private const int k_fontSize = 8;
        private const string k_contentName = "PlayerLifeHud";
        private const string k_iconName = "LifeIcon";
        private const string k_countName = "LifeCount";

        [SerializeField] private Sprite m_lifeIconSprite;

        private PlayerLifeState m_lifeState;
        private Text m_countText;
        private string[] m_cachedCountLabels;

        /// <summary>
        /// Binds the HUD to the specified session life state and displays its current value.
        /// </summary>
        /// <param name="lifeState">Authoritative session life state.</param>
        public void Bind(PlayerLifeState lifeState)
        {
            if (ReferenceEquals(m_lifeState, lifeState))
            {
                return;
            }

            Unbind();
            if (lifeState == null)
            {
                return;
            }

            EnsureVisuals();
            EnsureLabelCache(lifeState.MaximumLives);
            m_lifeState = lifeState;
            m_lifeState.LivesChanged += OnLivesChanged;
            SetDisplayedCount(m_lifeState.CurrentLives);
        }

        /// <summary>
        /// Stops observing the currently bound session life state.
        /// </summary>
        public void Unbind()
        {
            if (m_lifeState != null)
            {
                m_lifeState.LivesChanged -= OnLivesChanged;
                m_lifeState = null;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void EnsureVisuals()
        {
            if (m_countText != null)
            {
                return;
            }

            RectTransform contentRect = CreateRectTransform(k_contentName, transform);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.zero;
            contentRect.pivot = Vector2.zero;
            contentRect.anchoredPosition = new Vector2(4f, 4f);
            contentRect.sizeDelta = new Vector2(44f, 14f);

            RectTransform iconRect = CreateRectTransform(k_iconName, contentRect);
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.zero;
            iconRect.pivot = Vector2.zero;
            iconRect.anchoredPosition = new Vector2(1f, 1f);
            iconRect.sizeDelta = new Vector2(9f, 12f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = m_lifeIconSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            RectTransform countRect = CreateRectTransform(k_countName, contentRect);
            countRect.anchorMin = Vector2.zero;
            countRect.anchorMax = Vector2.zero;
            countRect.pivot = Vector2.zero;
            countRect.anchoredPosition = new Vector2(13f, 0f);
            countRect.sizeDelta = new Vector2(31f, 14f);
            m_countText = countRect.gameObject.AddComponent<Text>();
            m_countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_countText.fontSize = k_fontSize;
            m_countText.color = Color.white;
            m_countText.alignment = TextAnchor.MiddleLeft;
            m_countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_countText.verticalOverflow = VerticalWrapMode.Overflow;
            m_countText.raycastTarget = false;
            m_countText.supportRichText = false;
        }

        private void EnsureLabelCache(int maximumLives)
        {
            int cacheSize = maximumLives + 1;
            if (m_cachedCountLabels != null && m_cachedCountLabels.Length == cacheSize)
            {
                return;
            }

            m_cachedCountLabels = new string[cacheSize];
            for (int count = 0; count < cacheSize; count++)
            {
                m_cachedCountLabels[count] = string.Concat(
                    "x ",
                    count.ToString("D2", CultureInfo.InvariantCulture));
            }
        }

        private void OnLivesChanged(int currentLives)
        {
            SetDisplayedCount(currentLives);
        }

        private void SetDisplayedCount(int currentLives)
        {
            int safeIndex = Mathf.Clamp(currentLives, 0, m_cachedCountLabels.Length - 1);
            string label = m_cachedCountLabels[safeIndex];
            if (!ReferenceEquals(m_countText.text, label))
            {
                m_countText.text = label;
            }
        }

        private static RectTransform CreateRectTransform(string objectName, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rectTransform = (RectTransform)child.transform;
            rectTransform.SetParent(parent, false);
            return rectTransform;
        }
    }
}
