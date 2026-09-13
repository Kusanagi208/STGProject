using System;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Owns fixed-capacity enemy and enemy-bullet simulation for one stage run.
    /// </summary>
    internal sealed class EnemyRuntime : IDisposable
    {
        private readonly ComponentPool<EnemyController> m_straightPool;
        private readonly ComponentPool<EnemyController> m_shooterPool;
        private readonly ComponentPool<Bullet> m_bulletPool;
        private readonly EnemyController[] m_activeEnemies;
        private readonly Bullet[] m_activeBullets;
        private readonly BossController m_bossPrefab;
        private readonly BossShot[] m_bossShots;
        private readonly Transform m_parent;

        private int m_activeEnemyCount;
        private int m_activeBulletCount;
        private BossController m_activeBoss;
        private bool m_isDisposed;
        private bool m_bossDefeatedThisTick;

        internal EnemyRuntime(StageSetting setting, Transform parent)
        {
            m_parent = parent;
            m_bossPrefab = setting.BossPrefab;
            m_straightPool = new ComponentPool<EnemyController>(
                setting.StraightEnemyPrefab,
                setting.StraightEnemyPoolCapacity,
                parent);
            m_shooterPool = new ComponentPool<EnemyController>(
                setting.ShooterEnemyPrefab,
                setting.ShooterEnemyPoolCapacity,
                parent);
            m_bulletPool = new ComponentPool<Bullet>(
                setting.BulletPrefab,
                setting.BulletPoolCapacity,
                parent);
            m_activeEnemies = new EnemyController[
                setting.StraightEnemyPoolCapacity + setting.ShooterEnemyPoolCapacity];
            m_activeBullets = new Bullet[setting.BulletPoolCapacity];
            m_bossShots = new BossShot[5];
        }

        internal event Action BossDefeated;

        internal int ActiveEnemyCount => m_activeEnemyCount;

        internal int ActiveBulletCount => m_activeBulletCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets the active level-scoped boss for runtime diagnostics.</summary>
        internal BossController ActiveBoss => m_activeBoss;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Gets an active enemy by its dense runtime index for diagnostics.</summary>
        internal EnemyController GetActiveEnemy(int index)
        {
            return m_activeEnemies[index];
        }

        /// <summary>Gets an active enemy bullet by its dense runtime index for diagnostics.</summary>
        internal Bullet GetActiveBullet(int index)
        {
            return m_activeBullets[index];
        }
#endif

        internal void TrySpawn(StageEnemySpawner spawner)
        {
            ComponentPool<EnemyController> pool = spawner.EnemyType == EnemyType.Shooter
                ? m_shooterPool
                : m_straightPool;
            if (!pool.TryRent(out EnemyController enemy))
            {
                return;
            }

            enemy.Spawn(spawner.transform.position);
            m_activeEnemies[m_activeEnemyCount++] = enemy;
        }

        internal bool TrySpawn(StageBossSpawner spawner)
        {
            if (m_activeBoss != null || m_bossPrefab == null)
            {
                return false;
            }

            m_activeBoss = UnityEngine.Object.Instantiate(
                m_bossPrefab,
                spawner.transform.position,
                Quaternion.identity,
                m_parent);
            m_activeBoss.Defeated += OnBossDefeated;
            m_activeBoss.Spawn(spawner.transform.position);
            return true;
        }

        internal bool Tick(PlayerController player, Rect enemyRecycleArea, Rect bulletRecycleArea)
        {
            m_bossDefeatedThisTick = false;
            bool wasPlayerDestroyed = player.IsDestroyed;
            TickEnemyMovement(enemyRecycleArea);
            m_activeBoss?.TickMovementAndAnimation();
            ResolveWeaponHits(player);
            m_activeBoss?.ResolveWeaponHits(player);
            if (m_bossDefeatedThisTick)
            {
                return false;
            }

            ResolveBodyHits(player);
            ResolveBossBodyHit(player);
            TickEnemyFire(player);
            TickBossFire(player);
            TickBullets(player, bulletRecycleArea);
            return !wasPlayerDestroyed && player.IsDestroyed;
        }

        /// <summary>Returns every active enemy bullet to the fixed-capacity pool.</summary>
        internal void ReturnAllBullets()
        {
            m_bulletPool.ReturnAll();
            Array.Clear(m_activeBullets, 0, m_activeBulletCount);
            m_activeBulletCount = 0;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            m_straightPool.Dispose();
            m_shooterPool.Dispose();
            m_bulletPool.Dispose();
            if (m_activeBoss != null)
            {
                m_activeBoss.Defeated -= OnBossDefeated;
                UnityEngine.Object.Destroy(m_activeBoss.gameObject);
                m_activeBoss = null;
            }

            BossDefeated = null;
            Array.Clear(m_activeEnemies, 0, m_activeEnemyCount);
            Array.Clear(m_activeBullets, 0, m_activeBulletCount);
            m_activeEnemyCount = 0;
            m_activeBulletCount = 0;
        }

        private void TickEnemyMovement(Rect recycleArea)
        {
            int index = 0;
            while (index < m_activeEnemyCount)
            {
                EnemyController enemy = m_activeEnemies[index];
                if (enemy.TickMovement(recycleArea))
                {
                    index++;
                    continue;
                }

                ReturnEnemyAt(index);
            }
        }

        private void ResolveWeaponHits(PlayerController player)
        {
            if (player.IsDestroyed)
            {
                return;
            }

            int index = 0;
            while (index < m_activeEnemyCount)
            {
                EnemyController enemy = m_activeEnemies[index];
                if (!player.TryConsumeWeaponHit(enemy.WorldDamageRect))
                {
                    index++;
                    continue;
                }

                enemy.DestroyByDamage();
                ReturnEnemyAt(index);
            }
        }

        private void ResolveBodyHits(PlayerController player)
        {
            if (player.IsDestroyed)
            {
                return;
            }

            Rect playerRect = player.WorldDamageRect;
            int index = 0;
            while (index < m_activeEnemyCount)
            {
                EnemyController enemy = m_activeEnemies[index];
                if (!playerRect.Overlaps(enemy.WorldDamageRect))
                {
                    index++;
                    continue;
                }

                enemy.DestroyByDamage();
                ReturnEnemyAt(index);
                player.DestroyByDamage();
                return;
            }
        }

        private void TickEnemyFire(PlayerController player)
        {
            if (player.IsDestroyed)
            {
                return;
            }

            Vector2 playerPosition = player.transform.position;
            for (int index = 0; index < m_activeEnemyCount; index++)
            {
                EnemyController enemy = m_activeEnemies[index];
                if (!enemy.TryGetFire(playerPosition, out Vector3 firePosition, out Vector2 direction))
                {
                    continue;
                }

                if (!m_bulletPool.TryRent(out Bullet bullet))
                {
                    continue;
                }

                bullet.Spawn(firePosition, direction);
                m_activeBullets[m_activeBulletCount++] = bullet;
            }
        }

        private void TickBullets(PlayerController player, Rect recycleArea)
        {
            int index = 0;
            while (index < m_activeBulletCount)
            {
                Bullet bullet = m_activeBullets[index];
                if (!bullet.Tick(recycleArea))
                {
                    ReturnBulletAt(index);
                    continue;
                }

                if (!player.IsDestroyed && player.WorldDamageRect.Overlaps(bullet.WorldDamageRect))
                {
                    player.DestroyByDamage();
                    ReturnBulletAt(index);
                    continue;
                }

                index++;
            }
        }

        private void ResolveBossBodyHit(PlayerController player)
        {
            if (player.IsDestroyed || m_activeBoss == null)
            {
                return;
            }

            if (m_activeBoss.OverlapsOperationalPart(player.WorldDamageRect))
            {
                player.DestroyByDamage();
            }
        }

        private void TickBossFire(PlayerController player)
        {
            if (player.IsDestroyed || m_activeBoss == null)
            {
                return;
            }

            int shotCount = m_activeBoss.CollectShots(player.transform.position, m_bossShots);
            for (int index = 0; index < shotCount; index++)
            {
                if (!m_bulletPool.TryRent(out Bullet bullet))
                {
                    continue;
                }

                BossShot shot = m_bossShots[index];
                bullet.Spawn(shot.Position, shot.Direction);
                m_activeBullets[m_activeBulletCount++] = bullet;
            }
        }

        private void OnBossDefeated()
        {
            m_bossDefeatedThisTick = true;
            BossDefeated?.Invoke();
        }

        private void ReturnEnemyAt(int index)
        {
            EnemyController enemy = m_activeEnemies[index];
            if (enemy.EnemyType == EnemyType.Shooter)
            {
                m_shooterPool.Return(enemy);
            }
            else
            {
                m_straightPool.Return(enemy);
            }

            int lastIndex = --m_activeEnemyCount;
            m_activeEnemies[index] = m_activeEnemies[lastIndex];
            m_activeEnemies[lastIndex] = null;
        }

        private void ReturnBulletAt(int index)
        {
            Bullet bullet = m_activeBullets[index];
            m_bulletPool.Return(bullet);
            int lastIndex = --m_activeBulletCount;
            m_activeBullets[index] = m_activeBullets[lastIndex];
            m_activeBullets[lastIndex] = null;
        }
    }
}
