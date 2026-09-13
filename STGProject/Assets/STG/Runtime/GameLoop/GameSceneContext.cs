using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class GameSceneContext
    {
        public int frame;
        public float deltaTime;
        public readonly GameSetting gameSetting;
        public readonly InputActions inputActions;

        /// <summary>
        /// Gets the player-life state shared by every stage in the current session.
        /// </summary>
        public PlayerLifeState PlayerLives { get; }

        /// <summary>Gets the persistent presentation surface for stage-flow messages.</summary>
        public StageFlowHud StageFlowHud { get; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal DebugHudController debugHud;
#endif

        /// <summary>
        /// Creates a context with a new life state configured from the game settings.
        /// </summary>
        /// <param name="gameSetting">Shared game configuration.</param>
        /// <param name="inputActions">Input actions owned by the game engine.</param>
        public GameSceneContext(GameSetting gameSetting, InputActions inputActions)
            : this(
                gameSetting,
                inputActions,
                new PlayerLifeState(gameSetting.InitialLifeCount, gameSetting.MaximumLifeCount),
                null)
        {
        }

        /// <summary>
        /// Creates a context with the specified shared session life state.
        /// </summary>
        /// <param name="gameSetting">Shared game configuration.</param>
        /// <param name="inputActions">Input actions owned by the game engine.</param>
        /// <param name="playerLives">Life state shared across gameplay stages.</param>
        public GameSceneContext(
            GameSetting gameSetting,
            InputActions inputActions,
            PlayerLifeState playerLives)
            : this(gameSetting, inputActions, playerLives, null)
        {
        }

        /// <summary>
        /// Creates a context with the specified session life state and stage-flow HUD.
        /// </summary>
        /// <param name="gameSetting">Shared game configuration.</param>
        /// <param name="inputActions">Input actions owned by the game engine.</param>
        /// <param name="playerLives">Life state shared across gameplay stages.</param>
        /// <param name="stageFlowHud">Persistent stage-flow presentation surface.</param>
        public GameSceneContext(
            GameSetting gameSetting,
            InputActions inputActions,
            PlayerLifeState playerLives,
            StageFlowHud stageFlowHud)
        {
            this.gameSetting = gameSetting;
            this.inputActions = inputActions;
            PlayerLives = playerLives;
            StageFlowHud = stageFlowHud;
        }
    }

}
