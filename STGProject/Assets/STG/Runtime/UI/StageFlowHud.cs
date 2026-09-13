using UnityEngine;
using UnityEngine.UI;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Presents transient stage-start and terminal game-over messages on the persistent Canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageFlowHud : MonoBehaviour
    {
        private const int k_fontSize = 16;
        private const string k_contentName = "StageFlowHudContent";
        private const string k_messageName = "StageFlowMessage";
        private const string k_startLabel = "START";
        private const string k_gameOverLabel = "GAME OVER";

        private CanvasGroup m_canvasGroup;
        private Image m_backdrop;
        private Text m_messageText;

        /// <summary>Gets the last terminal reason shown by this HUD.</summary>
        public StageEndReason LastEndReason { get; private set; }

        /// <summary>Gets whether the flow message is currently visible.</summary>
        public bool IsVisible => m_canvasGroup != null && m_canvasGroup.alpha > 0f;

        /// <summary>Shows the non-interactive stage-start notification.</summary>
        public void ShowStart()
        {
            EnsureVisuals();
            m_backdrop.enabled = false;
            m_messageText.text = k_startLabel;
            SetVisible(true);
        }

        /// <summary>Shows the terminal game-over notification for the specified reason.</summary>
        /// <param name="reason">Authoritative reason why gameplay ended.</param>
        public void ShowGameOver(StageEndReason reason)
        {
            EnsureVisuals();
            LastEndReason = reason;
            m_backdrop.enabled = true;
            m_messageText.text = k_gameOverLabel;
            SetVisible(true);
        }

        /// <summary>Hides all stage-flow presentation.</summary>
        public void Hide()
        {
            if (m_canvasGroup == null)
            {
                return;
            }

            SetVisible(false);
        }

        private void Awake()
        {
            EnsureVisuals();
            SetVisible(false);
        }

        private void EnsureVisuals()
        {
            if (m_canvasGroup != null)
            {
                return;
            }

            RectTransform contentRect = CreateRectTransform(k_contentName, transform);
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(160f, 144f);
            m_canvasGroup = contentRect.gameObject.AddComponent<CanvasGroup>();
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;

            m_backdrop = contentRect.gameObject.AddComponent<Image>();
            m_backdrop.color = new Color(0f, 0f, 0f, 0.65f);
            m_backdrop.raycastTarget = false;

            RectTransform messageRect = CreateRectTransform(k_messageName, contentRect);
            messageRect.anchorMin = new Vector2(0.5f, 0.5f);
            messageRect.anchorMax = new Vector2(0.5f, 0.5f);
            messageRect.pivot = new Vector2(0.5f, 0.5f);
            messageRect.anchoredPosition = Vector2.zero;
            messageRect.sizeDelta = new Vector2(150f, 28f);
            m_messageText = messageRect.gameObject.AddComponent<Text>();
            m_messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_messageText.fontSize = k_fontSize;
            m_messageText.fontStyle = FontStyle.Bold;
            m_messageText.color = Color.white;
            m_messageText.alignment = TextAnchor.MiddleCenter;
            m_messageText.horizontalOverflow = HorizontalWrapMode.Overflow;
            m_messageText.verticalOverflow = VerticalWrapMode.Overflow;
            m_messageText.raycastTarget = false;
            m_messageText.supportRichText = false;
            Outline outline = messageRect.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        private void SetVisible(bool isVisible)
        {
            m_canvasGroup.alpha = isVisible ? 1f : 0f;
            m_canvasGroup.interactable = false;
            m_canvasGroup.blocksRaycasts = false;
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
