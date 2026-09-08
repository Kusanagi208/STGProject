using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GenjitsuLAB.Animation.Editor
{
    public sealed partial class AnimationPackEditorWindow
    {
        private const float k_basePointsPerUnit = 100f;
        private const float k_gridTargetPoints = 12f;
        private const int k_gridMajorLineCount = 5;
        private const float k_handleSize = 7f;
        private static readonly string[] s_toolNames = { "Select", "Hit Box", "Hurt Box" };
        private PreviewRenderUtility m_preview;
        private SpriteRenderer m_previewSprite;
        private Material m_spriteMaterial;
        private Texture m_previewTexture;
        private readonly Vector3[] m_rectangleVertices = new Vector3[4];
        private int m_tool;
        private bool m_boxDragging;
        private bool m_boxCreating;
        private int m_boxControl;
        private int m_dragHandle = -1;
        private Vector2 m_dragStart;
        private Rect m_dragOriginal;
        private Rect m_dragDraft;
        private Rect m_dragViewport;
        private float m_dragPointsPerUnit;
        private Rect m_previewViewport;

        private void DrawCenter()
        {
            GUILayout.Label("Preview", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(m_clock.TotalTicks == 0 || m_boxDragging))
                {
                    if (GUILayout.Button("Play", EditorStyles.toolbarButton))
                    {
                        m_clock.Play();
                        m_lastUpdate = EditorApplication.timeSinceStartup;
                        SyncElementToTick();
                    }
                    using (new EditorGUI.DisabledScope(!m_clock.IsPlaying))
                    {
                        if (GUILayout.Button(m_clock.IsPaused ? "Resume" : "Pause", EditorStyles.toolbarButton))
                        {
                            m_clock.TogglePause();
                            m_lastUpdate = EditorApplication.timeSinceStartup;
                        }
                    }
                    if (GUILayout.Button("Stop", EditorStyles.toolbarButton))
                    {
                        m_clock.Stop();
                        SyncElementToTick();
                    }
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reset View", EditorStyles.toolbarButton))
                {
                    m_size = 1;
                    m_tickWidth = 20;
                    m_timelineScroll = Vector2.zero;
                }
            }
            float labelWidth = EditorGUIUtility.labelWidth;
            using (new EditorGUI.DisabledScope(m_boxDragging))
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUIUtility.labelWidth = 60;
                m_tickRate = PositiveField("Ticks/s", m_tickRate, 1, 1000);
                EditorGUIUtility.labelWidth = 45;
                m_speed = PositiveField("Speed", m_speed, 0.01f, 100);
                EditorGUIUtility.labelWidth = 32;
                m_size = PositiveField("Size", m_size, 0.05f, 20);
            }
            EditorGUIUtility.labelWidth = labelWidth;
            using (new EditorGUI.DisabledScope(!CanEdit || m_elementIndex < 0 || m_boxDragging))
                m_tool = GUILayout.Toolbar(m_tool, s_toolNames);
            Rect viewport = GUILayoutUtility.GetRect(100, 10000, 100, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawPreview(viewport);
            DrawTimeline();
        }

        private static float PositiveField(string label, float value, float min, float max)
        {
            float result = EditorGUILayout.DelayedFloatField(label, value, GUILayout.MinWidth(80));
            return float.IsFinite(result) ? Mathf.Clamp(result, min, max) : value;
        }

        private void EnsurePreview()
        {
            if (m_preview != null)
            {
                return;
            }
            m_preview = new PreviewRenderUtility();
            Camera camera = m_preview.camera;
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20;
            camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.13f, 0.15f, 1);
            var previewObject = new GameObject("Animation Pack Preview") { hideFlags = HideFlags.HideAndDontSave };
            m_previewSprite = previewObject.AddComponent<SpriteRenderer>();
            m_preview.AddSingleGO(previewObject);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                m_spriteMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                m_previewSprite.sharedMaterial = m_spriteMaterial;
            }
        }

        private void DisposePreview()
        {
            m_preview?.Cleanup();
            m_preview = null;
            m_previewSprite = null;
            m_previewTexture = null;
            if (m_spriteMaterial != null)
            {
                DestroyImmediate(m_spriteMaterial);
            }
            m_spriteMaterial = null;
        }

        private AnimationElement CurrentElement => m_clip != null && m_elementIndex >= 0 &&
            m_elementIndex < m_clip.Elements.Count ? m_clip.Elements[m_elementIndex] : null;

        private void DrawPreview(Rect viewport)
        {
            if (viewport.width < 1 || viewport.height < 1)
            {
                return;
            }
            m_previewViewport = viewport;
            float pointsPerUnit = k_basePointsPerUnit * m_size;
            AnimationElement element = CurrentElement;
            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreview();
                m_previewSprite.sprite = element?.Sprite;
                m_preview.camera.orthographicSize = viewport.height / (2 * pointsPerUnit);
                m_preview.camera.aspect = viewport.width / viewport.height;
                m_preview.BeginPreview(viewport, GUIStyle.none);
                m_preview.Render();
                m_previewTexture = m_preview.EndPreview();
                GUI.DrawTexture(viewport, m_previewTexture, ScaleMode.StretchToFill, false);
                GUI.BeginClip(viewport);
                Rect local = new Rect(0, 0, viewport.width, viewport.height);
                Handles.BeginGUI();
                Color oldColor = Handles.color;
                DrawWorldGrid(local, pointsPerUnit);
                Handles.color = new Color(1, 1, 1, 0.18f);
                Handles.DrawLine(new Vector3(0, local.center.y), new Vector3(local.width, local.center.y));
                Handles.DrawLine(new Vector3(local.center.x, 0), new Vector3(local.center.x, local.height));
                Handles.color = Color.white;
                Handles.DrawLine(local.center - Vector2.right * 6, local.center + Vector2.right * 6);
                Handles.DrawLine(local.center - Vector2.up * 6, local.center + Vector2.up * 6);
                DrawBoxes(element?.HitBoxes, false, local, pointsPerUnit);
                DrawBoxes(element?.HurtBoxes, true, local, pointsPerUnit);
                if (m_boxDragging && m_boxCreating)
                {
                    DrawBox(m_dragDraft, m_boxIsHurt, true, local, pointsPerUnit);
                }
                Handles.color = oldColor;
                Handles.EndGUI();
                GUI.Label(new Rect(local.center.x + 8, local.center.y + 4, 110, 20), "(0, 0) pivot", EditorStyles.whiteMiniLabel);
                if (element?.Sprite == null)
                {
                    GUI.Label(new Rect(0, 8, local.width, 22), "No Sprite — boxes can still be edited", m_centerLabel);
                }
                GUI.EndClip();
            }
            HandleBoxInput(viewport, pointsPerUnit);
        }

        /// <summary>Draws an adaptive world-unit grid aligned to the Sprite pivot at (0, 0).</summary>
        private static void DrawWorldGrid(Rect viewport, float pointsPerUnit)
        {
            float minorStep = GetGridMinorStep(pointsPerUnit);
            float majorStep = minorStep * k_gridMajorLineCount;
            Vector2 min = AnimationPreviewCoordinates.ToLocal(viewport.min, viewport, pointsPerUnit);
            Vector2 max = AnimationPreviewCoordinates.ToLocal(viewport.max, viewport, pointsPerUnit);
            float minX = Mathf.Min(min.x, max.x);
            float maxX = Mathf.Max(min.x, max.x);
            float minY = Mathf.Min(min.y, max.y);
            float maxY = Mathf.Max(min.y, max.y);
            int firstX = Mathf.CeilToInt(minX / minorStep);
            int lastX = Mathf.FloorToInt(maxX / minorStep);
            int firstY = Mathf.CeilToInt(minY / minorStep);
            int lastY = Mathf.FloorToInt(maxY / minorStep);
            Color minorColor = new Color(1f, 1f, 1f, 0.045f);
            Color majorColor = new Color(1f, 1f, 1f, 0.1f);

            for (int i = firstX; i <= lastX; i++)
            {
                float worldX = i * minorStep;
                if (Mathf.Approximately(worldX, 0f))
                {
                    continue;
                }
                Handles.color = IsMajorGridLine(i) ? majorColor : minorColor;
                float screenX = AnimationPreviewCoordinates.ToScreen(new Vector2(worldX, 0f), viewport, pointsPerUnit).x;
                Handles.DrawLine(new Vector3(screenX, viewport.yMin), new Vector3(screenX, viewport.yMax));
            }

            for (int i = firstY; i <= lastY; i++)
            {
                float worldY = i * minorStep;
                if (Mathf.Approximately(worldY, 0f))
                {
                    continue;
                }
                Handles.color = IsMajorGridLine(i) ? majorColor : minorColor;
                float screenY = AnimationPreviewCoordinates.ToScreen(new Vector2(0f, worldY), viewport, pointsPerUnit).y;
                Handles.DrawLine(new Vector3(viewport.xMin, screenY), new Vector3(viewport.xMax, screenY));
            }
        }

        /// <summary>Gets a SceneView-style 1/2/5 adaptive grid interval in local Unity units.</summary>
        internal static float GetGridMinorStep(float pointsPerUnit)
        {
            float targetUnits = k_gridTargetPoints / Mathf.Max(0.0001f, pointsPerUnit);
            float power = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(targetUnits)));
            float normalized = targetUnits / power;
            float multiplier = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
            return multiplier * power;
        }

        private static bool IsMajorGridLine(int index) => index % k_gridMajorLineCount == 0;

        private void DrawBoxes(IReadOnlyList<Rect> boxes, bool hurt, Rect viewport, float pointsPerUnit)
        {
            if (boxes == null)
            {
                return;
            }
            for (int i = 0; i < boxes.Count; i++)
            {
                bool selected = m_boxIsHurt == hurt && m_boxIndex == i;
                Rect value = selected && m_boxDragging && !m_boxCreating ? m_dragDraft : boxes[i];
                DrawBox(value, hurt, selected, viewport, pointsPerUnit);
            }
        }

        private void DrawBox(Rect box, bool hurt, bool selected, Rect viewport, float pointsPerUnit)
        {
            Rect screen = BoxScreenRect(box, viewport, pointsPerUnit);
            Color border = hurt ? new Color(0.15f, 0.45f, 1f) : new Color(1f, 0.15f, 0.15f);
            Color fill = border;
            fill.a = 0.25f;
            m_rectangleVertices[0] = new Vector3(screen.xMin, screen.yMin);
            m_rectangleVertices[1] = new Vector3(screen.xMax, screen.yMin);
            m_rectangleVertices[2] = new Vector3(screen.xMax, screen.yMax);
            m_rectangleVertices[3] = new Vector3(screen.xMin, screen.yMax);
            Handles.DrawSolidRectangleWithOutline(m_rectangleVertices, fill, selected ? Color.white : border);
            if (!selected || !CanEdit)
            {
                return;
            }
            for (int i = 0; i < 8; i++)
            {
                Vector2 point = HandlePoint(screen, i);
                EditorGUI.DrawRect(new Rect(point.x - k_handleSize / 2, point.y - k_handleSize / 2, k_handleSize, k_handleSize), Color.white);
            }
        }

        private static Rect BoxScreenRect(Rect box, Rect viewport, float pointsPerUnit)
            => AnimationPreviewCoordinates.FromCorners(
                AnimationPreviewCoordinates.ToScreen(box.min, viewport, pointsPerUnit),
                AnimationPreviewCoordinates.ToScreen(box.max, viewport, pointsPerUnit));

        // Handles run clockwise from the top-left corner, including each edge midpoint.
        private static Vector2 HandlePoint(Rect rect, int index)
        {
            return index switch
            {
                0 => new Vector2(rect.xMin, rect.yMin),
                1 => new Vector2(rect.center.x, rect.yMin),
                2 => new Vector2(rect.xMax, rect.yMin),
                3 => new Vector2(rect.xMax, rect.center.y),
                4 => new Vector2(rect.xMax, rect.yMax),
                5 => new Vector2(rect.center.x, rect.yMax),
                6 => new Vector2(rect.xMin, rect.yMax),
                _ => new Vector2(rect.xMin, rect.center.y)
            };
        }

        private void HandleBoxInput(Rect viewport, float pointsPerUnit)
        {
            int control = GUIUtility.GetControlID("AnimationBoxCanvas".GetHashCode(), FocusType.Keyboard);
            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && m_boxDragging)
            {
                CancelBoxDrag();
                current.Use();
                Repaint();
                return;
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Delete &&
                GUIUtility.keyboardControl == control && !EditorGUIUtility.editingTextField && !m_boxDragging)
            {
                DeleteSelectedBox();
                current.Use();
                return;
            }
            if (!CanEdit || CurrentElement == null)
            {
                return;
            }
            if (current.type == EventType.MouseDown && current.button == 0 && viewport.Contains(current.mousePosition))
            {
                GUI.FocusControl(null);
                GUIUtility.keyboardControl = control;
                m_clock.Pause();
                m_dragViewport = viewport;
                m_dragPointsPerUnit = pointsPerUnit;
                m_dragStart = AnimationPreviewCoordinates.ToLocal(current.mousePosition, viewport, pointsPerUnit);
                m_boxCreating = m_tool != 0;
                m_dragHandle = -1;
                if (m_boxCreating)
                {
                    m_boxIsHurt = m_tool == 2;
                    m_boxIndex = -1;
                    m_dragOriginal = new Rect(m_dragStart, Vector2.zero);
                }
                else if (!PickBox(current.mousePosition, viewport, pointsPerUnit))
                {
                    m_boxIndex = -1;
                    current.Use();
                    Repaint();
                    return;
                }
                m_dragDraft = m_dragOriginal;
                m_boxDragging = true;
                m_boxControl = control;
                GUIUtility.hotControl = control;
                current.Use();
                Repaint();
            }
            if (!m_boxDragging || GUIUtility.hotControl != control)
            {
                return;
            }
            if (current.type == EventType.MouseDrag)
            {
                Vector2 point = AnimationPreviewCoordinates.ToLocal(current.mousePosition, m_dragViewport, m_dragPointsPerUnit);
                if (m_boxCreating)
                {
                    m_dragDraft = AnimationPreviewCoordinates.FromCorners(m_dragStart, point);
                }
                else if (m_dragHandle < 0)
                {
                    m_dragDraft = new Rect(m_dragOriginal.position + point - m_dragStart, m_dragOriginal.size);
                }
                else
                {
                    Vector2 min = m_dragOriginal.min;
                    Vector2 max = m_dragOriginal.max;
                    if (m_dragHandle == 0 || m_dragHandle == 6 || m_dragHandle == 7)
                    {
                        min.x = point.x;
                    }
                    if (m_dragHandle == 2 || m_dragHandle == 3 || m_dragHandle == 4)
                    {
                        max.x = point.x;
                    }
                    if (m_dragHandle == 0 || m_dragHandle == 1 || m_dragHandle == 2)
                    {
                        max.y = point.y;
                    }
                    if (m_dragHandle == 4 || m_dragHandle == 5 || m_dragHandle == 6)
                    {
                        min.y = point.y;
                    }
                    m_dragDraft = AnimationPreviewCoordinates.FromCorners(min, max);
                }
                current.Use();
                Repaint();
            }
            if (current.rawType == EventType.MouseUp && current.button == 0)
            {
                CommitBoxDrag();
                current.Use();
            }
        }

        private bool PickBox(Vector2 mouse, Rect viewport, float pointsPerUnit)
        {
            AnimationElement element = CurrentElement;
            IReadOnlyList<Rect> selected = m_boxIsHurt ? element.HurtBoxes : element.HitBoxes;
            if (m_boxIndex >= 0 && m_boxIndex < selected.Count)
            {
                Rect screen = BoxScreenRect(selected[m_boxIndex], viewport, pointsPerUnit);
                for (int i = 0; i < 8; i++)
                {
                    if ((mouse - HandlePoint(screen, i)).sqrMagnitude > 64)
                    {
                        continue;
                    }
                    m_dragHandle = i;
                    m_dragOriginal = selected[m_boxIndex];
                    return true;
                }
                // An obscured box selected in the inspector must remain movable on the canvas.
                if (screen.Contains(mouse))
                {
                    m_dragOriginal = selected[m_boxIndex];
                    return true;
                }
            }
            // Hurt boxes are drawn last, so pick them first; the inspector can select obscured boxes.
            for (int kind = 1; kind >= 0; kind--)
            {
                IReadOnlyList<Rect> boxes = kind == 1 ? element.HurtBoxes : element.HitBoxes;
                for (int i = boxes.Count - 1; i >= 0; i--)
                {
                    if (!BoxScreenRect(boxes[i], viewport, pointsPerUnit).Contains(mouse))
                    {
                        continue;
                    }
                    m_boxIndex = i;
                    m_boxIsHurt = kind == 1;
                    m_dragOriginal = boxes[i];
                    return true;
                }
            }
            return false;
        }

        private void CommitBoxDrag()
        {
            if (IsFinite(m_dragDraft) && m_dragDraft.width > 0 && m_dragDraft.height > 0 &&
                (m_boxCreating || m_dragDraft != m_dragOriginal))
            {
                m_clipObject.Update();
                var boxes = GetBoxes(m_boxIsHurt);
                if (boxes != null)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName(m_boxCreating ? "Create Animation Box" : "Transform Animation Box");
                    if (m_boxCreating)
                    {
                        m_boxIndex = boxes.arraySize++;
                    }
                    if (m_boxIndex >= 0 && m_boxIndex < boxes.arraySize)
                        boxes.GetArrayElementAtIndex(m_boxIndex).rectValue = m_dragDraft;
                    m_clipObject.ApplyModifiedProperties();
                }
            }
            CancelBoxDrag();
            Repaint();
        }

        private void CancelBoxDrag()
        {
            if (m_boxDragging && GUIUtility.hotControl == m_boxControl)
            {
                GUIUtility.hotControl = 0;
            }
            m_boxDragging = false;
            m_boxCreating = false;
        }

        private void DrawTimeline()
        {
            using (new EditorGUI.DisabledScope(m_clock.TotalTicks == 0 || m_boxDragging))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("<", GUILayout.Width(25)))
                    {
                        Seek(m_clock.Tick - 1);
                    }
                    EditorGUI.BeginChangeCheck();
                    int tick = EditorGUILayout.DelayedIntField(m_clock.Tick, GUILayout.Width(65));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Seek(tick);
                    }
                    if (GUILayout.Button(">", GUILayout.Width(25)))
                    {
                        Seek(m_clock.Tick + 1);
                    }
                    GUILayout.Label("tick", GUILayout.Width(25));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Zoom", GUILayout.Width(36));
                    m_tickWidth = GUILayout.HorizontalSlider(m_tickWidth, 2, 80, GUILayout.MinWidth(55));
                }
                Rect viewport = GUILayoutUtility.GetRect(100, 10000, 112, 112);
                float contentWidth = Mathf.Max(viewport.width - 16, Mathf.Min(10000000f, m_clock.TotalTicks * m_tickWidth + 24));
                Rect content = new Rect(0, 0, contentWidth, 88);
                m_timelineScroll = GUI.BeginScrollView(viewport, m_timelineScroll, content, true, false);
                int control = GUIUtility.GetControlID("AnimationTimeline".GetHashCode(), FocusType.Passive);
                Event current = Event.current;
                float visibleStart = m_timelineScroll.x;
                float visibleEnd = visibleStart + viewport.width;
                EditorGUI.DrawRect(content, new Color(0.12f, 0.12f, 0.12f));
                int step = Mathf.Max(1, Mathf.CeilToInt(45 / m_tickWidth));
                int firstTick = Mathf.Max(0, (int)(visibleStart / m_tickWidth) / step * step);
                int lastTick = Mathf.Min(m_clock.TotalTicks, Mathf.CeilToInt(visibleEnd / m_tickWidth));
                for (int i = firstTick; i <= lastTick; i += step)
                {
                    float x = i * m_tickWidth;
                    EditorGUI.DrawRect(new Rect(x, 18, 1, 65), new Color(1, 1, 1, 0.15f));
                    GUI.Label(new Rect(x + 2, 0, 55, 18), i.ToString(), EditorStyles.miniLabel);
                }
                for (int i = 0; i < m_elementLabels.Length; i++)
                {
                    float start = m_clock.Start(i) * m_tickWidth;
                    float end = (i + 1 < m_elementLabels.Length ? m_clock.Start(i + 1) : m_clock.TotalTicks) * m_tickWidth;
                    if (end < visibleStart || start > visibleEnd)
                    {
                        continue;
                    }
                    Rect segment = new Rect(start + 1, 25, Mathf.Max(1, end - start - 2), 48);
                    EditorGUI.DrawRect(segment, i == m_elementIndex ? new Color(0.2f, 0.45f, 0.7f) : new Color(0.26f, 0.28f, 0.32f));
                    GUI.Label(segment, m_elementLabels[i], EditorStyles.whiteMiniLabel);
                }
                EditorGUI.DrawRect(new Rect(m_clock.Tick * m_tickWidth, 0, 2, 86), new Color(1f, 0.8f, 0.2f));
                if (GUI.enabled && current.type == EventType.MouseDown && current.button == 0 && content.Contains(current.mousePosition))
                {
                    int tickAtMouse = Mathf.FloorToInt(current.mousePosition.x / m_tickWidth);
                    int index = m_clock.FindElement(tickAtMouse);
                    Seek(current.mousePosition.y >= 25 && index >= 0 ? m_clock.Start(index) : tickAtMouse);
                    m_timelineDragging = true;
                    GUIUtility.hotControl = control;
                    current.Use();
                }
                if (m_timelineDragging && GUIUtility.hotControl == control)
                {
                    if (current.type == EventType.MouseDrag)
                    {
                        Seek(Mathf.FloorToInt(current.mousePosition.x / m_tickWidth));
                        current.Use();
                    }
                    if (current.rawType == EventType.MouseUp)
                    {
                        m_timelineDragging = false;
                        GUIUtility.hotControl = 0;
                        current.Use();
                    }
                }
                GUI.EndScrollView();
            }
        }
    }
}
