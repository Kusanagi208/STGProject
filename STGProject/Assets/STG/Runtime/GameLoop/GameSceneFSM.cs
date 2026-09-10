using GenjitsuLAB.Core;

namespace GenjitsuLAB.STG
{
    public class GameSceneFSM : StateMachine<GameSceneState, GameSceneContext>
    {
        public GameSceneFSM(GameSceneContext ctx) : base(ctx)
        {

        }
    }
}
