using GenjitsuLAB.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GenjitsuLAB.STG
{
    public class StageState : GameSceneState
    {
        private PlayerController m_playerController;
        private Transform m_background;

        public override void Enter(GameSceneContext ctx)
        {
            m_background = Object.Instantiate(ctx.gameSetting.background);
            m_background.position = Vector3.zero;
            m_playerController = Object.Instantiate(ctx.gameSetting.playerPrefab);
            m_playerController.transform.position = Vector3.zero + Vector3.up;
            m_playerController.Initialize();
        }

        public override void Exit(GameSceneContext ctx)
        {
            
        }

        public override void Update(GameSceneContext ctx)
        {
            m_playerController?.Tick(ctx);

        }

    }
}