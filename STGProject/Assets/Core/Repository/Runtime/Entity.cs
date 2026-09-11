using System;

namespace GenjitsuLAB.Core
{
    public abstract class Entity<EntityID>
        where EntityID : IEquatable<EntityID>
    {
        public readonly EntityID id;

        public Entity(EntityID id)
        {
            this.id = id;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Entity<EntityID>))
            {
                return false;
            }

            if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (this.GetType() != obj.GetType())
            {
                return false;
            }

            Entity<EntityID> item = (Entity<EntityID>)obj;
            return id.Equals(item.id);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Entity<EntityID> left, Entity<EntityID> right)
        {
            if (Object.Equals(left, null))
            {
                return Object.Equals(right, null);
            }
            else
            {
                return left.Equals(right);
            }
        }

        public static bool operator !=(Entity<EntityID> left, Entity<EntityID> right)
        {
            return !(left == right);
        }
    }
}