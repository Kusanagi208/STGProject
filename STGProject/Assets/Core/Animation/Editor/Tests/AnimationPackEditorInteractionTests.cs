using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GenjitsuLAB.Animation.Editor.Tests
{
    /// <summary>Exercises the real IMGUI canvas and preview lifetime in EditMode.</summary>
    public sealed class AnimationPackEditorInteractionTests
    {
        private const BindingFlags k_privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private string m_folder;
        private AnimationPack m_pack;
        private AnimationClip m_clip;
        private AnimationPackEditorWindow m_window;

        /// <summary>Creates test-owned data and opens an isolated editor window.</summary>
        [SetUp]
        public void SetUp()
        {
            string folder = "AnimationEditorInteraction_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder);
            m_folder = "Assets/" + folder;
            m_pack = ScriptableObject.CreateInstance<AnimationPack>();
            AssetDatabase.CreateAsset(m_pack, m_folder + "/Pack.asset");
            m_clip = AnimationPackAuthoring.AddClip(m_pack);
            using (var serialized = new SerializedObject(m_clip)) AnimationPackAuthoring.AddElement(serialized);
            m_window = ScriptableObject.CreateInstance<AnimationPackEditorWindow>();
            m_window.position = new Rect(60, 60, 1200, 750);
            Invoke("SetPack", m_pack);
            m_window.Show();
        }

        /// <summary>Closes the window and removes only this fixture's assets.</summary>
        [TearDown]
        public void TearDown()
        {
            if (m_window != null)
            {
                m_window.Close();
            }
            if (m_clip != null)
            {
                Undo.ClearUndo(m_clip);
            }
            if (m_pack != null)
            {
                Undo.ClearUndo(m_pack);
            }
            AssetDatabase.DeleteAsset(m_folder);
        }

        /// <summary>Verifies actual canvas creation, drag, Escape cancellation and atomic Undo.</summary>
        [UnityTest]
        public IEnumerator CanvasCreatesMovesCancelsAndUndoesBoxes()
        {
            for (int i = 0; i < 10; i++)
            {
                m_window.Repaint();
                yield return null;
            }
            Rect viewport = Get<Rect>("m_previewViewport");
            Assert.That(viewport.width, Is.GreaterThan(100), "The editor must render a usable canvas.");
            Vector2 origin = viewport.center + new Vector2(Get<float>("m_leftWidth") + 5, 0);
            Set("m_tool", 1);
            Send(EventType.MouseDown, origin + new Vector2(20, 20));
            // Native SendEvent includes the host view's tab offset; test the actual canvas origin.
            Vector2 dragStart = Get<Vector2>("m_dragStart");
            Send(EventType.MouseDrag, origin + new Vector2(120, -80));
            Send(EventType.MouseUp, origin + new Vector2(120, -80));
            Assert.That(m_clip.Elements[0].HitBoxes.Count, Is.EqualTo(1));
            Rect original = m_clip.Elements[0].HitBoxes[0];
            Assert.That(original.x, Is.EqualTo(dragStart.x).Within(0.01f));
            Assert.That(original.y, Is.EqualTo(dragStart.y).Within(0.01f));
            Assert.That(original.width, Is.EqualTo(1).Within(0.01f));
            Assert.That(original.height, Is.EqualTo(1).Within(0.01f));

            Set("m_tool", 0);
            Send(EventType.MouseDown, origin + new Vector2(70, -30));
            var dragEvent = new Event { button = 0 };
            using var dragRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 100; i++)
            {
                dragEvent.type = EventType.MouseDrag;
                dragEvent.mousePosition = origin + new Vector2(70 + i * 0.2f, -30 - i * 0.2f);
                m_window.SendEvent(dragEvent);
            }
            dragRecorder.Stop();
            Assert.That(dragRecorder.Count, Is.LessThan(dragRecorder.Capacity));
            TestContext.WriteLine($"Canvas: {dragRecorder.Count} GC.Alloc samples / 100 synthetic drag events (includes IMGUI and serialization).");
            Send(EventType.MouseDrag, origin + new Vector2(90, -50));
            m_window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
            Assert.That(m_clip.Elements[0].HitBoxes[0], Is.EqualTo(original));
            Send(EventType.MouseDown, origin + new Vector2(70, -30));
            Send(EventType.MouseDrag, origin + new Vector2(90, -50));
            Send(EventType.MouseUp, origin + new Vector2(90, -50));
            Assert.That(m_clip.Elements[0].HitBoxes[0].x, Is.EqualTo(original.x + 0.2f).Within(0.01f));
            Undo.PerformUndo();
            Assert.That(m_clip.Elements[0].HitBoxes[0], Is.EqualTo(original));
            Undo.PerformRedo();
            Assert.That(m_clip.Elements[0].HitBoxes[0].y, Is.EqualTo(original.y + 0.2f).Within(0.01f));
            Undo.PerformUndo();

            // Select the restored box, then drag its top-right resize handle.
            Send(EventType.MouseDown, origin + new Vector2(70, -30));
            Send(EventType.MouseUp, origin + new Vector2(70, -30));
            Send(EventType.MouseDown, origin + new Vector2(120, -80));
            Send(EventType.MouseDrag, origin + new Vector2(160, -100));
            Send(EventType.MouseUp, origin + new Vector2(160, -100));
            Assert.That(m_clip.Elements[0].HitBoxes[0].width, Is.EqualTo(1.4f).Within(0.01f));
            Assert.That(m_clip.Elements[0].HitBoxes[0].height, Is.EqualTo(1.2f).Within(0.01f));
            Undo.PerformUndo();

            Set("m_tool", 2);
            Send(EventType.MouseDown, origin + new Vector2(120, -80));
            Send(EventType.MouseDrag, origin + new Vector2(20, 20));
            Send(EventType.MouseUp, origin + new Vector2(20, 20));
            Assert.That(m_clip.Elements[0].HurtBoxes.Count, Is.EqualTo(1));
            Assert.That(m_clip.Elements[0].HurtBoxes[0].width, Is.EqualTo(1).Within(0.01f));
            Assert.That(m_clip.Elements[0].HitBoxes.Count, Is.EqualTo(1));
            m_window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Delete });
            Assert.That(m_clip.Elements[0].HurtBoxes.Count, Is.Zero);
        }

        /// <summary>Checks actual GPU sprite bounds against its non-central pivot and pixels-per-unit.</summary>
        [UnityTest]
        public IEnumerator RenderedSpriteMatchesPivotProjection()
        {
            var texture = new Texture2D(80, 40, TextureFormat.RGBA32, false);
            var pixels = new Color32[80 * 40];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            string texturePath = m_folder + "/Texture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 80, 40), new Vector2(0.2f, 0.75f), 50);
            AssetDatabase.AddObjectToAsset(sprite, texture);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            Texture2D readback = null;
            try
            {
                using (var serialized = new SerializedObject(m_clip))
                {
                    serialized.FindProperty("m_elements").GetArrayElementAtIndex(0).FindPropertyRelative("m_sprite").objectReferenceValue = sprite;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                AnimationPackAuthoring.Save(m_pack);
                Set("m_size", 1.5f);
                for (int i = 0; i < 10; i++)
                {
                    m_window.Repaint();
                    yield return null;
                }
                var rendered = Get<Texture>("m_previewTexture") as RenderTexture;
                Assert.That(rendered, Is.Not.Null);
                Assert.That(Get<SpriteRenderer>("m_previewSprite").sprite, Is.EqualTo(sprite));
                Rect viewport = Get<Rect>("m_previewViewport");
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = rendered;
                    readback = new Texture2D(rendered.width, rendered.height, TextureFormat.RGB24, false);
                    readback.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                    readback.Apply();
                }
                finally { RenderTexture.active = previous; }
                Color32[] actual = readback.GetPixels32();
                int minX = rendered.width;
                int minY = rendered.height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < rendered.height; y++)
                {
                    for (int x = 0; x < rendered.width; x++)
                    {
                        Color32 pixel = actual[y * rendered.width + x];
                        if (pixel.r < 240 || pixel.g < 240 || pixel.b < 240)
                        {
                            continue;
                        }
                        minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    }
                }
                float pixelScale = rendered.height / viewport.height;
                float scale = 150 * pixelScale;
                Assert.That(minX, Is.EqualTo(rendered.width / 2f + sprite.bounds.min.x * scale).Within(2));
                Assert.That(minY, Is.EqualTo(rendered.height / 2f + sprite.bounds.min.y * scale).Within(2));
                Assert.That(maxX + 1, Is.EqualTo(rendered.width / 2f + sprite.bounds.max.x * scale).Within(2));
                Assert.That(maxY + 1, Is.EqualTo(rendered.height / 2f + sprite.bounds.max.y * scale).Within(2));
            }
            finally
            {
                // Clear the test-owned reference before deleting its backing texture asset.
                using (var serialized = new SerializedObject(m_clip))
                {
                    serialized.FindProperty("m_elements").GetArrayElementAtIndex(0).FindPropertyRelative("m_sprite").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (readback != null)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        /// <summary>Checks native preview objects and materials are destroyed when the window closes.</summary>
        [UnityTest]
        public IEnumerator PreviewResourcesAreReleasedOnClose()
        {
            for (int i = 0; i < 10; i++)
            {
                m_window.Repaint();
                yield return null;
            }
            SpriteRenderer renderer = Get<SpriteRenderer>("m_previewSprite");
            Material material = Get<Material>("m_spriteMaterial");
            Assert.That(renderer != null, Is.True);
            Assert.That(material != null, Is.True);
            AnimationPreviewClock clock = Get<AnimationPreviewClock>("m_clock");
            clock.Play();
            var repaintEvent = new Event();
            using var repaintRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            for (int i = 0; i < 100; i++)
            {
                clock.Advance(1.0 / 60, 60, 1, true);
                repaintEvent.type = EventType.Repaint;
                m_window.SendEvent(repaintEvent);
            }
            repaintRecorder.Stop();
            Assert.That(repaintRecorder.Count, Is.LessThan(repaintRecorder.Capacity));
            TestContext.WriteLine($"Preview: {repaintRecorder.Count} GC.Alloc samples / 100 forced playback repaints (includes IMGUI and rendering).");
            m_window.Close();
            m_window = null;
            yield return null;
            Assert.That(renderer == null, Is.True);
            Assert.That(material == null, Is.True);
        }

        private void Send(EventType type, Vector2 point)
            => m_window.SendEvent(new Event { type = type, mousePosition = point, button = 0 });
        private T Get<T>(string name)
            => (T)typeof(AnimationPackEditorWindow).GetField(name, k_privateInstance).GetValue(m_window);
        private void Set(string name, object value)
            => typeof(AnimationPackEditorWindow).GetField(name, k_privateInstance).SetValue(m_window, value);
        private void Invoke(string name, params object[] args)
            => typeof(AnimationPackEditorWindow).GetMethod(name, k_privateInstance).Invoke(m_window, args);
    }
}
