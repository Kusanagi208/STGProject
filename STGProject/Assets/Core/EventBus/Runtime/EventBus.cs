using System;
using System.Collections.Generic;

namespace GenjitsuLAB.Core
{
    public class EventBus
    {
        private readonly Dictionary<Type, Delegate> m_eventTable = new();

        public void Subscribe<T>(Action<T> callback)
        {
            if (callback == null)
            {
                return;
            }

            var type = typeof(T);
            m_eventTable.TryGetValue(type, out var subscribers);

            Delegate[] callbacks = callback.GetInvocationList();
            for (int i = 0; i < callbacks.Length; i++)
            {
                if (Contains(subscribers, callbacks[i]))
                {
                    continue;
                }

                subscribers = Delegate.Combine(subscribers, callbacks[i]);
            }

            if (subscribers != null)
            {
                m_eventTable[type] = subscribers;
            }
        }

        public void Unsubscribe<T>(Action<T> callback)
        {
            var type = typeof(T);

            if (m_eventTable.TryGetValue(type, out var existing))
            {
                var current = Delegate.Remove(existing, callback);

                if (current == null)
                {
                    m_eventTable.Remove(type);
                }
                else
                {
                    m_eventTable[type] = current;
                }
            }
        }

        public void Publish<T>(T evt)
        {
            var type = typeof(T);

            if (m_eventTable.TryGetValue(type, out var callback))
            {
                ((Action<T>)callback)?.Invoke(evt);
            }
        }

        private static bool Contains(Delegate subscribers, Delegate callback)
        {
            if (subscribers == null)
            {
                return false;
            }

            Delegate[] invocationList = subscribers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (invocationList[i].Equals(callback))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
