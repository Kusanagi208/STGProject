using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("GenjitsuLAB.Animation.Editor.Tests")]

namespace GenjitsuLAB.Animation.Editor
{
    /// <summary>Editor-only asset transactions; runtime data contracts remain unchanged.</summary>
    internal static class AnimationPackAuthoring
    {
        /// <summary>Checks that a clip is a sub-asset owned by the specified pack.</summary>
        internal static bool Owns(AnimationPack pack, AnimationClip clip)
        {
            // Unity 6.3 can temporarily report IsSubAsset=false after Undo/Redo while
            // retaining the correct persistent asset path. Ownership is the containing file.
            return pack != null && clip != null && AssetDatabase.Contains(clip) &&
                AssetDatabase.GetAssetPath(pack) == AssetDatabase.GetAssetPath(clip);
        }

        /// <summary>Finds conflicting IDs without modifying existing assets.</summary>
        internal static bool HasId(AnimationPack pack, int id, AnimationClip except = null)
        {
            if (pack == null)
            {
                return false;
            }
            for (int i = 0; i < pack.Clips.Count; i++)
            {
                AnimationClip clip = pack.Clips[i];
                if (clip != null && clip != except && clip.AnimId == id)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Creates and registers a clip and its pack reference in one Undo group.</summary>
        internal static AnimationClip AddClip(AnimationPack pack)
        {
            if (pack == null || !AssetDatabase.Contains(pack))
                throw new ArgumentException("Save the AnimationPack before adding a clip.", nameof(pack));

            int id = 0;
            while (HasId(pack, id))
            {
                id++;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Animation Clip");
            var clip = ScriptableObject.CreateInstance<AnimationClip>();
            clip.name = "New Clip";
            using (var serialized = new SerializedObject(clip))
            {
                serialized.FindProperty("m_animId").intValue = id;
                serialized.FindProperty("m_animName").stringValue = "New Clip";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.AddObjectToAsset(clip, pack);
            Undo.RegisterCreatedObjectUndo(clip, "Add Animation Clip");
            Undo.RegisterCompleteObjectUndo(pack, "Add Animation Clip");
            using (var serialized = new SerializedObject(pack))
            {
                SerializedProperty clips = serialized.FindProperty("m_clips");
                int index = clips.arraySize;
                clips.arraySize++;
                clips.GetArrayElementAtIndex(index).objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(pack);
            Undo.CollapseUndoOperations(group);
            Save(pack);
            return clip;
        }

        /// <summary>Deletes only owned sub-assets, including all references in this pack.</summary>
        internal static bool DeleteClip(AnimationPack pack, AnimationClip clip)
        {
            if (!Owns(pack, clip))
            {
                return false;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Delete Animation Clip");
            Undo.RegisterCompleteObjectUndo(pack, "Delete Animation Clip");
            using (var serialized = new SerializedObject(pack))
            {
                SerializedProperty clips = serialized.FindProperty("m_clips");
                for (int i = clips.arraySize - 1; i >= 0; i--)
                {
                    if (clips.GetArrayElementAtIndex(i).objectReferenceValue != clip)
                    {
                        continue;
                    }
                    clips.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    clips.DeleteArrayElementAtIndex(i);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Undo.DestroyObjectImmediate(clip);
            EditorUtility.SetDirty(pack);
            Undo.CollapseUndoOperations(group);
            Save(pack);
            return true;
        }

        /// <summary>Saves this asset file, including owned dirty clips, without saving unrelated assets.</summary>
        internal static void Save(AnimationPack pack)
        {
            if (pack == null || !AssetDatabase.Contains(pack))
            {
                return;
            }
            // Saving a sub-asset writes the containing asset file as well.
            for (int i = 0; i < pack.Clips.Count; i++)
            {
                AnimationClip clip = pack.Clips[i];
                if (Owns(pack, clip) && EditorUtility.IsDirty(clip))
                    AssetDatabase.SaveAssetIfDirty(clip);
            }
            AssetDatabase.SaveAssetIfDirty(pack);
        }

        /// <summary>Appends a clean element or a serialized deep copy of an existing element.</summary>
        internal static int AddElement(SerializedObject serialized, int copyIndex = -1)
        {
            serialized.Update();
            SerializedProperty elements = serialized.FindProperty("m_elements");
            int index;
            if (copyIndex >= 0 && copyIndex < elements.arraySize)
            {
                elements.InsertArrayElementAtIndex(copyIndex);
                index = copyIndex + 1;
            }
            else
            {
                index = elements.arraySize;
                elements.arraySize++;
                SerializedProperty element = elements.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("m_sprite").objectReferenceValue = null;
                element.FindPropertyRelative("m_duration").intValue = 1;
                element.FindPropertyRelative("m_hitBoxes").ClearArray();
                element.FindPropertyRelative("m_hurtBoxes").ClearArray();
            }
            serialized.ApplyModifiedProperties();
            return index;
        }
    }

    /// <summary>Cached integer-tick preview state with allocation-free steady-state advancement.</summary>
    internal sealed class AnimationPreviewClock
    {
        private int[] m_starts = Array.Empty<int>();
        private int m_count;
        private double m_fraction;

        /// <summary>Gets the sum of effective element durations.</summary>
        internal int TotalTicks { get; private set; }
        /// <summary>Gets the current zero-based tick.</summary>
        internal int Tick { get; private set; }
        /// <summary>Gets whether playback has started and not stopped or finished.</summary>
        internal bool IsPlaying { get; private set; }
        /// <summary>Gets whether a started playback is paused.</summary>
        internal bool IsPaused { get; private set; }
        /// <summary>Gets the zero-based element at the current tick, or -1 for an empty clip.</summary>
        internal int Element => FindElement(Tick);

        /// <summary>Rebuilds timing only after clip data changes; null elements follow runtime's one-tick rule.</summary>
        internal void Rebuild(AnimationClip clip)
        {
            m_count = clip == null ? 0 : clip.Elements.Count;
            if (m_starts.Length < m_count)
            {
                m_starts = new int[m_count];
            }
            long total = 0;
            for (int i = 0; i < m_count; i++)
            {
                m_starts[i] = (int)Math.Min(total, int.MaxValue);
                total += clip.Elements[i] == null ? 1 : Mathf.Max(1, clip.Elements[i].Duration);
            }
            TotalTicks = (int)Math.Min(total, int.MaxValue);
            Tick = Mathf.Clamp(Tick, 0, Mathf.Max(0, TotalTicks - 1));
            if (TotalTicks == 0)
            {
                Stop();
            }
        }

        /// <summary>Gets a cached element start tick.</summary>
        internal int Start(int index) => index >= 0 && index < m_count ? m_starts[index] : 0;

        /// <summary>Locates an element using the half-open interval [start, end).</summary>
        internal int FindElement(int tick)
        {
            if (m_count == 0)
            {
                return -1;
            }
            int low = 0;
            int high = m_count - 1;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (m_starts[middle] <= tick)
                {
                    low = middle;
                }
                else high = middle - 1;
            }
            return low;
        }

        /// <summary>Always restarts playback at tick zero.</summary>
        internal void Play() { Tick = 0; m_fraction = 0; IsPaused = false; IsPlaying = TotalTicks > 0; }
        /// <summary>Stops and rewinds the preview.</summary>
        internal void Stop() { Tick = 0; m_fraction = 0; IsPaused = false; IsPlaying = false; }
        /// <summary>Toggles pause without rewinding.</summary>
        internal void TogglePause()
        {
            if (IsPlaying)
            {
                IsPaused = !IsPaused;
            }
        }
        /// <summary>Pauses an active preview before authoring.</summary>
        internal void Pause()
        {
            if (IsPlaying)
            {
                IsPaused = true;
            }
        }
        /// <summary>Scrubs to a valid tick and clears any partial tick.</summary>
        internal void Seek(int tick) { Pause(); Tick = Mathf.Clamp(tick, 0, Mathf.Max(0, TotalTicks - 1)); m_fraction = 0; }

        /// <summary>Advances from elapsed editor time without a per-tick loop or allocations.</summary>
        internal bool Advance(double seconds, double tickRate, double speed, bool loop)
        {
            if (!IsPlaying || IsPaused || seconds <= 0 || TotalTicks == 0)
            {
                return false;
            }
            double elapsed = m_fraction + seconds * tickRate * speed;
            if (double.IsNaN(elapsed) || double.IsInfinity(elapsed))
            {
                return false;
            }
            double whole = Math.Floor(elapsed);
            m_fraction = elapsed - whole;
            if (whole < 1)
            {
                return false;
            }
            double next = Tick + whole;
            if (loop)
            {
                Tick = (int)(next % TotalTicks);
            }
            else if (next >= TotalTicks)
            {
                Tick = TotalTicks - 1;
                IsPlaying = false;
                m_fraction = 0;
            }
            else Tick = (int)next;
            return true;
        }
    }

    /// <summary>Shared preview projection in GUI points, independent of display pixel density.</summary>
    internal static class AnimationPreviewCoordinates
    {
        /// <summary>Projects pivot-relative Unity units into a GUI rectangle.</summary>
        internal static Vector2 ToScreen(Vector2 point, Rect viewport, float pointsPerUnit)
            => viewport.center + new Vector2(point.x, -point.y) * pointsPerUnit;
        /// <summary>Unprojects a GUI point into pivot-relative Unity units.</summary>
        internal static Vector2 ToLocal(Vector2 point, Rect viewport, float pointsPerUnit)
        {
            Vector2 delta = (point - viewport.center) / pointsPerUnit;
            return new Vector2(delta.x, -delta.y);
        }
        /// <summary>Creates a positive-size rectangle from arbitrary opposing corners.</summary>
        internal static Rect FromCorners(Vector2 a, Vector2 b)
            => Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
