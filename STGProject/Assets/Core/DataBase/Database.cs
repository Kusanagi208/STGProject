using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenjitsuLAB.Data
{
    internal static class DatabaseRegistry
    {
        internal static Dictionary<Type, IDatabase> s_indices = new Dictionary<Type, IDatabase>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            s_indices.Clear();
        }
    }

    /// <summary>Typed access to explicitly initialized local databases.</summary>
    public static class Database<T> where T : Data
    {
        /// <summary>Validates and replaces this index. Failure preserves the previous index.</summary>
        public static void Initialize(IReadOnlyList<T> records)
        {
            DatabaseRegistry.s_indices[typeof(T)] = new DatabaseRepository<T>(records);
        }

        /// <summary>Returns false for missing keys or uninitialized databases without allocating.</summary>
        public static bool TryGet(string key, out T data)
        {
            data = null;
            return DatabaseRegistry.s_indices.TryGetValue(typeof(T), out IDatabase index) &&
                ((DatabaseRepository<T>)index).TryGet(key, out data);
        }

        /// <summary>Returns a record or null.</summary>
        public static T Load(string key)
        {
            TryGet(key, out T value);
            return value;
        }
        /// <summary>Tests a key in the initialized index.</summary>
        public static bool Exists(string key)
        {
            return TryGet(key, out _);
        }
        /// <summary>Releases this type's index.</summary>
        public static void Shutdown()
        {
            DatabaseRegistry.s_indices.Remove(typeof(T));
        }
    }
}
