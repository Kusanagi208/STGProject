using UnityEngine;

namespace GenjitsuLAB.STG
{
    public class StageState : GameSceneState
    {
        private const string k_stageRuntimeName = "StageRuntime";

        private readonly StageSetting m_stageSetting;

        private Transform m_stageRoot;
        private PlayerController m_playerController;
        private ComponentPool<Pickup> m_pickupPool;
        private Pickup[] m_activePickups;
        private int m_activePickupCount;

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
            InitializePickupPool();
            TrySpawnTestPickup();
            ctx.inputActions.Player.Enable();
        }

        public override void Exit(GameSceneContext ctx)
        {
            ctx.inputActions.Player.Disable();

            m_pickupPool?.Dispose();
            m_pickupPool = null;
            m_activePickups = null;
            m_activePickupCount = 0;

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
            TickPickups(ctx.gameSetting.PickupRecycleArea);
        }

        private void InitializePickupPool()
        {
            if (m_stageSetting.PickupPrefab == null)
            {
                Debug.LogError("StageSetting requires a Pickup prefab. Pickup spawning is disabled.", m_stageSetting);
                return;
            }

            int capacity = m_stageSetting.PickupPoolCapacity;
            m_pickupPool = new ComponentPool<Pickup>(m_stageSetting.PickupPrefab, capacity, m_stageRoot);
            m_activePickups = new Pickup[capacity];
            m_activePickupCount = 0;
        }

        private void TrySpawnTestPickup()
        {
            if (m_pickupPool == null || !m_pickupPool.TryRent(out Pickup pickup))
            {
                return;
            }

            pickup.Spawn(m_stageSetting.TestPickupSpawnPosition);
            m_activePickups[m_activePickupCount++] = pickup;
        }

        private void TickPickups(Rect recycleArea)
        {
            if (m_pickupPool == null || m_playerController == null)
            {
                return;
            }

            Rect playerRect = m_playerController.WorldPickupCollisionRect;
            int index = 0;
            while (index < m_activePickupCount)
            {
                Pickup pickup = m_activePickups[index];
                if (!pickup.Tick(recycleArea))
                {
                    ReturnPickupAt(index);
                    continue;
                }

                if (!playerRect.Overlaps(pickup.WorldCollisionRect))
                {
                    index++;
                    continue;
                }

                m_playerController.CollectPickup(pickup.PickupType);
                ReturnPickupAt(index);
            }
        }

        private void ReturnPickupAt(int index)
        {
            Pickup pickup = m_activePickups[index];
            m_pickupPool.Return(pickup);

            int lastIndex = --m_activePickupCount;
            m_activePickups[index] = m_activePickups[lastIndex];
            m_activePickups[lastIndex] = null;
        }
    }
}
