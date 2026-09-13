using System;
using GenjitsuLAB.Core;
using UnityEngine;

namespace GenjitsuLAB.Data
{
    /// <summary>Reference contract for editor validation and CSV conversion.</summary>
    [Serializable]
    public abstract class DataReference : SerializedCSV
    {
        /// <summary>Concrete referenced record type.</summary>
        public abstract Type DataType
        {
            get;
        }
        /// <summary>Stored key; empty means no selection.</summary>
        public abstract string GetKey();
        /// <summary>Sets the key without parsing a label.</summary>
        public abstract void SetKey(string key);
    }

    /// <summary>Typed reference; declare a concrete serializable subclass for authored fields.</summary>
    [Serializable]
    public abstract class DataReference<T> : DataReference where T : Data
    {
        [SerializeField] private string m_key = string.Empty;
        /// <inheritdoc/>
        public override Type DataType => typeof(T);
        /// <inheritdoc/>
        public override void SetKey(string key)
        {
            m_key = key ?? string.Empty;
        }
        /// <inheritdoc/>
        public override string GetKey()
        {
            return m_key;
        }
        /// <summary>Tests the initialized database.</summary>
        public bool Exists()
        {
            return Database<T>.Exists(m_key);
        }
        /// <summary>Returns the referenced record or null.</summary>
        public T Load()
        {
            return Database<T>.Load(m_key);
        }
        /// <summary>Resolves without reflection, logging or allocation.</summary>
        public bool TryResolve(out T data)
        {
            return Database<T>.TryGet(m_key, out data);
        }
        /// <inheritdoc/>
        public override string ToCSV()
        {
            return m_key;
        }
        /// <inheritdoc/>
        public override void FromCSV(string text)
        {
            SetKey(text);
        }
    }
}
