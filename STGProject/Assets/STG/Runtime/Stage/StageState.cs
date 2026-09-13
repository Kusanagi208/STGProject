using UnityEngine;
using UnityEngine.SceneManagement;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Loads, runs, and unloads one additive gameplay stage.
    /// </summary>
    public sealed class StageState : GameSceneState
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        , IDebugHudDataSource
#endif
    {
        private const string k_stageRuntimeName = "StageRuntime";

        private enum StageRunPhase
        {
            Loading,
            PlayerEntering,
            StartNotice,
            Playing,
            GameOver
        }

        private readonly StageSetting m_stageSetting;

        private AsyncOperation m_loadOperation;
        private Transform m_stageRoot;
        private StageController m_stageController;
        private PlayerController m_playerController;
        private PlayerLifeState m_playerLifeState;
        private EnemyRuntime m_enemyRuntime;
        private ComponentPool<Pickup> m_pickupPool;
        private Pickup[] m_activePickups;
        private int m_activePickupCount;
        private bool m_isReady;
        private bool m_isExiting;
        private bool m_pendingPlayerDestroyed;
        private bool m_pendingBossDefeated;
        private int m_startNoticeRemainingTicks;
        private StageRunPhase m_phase;

        /// <summary>Creates a gameplay state backed by the specified stage scene.</summary>
        public StageState(StageSetting stageSetting)
        {
            m_stageSetting = stageSetting;
        }

        /// <inheritdoc/>
        public override void Enter(GameSceneContext ctx)
        {
            ctx.inputActions.Player.Disable();
            ctx.StageFlowHud?.Hide();
            m_isExiting = false;
            m_pendingPlayerDestroyed = false;
            m_pendingBossDefeated = false;
            m_phase = StageRunPhase.Loading;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ctx.debugHud?.Unbind(this);
#endif
            m_isExiting = true;
            if (m_stageController != null)
            {
                m_stageController.SpawnerTriggered -= OnSpawnerTriggered;
                m_stageController.BossSpawnerTriggered -= OnBossSpawnerTriggered;
            }

            if (m_playerController != null)
            {
                m_playerController.PickupCollected -= OnPickupCollected;
                m_playerController.Destroyed -= OnPlayerDestroyed;
            }

            if (m_enemyRuntime != null)
            {
                m_enemyRuntime.BossDefeated -= OnBossDefeated;
                m_enemyRuntime.Dispose();
            }

            m_enemyRuntime = null;
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
            m_playerLifeState = null;
            m_stageRoot = null;
            m_isReady = false;
            m_pendingPlayerDestroyed = false;
            m_pendingBossDefeated = false;
            m_phase = StageRunPhase.Loading;
            ctx.StageFlowHud?.Hide();
        }

        /// <inheritdoc/>
        public override void Update(GameSceneContext ctx)
        {
            if (!m_isReady)
            {
                TryCompleteLoad(ctx);
                return;
            }

            switch (m_phase)
            {
                case StageRunPhase.PlayerEntering:
                    TickPlayerEntry(ctx);
                    break;
                case StageRunPhase.StartNotice:
                    TickStartNotice(ctx);
                    break;
                case StageRunPhase.Playing:
                    TickPlaying(ctx);
                    break;
            }
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
                m_stageSetting.PlayerEntryPosition,
                Quaternion.identity,
                m_stageRoot);
            m_playerController.Initialize();
            m_playerLifeState = ctx.PlayerLives;
            m_playerController.PickupCollected += OnPickupCollected;
            m_playerController.Destroyed += OnPlayerDestroyed;
            m_enemyRuntime = new EnemyRuntime(m_stageSetting, m_stageRoot);
            m_enemyRuntime.BossDefeated += OnBossDefeated;
            m_stageController.SpawnerTriggered += OnSpawnerTriggered;
            m_stageController.BossSpawnerTriggered += OnBossSpawnerTriggered;
            InitializePickupPool();
            TrySpawnTestPickup();
            m_isReady = true;
            m_loadOperation = null;
            if (m_playerLifeState == null || m_playerLifeState.CurrentLives <= 0)
            {
                m_playerController.DestroyByDamage();
                m_pendingPlayerDestroyed = false;
                EnterGameOver(ctx, StageEndReason.LivesDepleted);
            }
            else
            {
                BeginPlayerEntry(ctx, StageScrollPauseReason.StageStart);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ctx.debugHud?.Bind(this);
#endif
        }

        private void TickPlayerEntry(GameSceneContext ctx)
        {
            if (!m_playerController.TickEntry())
            {
                return;
            }

            m_startNoticeRemainingTicks = m_stageSetting.StartNoticeTicks;
            ctx.StageFlowHud?.ShowStart();
            m_phase = StageRunPhase.StartNotice;
        }

        private void TickStartNotice(GameSceneContext ctx)
        {
            m_playerController.TickPresentation();
            m_startNoticeRemainingTicks--;
            if (m_startNoticeRemainingTicks > 0)
            {
                return;
            }

            ctx.StageFlowHud?.Hide();
            m_stageController.Resume(StageScrollPauseReason.StageStart);
            m_stageController.Resume(StageScrollPauseReason.PlayerRespawn);
            ctx.inputActions.Player.Enable();
            m_phase = StageRunPhase.Playing;
        }

        private void TickPlaying(GameSceneContext ctx)
        {
            m_stageController.TickScroll();
            m_playerController.Tick(ctx);
            TickObstacleCollision();
            m_enemyRuntime.Tick(
                m_playerController,
                ctx.gameSetting.EnemyRecycleArea,
                ctx.gameSetting.BulletRecycleArea);
            if (m_pendingBossDefeated)
            {
                EnterGameOver(ctx, StageEndReason.BossDefeated);
                return;
            }

            if (m_pendingPlayerDestroyed)
            {
                ResolvePlayerDestroyed(ctx);
                return;
            }

            m_stageController.TickSpawners();
            TickPickups(ctx.gameSetting.PickupRecycleArea);
        }

        private void BeginPlayerEntry(GameSceneContext ctx, StageScrollPauseReason pauseReason)
        {
            ctx.inputActions.Player.Disable();
            ctx.StageFlowHud?.Hide();
            m_stageController.Pause(pauseReason);
            m_playerController.BeginEntry(
                m_stageSetting.PlayerEntryPosition,
                m_stageSetting.PlayerSpawnPosition,
                m_stageSetting.PlayerEntrySpeedPerTick);
            m_pendingPlayerDestroyed = false;
            m_phase = StageRunPhase.PlayerEntering;
        }

        private void ResolvePlayerDestroyed(GameSceneContext ctx)
        {
            m_pendingPlayerDestroyed = false;
            ctx.inputActions.Player.Disable();
            m_enemyRuntime.ReturnAllBullets();
            m_playerLifeState?.TryConsumeLife();
            if (m_playerLifeState == null || m_playerLifeState.CurrentLives <= 0)
            {
                EnterGameOver(ctx, StageEndReason.LivesDepleted);
                return;
            }

            BeginPlayerEntry(ctx, StageScrollPauseReason.PlayerRespawn);
        }

        private void EnterGameOver(GameSceneContext ctx, StageEndReason reason)
        {
            if (m_phase == StageRunPhase.GameOver)
            {
                return;
            }

            m_pendingPlayerDestroyed = false;
            m_pendingBossDefeated = false;
            m_phase = StageRunPhase.GameOver;
            ctx.inputActions.Player.Disable();
            m_stageController.Pause(StageScrollPauseReason.Result);
            m_enemyRuntime.ReturnAllBullets();
            m_playerController.ReturnAllWeapons();
            ctx.StageFlowHud?.ShowGameOver(reason);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <inheritdoc/>
        DebugHudCapacities IDebugHudDataSource.Capacities
        {
            get
            {
                int enemyCapacity = m_stageSetting.StraightEnemyPoolCapacity +
                                    m_stageSetting.ShooterEnemyPoolCapacity;
                int obstacleCount = m_stageController != null && m_stageController.Obstacles != null
                    ? m_stageController.Obstacles.Length
                    : 0;
                int spawnerCount = m_stageController != null && m_stageController.Spawners != null
                    ? m_stageController.Spawners.Length
                    : 0;
                int bossSpawnerCount = m_stageController != null && m_stageController.BossSpawners != null
                    ? m_stageController.BossSpawners.Length
                    : 0;
                int weaponCapacity = m_playerController != null ? m_playerController.WeaponPoolCapacity : 0;
                int primitiveCapacity = 14 + weaponCapacity + (enemyCapacity * 2) +
                                        m_stageSetting.BulletPoolCapacity + m_stageSetting.PickupPoolCapacity +
                                        obstacleCount + ((spawnerCount + bossSpawnerCount) * 3);
                return new DebugHudCapacities(
                    primitiveCapacity,
                    enemyCapacity,
                    m_stageSetting.BulletPoolCapacity,
                    weaponCapacity,
                    m_stageSetting.PickupPoolCapacity,
                    spawnerCount + bossSpawnerCount);
            }
        }

        /// <inheritdoc/>
        void IDebugHudDataSource.Populate(DebugHudFrameBuffer frameBuffer, GameSetting gameSetting)
        {
            frameBuffer.AddRect(gameSetting.PlayerMovementArea, DebugHudVisual.MoveArea, true);
            frameBuffer.AddRect(gameSetting.EnemyRecycleArea, DebugHudVisual.EnemyRecycleArea, true);
            frameBuffer.AddRect(gameSetting.BulletRecycleArea, DebugHudVisual.BulletRecycleArea, true);
            frameBuffer.AddRect(gameSetting.WeaponRecycleArea, DebugHudVisual.WeaponRecycleArea, true);
            frameBuffer.AddRect(gameSetting.PickupRecycleArea, DebugHudVisual.PickupRecycleArea, true);

            if (!m_isReady || m_playerController == null)
            {
                return;
            }

            frameBuffer.AddRect(m_playerController.WorldDamageRect, DebugHudVisual.DamageRect, false);
            frameBuffer.AddRect(m_playerController.WorldPickupCollisionRect, DebugHudVisual.PickupRect, false);
            if (m_playerController.TryGetFirePointPosition(out Vector3 playerFirePoint))
            {
                frameBuffer.AddPoint(playerFirePoint, DebugHudVisual.FirePoint);
            }

            int weaponCount = m_playerController.ActiveWeaponCount;
            for (int index = 0; index < weaponCount; index++)
            {
                frameBuffer.AddRect(
                    m_playerController.GetActiveWeapon(index).WorldDamageRect,
                    DebugHudVisual.DamageRect,
                    false);
            }

            int enemyCount = m_enemyRuntime != null ? m_enemyRuntime.ActiveEnemyCount : 0;
            int bulletCount = m_enemyRuntime != null ? m_enemyRuntime.ActiveBulletCount : 0;
            for (int index = 0; index < enemyCount; index++)
            {
                EnemyController enemy = m_enemyRuntime.GetActiveEnemy(index);
                frameBuffer.AddRect(enemy.WorldDamageRect, DebugHudVisual.DamageRect, false);
                if (enemy.TryGetFirePointPosition(out Vector3 enemyFirePoint))
                {
                    frameBuffer.AddPoint(enemyFirePoint, DebugHudVisual.FirePoint);
                }
            }

            BossController boss = m_enemyRuntime != null ? m_enemyRuntime.ActiveBoss : null;
            if (boss != null)
            {
                AddBossPartDebug(frameBuffer, boss.LeftPart);
                AddBossPartDebug(frameBuffer, boss.CenterPart);
                AddBossPartDebug(frameBuffer, boss.RightPart);
            }

            for (int index = 0; index < bulletCount; index++)
            {
                frameBuffer.AddRect(
                    m_enemyRuntime.GetActiveBullet(index).WorldDamageRect,
                    DebugHudVisual.DamageRect,
                    false);
            }

            StageObstacle[] obstacles = m_stageController.Obstacles;
            for (int index = 0; index < obstacles.Length; index++)
            {
                frameBuffer.AddRect(obstacles[index].WorldDamageRect, DebugHudVisual.DamageRect, false);
            }

            for (int index = 0; index < m_activePickupCount; index++)
            {
                frameBuffer.AddRect(m_activePickups[index].WorldCollisionRect, DebugHudVisual.PickupRect, false);
            }

            StageEnemySpawner[] spawners = m_stageController.Spawners;
            int triggeredSpawnerCount = 0;
            float triggerLineMinX = gameSetting.EnemyRecycleArea.xMin;
            float triggerLineMaxX = gameSetting.EnemyRecycleArea.xMax;
            for (int index = 0; index < spawners.Length; index++)
            {
                StageEnemySpawner spawner = spawners[index];
                DebugHudVisual visual = spawner.IsTriggered
                    ? DebugHudVisual.TriggeredSpawner
                    : DebugHudVisual.Spawner;
                if (spawner.IsTriggered)
                {
                    triggeredSpawnerCount++;
                }

                Vector3 markerPosition = spawner.transform.position;
                Vector3 activationPosition = new Vector3(
                    markerPosition.x,
                    spawner.ActivationY,
                    markerPosition.z);
                frameBuffer.AddPoint(markerPosition, visual);
                frameBuffer.AddLine(markerPosition, activationPosition, visual, false);
                frameBuffer.AddLine(
                    new Vector3(triggerLineMinX, spawner.ActivationY, markerPosition.z),
                    new Vector3(triggerLineMaxX, spawner.ActivationY, markerPosition.z),
                    visual,
                    true);
            }

            StageBossSpawner[] bossSpawners = m_stageController.BossSpawners;
            for (int index = 0; index < bossSpawners.Length; index++)
            {
                StageBossSpawner spawner = bossSpawners[index];
                DebugHudVisual visual = spawner.IsTriggered
                    ? DebugHudVisual.TriggeredSpawner
                    : DebugHudVisual.Spawner;
                if (spawner.IsTriggered)
                {
                    triggeredSpawnerCount++;
                }

                Vector3 markerPosition = spawner.transform.position;
                Vector3 activationPosition = new Vector3(
                    markerPosition.x,
                    spawner.ActivationY,
                    markerPosition.z);
                frameBuffer.AddPoint(markerPosition, visual);
                frameBuffer.AddLine(markerPosition, activationPosition, visual, false);
                frameBuffer.AddLine(
                    new Vector3(triggerLineMinX, spawner.ActivationY, markerPosition.z),
                    new Vector3(triggerLineMaxX, spawner.ActivationY, markerPosition.z),
                    visual,
                    true);
            }

            frameBuffer.Counts = new DebugHudCounts(
                enemyCount,
                bulletCount,
                weaponCount,
                m_activePickupCount,
                triggeredSpawnerCount,
                spawners.Length + bossSpawners.Length);
        }

        private static void AddBossPartDebug(DebugHudFrameBuffer frameBuffer, BossPartController part)
        {
            if (!part.IsOperational)
            {
                return;
            }

            frameBuffer.AddRect(part.WorldDamageRect, DebugHudVisual.DamageRect, false);
            if (part.TryGetFirePointPosition(out Vector3 firePoint))
            {
                frameBuffer.AddPoint(firePoint, DebugHudVisual.FirePoint);
            }
        }
#endif

        private void TickObstacleCollision()
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

        private void OnSpawnerTriggered(StageEnemySpawner spawner)
        {
            m_enemyRuntime.TrySpawn(spawner);
        }

        private void OnBossSpawnerTriggered(StageBossSpawner spawner)
        {
            if (m_enemyRuntime.TrySpawn(spawner))
            {
                m_stageController.Pause(StageScrollPauseReason.Boss);
            }
        }

        private void OnBossDefeated()
        {
            m_pendingBossDefeated = true;
        }

        private void OnPlayerDestroyed()
        {
            m_pendingPlayerDestroyed = true;
        }

        private void OnPickupCollected(PickupType pickupType)
        {
            if (pickupType == PickupType.Test)
            {
                m_playerLifeState?.TryAddLives(1);
            }
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
