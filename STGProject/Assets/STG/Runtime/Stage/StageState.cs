using UnityEngine;
using UnityEngine.SceneManagement;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Loads, runs, and unloads one additive gameplay stage.
    /// </summary>
    public sealed class StageState : GameSceneState
    {
        private const string k_stageRuntimeName = "StageRuntime";

        private readonly StageSetting m_stageSetting;

        private AsyncOperation m_loadOperation;
        private Transform m_stageRoot;
        private StageController m_stageController;
        private PlayerController m_playerController;
        private ComponentPool<Pickup> m_pickupPool;
        private Pickup[] m_activePickups;
        private int m_activePickupCount;
        private bool m_isReady;
        private bool m_isExiting;

        /// <summary>Creates a gameplay state backed by the specified stage scene.</summary>
        public StageState(StageSetting stageSetting)
        {
            m_stageSetting = stageSetting;
        }

        /// <inheritdoc/>
        public override void Enter(GameSceneContext ctx)
        {
            ctx.inputActions.Player.Disable();
            m_isExiting = false;
            if (m_stageSetting == null || string.IsNullOrWhiteSpace(m_stageSetting.SceneName))
            {
                Debug.LogError("StageState requires a valid StageSetting with a scene name.");
                return;
            }

            m_loadOperation = SceneManager.LoadSceneAsync(m_stageSetting.SceneName, LoadSceneMode.Additive);
            if (m_loadOperation == null)
            {
                Debug.LogError($"Unable to start loading stage scene '{m_stageSetting.SceneName}'.", m_stageSetting);
            }
        }

        /// <inheritdoc/>
        public override void Exit(GameSceneContext ctx)
        {
            ctx.inputActions.Player.Disable();
            m_isExiting = true;
            m_pickupPool?.Dispose();
            m_pickupPool = null;
            m_activePickups = null;
            m_activePickupCount = 0;

            if (m_stageRoot != null)
            {
                Object.Destroy(m_stageRoot.gameObject);
            }

            Scene stageScene = SceneManager.GetSceneByName(m_stageSetting.SceneName);
            if (stageScene.IsValid() && stageScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(stageScene);
            }
            else if (m_loadOperation != null && !m_loadOperation.isDone)
            {
                m_loadOperation.completed += UnloadPendingStage;
            }

            m_loadOperation = null;
            m_stageController = null;
            m_playerController = null;
            m_stageRoot = null;
            m_isReady = false;
        }

        /// <inheritdoc/>
        public override void Update(GameSceneContext ctx)
        {
            if (!m_isReady)
            {
                TryCompleteLoad(ctx);
                return;
            }

            m_stageController.TickScroll();
            m_playerController.Tick(ctx);
            TickObstacleCollision(ctx);
            m_stageController.TickSpawners();
            TickPickups(ctx.gameSetting.PickupRecycleArea);
        }

        private void TryCompleteLoad(GameSceneContext ctx)
        {
            if (m_isExiting || m_loadOperation == null || !m_loadOperation.isDone)
            {
                return;
            }

            Scene stageScene = SceneManager.GetSceneByName(m_stageSetting.SceneName);
            GameObject[] roots = stageScene.GetRootGameObjects();
            for (int index = 0; index < roots.Length && m_stageController == null; index++)
            {
                m_stageController = roots[index].GetComponent<StageController>();
            }

            if (m_stageController == null)
            {
                Debug.LogError($"Stage scene '{m_stageSetting.SceneName}' requires exactly one root StageController.");
                m_loadOperation = null;
                return;
            }

            m_stageController.Initialize();
            m_stageRoot = new GameObject(k_stageRuntimeName).transform;
            SceneManager.MoveGameObjectToScene(m_stageRoot.gameObject, stageScene);
            m_playerController = Object.Instantiate(
                ctx.gameSetting.PlayerPrefab,
                m_stageSetting.PlayerSpawnPosition,
                Quaternion.identity,
                m_stageRoot);
            m_playerController.Initialize();
            InitializePickupPool();
            TrySpawnTestPickup();
            ctx.inputActions.Player.Enable();
            m_isReady = true;
            m_loadOperation = null;
        }

        private void TickObstacleCollision(GameSceneContext ctx)
        {
            if (m_playerController.IsDestroyed)
            {
                return;
            }

            Rect playerRect = m_playerController.WorldDamageRect;
            StageObstacle[] obstacles = m_stageController.Obstacles;
            for (int index = 0; index < obstacles.Length; index++)
            {
                if (!playerRect.Overlaps(obstacles[index].WorldDamageRect))
                {
                    continue;
                }

                m_playerController.DestroyByDamage();
                ctx.inputActions.Player.Disable();
                return;
            }
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
            if (m_pickupPool == null)
            {
                return;
            }

            bool canCollect = !m_playerController.IsDestroyed;
            Rect playerRect = canCollect ? m_playerController.WorldPickupCollisionRect : default;
            int index = 0;
            while (index < m_activePickupCount)
            {
                Pickup pickup = m_activePickups[index];
                if (!pickup.Tick(recycleArea))
                {
                    ReturnPickupAt(index);
                    continue;
                }

                if (!canCollect || !playerRect.Overlaps(pickup.WorldCollisionRect))
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

        private void UnloadPendingStage(AsyncOperation operation)
        {
            operation.completed -= UnloadPendingStage;
            Scene stageScene = SceneManager.GetSceneByName(m_stageSetting.SceneName);
            if (stageScene.IsValid() && stageScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(stageScene);
            }
        }
    }
}
