using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenjitsuLAB.Data
{
    /// <summary>Explicit set of database assets for a session.</summary>
    [CreateAssetMenu(menuName = "GenjitsuLAB/Data/Database Catalog")]
    public sealed class DatabaseCatalog : ScriptableObject
    {
        [SerializeField] private Repository[] m_repositories = Array.Empty<Repository>();
        private Dictionary<Type, IDatabase> m_ownedIndices;
        /// <summary>Configured assets, possibly several per record type.</summary>
        public IReadOnlyList<Repository> Repositories => m_repositories;

        /// <summary>Builds every index before replacing the active catalog; errors preserve old state.</summary>
        public void Initialize()
        {
            Dictionary<Type, IDatabase> indices = new Dictionary<Type, IDatabase>();
            for (int i = 0; i < m_repositories.Length; i++)
            {
                Repository repository = m_repositories[i];
                if (repository == null)
                {
                    throw new InvalidOperationException("Catalog contains a null repository.");
                }
                if (!indices.ContainsKey(repository.DataType))
                {
                    indices.Add(repository.DataType, repository.CreateIndex(m_repositories));
                }
            }
            m_ownedIndices = indices;
            DatabaseRegistry.s_indices = new Dictionary<Type, IDatabase>(indices);
        }

        /// <summary>Releases indexes only if they are still owned by this catalog.</summary>
        public void Shutdown()
        {
            if (m_ownedIndices == null)
            {
                return;
            }
            foreach (KeyValuePair<Type, IDatabase> pair in m_ownedIndices)
            {
                if (DatabaseRegistry.s_indices.TryGetValue(pair.Key, out IDatabase current) &&
                    ReferenceEquals(current, pair.Value))
                {
                    DatabaseRegistry.s_indices.Remove(pair.Key);
                }
            }
            m_ownedIndices = null;
        }
    }
}
