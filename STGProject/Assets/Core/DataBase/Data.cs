using System;
using UnityEngine;

namespace GenjitsuLAB.Data
{
    /// <summary>A keyed record stored as a repository sub-asset.</summary>
    public abstract class Data : ScriptableObject, IComparable<Data>
    {
        /// <summary>Case-sensitive identity within this concrete data type.</summary>
        [HideInInspector] public string key = string.Empty;
        [SerializeField] protected string m_titlePath = string.Empty;
        [SerializeField] protected string m_comment = string.Empty;

        /// <summary>Editor label, independent of the stored key.</summary>
        public virtual string GetTitle()
        {
            string path = (m_titlePath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return (path.Length == 0 ? string.Empty : path + "/") + key + ": " + m_comment;
        }

        /// <inheritdoc/>
        public int CompareTo(Data other)
        {
            return other == null ? 1 : string.Compare(key, other.key, StringComparison.Ordinal);
        }

        /// <summary>Diagnostic record information.</summary>
        public virtual string GetLog()
        {
            return GetTitle();
        }
        /// <summary>Optional authoring warnings.</summary>
        public virtual string GetWarningLog()
        {
            return string.Empty;
        }
        /// <summary>Validation errors that prevent saving.</summary>
        public virtual string GetErrorLog()
        {
            return string.Empty;
        }
    }
}
