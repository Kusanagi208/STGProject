using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Database asset authoring with guarded record lifecycle actions.</summary>
    [CustomEditor(typeof(Repository), true)]
    public sealed class RepositoryInspector : UnityEditor.Editor
    {
        private string m_filter = string.Empty;
        private string m_newKey = string.Empty;
        private string m_message;
        private UnityEditor.Editor m_recordEditor;
        private Data m_selected;

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            Repository repository = (Repository)target;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_spreadsheetId"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_sheetGid"));
                serializedObject.ApplyModifiedProperties();

                if (GUILayout.Button("Open Google Sheet"))
                {
                    Run(() =>
                    {
                        GoogleSheetDownloader.GetCsvUrl(repository.SpreadsheetId, repository.SheetGid);
                        Application.OpenURL(
                            "https://docs.google.com/spreadsheets/d/" +
                            repository.SpreadsheetId +
                            "/edit#gid=" +
                            repository.SheetGid);
                    });
                }

                if (GUILayout.Button("Import / Preview CSV or Google Sheet"))
                {
                    DataEditorWindow.Open(new[] { repository });
                }

                if (GUILayout.Button("Export Local CSV"))
                {
                    Run(() =>
                    {
                        string csv = DatabaseCSV.ToCSV(repository);
                        string path = EditorUtility.SaveFilePanel(
                            "Export CSV",
                            string.Empty,
                            repository.name,
                            "csv");
                        if (!string.IsNullOrEmpty(path))
                        {
                            File.WriteAllText(path, csv, new System.Text.UTF8Encoding(false));
                        }
                    });
                }

                if (GUILayout.Button("Validate Databases"))
                {
                    DatabaseEditorUtils.Invalidate();
                    List<string> errors = DatabaseEditorUtils.Validate();
                    m_message = errors.Count == 0
                        ? "Validation passed."
                        : string.Join("\n", errors);
                }

                m_newKey = EditorGUILayout.TextField("New record key", m_newKey);
                if (GUILayout.Button("Add Record"))
                {
                    Run(() =>
                    {
                        Data draft = (Data)ScriptableObject.CreateInstance(repository.DataType);
                        try
                        {
                            draft.key = m_newKey;
                            SelectRecord(DatabaseEditorUtils.Save(repository, draft));
                        }
                        finally
                        {
                            DestroyImmediate(draft);
                        }
                    });
                }

                m_filter = EditorGUILayout.TextField("Search", m_filter);
                for (int i = 0; i < repository.Count; i++)
                {
                    Data record = repository.GetRecord(i);
                    if (record == null)
                    {
                        EditorGUILayout.HelpBox("Null record in container.", MessageType.Error);
                        continue;
                    }

                    if (record.GetTitle().IndexOf(m_filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    if (GUILayout.Button(record.GetTitle()))
                    {
                        SelectRecord(record);
                    }
                }

                if (m_selected != null)
                {
                    EditorGUILayout.Space();
                    m_recordEditor.OnInspectorGUI();
                    if (GUILayout.Button("Delete Selected Record"))
                    {
                        Run(() =>
                        {
                            if (EditorUtility.DisplayDialog(
                                "Delete record",
                                m_selected.GetTitle(),
                                "Delete",
                                "Cancel"))
                            {
                                DatabaseEditorUtils.Delete(repository, m_selected);
                                SelectRecord(null);
                            }
                        });
                    }
                }
            }

            if (!string.IsNullOrEmpty(m_message))
            {
                EditorGUILayout.HelpBox(m_message, MessageType.Info);
            }
        }

        private void Run(Action action)
        {
            try
            {
                action();
                m_message = null;
            }
            catch (Exception exception)
            {
                m_message = exception.Message;
            }
        }

        private void SelectRecord(Data record)
        {
            if (m_recordEditor != null)
            {
                DestroyImmediate(m_recordEditor);
            }

            m_selected = record;
            m_recordEditor = record == null
                ? null
                : CreateEditor(record);
        }

        private void OnDisable()
        {
            SelectRecord(null);
        }
    }

    /// <summary>Edits a detached draft before validation writes to a persisted record.</summary>
    [CustomEditor(typeof(Data), true)]
    public sealed class DataRecordInspector : UnityEditor.Editor
    {
        private Data m_draft;
        private SerializedObject m_draftSerializedObject;
        private string m_originalJson;
        private string m_error;

        private void OnEnable()
        {
            ResetDraft();
        }

        private void OnDisable()
        {
            ClearDraft();
        }

        private void ClearDraft()
        {
            m_draftSerializedObject?.Dispose();
            m_draftSerializedObject = null;
            if (m_draft != null)
            {
                DestroyImmediate(m_draft);
            }

            m_draft = null;
        }

        private void ResetDraft()
        {
            ClearDraft();
            if (target == null)
            {
                return;
            }

            m_originalJson = EditorJsonUtility.ToJson(target);
            m_draft = Instantiate((Data)target);
            m_draft.hideFlags = HideFlags.HideAndDontSave;
            m_draftSerializedObject = new SerializedObject(m_draft);
            m_error = null;
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            if (target == null)
            {
                return;
            }

            if (m_draft == null || EditorJsonUtility.ToJson(target) != m_originalJson)
            {
                ResetDraft();
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.HelpBox(
                    "Edit this draft, then Apply. Renaming a referenced key is blocked.",
                    MessageType.Info);

                m_draftSerializedObject.Update();
                EditorGUILayout.PropertyField(m_draftSerializedObject.FindProperty("key"));
                DrawPropertiesExcluding(m_draftSerializedObject, "m_Script", "key");
                m_draftSerializedObject.ApplyModifiedProperties();

                if (GUILayout.Button("Apply Record"))
                {
                    try
                    {
                        DatabaseEditorUtils.Save(
                            DatabaseEditorUtils.GetOwner((Data)target),
                            m_draft,
                            (Data)target);
                        ResetDraft();
                    }
                    catch (Exception exception)
                    {
                        m_error = exception.Message;
                    }
                }

                if (GUILayout.Button("Discard Changes"))
                {
                    ResetDraft();
                }
            }

            if (!string.IsNullOrEmpty(m_error))
            {
                EditorGUILayout.HelpBox(m_error, MessageType.Error);
            }
        }
    }

    /// <summary>Unity-native typed key dropdown for every DataReference subclass.</summary>
    [CustomPropertyDrawer(typeof(DataReference), true)]
    public sealed class DataReferenceDrawer : PropertyDrawer
    {
        private const float k_warningHeight = 34f;
        private static readonly GUIContent s_missingContent =
            new GUIContent("The selected key is missing or duplicated.");

        /// <inheritdoc/>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty keyProperty = property.FindPropertyRelative("m_key");
            Type dataType = GetDataType();
            bool missing = keyProperty != null &&
                !string.IsNullOrEmpty(keyProperty.stringValue) &&
                (dataType == null || !DatabaseEditorUtils.Exists(dataType, keyProperty.stringValue));
            return missing
                ? EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + k_warningHeight
                : EditorGUIUtility.singleLineHeight;
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty keyProperty = property.FindPropertyRelative("m_key");
            Type dataType = GetDataType();
            if (keyProperty == null || dataType == null)
            {
                EditorGUI.LabelField(position, label.text, "Unsupported DataReference field.");
                return;
            }

            IReadOnlyList<DatabaseEditorUtils.DatabaseChoice> choices =
                DatabaseEditorUtils.GetChoices(dataType);
            string currentLabel = string.IsNullOrEmpty(keyProperty.stringValue)
                ? "None"
                : "[Missing] " + keyProperty.stringValue;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].Key == keyProperty.stringValue)
                {
                    currentLabel = choices[i].Label;
                    break;
                }
            }

            Rect popupPosition = position;
            popupPosition.height = EditorGUIUtility.singleLineHeight;
            popupPosition = EditorGUI.PrefixLabel(popupPosition, label);
            if (EditorGUI.DropdownButton(
                popupPosition,
                new GUIContent(currentLabel),
                FocusType.Keyboard,
                EditorStyles.popup))
            {
                ShowMenu(keyProperty.serializedObject, keyProperty.propertyPath, choices);
            }

            if (!string.IsNullOrEmpty(keyProperty.stringValue) &&
                !DatabaseEditorUtils.Exists(dataType, keyProperty.stringValue))
            {
                Rect warningPosition = position;
                warningPosition.y += EditorGUIUtility.singleLineHeight +
                    EditorGUIUtility.standardVerticalSpacing;
                warningPosition.height = k_warningHeight;
                EditorGUI.HelpBox(warningPosition, s_missingContent.text, MessageType.Warning);
            }
        }

        private Type GetDataType()
        {
            Type referenceType = fieldInfo.FieldType;
            if (referenceType.IsArray)
            {
                referenceType = referenceType.GetElementType();
            }
            else if (referenceType.IsGenericType &&
                referenceType.GetGenericTypeDefinition() == typeof(List<>))
            {
                referenceType = referenceType.GetGenericArguments()[0];
            }

            if (!typeof(DataReference).IsAssignableFrom(referenceType))
            {
                return null;
            }

            DataReference reference = Activator.CreateInstance(referenceType) as DataReference;
            return reference?.DataType;
        }

        private static void ShowMenu(
            SerializedObject serializedObject,
            string propertyPath,
            IReadOnlyList<DatabaseEditorUtils.DatabaseChoice> choices)
        {
            GenericMenu menu = new GenericMenu();
            string currentKey = serializedObject.FindProperty(propertyPath).stringValue;
            for (int i = 0; i < choices.Count; i++)
            {
                DatabaseEditorUtils.DatabaseChoice choice = choices[i];
                menu.AddItem(
                    new GUIContent(choice.Label),
                    choice.Key == currentKey,
                    () =>
                    {
                        serializedObject.Update();
                        SerializedProperty property = serializedObject.FindProperty(propertyPath);
                        property.stringValue = choice.Key;
                        serializedObject.ApplyModifiedProperties();
                    });
            }

            menu.ShowAsContext();
        }
    }
}
