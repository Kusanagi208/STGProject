using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GenjitsuLAB.Animation.Editor
{
    public sealed partial class AnimationPackEditorWindow
    {
        private void BuildElementList()
        {
            m_elementList = new ReorderableList(m_clipObject, m_clipObject.FindProperty("m_elements"), true, true, true, true);
            m_elementList.elementHeight = 46;
            m_elementList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Elements (drag to reorder)");
            m_elementList.drawElementCallback = DrawElementRow;
            m_elementList.onSelectCallback = list => SelectElement(list.index);
            m_elementList.onAddCallback = list =>
            {
                ApplyClipChanges();
                int index = AnimationPackAuthoring.AddElement(m_clipObject);
                RefreshTiming();
                SelectElement(index);
            };
            m_elementList.onRemoveCallback = list =>
            {
                if (list.index < 0) return;
                m_clock.Pause();
                CancelBoxDrag();
                m_clipObject.FindProperty("m_elements").DeleteArrayElementAtIndex(list.index);
                ApplyClipChanges();
                SelectElement(Mathf.Min(list.index, m_clip.Elements.Count - 1));
            };
            m_elementList.onReorderCallback = list =>
            {
                int index = list.index;
                ApplyClipChanges();
                SelectElement(index);
            };
        }

        private void SelectElement(int index)
        {
            if (m_clip == null || index < 0 || index >= m_clip.Elements.Count)
            {
                m_elementIndex = -1;
                m_boxIndex = -1;
                return;
            }
            Seek(m_clock.Start(index));
            m_elementIndex = index;
            m_elementList.index = index;
            m_boxIndex = -1;
        }

        private void DrawElementRow(Rect rect, int index, bool active, bool focused)
        {
            SerializedProperty elements = m_clipObject.FindProperty("m_elements");
            if (index >= elements.arraySize) return;
            SerializedProperty element = elements.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 27, rect.height), (index + 1).ToString());
            EditorGUI.PropertyField(new Rect(rect.x + 28, rect.y, rect.width - 28, rect.height),
                element.FindPropertyRelative("m_sprite"), GUIContent.none);
            rect.y += 21;
            EditorGUI.LabelField(new Rect(rect.x + 28, rect.y, 90, rect.height), "Duration (ticks)");
            SerializedProperty duration = element.FindPropertyRelative("m_duration");
            EditorGUI.BeginChangeCheck();
            int value = EditorGUI.DelayedIntField(new Rect(rect.x + 122, rect.y, Mathf.Max(45, rect.width - 122), rect.height), duration.intValue);
            if (EditorGUI.EndChangeCheck()) duration.intValue = Mathf.Max(1, value);
        }

        private void DrawInspector()
        {
            GUILayout.Label("Clip Inspector", EditorStyles.boldLabel);
            if (m_clipObject == null)
            {
                EditorGUILayout.HelpBox("Select or add a Clip.", MessageType.Info);
                return;
            }
            bool editable = CanEdit;
            if (!editable) EditorGUILayout.HelpBox("External Clip: preview only. This editor does not modify or move external assets.", MessageType.Warning);
            if (m_clipObject.UpdateIfRequiredOrScript()) RefreshTiming();
            m_rightScroll = EditorGUILayout.BeginScrollView(m_rightScroll);
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 105;
            using (new EditorGUI.DisabledScope(!editable || m_boxDragging))
            {
                SerializedProperty id = m_clipObject.FindProperty("m_animId");
                EditorGUI.BeginChangeCheck();
                int newId = Mathf.Max(0, EditorGUILayout.DelayedIntField("Id", id.intValue));
                if (EditorGUI.EndChangeCheck())
                {
                    if (AnimationPackAuthoring.HasId(m_pack, newId, m_clip)) m_idError = "Id already exists in this AnimationPack.";
                    else { id.intValue = newId; m_idError = null; }
                }
                if (AnimationPackAuthoring.HasId(m_pack, m_clip.AnimId, m_clip))
                    EditorGUILayout.HelpBox("Duplicate Id in existing data. Assign a unique Id.", MessageType.Error);
                if (!string.IsNullOrEmpty(m_idError)) EditorGUILayout.HelpBox(m_idError, MessageType.Error);
                SerializedProperty animationName = m_clipObject.FindProperty("m_animName");
                EditorGUI.BeginChangeCheck();
                string newName = EditorGUILayout.DelayedTextField("Name", animationName.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    animationName.stringValue = newName;
                    // Keep the Project window's embedded asset label consistent with the clip name.
                    m_clipObject.FindProperty("m_Name").stringValue = newName;
                }
                EditorGUILayout.PropertyField(m_clipObject.FindProperty("m_isLoop"), new GUIContent("Loop"));
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.IntField("TotalTicks", m_clock.TotalTicks);
                m_elementList.DoLayoutList();
                using (new EditorGUI.DisabledScope(m_elementIndex < 0))
                {
                    if (GUILayout.Button("Duplicate Selected Element"))
                    {
                        ApplyClipChanges();
                        int index = AnimationPackAuthoring.AddElement(m_clipObject, m_elementIndex);
                        RefreshTiming();
                        SelectElement(index);
                    }
                }
                if (m_elementIndex < 0)
                    EditorGUILayout.HelpBox("Add an Element, then assign its Sprite and duration.", MessageType.Info);
                else
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField($"Element {m_elementIndex + 1} Boxes", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Pivot-relative units; X/Y = bottom-left", EditorStyles.miniLabel);
                    DrawBoxList(false);
                    DrawBoxList(true);
                }
                if (editable) ApplyClipChanges();
            }
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUILayout.EndScrollView();
        }

        private void ApplyClipChanges()
        {
            if (m_clipObject == null || !m_clipObject.ApplyModifiedProperties()) return;
            m_clock.Pause();
            RefreshTiming();
            // Keep preview and inspector on the same element after changing its duration.
            if (m_elementIndex >= 0) m_clock.Seek(m_clock.Start(m_elementIndex));
            Repaint();
        }

        private SerializedProperty GetBoxes(bool hurt)
        {
            if (m_clipObject == null || m_elementIndex < 0) return null;
            SerializedProperty elements = m_clipObject.FindProperty("m_elements");
            if (m_elementIndex >= elements.arraySize) return null;
            return elements.GetArrayElementAtIndex(m_elementIndex).FindPropertyRelative(hurt ? "m_hurtBoxes" : "m_hitBoxes");
        }

        private void DrawBoxList(bool hurt)
        {
            SerializedProperty boxes = GetBoxes(hurt);
            if (boxes == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                Color old = GUI.contentColor;
                GUI.contentColor = hurt ? new Color(0.4f, 0.65f, 1f) : new Color(1f, 0.4f, 0.4f);
                GUILayout.Label(hurt ? "Hurt Boxes" : "Hit Boxes", EditorStyles.boldLabel);
                GUI.contentColor = old;
                if (GUILayout.Button("+", GUILayout.Width(28)))
                {
                    int index = boxes.arraySize++;
                    boxes.GetArrayElementAtIndex(index).rectValue = new Rect(0, 0, 1, 1);
                    m_boxIndex = index;
                    m_boxIsHurt = hurt;
                }
            }
            for (int i = 0; i < boxes.arraySize; i++)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool selected = m_boxIndex == i && m_boxIsHurt == hurt;
                        if (GUILayout.Toggle(selected, $"Box {i + 1}", EditorStyles.miniButton))
                        {
                            m_boxIndex = i;
                            m_boxIsHurt = hurt;
                        }
                        if (GUILayout.Button("-", GUILayout.Width(26)))
                        {
                            boxes.DeleteArrayElementAtIndex(i);
                            m_boxIndex = Mathf.Min(i, boxes.arraySize - 1);
                            m_boxIsHurt = hurt;
                            break;
                        }
                    }
                    SerializedProperty property = boxes.GetArrayElementAtIndex(i);
                    Rect value = m_boxDragging && !m_boxCreating && m_boxIndex == i && m_boxIsHurt == hurt
                        ? m_dragDraft : property.rectValue;
                    EditorGUI.BeginChangeCheck();
                    value = EditorGUILayout.RectField(GUIContent.none, value);
                    if (EditorGUI.EndChangeCheck() && IsFinite(value) && value.width > 0 && value.height > 0)
                        property.rectValue = value;
                }
            }
        }

        private static bool IsFinite(Rect value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) &&
               float.IsFinite(value.width) && float.IsFinite(value.height);

        private void DeleteSelectedBox()
        {
            if (!CanEdit || m_boxIndex < 0) return;
            m_clock.Pause();
            m_clipObject.Update();
            SerializedProperty boxes = GetBoxes(m_boxIsHurt);
            if (boxes == null || m_boxIndex >= boxes.arraySize) return;
            boxes.DeleteArrayElementAtIndex(m_boxIndex);
            m_boxIndex = Mathf.Min(m_boxIndex, boxes.arraySize - 1);
            m_clipObject.ApplyModifiedProperties();
            Repaint();
        }
    }
}
