using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

namespace GenjitsuLAB.Animation.Editor
{
    /// <summary>Three-column authoring window for embedded tick-based animation clips.</summary>
    public sealed partial class AnimationPackEditorWindow : EditorWindow
    {
        private const float k_splitterWidth = 5f;
        [SerializeField] private AnimationPack m_pack;
        [SerializeField] private AnimationClip m_clip;
        [SerializeField] private float m_leftWidth = 240f;
        [SerializeField] private float m_rightWidth = 340f;
        [SerializeField] private float m_tickRate = 60f;
        [SerializeField] private float m_speed = 1f;
        [SerializeField] private float m_size = 1f;
        [SerializeField] private float m_tickWidth = 20f;
        [SerializeField] private int m_elementIndex = -1;
        private SerializedObject m_packObject;
        private SerializedObject m_clipObject;
        private ReorderableList m_clipList;
        private ReorderableList m_elementList;
        private readonly AnimationPreviewClock m_clock = new();
        private Vector2 m_leftScroll;
        private Vector2 m_rightScroll;
        private Vector2 m_timelineScroll;
        private double m_lastUpdate;
        private int m_boxIndex = -1;
        private bool m_boxIsHurt;
        private int m_splitter;
        private bool m_timelineDragging;
        private string m_idError;
        private string[] m_clipLabels = Array.Empty<string>();
        private string[] m_elementLabels = Array.Empty<string>();
        private GUIStyle m_centerLabel;

        /// <summary>Opens the editor through the Tools menu.</summary>
        [MenuItem("Tools/Animation/Animation Pack Editor")]
        public static void Open()
        {
            var window = GetWindow<AnimationPackEditorWindow>();
            window.titleContent = new GUIContent("Animation Pack");
            window.Show();
            if (Selection.activeObject is AnimationPack pack) window.SetPack(pack);
        }

        /// <summary>Routes double-clicks on AnimationPack assets to this editor.</summary>
        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not AnimationPack pack) return false;
            var window = GetWindow<AnimationPackEditorWindow>();
            window.SetPack(pack);
            window.Show();
            return true;
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Animation Pack");
            minSize = new Vector2(1020, 620);
            m_lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdatePreview;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            Rebind();
        }

        private void OnDisable()
        {
            CancelBoxDrag();
            AnimationPackAuthoring.Save(m_pack);
            EditorApplication.update -= UpdatePreview;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            DisposePreview();
            DisposeSerializedObjects();
        }

        private void BeforeReload()
        {
            CancelBoxDrag();
            AnimationPackAuthoring.Save(m_pack);
            DisposePreview();
        }

        private void DisposeSerializedObjects()
        {
            m_packObject?.Dispose();
            m_clipObject?.Dispose();
            m_packObject = null;
            m_clipObject = null;
            m_clipList = null;
            m_elementList = null;
        }

        private void SetPack(AnimationPack pack)
        {
            if (m_pack == pack && m_packObject != null) return;
            CancelBoxDrag();
            AnimationPackAuthoring.Save(m_pack);
            m_pack = pack;
            m_clip = pack != null && pack.Clips.Count > 0 ? pack.Clips[0] : null;
            m_clock.Stop();
            m_elementIndex = 0;
            m_boxIndex = -1;
            m_timelineScroll = Vector2.zero;
            Rebind();
            Repaint();
        }

        private void SelectClip(AnimationClip clip)
        {
            CancelBoxDrag();
            m_clip = clip;
            m_clock.Stop();
            m_elementIndex = 0;
            m_boxIndex = -1;
            m_idError = null;
            m_timelineScroll = Vector2.zero;
            // Keep the pack list alive while its selection/drag callback is executing.
            m_clipObject?.Dispose();
            m_clipObject = m_clip == null ? null : new SerializedObject(m_clip);
            m_elementList = null;
            if (m_clipObject != null) BuildElementList();
            m_packObject?.Update();
            RefreshTiming();
            if (m_clipList != null)
            {
                m_clipList.index = -1;
                for (int i = 0; i < m_pack.Clips.Count; i++)
                    if (m_pack.Clips[i] == clip) m_clipList.index = i;
            }
            GUI.FocusControl(null);
            Repaint();
        }

        private void Rebind()
        {
            DisposeSerializedObjects();
            if (m_pack != null)
            {
                m_packObject = new SerializedObject(m_pack);
                BuildClipList();
            }
            if (m_clip != null)
            {
                m_clipObject = new SerializedObject(m_clip);
                BuildElementList();
            }
            RefreshTiming();
        }

        private void RefreshTiming()
        {
            m_clock.Rebuild(m_clip);
            m_elementIndex = m_clip == null || m_clip.Elements.Count == 0 ? -1 :
                Mathf.Clamp(m_elementIndex, 0, m_clip.Elements.Count - 1);
            if (m_elementList != null) m_elementList.index = m_elementIndex;
            int count = m_pack == null ? 0 : m_pack.Clips.Count;
            m_clipLabels = new string[count];
            for (int i = 0; i < count; i++)
            {
                AnimationClip clip = m_pack.Clips[i];
                m_clipLabels[i] = clip == null ? "(Missing Clip)" :
                    $"{clip.AnimId}  {clip.AnimName}{(AnimationPackAuthoring.Owns(m_pack, clip) ? string.Empty : " [External]")}";
            }
            count = m_clip == null ? 0 : m_clip.Elements.Count;
            m_elementLabels = new string[count];
            for (int i = 0; i < count; i++)
            {
                AnimationElement element = m_clip.Elements[i];
                m_elementLabels[i] = $"{i + 1}  {(element?.Sprite == null ? "(No Sprite)" : element.Sprite.name)}  [{element?.Duration ?? 1} ticks]";
            }
        }

        private void OnUndoRedo()
        {
            CancelBoxDrag();
            m_clock.Pause();
            // Restored sub-assets are resolved from the pack's serialized references.
            if (m_pack != null && (m_clip == null || !ContainsClip(m_clip)))
                m_clip = m_pack.Clips.Count > 0 ? m_pack.Clips[0] : null;
            m_boxIndex = -1;
            Rebind();
            SyncElementToTick();
            Repaint();
        }

        private bool ContainsClip(AnimationClip clip)
        {
            if (m_pack == null) return false;
            for (int i = 0; i < m_pack.Clips.Count; i++)
                if (m_pack.Clips[i] == clip) return true;
            return false;
        }

        private void OnProjectChanged()
        {
            if (m_boxDragging) CancelBoxDrag();
            if (!ContainsClip(m_clip)) m_clip = null;
            m_clock.Pause();
            Rebind();
            SyncElementToTick();
            Repaint();
        }

        private void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            double delta = now - m_lastUpdate;
            m_lastUpdate = now;
            if (m_clip != null && m_clock.Advance(delta, m_tickRate, m_speed, m_clip.IsLoop))
            {
                SyncElementToTick();
                Repaint();
            }
        }

        private void SyncElementToTick()
        {
            int element = m_clock.Element;
            if (element != m_elementIndex) m_boxIndex = -1;
            m_elementIndex = element;
            if (m_elementList != null) m_elementList.index = element;
        }

        private void Seek(int tick)
        {
            CancelBoxDrag();
            m_clock.Seek(tick);
            SyncElementToTick();
            Repaint();
        }

        private bool CanEdit => AnimationPackAuthoring.Owns(m_pack, m_clip);

        private void OnGUI()
        {
            m_centerLabel ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
            HandleSaveShortcut();
            if (m_pack == null && m_packObject != null) Rebind();
            if (m_clip == null && m_clipObject != null) Rebind();
            float width = position.width;
            m_leftWidth = Mathf.Clamp(m_leftWidth, 200, width - m_rightWidth - 370);
            m_rightWidth = Mathf.Clamp(m_rightWidth, 300, width - m_leftWidth - 370);
            Rect left = new Rect(0, 0, m_leftWidth, position.height);
            Rect right = new Rect(width - m_rightWidth, 0, m_rightWidth, position.height);
            Rect center = new Rect(left.xMax + k_splitterWidth, 0,
                right.x - left.xMax - 2 * k_splitterWidth, position.height);
            DrawSplitters(left.xMax, right.x - k_splitterWidth);
            GUILayout.BeginArea(left);
            DrawLeft();
            GUILayout.EndArea();
            GUILayout.BeginArea(center);
            DrawCenter();
            GUILayout.EndArea();
            GUILayout.BeginArea(right);
            DrawInspector();
            GUILayout.EndArea();
        }

        private void HandleSaveShortcut()
        {
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.S &&
                (current.control || current.command))
            {
                AnimationPackAuthoring.Save(m_pack);
                current.Use();
            }
        }

        private void DrawSplitters(float leftX, float rightX)
        {
            Rect left = new Rect(leftX, 0, k_splitterWidth, position.height);
            Rect right = new Rect(rightX, 0, k_splitterWidth, position.height);
            EditorGUI.DrawRect(left, new Color(0.12f, 0.12f, 0.12f));
            EditorGUI.DrawRect(right, new Color(0.12f, 0.12f, 0.12f));
            EditorGUIUtility.AddCursorRect(left, MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(right, MouseCursor.ResizeHorizontal);
            int control = GUIUtility.GetControlID(FocusType.Passive);
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 &&
                (left.Contains(current.mousePosition) || right.Contains(current.mousePosition)))
            {
                m_splitter = left.Contains(current.mousePosition) ? 1 : 2;
                GUIUtility.hotControl = control;
                current.Use();
            }
            if (GUIUtility.hotControl != control || m_splitter == 0) return;
            if (current.type == EventType.MouseDrag)
            {
                if (m_splitter == 1) m_leftWidth += current.delta.x;
                else m_rightWidth -= current.delta.x;
                current.Use();
                Repaint();
            }
            if (current.rawType == EventType.MouseUp)
            {
                m_splitter = 0;
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        private void DrawLeft()
        {
            GUILayout.Label("AnimationPack", EditorStyles.boldLabel);
            AnimationPack pack = (AnimationPack)EditorGUILayout.ObjectField(m_pack, typeof(AnimationPack), false);
            if (pack != m_pack) { SetPack(pack); GUIUtility.ExitGUI(); }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton)) CreatePack();
                if (GUILayout.Button("Open", EditorStyles.toolbarButton)) OpenPack();
                using (new EditorGUI.DisabledScope(m_pack == null))
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton)) AnimationPackAuthoring.Save(m_pack);
            }
            if (m_packObject == null)
            {
                EditorGUILayout.HelpBox("Create or open an AnimationPack to begin.", MessageType.Info);
                return;
            }
            m_packObject.UpdateIfRequiredOrScript();
            m_leftScroll = EditorGUILayout.BeginScrollView(m_leftScroll);
            m_clipList.DoLayoutList();
            EditorGUILayout.EndScrollView();
            if (m_packObject.ApplyModifiedProperties()) RefreshTiming();
        }

        private void CreatePack()
        {
            string path = EditorUtility.SaveFilePanelInProject("New AnimationPack", "AnimationPack", "asset", "Choose an asset path.");
            if (string.IsNullOrEmpty(path)) return;
            // Never overwrite an existing user-owned asset.
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var pack = CreateInstance<AnimationPack>();
            AssetDatabase.CreateAsset(pack, path);
            SetPack(pack);
            AnimationPackAuthoring.Save(pack);
            EditorGUIUtility.PingObject(pack);
            GUIUtility.ExitGUI();
        }

        private void OpenPack()
        {
            string path = EditorUtility.OpenFilePanel("Open AnimationPack", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return;
            string relative = FileUtil.GetProjectRelativePath(path);
            var pack = AssetDatabase.LoadAssetAtPath<AnimationPack>(relative);
            if (pack == null) EditorUtility.DisplayDialog("AnimationPack", "Choose an AnimationPack inside this project's Assets folder.", "OK");
            else SetPack(pack);
            GUIUtility.ExitGUI();
        }

        private void BuildClipList()
        {
            m_clipList = new ReorderableList(m_packObject, m_packObject.FindProperty("m_clips"), true, true, true, true);
            m_clipList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Clips");
            m_clipList.drawElementCallback = (rect, index, active, focused) =>
            {
                if (index < m_clipLabels.Length) EditorGUI.LabelField(rect, m_clipLabels[index]);
            };
            m_clipList.onSelectCallback = list =>
            {
                if (list.index >= 0 && list.index < m_pack.Clips.Count)
                {
                    if (m_clip != m_pack.Clips[list.index]) SelectClip(m_pack.Clips[list.index]);
                }
            };
            m_clipList.onAddCallback = list =>
            {
                SelectClip(AnimationPackAuthoring.AddClip(m_pack));
                GUIUtility.ExitGUI();
            };
            m_clipList.onCanRemoveCallback = list => list.index >= 0 && list.index < m_pack.Clips.Count &&
                AnimationPackAuthoring.Owns(m_pack, m_pack.Clips[list.index]);
            m_clipList.onRemoveCallback = list =>
            {
                int index = list.index;
                AnimationClip clip = m_pack.Clips[index];
                if (!EditorUtility.DisplayDialog("Delete Clip", $"Delete embedded clip '{clip.AnimName}'? This can be undone.", "Delete", "Cancel")) return;
                AnimationPackAuthoring.DeleteClip(m_pack, clip);
                SelectClip(m_pack.Clips.Count == 0 ? null : m_pack.Clips[Mathf.Min(index, m_pack.Clips.Count - 1)]);
                GUIUtility.ExitGUI();
            };
            m_clipList.onReorderCallback = list =>
            {
                m_packObject.ApplyModifiedProperties();
                RefreshTiming();
                for (int i = 0; i < m_pack.Clips.Count; i++) if (m_pack.Clips[i] == m_clip) list.index = i;
            };
            m_clipList.index = -1;
            for (int i = 0; i < m_pack.Clips.Count; i++) if (m_pack.Clips[i] == m_clip) m_clipList.index = i;
        }
    }
}
