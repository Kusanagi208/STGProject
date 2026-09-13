using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GenjitsuLAB.STG.Tests
{
    public sealed class PlayerLifeTests
    {
        private const BindingFlags k_instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void PlayerLifeState_AddsClampsAndNotifiesOnlyOnChange()
        {
            Type stateType = RequireRuntimeType("GenjitsuLAB.STG.PlayerLifeState");
            object state = Activator.CreateInstance(stateType, 3, 4);
            EventInfo changedEvent = stateType.GetEvent("LivesChanged");
            int notificationCount = 0;
            int notifiedValue = -1;
            Action<int> handler = value =>
            {
                notificationCount++;
                notifiedValue = value;
            };
            changedEvent.AddEventHandler(state, handler);

            try
            {
                Assert.That(GetProperty<int>(state, "CurrentLives"), Is.EqualTo(3));
                Assert.That((bool)Invoke(state, "TryAddLives", 1), Is.True);
                Assert.That(GetProperty<int>(state, "CurrentLives"), Is.EqualTo(4));
                Assert.That(notificationCount, Is.EqualTo(1));
                Assert.That(notifiedValue, Is.EqualTo(4));

                Assert.That((bool)Invoke(state, "TryAddLives", 1), Is.False);
                Assert.That((bool)Invoke(state, "TryAddLives", 0), Is.False);
                Assert.That(notificationCount, Is.EqualTo(1));

                Assert.That((bool)Invoke(state, "TryConsumeLife"), Is.True);
                Assert.That(GetProperty<int>(state, "CurrentLives"), Is.EqualTo(3));
                Assert.That(notificationCount, Is.EqualTo(2));
                Assert.That(notifiedValue, Is.EqualTo(3));
            }
            finally
            {
                changedEvent.RemoveEventHandler(state, handler);
            }
        }

        [Test]
        public void PlayerLifeHud_UsesCanvasCachedLabelAndUnbinds()
        {
            Type stateType = RequireRuntimeType("GenjitsuLAB.STG.PlayerLifeState");
            Type hudType = RequireRuntimeType("GenjitsuLAB.STG.PlayerLifeHud");
            object state = Activator.CreateInstance(stateType, 3, 99);
            GameObject canvasObject = new GameObject("LifeHudTestCanvas", typeof(RectTransform), typeof(Canvas));
            GameObject sideBar = new GameObject("left", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            sideBar.transform.SetParent(canvasObject.transform, false);
            Component hud = sideBar.AddComponent(hudType);

            try
            {
                Invoke(hud, "Bind", state);
                Text countText = sideBar.GetComponentInChildren<Text>();
                Assert.That(countText, Is.Not.Null);
                Assert.That(countText.text, Is.EqualTo("x 03"));

                Graphic[] graphics = sideBar.GetComponentsInChildren<Graphic>(true);
                for (int index = 0; index < graphics.Length; index++)
                {
                    if (graphics[index].gameObject != sideBar)
                    {
                        Assert.That(graphics[index].raycastTarget, Is.False);
                    }
                }

                Assert.That((bool)Invoke(state, "TryAddLives", 1), Is.True);
                Assert.That(countText.text, Is.EqualTo("x 04"));

                Invoke(hud, "Unbind");
                Assert.That((bool)Invoke(state, "TryAddLives", 1), Is.True);
                Assert.That(countText.text, Is.EqualTo("x 04"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static object Invoke(object target, string methodName, object argument = null)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, k_instanceFlags);
            object[] arguments = method.GetParameters().Length == 0 ? null : new[] { argument };
            return method.Invoke(target, arguments);
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
