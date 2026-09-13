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
    public sealed class BossPrototypeTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void BossAssets_AreConfiguredAsThreeIndependentParts()
        {
            Type bossType = RequireRuntimeType("GenjitsuLAB.STG.BossController");
            Type partType = RequireRuntimeType("GenjitsuLAB.STG.BossPartController");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Boss.prefab");
            Assert.That(prefab, Is.Not.Null);
            Component boss = prefab.GetComponent(bossType);
            Assert.That(boss, Is.Not.Null);
            Component[] parts = prefab.GetComponentsInChildren(partType, true);
            Assert.That(parts, Has.Length.EqualTo(3));
            AssertPart(parts, "Left", 30, 30, 90);
            AssertPart(parts, "Center", 50, 60, 120);
            AssertPart(parts, "Right", 30, 75, 90);

            UnityEngine.Object setting = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/STG/Prefab/StageSettingAsset.asset");
            SerializedObject serializedSetting = new SerializedObject(setting);
            Assert.That(serializedSetting.FindProperty("m_bossPrefab").objectReferenceValue, Is.EqualTo(boss));
        }

        [Test]
        public void BossSpawn_StopsAtSevenAndUsesConfiguredShotCadence()
        {
            GameObject instance = InstantiateBoss(out Component boss);
            try
            {
                Invoke(boss, "Spawn", new object[] { new Vector3(0f, 9f, 0f) });
                Type shotType = RequireRuntimeType("GenjitsuLAB.STG.BossShot");
                Array shots = Array.CreateInstance(shotType, 5);
                for (int tick = 0; tick < 66; tick++)
                {
                    Invoke(boss, "TickMovementAndAnimation", Array.Empty<object>());
                    Assert.That((bool)GetProperty(boss, "HasReachedStop"), Is.False);
                    Assert.That(CollectShots(boss, shots), Is.Zero);
                }

                Invoke(boss, "TickMovementAndAnimation", Array.Empty<object>());
                Assert.That((bool)GetProperty(boss, "HasReachedStop"), Is.True);
                Assert.That(instance.transform.position.y, Is.EqualTo(7f).Within(0.0001f));
                for (int tick = 0; tick < 29; tick++)
                {
                    Assert.That(CollectShots(boss, shots), Is.Zero);
                }

                Assert.That(CollectShots(boss, shots), Is.EqualTo(1));
                AssertShotAimsAt(shots.GetValue(0), new Vector2(2f, 1f));
                for (int tick = 0; tick < 29; tick++)
                {
                    Assert.That(CollectShots(boss, shots), Is.Zero);
                }

                Assert.That(CollectShots(boss, shots), Is.EqualTo(3));
                AssertCenterDirections(shots);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BossParts_StopFunctionAndDefeatEventFiresOnce()
        {
            GameObject instance = InstantiateBoss(out Component boss);
            try
            {
                Invoke(boss, "Spawn", new object[] { new Vector3(0f, 7f, 0f) });
                int defeatedCount = 0;
                Action handler = () => defeatedCount++;
                EventInfo defeatedEvent = boss.GetType().GetEvent("Defeated");
                defeatedEvent.AddEventHandler(boss, handler);
                object left = GetProperty(boss, "LeftPart");
                object center = GetProperty(boss, "CenterPart");
                object right = GetProperty(boss, "RightPart");
                DamagePart(left, 29);
                Assert.That((bool)GetProperty(left, "IsOperational"), Is.True);
                DamagePart(left, 1);
                Assert.That((bool)GetProperty(left, "IsOperational"), Is.False);
                Assert.That((int)GetProperty(left, "CurrentHealth"), Is.Zero);
                Rect leftRect = (Rect)GetProperty(left, "WorldDamageRect");
                Assert.That(
                    (bool)Invoke(boss, "OverlapsOperationalPart", new object[] { leftRect }),
                    Is.False);
                Assert.That((bool)GetProperty(boss, "IsDefeated"), Is.False);
                Rect centerRect = (Rect)GetProperty(center, "WorldDamageRect");
                Rect edgeContact = new Rect(centerRect.xMax, centerRect.yMin, 0.2f, centerRect.height);
                Assert.That(centerRect.Overlaps(edgeContact), Is.False);
                DamagePart(center, 50);
                Assert.That((bool)GetProperty(boss, "IsDefeated"), Is.False);
                DamagePart(right, 30);
                Invoke(boss, "RaiseDefeatedIfNeeded", Array.Empty<object>());
                Assert.That((bool)GetProperty(boss, "IsDefeated"), Is.True);
                Assert.That(defeatedCount, Is.EqualTo(1));
                Invoke(boss, "RaiseDefeatedIfNeeded", Array.Empty<object>());
                Assert.That(defeatedCount, Is.EqualTo(1));
                defeatedEvent.RemoveEventHandler(boss, handler);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BossSpawner_TriggersOnceAtActivationBoundary()
        {
            Type spawnerType = RequireRuntimeType("GenjitsuLAB.STG.StageBossSpawner");
            GameObject marker = new GameObject("BossSpawnerTest");
            try
            {
                Component spawner = marker.AddComponent(spawnerType);
                marker.transform.position = new Vector3(0f, 9.01f, 0f);
                Invoke(spawner, "Initialize", Array.Empty<object>());
                Assert.That((bool)Invoke(spawner, "TryTrigger", Array.Empty<object>()), Is.False);
                marker.transform.position = new Vector3(0f, 9f, 0f);
                Assert.That((bool)Invoke(spawner, "TryTrigger", Array.Empty<object>()), Is.True);
                Assert.That((bool)Invoke(spawner, "TryTrigger", Array.Empty<object>()), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(marker);
            }
        }

        [UnityTest]
        public IEnumerator MainScene_FinalSpawnerCreatesBoss()
        {
            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            yield return new EnterPlayMode();
            Type spawnerType = RequireRuntimeType("GenjitsuLAB.STG.StageBossSpawner");
            Type bossType = RequireRuntimeType("GenjitsuLAB.STG.BossController");
            Component spawner = null;
            for (int frame = 0; frame < 300 && spawner == null; frame++)
            {
                Component[] spawners = FindActiveComponents(spawnerType);
                if (spawners.Length > 0)
                {
                    spawner = spawners[0];
                }

                yield return null;
            }

            Assert.That(spawner, Is.Not.Null);
            Vector3 position = spawner.transform.position;
            position.y = 9f;
            spawner.transform.position = position;
            Component boss = null;
            for (int frame = 0; frame < 120 && boss == null; frame++)
            {
                Component[] bosses = FindActiveComponents(bossType);
                if (bosses.Length > 0)
                {
                    boss = bosses[0];
                }

                yield return null;
            }

            Assert.That(boss, Is.Not.Null);
            yield return new ExitPlayMode();
        }

        private static GameObject InstantiateBoss(out Component boss)
        {
            Type bossType = RequireRuntimeType("GenjitsuLAB.STG.BossController");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Boss.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            boss = instance.GetComponent(bossType);
            return instance;
        }

        private static void AssertPart(Component[] parts, string expectedPartType, int health, int firstDelay, int interval)
        {
            for (int index = 0; index < parts.Length; index++)
            {
                if (GetProperty(parts[index], "PartType").ToString() != expectedPartType)
                {
                    continue;
                }

                Assert.That((int)GetProperty(parts[index], "MaxHealth"), Is.EqualTo(health));
                SerializedObject serialized = new SerializedObject(parts[index]);
                Assert.That(serialized.FindProperty("m_firstShotDelayTicks").intValue, Is.EqualTo(firstDelay));
                Assert.That(serialized.FindProperty("m_fireIntervalTicks").intValue, Is.EqualTo(interval));
                Assert.That(serialized.FindProperty("m_firePoint").objectReferenceValue, Is.Not.Null);
                return;
            }

            Assert.Fail($"Boss part not found: {expectedPartType}");
        }

        private static void DamagePart(object part, int count)
        {
            for (int index = 0; index < count; index++)
            {
                Invoke(part, "TakeDamage", new object[] { 1 });
            }
        }

        private static int CollectShots(Component boss, Array shots)
        {
            return (int)Invoke(boss, "CollectShots", new object[] { new Vector2(2f, 1f), shots });
        }

        private static void AssertShotAimsAt(object shot, Vector2 playerPosition)
        {
            Vector3 position = (Vector3)GetProperty(shot, "Position");
            Vector2 direction = (Vector2)GetProperty(shot, "Direction");
            Assert.That(direction, Is.EqualTo((playerPosition - (Vector2)position).normalized));
        }

        private static void AssertCenterDirections(Array shots)
        {
            Assert.That((Vector2)GetProperty(shots.GetValue(0), "Direction"), Is.EqualTo(new Vector2(-0.42261827f, -0.9063078f)));
            Assert.That((Vector2)GetProperty(shots.GetValue(1), "Direction"), Is.EqualTo(Vector2.down));
            Assert.That((Vector2)GetProperty(shots.GetValue(2), "Direction"), Is.EqualTo(new Vector2(0.42261827f, -0.9063078f)));
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

        private static object Invoke(object target, string methodName, object[] arguments)
        {
            return target.GetType().GetMethod(methodName, k_instanceFlags).Invoke(target, arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, k_instanceFlags).GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }
    }
}
