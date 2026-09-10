using UnityEngine;
using GenjitsuLAB.Animation;

namespace GenjitsuLAB.STG
{
    public class PlayerController : MonoBehaviour
    {
        private const int k_invalidAnimId = -1;

        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private float m_speed;
        [SerializeField] private Vector2 m_bodySize = new Vector2(1f, 2f);
        [SerializeField] private int m_idleAnimId;
        [SerializeField] private int m_leftBankingAnimId = 1;
        [SerializeField] private int m_rightBankingAnimId = 2;
        [Range(0f, 1f)]
        [SerializeField] private float m_bankingThreshold = 0.5f;

        private AnimationPlayer m_animPlayer;
        private int m_resolvedIdleAnimId;
        private int m_resolvedLeftBankingAnimId;
        private int m_resolvedRightBankingAnimId;
        private int m_currentMovementAnimId;

        public void Initialize()
        {
            m_animPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
            m_resolvedIdleAnimId = ResolveAnimationId(m_idleAnimId, "Idle", k_invalidAnimId);
            m_resolvedLeftBankingAnimId = ResolveAnimationId(
                m_leftBankingAnimId,
                "Left Banking",
                m_resolvedIdleAnimId);
            m_resolvedRightBankingAnimId = ResolveAnimationId(
                m_rightBankingAnimId,
                "Right Banking",
                m_resolvedIdleAnimId);

            m_currentMovementAnimId = k_invalidAnimId;
            ChangeMovementAnimation(m_resolvedIdleAnimId);
        }

        public void Tick(GameSceneContext ctx)
        {
            Vector2 direction = Vector2.ClampMagnitude(ctx.inputActions.Player.Move.ReadValue<Vector2>(), 1f);
            Move(direction, ctx.gameSetting.PlayerMovementArea);
            UpdateMovementAnimation(direction.x);

            m_animPlayer.Tick();
        }

        private void OnValidate()
        {
            m_idleAnimId = Mathf.Max(0, m_idleAnimId);
            m_leftBankingAnimId = Mathf.Max(0, m_leftBankingAnimId);
            m_rightBankingAnimId = Mathf.Max(0, m_rightBankingAnimId);
            m_bankingThreshold = Mathf.Clamp01(m_bankingThreshold);
        }

        private int ResolveAnimationId(int animId, string animationName, int fallbackAnimId)
        {
            if (m_animPlayer.HasAnimation(animId))
            {
                return animId;
            }

            Debug.LogWarning(
                $"PlayerController {animationName} animation ID {animId} was not found. Using fallback animation.",
                this);
            return fallbackAnimId;
        }

        private void UpdateMovementAnimation(float horizontalInput)
        {
            int targetAnimId = m_resolvedIdleAnimId;
            if (horizontalInput < 0f && horizontalInput <= -m_bankingThreshold)
            {
                targetAnimId = m_resolvedLeftBankingAnimId;
            }
            else if (horizontalInput > 0f && horizontalInput >= m_bankingThreshold)
            {
                targetAnimId = m_resolvedRightBankingAnimId;
            }

            ChangeMovementAnimation(targetAnimId);
        }

        private void ChangeMovementAnimation(int animId)
        {
            if (animId == k_invalidAnimId || animId == m_currentMovementAnimId)
            {
                return;
            }

            m_currentMovementAnimId = animId;
            m_animPlayer.ChangeAnim(animId);
        }

        private void Move(Vector2 direction, Rect movementArea)
        {
            Vector3 position = transform.position;
            Vector2 nextPosition = new Vector2(position.x, position.y) + direction * m_speed;
            nextPosition = ClampPosition(nextPosition, movementArea);
            transform.position = new Vector3(nextPosition.x, nextPosition.y, position.z);
        }

        private Vector2 ClampPosition(Vector2 position, Rect movementArea)
        {
            Vector2 halfBodySize = m_bodySize * 0.5f;
            float minX = movementArea.xMin + halfBodySize.x;
            float maxX = movementArea.xMax - halfBodySize.x;
            float minY = movementArea.yMin + halfBodySize.y;
            float maxY = movementArea.yMax - halfBodySize.y;
            return new Vector2(
                Mathf.Clamp(position.x, minX, maxX),
                Mathf.Clamp(position.y, minY, maxY));
        }
    }

}
