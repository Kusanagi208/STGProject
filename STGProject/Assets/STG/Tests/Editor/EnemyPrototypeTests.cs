using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace GenjitsuLAB.STG.Tests
{
    public sealed class EnemyPrototypeTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void EnemyAndBulletAssets_AreConfiguredForFixedCapacityCombat()
        {
            Type enemyType = RequireRuntimeType("GenjitsuLAB.STG.EnemyController");
            Type bulletType = RequireRuntimeType("GenjitsuLAB.STG.Bullet");
            VerifyPrefabComponent("Assets/STG/Prefab/EnemyStraight.prefab", enemyType);
            VerifyPrefabComponent("Assets/STG/Prefab/EnemyShooter.prefab", enemyType);
            VerifyPrefabComponent("Assets/STG/Prefab/EnemyBullet.prefab", bulletType);

            UnityEngine.Object setting = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "Assets/STG/Prefab/StageSettingAsset.asset");
            SerializedObject serialized = new SerializedObject(setting);
            Assert.That(serialized.FindProperty("m_straightEnemyPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("m_shooterEnemyPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("m_bulletPrefab").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("m_straightEnemyPoolCapacity").intValue, Is.EqualTo(8));
            Assert.That(serialized.FindProperty("m_shooterEnemyPoolCapacity").intValue, Is.EqualTo(4));
            Assert.That(serialized.FindProperty("m_bulletPoolCapacity").intValue, Is.EqualTo(32));
        }

        [Test]
        public void PlayerAnimationPack_UsesGeneratedIdleAndBankingSprites()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(
                "Assets/STG/Art/PlayerAnimPack.asset");
            bool foundIdle = false;
            bool foundLeft = false;
            bool foundRight = false;
            for (int index = 0; index < assets.Length; index++)
            {
                SerializedObject serialized = new SerializedObject(assets[index]);
                SerializedProperty animId = serialized.FindProperty("m_animId");
                SerializedProperty elements = serialized.FindProperty("m_elements");
                if (animId == null || elements == null || elements.arraySize == 0)
                {
                    continue;
                }

                Sprite sprite = elements.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("m_sprite")
                    .objectReferenceValue as Sprite;
                Assert.That(sprite, Is.Not.Null);
                if (animId.intValue == 0)
                {
                    foundIdle = sprite.name == "PlayerIdle";
                }
                else if (animId.intValue == 1)
                {
                    foundLeft = sprite.name == "PlayerBankingLeft";
                }
                else if (animId.intValue == 2)
                {
                    foundRight = sprite.name == "PlayerBankingRight";
                }
            }

            Assert.That(foundIdle, Is.True);
            Assert.That(foundLeft, Is.True);
            Assert.That(foundRight, Is.True);
        }

        [Test]
        public void Shooter_StopsThenFiresAtSnapshotDirectionOnConfiguredTicks()
        {
            Type enemyType = RequireRuntimeType("GenjitsuLAB.STG.EnemyController");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/EnemyShooter.prefab");
            GameObject enemyObject = UnityEngine.Object.Instantiate(prefab);
            Component enemy = enemyObject.GetComponent(enemyType);

            try
            {
                Invoke(enemy, "Spawn", new object[] { new Vector3(2f, 9f, 0f) });
                Rect recycleArea = new Rect(-6f, -1f, 12f, 11f);
                for (int tick = 0; tick < 67; tick++)
                {
                    Assert.That((bool)Invoke(enemy, "TickMovement", new object[] { recycleArea }), Is.True);
                }

                Assert.That(enemyObject.transform.position.y, Is.EqualTo(7f).Within(0.0001f));
                Vector2 playerPosition = new Vector2(-1f, 1f);
                for (int tick = 0; tick < 29; tick++)
                {
                    object[] waitingArguments = { playerPosition, null, null };
                    Assert.That((bool)Invoke(enemy, "TryGetFire", waitingArguments), Is.False);
                }

                object[] firingArguments = { playerPosition, null, null };
                Assert.That((bool)Invoke(enemy, "TryGetFire", firingArguments), Is.True);
                Vector3 firePosition = (Vector3)firingArguments[1];
                Vector2 direction = (Vector2)firingArguments[2];
                Assert.That(direction, Is.EqualTo((playerPosition - (Vector2)firePosition).normalized));

                for (int tick = 0; tick < 89; tick++)
                {
                    object[] waitingArguments = { Vector2.zero, null, null };
                    Assert.That((bool)Invoke(enemy, "TryGetFire", waitingArguments), Is.False);
                }

                object[] secondShotArguments = { Vector2.zero, null, null };
                Assert.That((bool)Invoke(enemy, "TryGetFire", secondShotArguments), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
            }
        }

        [Test]
        public void Bullet_KeepsLaunchDirectionAndUsesPositiveAreaOverlap()
        {
            Type bulletType = RequireRuntimeType("GenjitsuLAB.STG.Bullet");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/EnemyBullet.prefab");
            GameObject bulletObject = UnityEngine.Object.Instantiate(prefab);
            Component bullet = bulletObject.GetComponent(bulletType);

            try
            {
                Invoke(bullet, "Spawn", new object[] { Vector3.zero, new Vector2(3f, -4f) });
                Vector2 launchDirection = (Vector2)GetProperty(bullet, "Direction");
                Assert.That(launchDirection, Is.EqualTo(new Vector2(0.6f, -0.8f)));
                Invoke(bullet, "Tick", new object[] { new Rect(-6f, -1f, 12f, 11f) });
                Assert.That(bulletObject.transform.position.x, Is.EqualTo(0.048f).Within(0.0001f));
                Assert.That(bulletObject.transform.position.y, Is.EqualTo(-0.064f).Within(0.0001f));

                Rect bulletRect = (Rect)GetProperty(bullet, "WorldDamageRect");
                Rect edgeContact = new Rect(bulletRect.xMax, bulletRect.yMin, 0.4f, 0.5f);
                Assert.That(bulletRect.Overlaps(edgeContact), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bulletObject);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_SpawnersCreateBothEnemyTypes()
        {
            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            yield return new EnterPlayMode();

            Type spawnerType = RequireRuntimeType("GenjitsuLAB.STG.StageEnemySpawner");
            Type enemyType = RequireRuntimeType("GenjitsuLAB.STG.EnemyController");
            Component[] spawners = null;
            for (int frame = 0; frame < 300; frame++)
            {
                spawners = FindActiveComponents(spawnerType);
                if (spawners.Length == 2)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(spawners, Has.Length.EqualTo(2));
            for (int index = 0; index < spawners.Length; index++)
            {
                Vector3 position = spawners[index].transform.position;
                position.y = 9f;
                spawners[index].transform.position = position;
            }

            Component[] enemies = null;
            for (int frame = 0; frame < 120; frame++)
            {
                enemies = FindActiveComponents(enemyType);
                if (enemies.Length == 2)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(enemies, Has.Length.EqualTo(2));
            bool foundStraight = false;
            bool foundShooter = false;
            for (int index = 0; index < enemies.Length; index++)
            {
                string typeName = GetProperty(enemies[index], "EnemyType").ToString();
                foundStraight |= typeName == "Straight";
                foundShooter |= typeName == "Shooter";
            }

            Assert.That(foundStraight, Is.True);
            Assert.That(foundShooter, Is.True);
            yield return new ExitPlayMode();
        }

        private static void VerifyPrefabComponent(string path, Type componentType)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponent(componentType), Is.Not.Null, path);
        }

        private static Component[] FindActiveComponents(Type componentType)
        {
            UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(componentType);
            Component[] results = new Component[candidates.Length];
            int count = 0;
            for (int index = 0; index < candidates.Length; index++)
            {
                Component candidate = candidates[index] as Component;
                if (candidate == null || !candidate.gameObject.scene.IsValid() || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                results[count++] = candidate;
            }

            Array.Resize(ref results, count);
            return results;
        }

        private static object Invoke(object target, string name, object[] arguments)
        {
            return target.GetType().GetMethod(name, k_instanceFlags).Invoke(target, arguments);
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().GetProperty(name, k_instanceFlags).GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }
    }
}
