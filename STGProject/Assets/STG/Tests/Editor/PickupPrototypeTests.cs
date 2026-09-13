using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GenjitsuLAB.STG.Tests
{
    public sealed class PickupPrototypeTests
    {
        private const string k_pickupPrefabPath = "Assets/STG/Prefab/Pickup.prefab";
        private const string k_pickupAnimationPath = "Assets/STG/Art/PickupAnimPack.asset";
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void PickupAssets_AreWiredForFourFrameLoop()
        {
            Type pickupType = RequireRuntimeType("GenjitsuLAB.STG.Pickup");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_pickupPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Component pickup = prefab.GetComponent(pickupType);
            Assert.That(pickup, Is.Not.Null);

            SerializedObject pickupObject = new SerializedObject(pickup);
            Assert.That(pickupObject.FindProperty("m_spriteRenderer").objectReferenceValue, Is.Not.Null);
            Assert.That(pickupObject.FindProperty("m_animationPack").objectReferenceValue, Is.Not.Null);
            Assert.That(pickupObject.FindProperty("m_speedPerTick").floatValue, Is.EqualTo(0.03f));

            UnityEngine.Object[] animationAssets = AssetDatabase.LoadAllAssetsAtPath(k_pickupAnimationPath);
            UnityEngine.Object clip = null;
            for (int index = 0; index < animationAssets.Length; index++)
            {
                if (animationAssets[index] != null && animationAssets[index].GetType().Name == "AnimationClip")
                {
                    clip = animationAssets[index];
                    break;
                }
            }

            Assert.That(clip, Is.Not.Null);
            SerializedObject clipObject = new SerializedObject(clip);
            Assert.That(clipObject.FindProperty("m_isLoop").boolValue, Is.True);
            SerializedProperty elements = clipObject.FindProperty("m_elements");
            Assert.That(elements.arraySize, Is.EqualTo(4));

            for (int index = 0; index < elements.arraySize; index++)
            {
                SerializedProperty element = elements.GetArrayElementAtIndex(index);
                Assert.That(element.FindPropertyRelative("m_duration").intValue, Is.EqualTo(6));
                Assert.That(element.FindPropertyRelative("m_sprite").objectReferenceValue, Is.Not.Null);
            }
        }

        [Test]
        public void RectOverlaps_RequiresPositiveIntersectionArea()
        {
            Rect player = new Rect(-0.25f, -0.35f, 0.5f, 0.7f);

            Assert.That(player.Overlaps(new Rect(0.1f, 0.1f, 0.75f, 0.75f)), Is.True);
            Assert.That(player.Overlaps(new Rect(-0.1f, -0.1f, 0.2f, 0.2f)), Is.True);
            Assert.That(player.Overlaps(new Rect(2f, 2f, 0.75f, 0.75f)), Is.False);
            Assert.That(player.Overlaps(new Rect(player.xMax, player.yMin, 0.75f, 0.75f)), Is.False);
        }

        [UnityTest]
        public IEnumerator PickupRuntime_ResetsAnimationMovesAndEmitsTypedEvent()
        {
            yield return new EnterPlayMode();

            Type pickupType = RequireRuntimeType("GenjitsuLAB.STG.Pickup");
            Type playerType = RequireRuntimeType("GenjitsuLAB.STG.PlayerController");
            GameObject pickupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_pickupPrefabPath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Player.prefab");
            GameObject pickupObject = UnityEngine.Object.Instantiate(pickupPrefab);
            GameObject playerObject = UnityEngine.Object.Instantiate(playerPrefab);

            try
            {
                yield return null;

                Component pickup = pickupObject.GetComponent(pickupType);
                Component player = playerObject.GetComponent(playerType);
                MethodInfo spawn = pickupType.GetMethod("Spawn", k_instanceFlags);
                MethodInfo tick = pickupType.GetMethod("Tick", k_instanceFlags);
                MethodInfo collect = playerType.GetMethod("CollectPickup", k_instanceFlags);
                Assert.That(spawn, Is.Not.Null);
                Assert.That(tick, Is.Not.Null);
                Assert.That(collect, Is.Not.Null);

                spawn.Invoke(pickup, new object[] { new Vector3(0f, 8f, 0f) });
                SpriteRenderer renderer = pickupObject.GetComponent<SpriteRenderer>();
                Sprite firstSprite = renderer.sprite;
                Rect recycleArea = new Rect(-6f, -1f, 12f, 11f);

                for (int tickIndex = 0; tickIndex < 6; tickIndex++)
                {
                    Assert.That((bool)tick.Invoke(pickup, new object[] { recycleArea }), Is.True);
                }

                Assert.That(pickupObject.transform.position.y, Is.EqualTo(7.82f).Within(0.0001f));
                Assert.That(renderer.sprite, Is.Not.SameAs(firstSprite));

                spawn.Invoke(pickup, new object[] { new Vector3(0f, 8f, 0f) });
                Assert.That(renderer.sprite, Is.SameAs(firstSprite));

                EventInfo collectedEvent = playerType.GetEvent("PickupCollected");
                Type pickupEnumType = RequireRuntimeType("GenjitsuLAB.STG.PickupType");
                PickupEventSink sink = new PickupEventSink();
                MethodInfo callback = typeof(PickupEventSink)
                    .GetMethod(nameof(PickupEventSink.Handle), k_instanceFlags)
                    .MakeGenericMethod(pickupEnumType);
                Delegate handler = Delegate.CreateDelegate(collectedEvent.EventHandlerType, sink, callback);
                collectedEvent.AddEventHandler(player, handler);

                object testValue = Enum.ToObject(pickupEnumType, 0);
                collect.Invoke(player, new[] { testValue });
                Assert.That(sink.Count, Is.EqualTo(1));
                Assert.That(Convert.ToInt32(sink.Value), Is.EqualTo(0));
                collectedEvent.RemoveEventHandler(player, handler);

                VerifyFixedCapacityPool(pickupType, pickup);
            }
            finally
            {
                UnityEngine.Object.Destroy(pickupObject);
                UnityEngine.Object.Destroy(playerObject);
            }

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MainScene_PickupIsSpawnedCollectedOnceAndReturnedToPool()
        {
            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            yield return new EnterPlayMode();

            Type pickupType = RequireRuntimeType("GenjitsuLAB.STG.Pickup");
            Type playerType = RequireRuntimeType("GenjitsuLAB.STG.PlayerController");
            Type lifeHudType = RequireRuntimeType("GenjitsuLAB.STG.PlayerLifeHud");
            for (int frameIndex = 0; frameIndex < 300; frameIndex++)
            {
                if (FindActiveSceneComponent(pickupType) != null &&
                    FindActiveSceneComponent(playerType) != null &&
                    FindActiveSceneComponent(lifeHudType) != null)
                {
                    break;
                }

                yield return null;
            }

            Component pickup = FindSingleActiveSceneComponent(pickupType);
            Component player = FindSingleActiveSceneComponent(playerType);
            Component lifeHud = FindSingleActiveSceneComponent(lifeHudType);
            Text lifeCountText = lifeHud.GetComponentInChildren<Text>();
            Assert.That(pickup.transform.position.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(pickup.transform.position.y, Is.InRange(7.8f, 8f));
            Assert.That(lifeCountText, Is.Not.Null);
            Assert.That(lifeCountText.text, Is.EqualTo("x 03"));

            EventInfo collectedEvent = playerType.GetEvent("PickupCollected");
            Type pickupEnumType = RequireRuntimeType("GenjitsuLAB.STG.PickupType");
            PickupEventSink sink = new PickupEventSink();
            MethodInfo callback = typeof(PickupEventSink)
                .GetMethod(nameof(PickupEventSink.Handle), k_instanceFlags)
                .MakeGenericMethod(pickupEnumType);
            Delegate handler = Delegate.CreateDelegate(collectedEvent.EventHandlerType, sink, callback);
            collectedEvent.AddEventHandler(player, handler);

            pickup.transform.position = player.transform.position;
            for (int frameIndex = 0; frameIndex < 120 && sink.Count == 0; frameIndex++)
            {
                yield return null;
            }

            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(Convert.ToInt32(sink.Value), Is.EqualTo(0));
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            Assert.That(lifeCountText.text, Is.EqualTo("x 04"));
            collectedEvent.RemoveEventHandler(player, handler);

            yield return new ExitPlayMode();
        }

        private static void VerifyFixedCapacityPool(Type pickupType, Component prefab)
        {
            Type poolType = RequireRuntimeType("GenjitsuLAB.STG.ComponentPool`1").MakeGenericType(pickupType);
            GameObject poolRoot = new GameObject("PickupPoolCapacityTest");
            object pool = Activator.CreateInstance(poolType, prefab, 4, poolRoot.transform);
            MethodInfo tryRent = poolType.GetMethod("TryRent", k_instanceFlags);
            MethodInfo returnItem = poolType.GetMethod("Return", k_instanceFlags);
            object firstItem = null;

            try
            {
                for (int index = 0; index < 4; index++)
                {
                    object[] arguments = { null };
                    Assert.That((bool)tryRent.Invoke(pool, arguments), Is.True);
                    if (index == 0)
                    {
                        firstItem = arguments[0];
                    }
                }

                object[] fullPoolArguments = { null };
                Assert.That((bool)tryRent.Invoke(pool, fullPoolArguments), Is.False);
                Assert.That(fullPoolArguments[0], Is.Null);

                returnItem.Invoke(pool, new[] { firstItem });
                object[] rerentArguments = { null };
                Assert.That((bool)tryRent.Invoke(pool, rerentArguments), Is.True);
                Assert.That(rerentArguments[0], Is.SameAs(firstItem));
            }
            finally
            {
                ((IDisposable)pool).Dispose();
                UnityEngine.Object.Destroy(poolRoot);
            }
        }

        private static Component FindSingleActiveSceneComponent(Type componentType)
        {
            UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(componentType);
            Component result = null;
            int count = 0;

            for (int index = 0; index < candidates.Length; index++)
            {
                Component candidate = candidates[index] as Component;
                if (candidate == null || !candidate.gameObject.scene.IsValid() || !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                result = candidate;
                count++;
            }

            Assert.That(count, Is.EqualTo(1), $"Expected one active {componentType.Name} in the loaded scene.");
            return result;
        }

        private static Component FindActiveSceneComponent(Type componentType)
        {
            UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(componentType);
            for (int index = 0; index < candidates.Length; index++)
            {
                Component candidate = candidates[index] as Component;
                if (candidate != null && candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }

        private sealed class PickupEventSink
        {
            public int Count { get; private set; }
            public object Value { get; private set; }

            public void Handle<T>(T value)
            {
                Count++;
                Value = value;
            }
        }
    }
}
