using System;
using System.Runtime.CompilerServices;

namespace SkadiNet
{
    internal readonly struct ZdoIdKey : IEquatable<ZdoIdKey>
    {
        internal readonly long UserId;
        internal readonly uint Id;
        internal readonly int RuntimeId;
        internal readonly bool RuntimeFallback;

        internal ZdoIdKey(long userId, uint id)
        {
            UserId = userId;
            Id = id;
            RuntimeId = 0;
            RuntimeFallback = false;
        }

        private ZdoIdKey(int runtimeId)
        {
            UserId = 0;
            Id = 0;
            RuntimeId = runtimeId;
            RuntimeFallback = true;
        }

        internal static ZdoIdKey FromRuntimeObject(object obj)
        {
            return new ZdoIdKey(obj == null ? 0 : RuntimeHelpers.GetHashCode(obj));
        }

        public bool Equals(ZdoIdKey other)
        {
            return UserId == other.UserId &&
                   Id == other.Id &&
                   RuntimeId == other.RuntimeId &&
                   RuntimeFallback == other.RuntimeFallback;
        }

        public override bool Equals(object obj)
        {
            return obj is ZdoIdKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + UserId.GetHashCode();
                hash = hash * 31 + Id.GetHashCode();
                hash = hash * 31 + RuntimeId;
                hash = hash * 31 + (RuntimeFallback ? 1 : 0);
                return hash;
            }
        }

        public override string ToString()
        {
            return RuntimeFallback ? "runtime:" + RuntimeId : UserId + ":" + Id;
        }
    }
}
