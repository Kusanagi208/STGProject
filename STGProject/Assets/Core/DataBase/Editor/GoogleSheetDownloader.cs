using System;
using System.Globalization;
using UnityEditor;
using UnityEngine.Networking;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Editor-only cancellable CSV download; all callbacks run on the main thread.</summary>
    public sealed class GoogleSheetDownloader : IDisposable
    {
        private UnityWebRequest m_request;
        private Action<string, string> m_finished;
        private bool m_disposed;
        /// <summary>Current normalized download progress.</summary>
        public float Progress => m_request == null ? 0f : Math.Max(0f, m_request.downloadProgress);

        /// <summary>Starts a 30-second download. Callback arguments are CSV and error, respectively.</summary>
        public GoogleSheetDownloader(string spreadsheetId, string gid, Action<string, string> finished)
        {
            string url = GetCsvUrl(spreadsheetId, gid);
            m_finished = finished;
            m_request = UnityWebRequest.Get(url);
            m_request.timeout = 30;
            try
            {
                m_request.SendWebRequest();
                EditorApplication.update += Tick;
                AssemblyReloadEvents.beforeAssemblyReload += Dispose;
                EditorApplication.quitting += Dispose;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Builds a CSV export URL for a single public worksheet.</summary>
        public static string GetCsvUrl(string spreadsheetId, string gid)
        {
            if (string.IsNullOrWhiteSpace(spreadsheetId))
            {
                throw new ArgumentException("Spreadsheet ID is required.");
            }
            for (int i = 0; i < spreadsheetId.Length; i++)
            {
                char ch = spreadsheetId[i];
                if (!(ch >= 'a' && ch <= 'z') && !(ch >= 'A' && ch <= 'Z') &&
                    !(ch >= '0' && ch <= '9') && ch != '-' && ch != '_')
                {
                    throw new ArgumentException("Enter the spreadsheet ID, not its URL.");
                }
            }
            if (!long.TryParse(gid, NumberStyles.None, CultureInfo.InvariantCulture, out long sheet) || sheet < 0)
            {
                throw new ArgumentException("Worksheet gid must be a non-negative integer.");
            }
            return "https://docs.google.com/spreadsheets/d/" + spreadsheetId + "/export?format=csv&gid=" + gid;
        }

        /// <summary>Rejects HTTP failures and HTML/login pages before passing content to the parser.</summary>
        public static string ValidateResponse(bool success, string contentType, string text)
        {
            if (!success)
            {
                return "Download failed. Verify sharing permissions, ID and gid.";
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                return "The worksheet response is empty.";
            }
            string trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            if ((contentType ?? string.Empty).IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                trimmed.StartsWith("<", StringComparison.Ordinal))
            {
                return "Google returned an HTML/login page instead of CSV. Anonymous read access is required.";
            }
            return null;
        }

        private void Tick()
        {
            if (m_disposed || !m_request.isDone)
            {
                return;
            }
            string text = m_request.downloadHandler.text;
            string error = ValidateResponse(m_request.result == UnityWebRequest.Result.Success,
                m_request.GetResponseHeader("Content-Type"), text);
            Action<string, string> callback = m_finished;
            Dispose();
            callback?.Invoke(error == null ? text : null, error);
        }

        /// <summary>Cancels pending work without invoking its completion callback.</summary>
        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }
            m_disposed = true;
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
            if (m_request != null)
            {
                if (!m_request.isDone)
                {
                    m_request.Abort();
                }
                m_request.Dispose();
                m_request = null;
            }
            m_finished = null;
        }
    }
}
