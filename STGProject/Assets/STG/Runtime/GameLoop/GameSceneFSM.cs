using UnityEngine;
using GenjitsuLAB.Core;

namespace GenjitsuLAB.STG
{
    public class GameSceneFSM : StateMachine<GameSceneState, GameSceneContext>
    {
        public GameSceneFSM(GameSceneContext ctx) : base(ctx)
        {

        }

        public override void Update()
        {
            m_ctx.deltaTime = Time.deltaTime;
            base.Update();
        }
    }
}