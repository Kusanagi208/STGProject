using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class GameSceneContext
    {
        public int frame;
        public float deltaTime;
        public readonly GameSetting gameSetting;
        public readonly InputActions inputActions;

        public GameSceneContext(GameSetting gameSetting, InputActions inputActions)
        {
            this.gameSetting = gameSetting;
            this.inputActions = inputActions;
        }
    }

}