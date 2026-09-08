using System;
using System.Collections.Generic;

namespace GenjitsuLAB.Core
{
    public static class ServiceLocator
    {
        public const string LOG_TAG = "ServiceLocator";
        private static readonly Dictionary<Type, object> m_services = new();

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);

            if (m_services.ContainsKey(type))
            {
                DebugLogger.LogWarningTag(LOG_TAG, $"Service of type {type} is already registered.");
            }

            m_services[type] = service;
        }

        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            m_services.Remove(type);
        }

        public static T Get<T>() where T : class
        {
            var type = typeof(T);

            if (m_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            DebugLogger.LogWarningTag(LOG_TAG, $"Service of type {type} is not registered.");
            return null;

        }

        public static void ClearAll()
        {
            m_services.Clear();
        }
    }

}