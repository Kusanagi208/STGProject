using System;
using System.Collections.Generic;
using UnityEngine;

namespace GenjitsuLAB.STG
{
    /// <summary>
    /// Provides a fixed-capacity pool for Unity components without runtime expansion.
    /// </summary>
    /// <typeparam name="T">The component type stored in the pool.</typeparam>
    public sealed class ComponentPool<T> : IDisposable where T : Component
    {
        private readonly T[] m_items;
        private readonly int[] m_availableIndices;
        private readonly bool[] m_isRented;
        private readonly Dictionary<T, int> m_itemIndices;

        private Transform m_root;
        private int m_availableCount;
        private bool m_isDisposed;

        /// <summary>
        /// Creates and prewarms a fixed-capacity component pool.
        /// </summary>
        /// <param name="prefab">Prefab used to create every pooled component.</param>
        /// <param name="capacity">Total number of instances owned by the pool.</param>
        /// <param name="parent">Parent of the pool runtime root.</param>
        public ComponentPool(T prefab, int capacity, Transform parent)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Pool capacity must be positive.");
            }

            m_items = new T[capacity];
            m_availableIndices = new int[capacity];
            m_isRented = new bool[capacity];
            m_itemIndices = new Dictionary<T, int>(capacity);

            GameObject rootObject = new GameObject($"{typeof(T).Name}Pool");
            m_root = rootObject.transform;
            m_root.SetParent(parent, false);

            for (int index = 0; index < capacity; index++)
            {
                T item = UnityEngine.Object.Instantiate(prefab, m_root);
                item.gameObject.SetActive(false);
                m_items[index] = item;
                m_availableIndices[index] = index;
                m_itemIndices.Add(item, index);
            }

            m_availableCount = capacity;
        }

        /// <summary>
        /// Gets the fixed total number of instances owned by this pool.
        /// </summary>
        public int Capacity => m_items.Length;

        /// <summary>
        /// Gets the number of instances currently available for rent.
        /// </summary>
        public int AvailableCount => m_availableCount;

        /// <summary>
        /// Attempts to rent an instance without expanding the pool.
        /// </summary>
        /// <param name="item">Receives the activated instance when one is available.</param>
        /// <returns><see langword="true"/> when an instance was rented; otherwise <see langword="false"/>.</returns>
        public bool TryRent(out T item)
        {
            if (m_isDisposed || m_availableCount == 0)
            {
                item = null;
                return false;
            }

            int itemIndex = m_availableIndices[--m_availableCount];
            m_isRented[itemIndex] = true;
            item = m_items[itemIndex];
            item.gameObject.SetActive(true);
            return true;
        }

        /// <summary>
        /// Returns a rented instance to the pool. Invalid and duplicate returns are ignored.
        /// </summary>
        /// <param name="item">Instance previously obtained from this pool.</param>
        public void Return(T item)
        {
            if (m_isDisposed || item == null || !m_itemIndices.TryGetValue(item, out int itemIndex) || !m_isRented[itemIndex])
            {
                return;
            }

            item.gameObject.SetActive(false);
            m_isRented[itemIndex] = false;
            m_availableIndices[m_availableCount++] = itemIndex;
        }

        /// <summary>
        /// Returns every rented instance to the pool.
        /// </summary>
        public void ReturnAll()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_availableCount = 0;
            for (int index = 0; index < m_items.Length; index++)
            {
                T item = m_items[index];
                if (item != null)
                {
                    item.gameObject.SetActive(false);
                }

                m_isRented[index] = false;
                m_availableIndices[m_availableCount++] = index;
            }
        }

        /// <summary>
        /// Destroys the pool runtime root and releases managed lookup state.
        /// </summary>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            m_availableCount = 0;
            m_itemIndices.Clear();

            if (m_root != null)
            {
                UnityEngine.Object.Destroy(m_root.gameObject);
                m_root = null;
            }
        }
    }
}
