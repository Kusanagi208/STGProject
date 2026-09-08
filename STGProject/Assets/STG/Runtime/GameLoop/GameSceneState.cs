using UnityEngine;
using GenjitsuLAB.Core;

namespace GenjitsuLAB.STG
{
    public abstract class GameSceneState : IState<GameSceneContext>
    {
        public virtual void Enter(GameSceneContext ctx)
        {
           
        }

        public virtual void Exit(GameSceneContext ctx)
        {
            
        }

        public virtual void Update(GameSceneContext ctx)
        {
            
        }
    }
}
