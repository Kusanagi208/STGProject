using System;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace GenjitsuLAB.Animation.Editor.Tests
{
    /// <summary>Focused EditMode coverage for asset integrity, tick semantics and preview projection.</summary>
    public sealed class AnimationPackEditorTests
    {
        private string m_folder;
        private string m_path;
        private AnimationPack m_pack;

        /// <summary>Creates a unique test-owned asset folder.</summary>
        [SetUp]
        public void SetUp()
        {
            string name = "AnimationEditorTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            m_folder = "Assets/" + name;
            m_path = m_folder + "/Pack.asset";
            m_pack = ScriptableObject.CreateInstance<AnimationPack>();
            AssetDatabase.CreateAsset(m_pack, m_path);
        }

        /// <summary>Removes only assets created by this test.</summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(m_path);
            for (int i = 0; i < assets.Length; i++) if (assets[i] != null) Undo.ClearUndo(assets[i]);
            AssetDatabase.DeleteAsset(m_folder);
        }

        /// <summary>Checks embedded persistence and smallest unused ID allocation.</summary>
        [Test]
        public void EmbeddedClipsPersistWithUniqueIds()
        {
            AnimationClip first = AnimationPackAuthoring.AddClip(m_pack);
            AnimationClip second = AnimationPackAuthoring.AddClip(m_pack);
            Assert.That(first.AnimId, Is.Zero);
            Assert.That(second.AnimId, Is.EqualTo(1));
            Assert.That(first.AnimName, Is.EqualTo("New Clip"));
            Assert.That(first.Elements.Count, Is.Zero);
            Assert.That(AnimationPackAuthoring.Owns(m_pack, first), Is.True);
            SetDurations(first, 2, 1, 3);
            using (var serialized = new SerializedObject(first))
            {
                SerializedProperty boxes = serialized.FindProperty("m_elements").GetArrayElementAtIndex(1).FindPropertyRelative("m_hitBoxes");
                boxes.arraySize = 1;
                boxes.GetArrayElementAtIndex(0).rectValue = new Rect(-1, 2, 3, 4);
                serialized.ApplyModifiedProperties();
            }
            AnimationPackAuthoring.Save(m_pack);
            AssetDatabase.ImportAsset(m_path, ImportAssetOptions.ForceUpdate);
            AnimationPack loaded = AssetDatabase.LoadAssetAtPath<AnimationPack>(m_path);
            Assert.That(loaded.Clips.Count, Is.EqualTo(2));
            Assert.That(loaded.Clips[0].Elements[1].HitBoxes[0], Is.EqualTo(new Rect(-1, 2, 3, 4)));
            Assert.That(loaded.Clips[0].TotalTicks, Is.EqualTo(6));
            AnimationPackAuthoring.DeleteClip(m_pack, loaded.Clips[0]);
            Assert.That(AnimationPackAuthoring.AddClip(m_pack).AnimId, Is.Zero);
        }

        /// <summary>Verifies both directions of creation Undo including disk serialization after redo.</summary>
        [Test]
        public void CreateUndoRedoRestoresEmbeddedOwnership()
        {
            AnimationPackAuthoring.AddClip(m_pack);
            Undo.PerformUndo();
            Assert.That(m_pack.Clips.Count, Is.Zero);
            AnimationPackAuthoring.Save(m_pack);
            Undo.PerformRedo();
            Assert.That(m_pack.Clips.Count, Is.EqualTo(1));
            Assert.That(AnimationPackAuthoring.Owns(m_pack, m_pack.Clips[0]), Is.True);
            AnimationPackAuthoring.Save(m_pack);
            AssetDatabase.ImportAsset(m_path, ImportAssetOptions.ForceUpdate);
            AnimationClip restored = AssetDatabase.LoadAssetAtPath<AnimationPack>(m_path).Clips[0];
            Assert.That(restored, Is.Not.Null);
            Assert.That(AssetDatabase.IsSubAsset(restored), Is.True);
        }

        /// <summary>Verifies deleted clips and their pack references are restored together.</summary>
        [Test]
        public void DeleteUndoRedoRestoresDataAndOwnership()
        {
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2, 1, 3);
            AnimationPackAuthoring.Save(m_pack);
            Assert.That(AnimationPackAuthoring.DeleteClip(m_pack, clip), Is.True);
            Assert.That(m_pack.Clips.Count, Is.Zero);
            Undo.PerformUndo();
            Assert.That(m_pack.Clips.Count, Is.EqualTo(1));
            Assert.That(AnimationPackAuthoring.Owns(m_pack, m_pack.Clips[0]), Is.True);
            Assert.That(m_pack.Clips[0].Elements[2].Duration, Is.EqualTo(3));
            AnimationPackAuthoring.Save(m_pack);
            Undo.PerformRedo();
            Assert.That(m_pack.Clips.Count, Is.Zero);
            AnimationPackAuthoring.Save(m_pack);
            AssetDatabase.ImportAsset(m_path, ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(m_path).Length, Is.EqualTo(1));
        }

        /// <summary>Ensures deletion cannot destroy an external clip.</summary>
        [Test]
        public void ExternalClipCannotBeDeleted()
        {
            var foreign = ScriptableObject.CreateInstance<AnimationClip>();
            string path = m_folder + "/External.asset";
            AssetDatabase.CreateAsset(foreign, path);
            Assert.That(AnimationPackAuthoring.DeleteClip(m_pack, foreign), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(path), Is.EqualTo(foreign));
        }

        /// <summary>Proves element copies do not share box list storage and new elements have clean defaults.</summary>
        [Test]
        public void DuplicateElementDeepCopiesBoxes()
        {
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2);
            using var serialized = new SerializedObject(clip);
            SerializedProperty boxes = serialized.FindProperty("m_elements").GetArrayElementAtIndex(0).FindPropertyRelative("m_hurtBoxes");
            boxes.arraySize = 1;
            boxes.GetArrayElementAtIndex(0).rectValue = new Rect(1, 2, 3, 4);
            serialized.ApplyModifiedProperties();
            int copy = AnimationPackAuthoring.AddElement(serialized, 0);
            Assert.That(copy, Is.EqualTo(1));
            Assert.That(clip.Elements[1].HurtBoxes[0], Is.EqualTo(new Rect(1, 2, 3, 4)));
            serialized.FindProperty("m_elements").GetArrayElementAtIndex(copy).FindPropertyRelative("m_hurtBoxes").GetArrayElementAtIndex(0).rectValue = new Rect(5, 6, 7, 8);
            serialized.ApplyModifiedProperties();
            Assert.That(clip.Elements[0].HurtBoxes[0], Is.EqualTo(new Rect(1, 2, 3, 4)));
            int clean = AnimationPackAuthoring.AddElement(serialized);
            Assert.That(clip.Elements[clean].Duration, Is.EqualTo(1));
            Assert.That(clip.Elements[clean].Sprite, Is.Null);
            Assert.That(clip.Elements[clean].HurtBoxes.Count, Is.Zero);
        }

        /// <summary>Checks time boundaries against the existing runtime player, including loops.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void PreviewMatchesRuntimeElementBoundaries(bool loop)
        {
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2, 1, 3);
            using (var serialized = new SerializedObject(clip))
            {
                serialized.FindProperty("m_isLoop").boolValue = loop;
                serialized.ApplyModifiedProperties();
            }
            var clock = new AnimationPreviewClock();
            clock.Rebuild(clip);
            var player = new AnimationPlayer(null, m_pack.Clips);
            player.ChangeAnim(clip);
            clock.Play();
            for (int i = 0; i < 20; i++)
            {
                Assert.That(clock.Element + 1, Is.EqualTo(player.AnimElem), "Element at tick " + i);
                Assert.That(clock.IsPlaying, Is.EqualTo(player.IsPlaying));
                clock.Advance(1, 1, 1, loop);
                player.Tick();
            }
        }

        /// <summary>Checks restart, pause, resume, stop, fractional advancement and scrub behavior.</summary>
        [Test]
        public void PlaybackControlsAndFractionalSpeedAreDeterministic()
        {
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2, 1, 3);
            var clock = new AnimationPreviewClock();
            clock.Rebuild(clip);
            clock.Play();
            clock.Advance(0.125, 4, 1, false);
            Assert.That(clock.Tick, Is.Zero);
            clock.Advance(0.125, 4, 1, false);
            Assert.That(clock.Tick, Is.EqualTo(1));
            clock.TogglePause();
            clock.Advance(100, 60, 1, false);
            Assert.That(clock.Tick, Is.EqualTo(1));
            clock.TogglePause();
            clock.Advance(0.25, 4, 2, false);
            Assert.That(clock.Tick, Is.EqualTo(3));
            clock.Seek(99);
            Assert.That(clock.Tick, Is.EqualTo(5));
            Assert.That(clock.IsPaused, Is.True);
            clock.Play();
            Assert.That(clock.Tick, Is.Zero);
            Assert.That(clock.IsPaused, Is.False);
            clock.Stop();
            Assert.That(clock.Tick, Is.Zero);
            Assert.That(clock.IsPlaying, Is.False);
        }

        /// <summary>Checks null and empty clips and a duration change while positioned near the end.</summary>
        [Test]
        public void EmptyAndChangedClipsClampSafely()
        {
            var clock = new AnimationPreviewClock();
            clock.Rebuild(null);
            clock.Play();
            Assert.That(clock.Element, Is.EqualTo(-1));
            Assert.That(clock.IsPlaying, Is.False);
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2, 1, 3);
            clock.Rebuild(clip);
            clock.Seek(5);
            SetDurations(clip, 1);
            clock.Rebuild(clip);
            Assert.That(clock.Tick, Is.Zero);
            Assert.That(clock.Element, Is.Zero);
        }

        /// <summary>Checks high-DPI-independent projection, arbitrary pivots and pixel densities.</summary>
        [TestCase(32f, 0.25f)]
        [TestCase(100f, 1f)]
        [TestCase(200f, 3f)]
        public void PivotCoordinatesRoundTrip(float pixelsPerUnit, float zoom)
        {
            var texture = new Texture2D(80, 40);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 80, 40), new Vector2(0.2f, 0.75f), pixelsPerUnit);
            try
            {
                Rect viewport = new Rect(27, 53, 600, 400);
                Vector2 local = -sprite.pivot / sprite.pixelsPerUnit;
                Vector2 screen = AnimationPreviewCoordinates.ToScreen(local, viewport, 100 * zoom);
                Vector2 restored = AnimationPreviewCoordinates.ToLocal(screen, viewport, 100 * zoom);
                Assert.That(Vector2.Distance(local, restored), Is.LessThan(0.00001f));
                Assert.That(AnimationPreviewCoordinates.ToScreen(Vector2.zero, viewport, 100 * zoom), Is.EqualTo(viewport.center));
                Assert.That(sprite.bounds.min.x, Is.EqualTo(local.x).Within(0.00001f));
                Assert.That(sprite.bounds.min.y, Is.EqualTo(local.y).Within(0.00001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
        }

        /// <summary>Checks all reverse-drag directions produce the same positive-size box.</summary>
        [Test]
        public void ReverseDragNormalizesRectangles()
        {
            Rect expected = new Rect(-2, -3, 6, 8);
            Assert.That(AnimationPreviewCoordinates.FromCorners(new Vector2(-2, -3), new Vector2(4, 5)), Is.EqualTo(expected));
            Assert.That(AnimationPreviewCoordinates.FromCorners(new Vector2(4, 5), new Vector2(-2, -3)), Is.EqualTo(expected));
            Assert.That(AnimationPreviewCoordinates.FromCorners(new Vector2(-2, 5), new Vector2(4, -3)), Is.EqualTo(expected));
        }

        /// <summary>Measures the managed allocations of steady-state clock advancement after warmup.</summary>
        [Test]
        public void SteadyStateClockAllocatesNoManagedMemory()
        {
            AnimationClip clip = AnimationPackAuthoring.AddClip(m_pack);
            SetDurations(clip, 2, 1, 3);
            var clock = new AnimationPreviewClock();
            clock.Rebuild(clip);
            clock.Play();
            for (int i = 0; i < 100; i++) clock.Advance(1.0 / 60, 60, 1, true);
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 16,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            var probe = new byte[1024];
            GC.KeepAlive(probe);
            recorder.Stop();
            Assert.That(recorder.Valid, Is.True);
            Assert.That(recorder.Count, Is.GreaterThan(0), "A known allocation must be detected before trusting a zero result.");
            recorder.Reset();
            recorder.Start();
            for (int i = 0; i < 10000; i++) clock.Advance(1.0 / 60, 60, 1, true);
            recorder.Stop();
            Assert.That(recorder.Count, Is.Zero);
        }

        private static void SetDurations(AnimationClip clip, params int[] durations)
        {
            using var serialized = new SerializedObject(clip);
            SerializedProperty elements = serialized.FindProperty("m_elements");
            elements.arraySize = durations.Length;
            for (int i = 0; i < durations.Length; i++)
                elements.GetArrayElementAtIndex(i).FindPropertyRelative("m_duration").intValue = durations[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
