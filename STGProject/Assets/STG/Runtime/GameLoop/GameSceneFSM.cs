using GenjitsuLAB.Core;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class GameSceneFSM : StateMachine<GameSceneState, GameSceneContext>
    {
        public GameSceneFSM(GameSceneContext ctx) : base(ctx)
        {

        }

        public override void Update()
        {
            m_currentState?.Update(m_ctx);
        }


    }
}