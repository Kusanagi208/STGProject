using UnityEngine;
using GenjitsuLAB.Core;
using GenjitsuLAB.Animation;

namespace GenjitsuLAB.STG
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private AnimationPack m_animationPack;
        [SerializeField] private float m_speed;

        private AnimationPlayer m_animPlayer;
        private Vector2 m_direction;

        public void Initialize()
        {
            m_animPlayer = new AnimationPlayer(m_spriteRenderer, m_animationPack.Clips);
            m_animPlayer.ChangeAnim(0);
        }

        public void Tick(GameSceneContext ctx)
        {
            if (ctx.inputActions.Player.Move.IsPressed())
            {
                m_direction = ctx.inputActions.Player.Move.ReadValue<Vector2>();
                Move();
                //DebugLogger.LogWarningTag(ToString(), m_direction.normalized);
            }

            m_animPlayer.Tick();
        }

        private void Move()
        {
            Vector2 pos = transform.position;
            pos += m_direction * m_speed;
            transform.position = ClampPos(pos);
        }

        private Vector2 ClampPos(Vector2 pos) 
        {
            Vector2 result = pos;
            if (pos.x - 0.5f < -5)
            {
                result.x = -4.5f;
            }
            else if (pos.x + 0.5f > 5)
            {
                result.x = 4.5f;
            }

            if (pos.y < 0)
            {
                result.y = 0;
            }
            else if (pos.y + 1f > 9)
            {
                result.y = 8;
            }

            return result;
        }
    }

}