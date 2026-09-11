#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GenjitsuLAB.STG.Tests
{
    public sealed class DebugHudTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags k_staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void FrameBuffer_UsesFixedCapacityAndClearsWithoutReplacement()
        {
            Type bufferType = RequireRuntimeType("GenjitsuLAB.STG.DebugHudFrameBuffer");
            Type visualType = RequireRuntimeType("GenjitsuLAB.STG.DebugHudVisual");
            object buffer = Activator.CreateInstance(
                bufferType,
                k_instanceFlags,
                null,
                new object[] { 2 },
                null);
            MethodInfo addRect = bufferType.GetMethod("AddRect", k_instanceFlags);
            object damageVisual = Enum.Parse(visualType, "DamageRect");

            addRect.Invoke(buffer, new[] { (object)new Rect(0f, 0f, 1f, 1f), damageVisual, false });
            addRect.Invoke(buffer, new[] { (object)new Rect(1f, 1f, 1f, 1f), damageVisual, false });
            addRect.Invoke(buffer, new[] { (object)new Rect(2f, 2f, 1f, 1f), damageVisual, false });

            Assert.That(GetProperty<int>(buffer, "Count"), Is.EqualTo(2));
            Array backingArray = (Array)bufferType.GetField("m_primitives", k_instanceFlags).GetValue(buffer);
            bufferType.GetMethod("Clear", k_instanceFlags).Invoke(buffer, null);
            Assert.That(GetProperty<int>(buffer, "Count"), Is.Zero);
            Assert.That(bufferType.GetField("m_primitives", k_instanceFlags).GetValue(buffer), Is.SameAs(backingArray));
        }

        [Test]
        public void Graphic_LineClipRejectsOutsideAndClipsCrossingSegment()
        {
            Type graphicType = RequireRuntimeType("GenjitsuLAB.STG.DebugHudGraphic");
            MethodInfo tryClipLine = graphicType.GetMethod("TryClipLine", k_staticFlags);
            Rect clipRect = new Rect(0f, 0f, 10f, 10f);

            object[] crossingArguments = { clipRect, new Vector2(-5f, 5f), new Vector2(15f, 5f) };
            Assert.That((bool)tryClipLine.Invoke(null, crossingArguments), Is.True);
            Assert.That((Vector2)crossingArguments[1], Is.EqualTo(new Vector2(0f, 5f)));
            Assert.That((Vector2)crossingArguments[2], Is.EqualTo(new Vector2(10f, 5f)));

            object[] outsideArguments = { clipRect, new Vector2(-5f, 11f), new Vector2(15f, 11f) };
            Assert.That((bool)tryClipLine.Invoke(null, outsideArguments), Is.False);
        }

        [Test]
        public void RuntimeDebugAccess_RemainsInternalOrPrivate()
        {
            Type dataSourceType = RequireRuntimeType("GenjitsuLAB.STG.IDebugHudDataSource");
            Type stageStateType = RequireRuntimeType("GenjitsuLAB.STG.StageState");
            Assert.That(dataSourceType.IsPublic, Is.False);
            Assert.That(dataSourceType.IsAssignableFrom(stageStateType), Is.True);

            Type mainEngineType = RequireRuntimeType("GenjitsuLAB.STG.MainEngine");
            Assert.That(mainEngineType.GetField("m_debugCamera", k_instanceFlags).IsPrivate, Is.True);
            Assert.That(mainEngineType.GetField("m_debugCanvas", k_instanceFlags).IsPrivate, Is.True);
            Assert.That(mainEngineType.GetField("m_debugHud", k_instanceFlags).IsPrivate, Is.True);
        }

        [Test]
        public void Controller_StartsHiddenTogglesVisibleAndDoesNotBlockRaycasts()
        {
            GameObject cameraObject = new GameObject("DebugHudTestCamera", typeof(Camera));
            GameObject canvasObject = new GameObject("DebugHudTestCanvas", typeof(Canvas));
            ScriptableObject gameSetting = ScriptableObject.CreateInstance(
                RequireRuntimeType("GenjitsuLAB.STG.GameSetting"));
            Type controllerType = RequireRuntimeType("GenjitsuLAB.STG.DebugHudController");
            MethodInfo tryCreate = controllerType.GetMethod("TryCreate", k_staticFlags);
            object controller = tryCreate.Invoke(
                null,
                new object[]
                {
                    cameraObject.GetComponent<Camera>(),
                    canvasObject.GetComponent<Canvas>(),
                    gameSetting,
                    null
                });
            GameObject root = (GameObject)controllerType.GetField("m_root", k_instanceFlags).GetValue(controller);

            try
            {
                Assert.That(root.activeSelf, Is.False);
                controllerType.GetMethod("Toggle", k_instanceFlags).Invoke(controller, null);
                Assert.That(root.activeSelf, Is.True);

                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
                Assert.That(graphics.Length, Is.GreaterThan(0));
                for (int index = 0; index < graphics.Length; index++)
                {
                    Assert.That(graphics[index].raycastTarget, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
                UnityEngine.Object.DestroyImmediate(gameSetting);
            }
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)target.GetType().GetProperty(propertyName, k_instanceFlags).GetValue(target);
        }

        private static Type RequireRuntimeType(string fullName)
        {
            Type type = Type.GetType($"{fullName}, Assembly-CSharp");
            Assert.That(type, Is.Not.Null, $"Runtime type not found: {fullName}");
            return type;
        }
    }
}
#endif
