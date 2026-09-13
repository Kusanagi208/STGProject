using System;
using System.Collections.Generic;
using System.Reflection;
using GenjitsuLAB.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Immutable source pairing used by both local and remote batch imports.</summary>
    public sealed class DatabaseImportSource
    {
        /// <summary>Destination asset.</summary>
        public Repository Target
        {
            get;
        }
        /// <summary>Downloaded or local UTF-8 CSV text.</summary>
        public string Csv
        {
            get;
        }
        /// <summary>Creates a source without modifying its target.</summary>
        public DatabaseImportSource(Repository target, string csv)
        {
            Target = target; Csv = csv;
        }
    }

    /// <summary>Disposable import preview owning temporary drafts until apply or cancellation.</summary>
    public sealed class DatabaseImport : IDisposable
    {
        private sealed class Row
        {
            internal Repository Target;
            internal Data Original;
            internal Data Draft;
            internal int Line;
            internal string OriginalJson;
        }

        private readonly List<Row> m_rows = new List<Row>();
        private readonly List<string> m_errors = new List<string>();
        private readonly List<string> m_changes = new List<string>();
        private readonly Dictionary<Repository, string> m_snapshots = new Dictionary<Repository, string>();
        private bool m_disposed;
        private bool m_applied;

        /// <summary>Validation errors preventing apply.</summary>
        public IReadOnlyList<string> Errors => m_errors;
        /// <summary>Added, updated and retained keys.</summary>
        public IReadOnlyList<string> Changes => m_changes;
        /// <summary>Whether this preview can be applied.</summary>
        public bool CanApply => !m_disposed && !m_applied && m_errors.Count == 0;

        /// <summary>Parses every source, then validates all candidate references together.</summary>
        public static DatabaseImport Preview(IReadOnlyList<DatabaseImportSource> sources)
        {
            DatabaseImport preview = new DatabaseImport();
            try
            {
                DatabaseEditorUtils.Invalidate();
                HashSet<Repository> targets = new HashSet<Repository>();
                for (int i = 0; i < sources.Count; i++)
                {
                    DatabaseImportSource source = sources[i];
                    if (source == null || source.Target == null || !AssetDatabase.Contains(source.Target) || !targets.Add(source.Target))
                    {
                        preview.m_errors.Add("Each source must have a unique persisted target repository.");
                        continue;
                    }
                    preview.m_snapshots.Add(source.Target, EditorJsonUtility.ToJson(source.Target));
                    preview.ReadSource(source);
                }
                preview.ValidateCandidates();
            }
            catch (Exception exception)
            {
                preview.m_errors.Add(exception.Message);
            }
            return preview;
        }

        private void ReadSource(DatabaseImportSource source)
        {
            try
            {
                CSVUtils.Table table = CSVUtils.ParseTable(source.Csv);
                FieldInfo[] schema = DatabaseCSV.GetFields(source.Target.DataType);
                Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
                foreach (FieldInfo field in schema)
                {
                    fields.Add(field.Name, field);
                }
                int keyColumn = Array.IndexOf(table.Fields, "key");
                if (keyColumn < 0)
                {
                    throw new FormatException("Missing required key column.");
                }
                FieldInfo[] columns = new FieldInfo[table.Fields.Length];
                for (int i = 0; i < columns.Length; i++)
                {
                    if (!fields.TryGetValue(table.Fields[i], out columns[i]) ||
                        !DatabaseCSV.IsSupported(columns[i].FieldType))
                    {
                        throw new FormatException("Unsupported or unknown column: " + table.Fields[i]);
                    }
                }
                HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
                for (int row = 0; row < table.Rows.Count; row++)
                {
                    string key = table.Rows[row][keyColumn];
                    if (string.IsNullOrWhiteSpace(key) || !keys.Add(key))
                    {
                        m_errors.Add(source.Target.name + " line " + table.Lines[row] + ", column key: empty or duplicate key '" + key + "'.");
                        continue;
                    }
                    Data original = DatabaseEditorUtils.Find(source.Target.DataType, key, out Repository owner, out int count);
                    if (count > 1 || (owner != null && owner != source.Target))
                    {
                        m_errors.Add(source.Target.name + " line " + table.Lines[row] + ", column key: key '" + key + "' belongs to another repository or is duplicated.");
                        continue;
                    }
                    Data draft = original != null ? Object.Instantiate(original) : (Data)ScriptableObject.CreateInstance(source.Target.DataType);
                    draft.hideFlags = HideFlags.HideAndDontSave;
                    Row candidate = new Row { Target = source.Target, Original = original, Draft = draft,
                        Line = table.Lines[row], OriginalJson = original != null ? EditorJsonUtility.ToJson(original) : null };
                    m_rows.Add(candidate);
                    for (int col = 0; col < columns.Length; col++)
                    {
                        try
                        {
                            columns[col].SetValue(draft, DatabaseCSV.ParseCell(columns[col].FieldType, table.Rows[row][col]));
                        }
                        catch (Exception exception)
                        {
                            m_errors.Add(source.Target.name + " line " + table.Lines[row] + ", key '" + key +
                                "', column " + table.Fields[col] + ": " + exception.Message);
                        }
                    }
                    m_changes.Add((original == null ? "ADD " : "UPDATE ") + source.Target.name + " / " + key);
                }
                for (int i = 0; i < source.Target.Count; i++)
                {
                    Data record = source.Target.GetRecord(i);
                    if (record != null && !keys.Contains(record.key))
                    {
                        m_changes.Add("KEEP (absent from sheet) " + source.Target.name + " / " + record.key);
                    }
                }
            }
            catch (Exception exception)
            {
                m_errors.Add(source.Target.name + ": " + exception.Message);
            }
        }

        private void ValidateCandidates()
        {
            List<Data> drafts = new List<Data>();
            List<Data> originals = new List<Data>();
            foreach (Row row in m_rows)
            {
                drafts.Add(row.Draft); originals.Add(row.Original);
            }
            m_errors.AddRange(DatabaseEditorUtils.Validate(drafts, originals));
            foreach (Row row in m_rows)
            {
                DatabaseEditorUtils.VisitReferences(row.Draft, (reference, field) =>
                {
                    if (string.IsNullOrEmpty(reference.GetKey()))
                    {
                        return;
                    }
                    bool found = DatabaseEditorUtils.Exists(reference.DataType, reference.GetKey());
                    foreach (Row other in m_rows)
                    {
                        if (other.Draft.GetType() == reference.DataType && other.Draft.key == reference.GetKey())
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        m_errors.Add(row.Target.name + " line " + row.Line + ", key '" + row.Draft.key +
                            "', column " + field + ": missing reference '" + reference.GetKey() + "'.");
                    }
                });
            }
        }

        /// <summary>Revalidates the preview and commits as one Undo group. Stale previews are rejected.</summary>
        public void Apply()
        {
            DatabaseEditorUtils.RequireEditable();
            if (!CanApply)
            {
                throw new InvalidOperationException("Import preview has errors or was already consumed.");
            }
            foreach (KeyValuePair<Repository, string> pair in m_snapshots)
            {
                if (pair.Key == null || EditorJsonUtility.ToJson(pair.Key) != pair.Value)
                {
                    throw new InvalidOperationException("Repository changed; create a new preview.");
                }
            }
            foreach (Row row in m_rows)
            {
                if (row.OriginalJson != null && (row.Original == null || EditorJsonUtility.ToJson(row.Original) != row.OriginalJson))
                {
                    throw new InvalidOperationException("Record changed; create a new preview.");
                }
            }
            DatabaseEditorUtils.Invalidate();
            ValidateCandidates();
            if (!CanApply)
            {
                throw new InvalidOperationException(string.Join("\n", m_errors));
            }
            DatabaseEditorUtils.Transaction("Import databases", () =>
            {
                foreach (Row row in m_rows)
                {
                    DatabaseEditorUtils.Commit(row.Target, row.Draft, row.Original);
                }
            });
            m_applied = true;
        }

        /// <summary>Discards temporary drafts, never persisted assets.</summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            foreach (Row row in m_rows)
            {
                if (row.Draft != null)
                {
                    Object.DestroyImmediate(row.Draft);
                }
            }
            m_rows.Clear();
        }
    }
}
