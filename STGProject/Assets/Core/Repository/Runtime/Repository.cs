using System.Collections.Generic;

namespace GenjitsuLAB.Core
{
    public class Repository<EntityID, Entity>
    {
        public Dictionary<EntityID, Entity> repository { get { return m_repository; } }
        protected Dictionary<EntityID, Entity> m_repository = new Dictionary<EntityID, Entity>();

        public bool Exists(EntityID id)
        {
            return m_repository.ContainsKey(id);
        }

        public int Count()
        {
            return m_repository.Count;
        }

        public virtual Entity GetByID(EntityID id)
        {
            if (Exists(id))
            {
                return m_repository[id];
            }

            return default;
        }

        public bool TryGetValue(EntityID id, out Entity entity)
        {
            return m_repository.TryGetValue(id, out entity);
        }

        public virtual void Add(EntityID id, Entity entity)
        {
            if (Exists(id))
            {
                m_repository[id] = entity;
            }
            else
            {
                m_repository.Add(id, entity);
            }
        }

        public virtual void Remove(EntityID id)
        {
            m_repository.Remove(id);
        }

        public virtual void Clear()
        {
            m_repository.Clear();
        }
    }
}