using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class StageState : GameSceneState
    {
        private const string k_stageRuntimeName = "StageRuntime";

        private readonly StageSetting m_stageSetting;

        private Transform m_stageRoot;
        private PlayerController m_playerController;

        /// <summary>
        /// Creates a gameplay state backed by the specified stage content.
        /// </summary>
        /// <param name="stageSetting">Stage-specific background and spawn settings.</param>
        public StageState(StageSetting stageSetting)
        {
            m_stageSetting = stageSetting;
        }

        public override void Enter(GameSceneContext ctx)
        {
            if (m_stageSetting == null || m_stageSetting.BackgroundPrefab == null)
            {
                Debug.LogError("StageState requires a valid StageSetting with a background prefab.");
                return;
            }

            m_stageRoot = new GameObject(k_stageRuntimeName).transform;
            Object.Instantiate(
                m_stageSetting.BackgroundPrefab,
                m_stageSetting.BackgroundPosition,
                Quaternion.identity,
                m_stageRoot);

            m_playerController = Object.Instantiate(
                ctx.gameSetting.PlayerPrefab,
                m_stageSetting.PlayerSpawnPosition,
                Quaternion.identity,
                m_stageRoot);
            m_playerController.Initialize();
            ctx.inputActions.Player.Enable();
        }

        public override void Exit(GameSceneContext ctx)
        {
            ctx.inputActions.Player.Disable();

            if (m_stageRoot != null)
            {
                Object.Destroy(m_stageRoot.gameObject);
            }

            m_playerController = null;
            m_stageRoot = null;
        }

        public override void Update(GameSceneContext ctx)
        {
            m_playerController?.Tick(ctx);
        }
    }
}
