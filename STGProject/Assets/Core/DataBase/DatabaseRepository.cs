using System;
using System.Collections.Generic;

namespace GenjitsuLAB.Data
{
    /// <summary>Validated index backed by the existing Core repository.</summary>
    public sealed class DatabaseRepository<T> : IDatabase where T : Data
    {
        private readonly GenjitsuLAB.Core.Repository<string, T> m_repository =
            new GenjitsuLAB.Core.Repository<string, T>();

        /// <summary>Builds an index; invalid records and duplicate keys are rejected.</summary>
        public DatabaseRepository(IReadOnlyList<T> records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }
            for (int i = 0; i < records.Count; i++)
            {
                T record = records[i];
                if (record == null || record.GetType() != typeof(T) || string.IsNullOrWhiteSpace(record.key))
                {
                    throw new ArgumentException("Invalid " + typeof(T).Name + " record at index " + i);
                }
                if (m_repository.Exists(record.key))
                {
                    throw new ArgumentException("Duplicate " + typeof(T).Name + " key: " + record.key);
                }
                m_repository.Add(record.key, record);
            }
        }

        /// <inheritdoc/>
        public Type DataType => typeof(T);
        /// <inheritdoc/>
        public int Count => m_repository.Count();
        /// <summary>Looks up a key without logging or allocating.</summary>
        public bool TryGet(string key, out T value)
        {
            value = null;
            return !string.IsNullOrEmpty(key) && m_repository.TryGetValue(key, out value);
        }
        /// <summary>Tests whether a key exists.</summary>
        public bool Exists(string key)
        {
            return TryGet(key, out _);
        }
        /// <summary>Returns a typed record or null.</summary>
        public T GetByKey(string key)
        {
            TryGet(key, out T value);
            return value;
        }
        /// <inheritdoc/>
        public Data GetData(string key)
        {
            return GetByKey(key);
        }
    }
}
