using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Editor cache, validation and Undo-aware sub-asset authoring.</summary>
    [InitializeOnLoad]
    public static class DatabaseEditorUtils
    {
        private static List<Repository> s_repositories;
        private static readonly Dictionary<Type, List<DatabaseChoice>> s_choices =
            new Dictionary<Type, List<DatabaseChoice>>();

        static DatabaseEditorUtils()
        {
            EditorApplication.projectChanged += Invalidate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        /// <summary>Invalidates authoring caches after asset or content changes.</summary>
        public static void Invalidate()
        {
            s_repositories = null;
            s_choices.Clear();
        }

        private static void OnUndoRedo()
        {
            Invalidate();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Cached persisted databases; never called by runtime code.</summary>
        public static IReadOnlyList<Repository> GetRepositories()
        {
            if (s_repositories != null)
            {
                return s_repositories;
            }
            s_repositories = new List<Repository>();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Repository repository = AssetDatabase.LoadMainAssetAtPath(path) as Repository;
                if (repository != null && !s_repositories.Contains(repository))
                {
                    s_repositories.Add(repository);
                }
            }
            s_repositories.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            return s_repositories;
        }

        /// <summary>Cached key-valued choices; display labels are never parsed.</summary>
        public static IReadOnlyList<DatabaseChoice> GetChoices(Type type)
        {
            if (s_choices.TryGetValue(type, out List<DatabaseChoice> choices))
            {
                return choices;
            }
            choices = new List<DatabaseChoice> { new DatabaseChoice("None", string.Empty) };
            List<Data> records = new List<Data>();
            foreach (Repository repository in GetRepositories())
            {
                if (repository.DataType != type)
                {
                    continue;
                }
                for (int i = 0; i < repository.Count; i++)
                {
                    if (repository.GetRecord(i) != null)
                    {
                        records.Add(repository.GetRecord(i));
                    }
                }
            }
            records.Sort();
            foreach (Data record in records)
            {
                choices.Add(new DatabaseChoice(record.GetTitle(), record.key));
            }
            s_choices.Add(type, choices);
            return choices;
        }

        /// <summary>Cached display label and independent serialized key.</summary>
        public readonly struct DatabaseChoice
        {
            /// <summary>Human-readable path and comment.</summary>
            public readonly string Label;
            /// <summary>Stable serialized identity.</summary>
            public readonly string Key;

            /// <summary>Creates an editor-only dropdown choice.</summary>
            public DatabaseChoice(string label, string key)
            {
                Label = label;
                Key = key;
            }
        }

        /// <summary>True only when one persisted record owns this type/key.</summary>
        public static bool Exists(Type type, string key)
        {
            return Find(type, key, out _, out int count) != null && count == 1;
        }

        /// <summary>Finds an owner and counts duplicate keys across this type.</summary>
        public static Data Find(Type type, string key, out Repository owner, out int count)
        {
            Data result = null;
            owner = null;
            count = 0;
            foreach (Repository repository in GetRepositories())
            {
                if (repository.DataType != type)
                {
                    continue;
                }
                for (int i = 0; i < repository.Count; i++)
                {
                    Data record = repository.GetRecord(i);
                    if (record != null && string.Equals(record.key, key, StringComparison.Ordinal))
                    {
                        result = record;
                        owner = repository;
                        count++;
                    }
                }
            }
            return result;
        }

        /// <summary>Finds the persisted container of a sub-asset.</summary>
        public static Repository GetOwner(Data data)
        {
            return AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GetAssetPath(data)) as Repository;
        }

        /// <summary>Visits references including nested serialized classes and arrays.</summary>
        public static void VisitReferences(Data data, Action<DataReference, string> visitor)
        {
            HashSet<object> visited = new HashSet<object>(ReferenceIdentityComparer.Instance);
            foreach (FieldInfo field in DatabaseCSV.GetFields(data.GetType()))
            {
                Visit(field.GetValue(data), field.Name, visitor, visited);
            }
        }

        private static void Visit(object value, string path, Action<DataReference, string> visitor, HashSet<object> visited)
        {
            if (value == null || value is Object || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum)
            {
                return;
            }
            if (value is DataReference reference)
            {
                visitor(reference, path);
                return;
            }
            if (!visited.Add(value))
            {
                return;
            }
            if (value is System.Collections.IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    Visit(list[i], path + "[" + i + "]", visitor, visited);
                }
                return;
            }
            foreach (FieldInfo field in DatabaseCSV.GetFields(value.GetType()))
            {
                Visit(field.GetValue(value), path + "." + field.Name, visitor, visited);
            }
        }

        /// <summary>Lists all database reference fields targeting a record.</summary>
        public static List<string> FindUsers(Data target)
        {
            List<string> users = new List<string>();
            foreach (Repository repository in GetRepositories())
            {
                for (int i = 0; i < repository.Count; i++)
                {
                    Data record = repository.GetRecord(i);
                    if (record == null)
                    {
                        continue;
                    }
                    VisitReferences(record, (reference, field) =>
                    {
                        if (reference.DataType == target.GetType() && reference.GetKey() == target.key)
                        {
                            users.Add(AssetDatabase.GetAssetPath(repository) + " / " + record.key + "." + field);
                        }
                    });
                }
            }
            return users;
        }

        /// <summary>Validates the persisted world plus candidate replacements before any write.</summary>
        public static List<string> Validate(IReadOnlyList<Data> candidates = null, IReadOnlyList<Data> originals = null)
        {
            Dictionary<Type, Dictionary<string, Data>> world = new Dictionary<Type, Dictionary<string, Data>>();
            List<string> errors = new List<string>();
            foreach (Repository repository in GetRepositories())
            {
                for (int i = 0; i < repository.Count; i++)
                {
                    Data record = repository.GetRecord(i);
                    bool replaced = false;
                    if (originals != null)
                    {
                        for (int j = 0; j < originals.Count; j++)
                        {
                            if (record != null && record == originals[j])
                            {
                                replaced = true;
                                break;
                            }
                        }
                    }
                    if (replaced)
                    {
                        continue;
                    }
                    if (record != null && (record.GetType() != repository.DataType || GetOwner(record) != repository))
                    {
                        errors.Add(repository.name + ": wrong type or external record " + record.name);
                    }
                    AddRecord(record, world, errors);
                }
            }
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    AddRecord(candidates[i], world, errors);
                }
            }
            foreach (Dictionary<string, Data> records in world.Values)
            {
                foreach (Data record in records.Values)
                {
                    string error = record.GetErrorLog();
                    if (!string.IsNullOrEmpty(error))
                    {
                        errors.Add(record.key + ": " + error);
                    }
                    VisitReferences(record, (reference, field) =>
                    {
                        string key = reference.GetKey();
                        if (!string.IsNullOrEmpty(key) &&
                            (!world.TryGetValue(reference.DataType, out Dictionary<string, Data> targets) || !targets.ContainsKey(key)))
                        {
                            errors.Add(record.GetType().Name + " key '" + record.key + "', column " + field +
                                ": missing " + reference.DataType.Name + " key '" + key + "'.");
                        }
                    });
                }
            }
            return errors;
        }

        private static void AddRecord(Data record, Dictionary<Type, Dictionary<string, Data>> world, List<string> errors)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.key))
            {
                errors.Add("Null record or empty key.");
                return;
            }
            Type type = record.GetType();
            if (!world.TryGetValue(type, out Dictionary<string, Data> records))
            {
                records = new Dictionary<string, Data>(StringComparer.Ordinal);
                world.Add(type, records);
            }
            if (records.ContainsKey(record.key))
            {
                errors.Add("Duplicate " + type.Name + " key: " + record.key);
            }
            else
            {
                records.Add(record.key, record);
            }
        }

        /// <summary>Saves a validated editor draft. A null original adds one sub-asset.</summary>
        public static Data Save(Repository repository, Data draft, Data original = null)
        {
            RequireEditable();
            if (repository == null || !AssetDatabase.Contains(repository) || draft == null || draft.GetType() != repository.DataType)
            {
                throw new InvalidOperationException("Select a persisted repository and matching draft.");
            }
            if (original != null && GetOwner(original) != repository)
            {
                throw new InvalidOperationException("Original record belongs to another repository.");
            }
            if (original != null && original.key != draft.key)
            {
                RequireNoUsers(original);
            }
            List<string> errors = Validate(new[] { draft }, new[] { original });
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", errors));
            }
            Data result = null;
            Transaction("Save database record", () => result = Commit(repository, draft, original));
            return result;
        }

        /// <summary>Deletes an unreferenced record while preserving the container, even when empty.</summary>
        public static void Delete(Repository repository, Data record)
        {
            RequireEditable();
            if (record == null || GetOwner(record) != repository)
            {
                throw new InvalidOperationException("Select a record belonging to this database.");
            }
            RequireNoUsers(record);
            Transaction("Delete database record", () =>
            {
                Undo.RegisterCompleteObjectUndo(repository, "Delete database record");
                SerializedObject serialized = new SerializedObject(repository);
                SerializedProperty records = serialized.FindProperty("m_datas");
                for (int i = records.arraySize - 1; i >= 0; i--)
                {
                    if (records.GetArrayElementAtIndex(i).objectReferenceValue == record)
                    {
                        records.GetArrayElementAtIndex(i).objectReferenceValue = null;
                        records.DeleteArrayElementAtIndex(i);
                    }
                }
                serialized.ApplyModifiedProperties();
                Undo.DestroyObjectImmediate(record);
                EditorUtility.SetDirty(repository);
            });
        }

        internal static Data Commit(Repository repository, Data draft, Data original)
        {
            Undo.RegisterCompleteObjectUndo(repository, "Update database");
            if (original != null)
            {
                Undo.RegisterCompleteObjectUndo(original, "Update database record");
                EditorUtility.CopySerialized(draft, original);
                original.hideFlags = HideFlags.None;
                original.name = original.GetType().Name + "_" + original.key;
                EditorUtility.SetDirty(original);
                EditorUtility.SetDirty(repository);
                return original;
            }
            Data created = Object.Instantiate(draft);
            created.hideFlags = HideFlags.None;
            created.name = created.GetType().Name + "_" + created.key;
            try
            {
                AssetDatabase.AddObjectToAsset(created, repository);
                Undo.RegisterCreatedObjectUndo(created, "Add database record");
                SerializedObject serialized = new SerializedObject(repository);
                SerializedProperty records = serialized.FindProperty("m_datas");
                int index = records.arraySize++;
                records.GetArrayElementAtIndex(index).objectReferenceValue = created;
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(repository);
                EditorUtility.SetDirty(created);
                return created;
            }
            catch
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created, true);
                }
                throw;
            }
        }

        internal static void Transaction(string name, Action action)
        {
            RequireEditable();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            try
            {
                action();
                Undo.FlushUndoRecordObjects();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                AssetDatabase.SaveAssets();
                throw;
            }
            finally
            {
                Invalidate();
            }
        }

        internal static void RequireEditable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Database authoring is only available outside Play Mode.");
            }
        }

        private static void RequireNoUsers(Data record)
        {
            List<string> users = FindUsers(record);
            if (users.Count > 0)
            {
                throw new InvalidOperationException("Record is referenced by:\n" + string.Join("\n", users));
            }
        }

        private sealed class ReferenceIdentityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceIdentityComparer Instance = new ReferenceIdentityComparer();
            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }
            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
