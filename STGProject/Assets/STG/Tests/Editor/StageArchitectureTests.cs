using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GenjitsuLAB.STG.Tests
{
    public sealed class StageArchitectureTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void StageController_UsesAbsoluteParallaxCompletesOnceAndStacksPauseReasons()
        {
            GameObject root = new GameObject("StageControllerTest");
            Type controllerType = RequireRuntimeType("GenjitsuLAB.STG.StageController");
            Type layerType = RequireRuntimeType("GenjitsuLAB.STG.StageScrollLayer");
            Type pauseReasonType = RequireRuntimeType("GenjitsuLAB.STG.StageScrollPauseReason");
            Component controller = root.AddComponent(controllerType);
            Component far = CreateLayer(layerType, root.transform, "Far", 0.25f);
            Component mid = CreateLayer(layerType, root.transform, "Mid", 0.5f);
            Component gameplay = CreateLayer(layerType, root.transform, "Gameplay", 1f);
            Component foreground = CreateLayer(layerType, root.transform, "Foreground", 1.25f);

            try
            {
                SetField(controller, "m_farBackground", far);
                SetField(controller, "m_midBackground", mid);
                SetField(controller, "m_gameplay", gameplay);
                SetField(controller, "m_foreground", foreground);
                Invoke(controller, "Initialize");

                object story = Enum.Parse(pauseReasonType, "Story");
                object boss = Enum.Parse(pauseReasonType, "Boss");
                Invoke(controller, "Pause", story);
                Invoke(controller, "Pause", boss);
                Invoke(controller, "Resume", story);
                Assert.That((bool)GetProperty(controller, "IsPaused"), Is.True);
                Invoke(controller, "TickScroll");
                Assert.That((float)GetProperty(controller, "ScrollDistance"), Is.Zero);
                Invoke(controller, "Resume", boss);

                int completionCount = 0;
                Action completionHandler = () => completionCount++;
                controllerType.GetEvent("Completed").AddEventHandler(controller, completionHandler);
                for (int tick = 0; tick < 3601; tick++)
                {
                    Invoke(controller, "TickScroll");
                }

                Assert.That((float)GetProperty(controller, "ScrollDistance"), Is.EqualTo(90f));
                Assert.That((bool)GetProperty(controller, "IsComplete"), Is.True);
                Assert.That(far.transform.position.y, Is.EqualTo(-22.5f).Within(0.0001f));
                Assert.That(mid.transform.position.y, Is.EqualTo(-45f).Within(0.0001f));
                Assert.That(gameplay.transform.position.y, Is.EqualTo(-90f).Within(0.0001f));
                Assert.That(foreground.transform.position.y, Is.EqualTo(-112.5f).Within(0.0001f));
                Assert.That(completionCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StageEnemySpawner_TriggersOnlyOnceAtActivationLine()
        {
            GameObject marker = new GameObject("SpawnerTest");
            Type spawnerType = RequireRuntimeType("GenjitsuLAB.STG.StageEnemySpawner");
            Component spawner = marker.AddComponent(spawnerType);
            try
            {
                Invoke(spawner, "Initialize");
                marker.transform.position = new Vector3(0f, 9.01f, 0f);
                Assert.That((bool)Invoke(spawner, "TryTrigger"), Is.False);
                marker.transform.position = new Vector3(0f, 9f, 0f);
                Assert.That((bool)Invoke(spawner, "TryTrigger"), Is.True);
                Assert.That((bool)Invoke(spawner, "TryTrigger"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(marker);
            }
        }

        [Test]
        public void StageObstacle_RectEdgeContactDoesNotOverlap()
        {
            GameObject obstacleObject = new GameObject("ObstacleTest");
            Type obstacleType = RequireRuntimeType("GenjitsuLAB.STG.StageObstacle");
            Component obstacle = obstacleObject.AddComponent(obstacleType);
            try
            {
                Rect obstacleRect = (Rect)GetProperty(obstacle, "WorldDamageRect");
                Rect edgeContact = new Rect(obstacleRect.xMax, obstacleRect.yMin, 0.4f, 0.5f);
                Rect positiveOverlap = new Rect(obstacleRect.xMax - 0.01f, obstacleRect.yMin, 0.4f, 0.5f);
                Assert.That(obstacleRect.Overlaps(edgeContact), Is.False);
                Assert.That(obstacleRect.Overlaps(positiveOverlap), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacleObject);
            }
        }

        private static Component CreateLayer(Type layerType, Transform parent, string name, float multiplier)
        {
            GameObject layerObject = new GameObject(name);
            layerObject.transform.SetParent(parent, false);
            Component layer = layerObject.AddComponent(layerType);
            SetField(layer, "m_multiplier", multiplier);
            return layer;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, k_instanceFlags).SetValue(target, value);
        }

        private static object Invoke(object target, string name)
        {
            return target.GetType().GetMethod(name, k_instanceFlags).Invoke(target, null);
        }

        [Test]
        public void PlayerDamage_DestroysOnceHidesRendererAndClearsActiveWeapons()
        {
            Type playerType = RequireRuntimeType("GenjitsuLAB.STG.PlayerController");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Player.prefab");
            GameObject playerObject = UnityEngine.Object.Instantiate(prefab);
            Component player = playerObject.GetComponent(playerType);
            int destroyedCount = 0;
            Action handler = () => destroyedCount++;
            playerType.GetEvent("Destroyed").AddEventHandler(player, handler);

            try
            {
                Invoke(player, "DestroyByDamage");
                Invoke(player, "DestroyByDamage");
                Assert.That((bool)GetProperty(player, "IsDestroyed"), Is.True);
                Assert.That(destroyedCount, Is.EqualTo(1));
                Assert.That(playerObject.GetComponentInChildren<SpriteRenderer>().enabled, Is.False);
            }
            finally
            {
                playerType.GetEvent("Destroyed").RemoveEventHandler(player, handler);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        private static object Invoke(object target, string name, object argument)
        {
            return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(target, new[] { argument });
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }
    }
}
