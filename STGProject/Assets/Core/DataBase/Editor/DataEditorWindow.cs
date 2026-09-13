using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Batch CSV/Google Sheets preview with explicit apply and cancellation.</summary>
    public sealed class DataEditorWindow : EditorWindow
    {
        private readonly List<Repository> m_targets = new List<Repository>();
        private readonly List<DatabaseImportSource> m_sources = new List<DatabaseImportSource>();
        private GoogleSheetDownloader m_download;
        private DatabaseImport m_preview;
        private int m_downloadIndex;
        private string m_error;
        private Vector2 m_scroll;

        [MenuItem("GenjitsuLAB/Data/Import Selected Databases")]
        private static void OpenSelected()
        {
            List<Repository> targets = new List<Repository>();
            foreach (UnityEngine.Object selected in Selection.objects)
            {
                if (selected is Repository repository)
                {
                    targets.Add(repository);
                }
            }
            Open(targets);
        }

        /// <summary>Opens an import window for explicit destination assets.</summary>
        public static void Open(IReadOnlyList<Repository> repositories)
        {
            DataEditorWindow window = GetWindow<DataEditorWindow>("Database Import");
            window.Cancel();
            window.m_targets.Clear();
            for (int i = 0; i < repositories.Count; i++)
            {
                window.m_targets.Add(repositories[i]);
            }
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Select database assets, download all sheets or load CSV, review changes, then Apply. Missing rows are retained.", MessageType.Info);
            if (m_targets.Count == 0)
            {
                EditorGUILayout.HelpBox("Select one or more database assets in Project and reopen this window.", MessageType.Warning);
                return;
            }
            foreach (Repository target in m_targets)
            {
                EditorGUILayout.ObjectField(target, typeof(Repository), false);
            }
            using (new EditorGUI.DisabledScope(m_download != null || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Download Google Sheets and Preview"))
                {
                    StartDownloads();
                }
                if (GUILayout.Button("Load Local CSV Files and Preview"))
                {
                    LoadFiles();
                }
            }
            if (m_download != null)
            {
                EditorGUILayout.LabelField("Downloading", (m_downloadIndex + 1) + "/" + m_targets.Count +
                    " (" + Mathf.RoundToInt(m_download.Progress * 100f) + "%)");
                if (GUILayout.Button("Cancel Download"))
                {
                    Cancel();
                }
                Repaint();
            }
            if (!string.IsNullOrEmpty(m_error))
            {
                EditorGUILayout.HelpBox(m_error, MessageType.Error);
            }
            if (m_preview != null)
            {
                m_scroll = EditorGUILayout.BeginScrollView(m_scroll);
                foreach (string error in m_preview.Errors)
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
                foreach (string change in m_preview.Changes)
                {
                    EditorGUILayout.LabelField(change);
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUI.DisabledScope(!m_preview.CanApply || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("Apply Preview"))
                    {
                        try
                        {
                            m_preview.Apply(); m_error = null;
                        }
                        catch (Exception exception)
                        {
                            m_error = exception.Message;
                        }
                    }
                }
                if (GUILayout.Button("Discard Preview"))
                {
                    Cancel();
                }
            }
        }

        private void StartDownloads()
        {
            Cancel();
            m_downloadIndex = 0;
            DownloadNext();
        }

        private void DownloadNext()
        {
            if (m_downloadIndex >= m_targets.Count)
            {
                m_preview = DatabaseImport.Preview(m_sources);
                Repaint();
                return;
            }
            try
            {
                Repository target = m_targets[m_downloadIndex];
                m_download = new GoogleSheetDownloader(target.SpreadsheetId, target.SheetGid, (csv, error) =>
                {
                    m_download = null;
                    if (error != null)
                    {
                        m_error = target.name + ": " + error;
                        m_sources.Clear();
                        Repaint();
                        return;
                    }
                    m_sources.Add(new DatabaseImportSource(target, csv));
                    m_downloadIndex++;
                    DownloadNext();
                });
            }
            catch (Exception exception)
            {
                m_error = exception.Message; m_sources.Clear();
            }
        }

        private void LoadFiles()
        {
            Cancel();
            try
            {
                foreach (Repository target in m_targets)
                {
                    string path = EditorUtility.OpenFilePanel("CSV for " + target.name, string.Empty, "csv");
                    if (string.IsNullOrEmpty(path))
                    {
                        m_sources.Clear();
                        return;
                    }
                    m_sources.Add(new DatabaseImportSource(target, File.ReadAllText(path, System.Text.Encoding.UTF8)));
                }
                m_preview = DatabaseImport.Preview(m_sources);
            }
            catch (Exception exception)
            {
                m_error = exception.Message; m_sources.Clear();
            }
        }

        private void Cancel()
        {
            m_download?.Dispose();
            m_download = null;
            m_preview?.Dispose();
            m_preview = null;
            m_sources.Clear();
            m_error = null;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
