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
    public sealed class StageFlowTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void PlayerEntry_ReusesInstanceAndReachesConfiguredPositionOnTickEightyFour()
        {
            Type playerType = RequireRuntimeType("GenjitsuLAB.STG.PlayerController");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/STG/Prefab/Player.prefab");
            GameObject playerObject = UnityEngine.Object.Instantiate(prefab);
            Component player = playerObject.GetComponent(playerType);
            try
            {
                Vector3 entryPosition = new Vector3(0f, -1.5f, 0f);
                Vector3 targetPosition = new Vector3(0f, 1f, 0f);
                Invoke(player, "BeginEntry", entryPosition, targetPosition, 0.03f);
                for (int tick = 0; tick < 83; tick++)
                {
                    Assert.That((bool)Invoke(player, "TickEntry"), Is.False);
                }

                Assert.That((bool)Invoke(player, "TickEntry"), Is.True);
                Assert.That(player.transform.position, Is.EqualTo(targetPosition));
                Assert.That((bool)GetProperty(player, "IsEntering"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void StageFlowHud_ShowsStartAndBothTerminalReasonsWithoutRaycasts()
        {
            Type hudType = RequireRuntimeType("GenjitsuLAB.STG.StageFlowHud");
            Type reasonType = RequireRuntimeType("GenjitsuLAB.STG.StageEndReason");
            GameObject canvasObject = new GameObject("StageFlowCanvas", typeof(RectTransform), typeof(Canvas));
            Component hud = canvasObject.AddComponent(hudType);
            try
            {
                Invoke(hud, "ShowStart");
                Assert.That((bool)GetProperty(hud, "IsVisible"), Is.True);
                Assert.That(canvasObject.GetComponentInChildren<Text>().text, Is.EqualTo("START"));

                object bossDefeated = Enum.Parse(reasonType, "BossDefeated");
                Invoke(hud, "ShowGameOver", bossDefeated);
                Assert.That(canvasObject.GetComponentInChildren<Text>().text, Is.EqualTo("GAME OVER"));
                Assert.That(GetProperty(hud, "LastEndReason"), Is.EqualTo(bossDefeated));

                Graphic[] graphics = canvasObject.GetComponentsInChildren<Graphic>(true);
                for (int index = 0; index < graphics.Length; index++)
                {
                    Assert.That(graphics[index].raycastTarget, Is.False);
                }

                Invoke(hud, "Hide");
                Assert.That((bool)GetProperty(hud, "IsVisible"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void MainScene_AndStageSettingsContainFlowDependencies()
        {
            UnityEngine.Object setting = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/STG/Prefab/StageSettingAsset.asset");
            SerializedObject serializedSetting = new SerializedObject(setting);
            Assert.That(serializedSetting.FindProperty("m_playerEntryPosition").vector3Value, Is.EqualTo(new Vector3(0f, -1.5f, 0f)));
            Assert.That(serializedSetting.FindProperty("m_playerSpawnPosition").vector3Value, Is.EqualTo(new Vector3(0f, 1f, 0f)));
            Assert.That(serializedSetting.FindProperty("m_playerEntrySpeedPerTick").floatValue, Is.EqualTo(0.03f));
            Assert.That(serializedSetting.FindProperty("m_startNoticeTicks").intValue, Is.EqualTo(60));

            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            Type engineType = RequireRuntimeType("GenjitsuLAB.STG.MainEngine");
            Component engine = UnityEngine.Object.FindFirstObjectByType(engineType) as Component;
            Assert.That(engine, Is.Not.Null);
            SerializedObject serializedEngine = new SerializedObject(engine);
            Assert.That(serializedEngine.FindProperty("m_stageFlowHud").objectReferenceValue, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MainScene_ConsumesLivesRespawnsSamePlayerAndStopsAtZero()
        {
            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            yield return new EnterPlayMode();

            Type engineType = RequireRuntimeType("GenjitsuLAB.STG.MainEngine");
            Component engine = null;
            object stageState = null;
            yield return WaitForPlaying(engineType, value => engine = value, value => stageState = value);
            object lifeState = GetField(engine, "m_playerLifeState");
            Component originalPlayer = (Component)GetField(stageState, "m_playerController");
            Assert.That((int)GetProperty(lifeState, "CurrentLives"), Is.EqualTo(3));

            for (int expectedLives = 2; expectedLives >= 0; expectedLives--)
            {
                Invoke(originalPlayer, "DestroyByDamage");
                string targetPhase = expectedLives == 0 ? "GameOver" : "PlayerEntering";
                yield return WaitForPhase(stageState, targetPhase, 300);
                Assert.That((int)GetProperty(lifeState, "CurrentLives"), Is.EqualTo(expectedLives));
                Assert.That(GetField(stageState, "m_playerController"), Is.SameAs(originalPlayer));
                if (expectedLives > 0)
                {
                    yield return WaitForPhase(stageState, "Playing", 600);
                }
            }

            Component flowHud = (Component)GetField(engine, "m_stageFlowHud");
            Assert.That((bool)GetProperty(flowHud, "IsVisible"), Is.True);
            Assert.That(GetProperty(flowHud, "LastEndReason").ToString(), Is.EqualTo("LivesDepleted"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MainScene_BossDefeatWinsSameTickAndDoesNotConsumeLife()
        {
            EditorSceneManager.OpenScene("Assets/STG/Scene/Main.unity");
            yield return new EnterPlayMode();

            Type engineType = RequireRuntimeType("GenjitsuLAB.STG.MainEngine");
            Component engine = null;
            object stageState = null;
            yield return WaitForPlaying(engineType, value => engine = value, value => stageState = value);
            object lifeState = GetField(engine, "m_playerLifeState");
            Component player = (Component)GetField(stageState, "m_playerController");
            Invoke(stageState, "OnBossDefeated");
            Invoke(player, "DestroyByDamage");
            yield return WaitForPhase(stageState, "GameOver", 120);

            Assert.That((int)GetProperty(lifeState, "CurrentLives"), Is.EqualTo(3));
            Component flowHud = (Component)GetField(engine, "m_stageFlowHud");
            Assert.That(GetProperty(flowHud, "LastEndReason").ToString(), Is.EqualTo("BossDefeated"));
            yield return new ExitPlayMode();
        }

        private static IEnumerator WaitForPlaying(
            Type engineType,
            Action<Component> setEngine,
            Action<object> setStageState)
        {
            for (int frame = 0; frame < 600; frame++)
            {
                Component engine = UnityEngine.Object.FindFirstObjectByType(engineType) as Component;
                if (engine != null)
                {
                    object stateMachine = GetField(engine, "m_gameSceneFSM");
                    object stageState = GetFieldFromHierarchy(stateMachine, "m_currentState");
                    if (stageState != null && GetField(stageState, "m_phase").ToString() == "Playing")
                    {
                        setEngine(engine);
                        setStageState(stageState);
                        yield break;
                    }
                }

                yield return null;
            }

            Assert.Fail("The Main scene did not reach the Playing phase.");
        }

        private static IEnumerator WaitForPhase(object stageState, string expectedPhase, int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (GetField(stageState, "m_phase").ToString() == expectedPhase)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Stage phase did not become {expectedPhase}.");
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            return target.GetType().GetMethod(methodName, k_instanceFlags).Invoke(target, arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            return target.GetType().GetProperty(propertyName, k_instanceFlags).GetValue(target);
        }

        private static object GetField(object target, string fieldName)
        {
            return target.GetType().GetField(fieldName, k_instanceFlags).GetValue(target);
        }

        private static object GetFieldFromHierarchy(object target, string fieldName)
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, k_instanceFlags);
                if (field != null)
                {
                    return field.GetValue(target);
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }
    }
}
