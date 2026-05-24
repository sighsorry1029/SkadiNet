using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkadiNet
{
    internal enum PayloadCullValueKind : byte
    {
        Vector3,
        Quaternion
    }

    internal readonly struct PayloadCullKey : IEquatable<PayloadCullKey>
    {
        private readonly ZdoIdKey _zdoId;
        private readonly int _hash;
        private readonly PayloadCullValueKind _kind;

        internal PayloadCullKey(ZdoIdKey zdoId, int hash, PayloadCullValueKind kind)
        {
            _zdoId = zdoId;
            _hash = hash;
            _kind = kind;
        }

        public bool Equals(PayloadCullKey other)
        {
            return _zdoId.Equals(other._zdoId) && _hash == other._hash && _kind == other._kind;
        }

        public override bool Equals(object obj)
        {
            return obj is PayloadCullKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _zdoId.GetHashCode();
                hash = hash * 31 + _hash;
                hash = hash * 31 + (int)_kind;
                return hash;
            }
        }
    }

    internal static class PayloadCullState
    {
        private const int MaxEntries = 65536;
        private const double MinEntryTtlSeconds = 60.0;
        private const double PruneIntervalSeconds = 30.0;

        private static readonly Dictionary<PayloadCullKey, double> LastAllowedByZdoKey = new Dictionary<PayloadCullKey, double>();
        private static readonly object Lock = new object();
        private static double _nextPruneTime;

        internal static bool ShouldAllowByTime(object zdo, int hash, PayloadCullValueKind kind)
        {
            float forceRefresh = Math.Max(0.05f, EffectiveConfig.PayloadForceRefreshSeconds);
            double now = Time.realtimeSinceStartupAsDouble;
            PayloadCullKey key = BuildKey(zdo, hash, kind);

            lock (Lock)
            {
                PruneIfDue(now);
                if (!LastAllowedByZdoKey.TryGetValue(key, out double last) || now - last >= forceRefresh)
                {
                    LastAllowedByZdoKey[key] = now;
                    return true;
                }
            }

            return false;
        }

        internal static void MarkAllowed(object zdo, int hash, PayloadCullValueKind kind)
        {
            PayloadCullKey key = BuildKey(zdo, hash, kind);
            double now = Time.realtimeSinceStartupAsDouble;
            lock (Lock)
            {
                PruneIfDue(now);
                LastAllowedByZdoKey[key] = now;
            }
        }

        private static void PruneIfDue(double now)
        {
            if (now < _nextPruneTime) return;
            _nextPruneTime = now + PruneIntervalSeconds;

            double ttl = Math.Max(MinEntryTtlSeconds, Math.Max(0.05f, EffectiveConfig.PayloadForceRefreshSeconds) * 20.0);
            var expired = new List<PayloadCullKey>();
            foreach (KeyValuePair<PayloadCullKey, double> pair in LastAllowedByZdoKey)
            {
                if (now - pair.Value >= ttl)
                    expired.Add(pair.Key);
            }

            foreach (PayloadCullKey key in expired)
                LastAllowedByZdoKey.Remove(key);

            while (LastAllowedByZdoKey.Count > MaxEntries && RemoveOldest())
            {
            }
        }

        private static bool RemoveOldest()
        {
            PayloadCullKey oldestKey = default;
            double oldest = double.MaxValue;
            bool found = false;

            foreach (KeyValuePair<PayloadCullKey, double> pair in LastAllowedByZdoKey)
            {
                if (pair.Value < oldest)
                {
                    oldest = pair.Value;
                    oldestKey = pair.Key;
                    found = true;
                }
            }

            if (!found) return false;
            LastAllowedByZdoKey.Remove(oldestKey);
            return true;
        }

        private static PayloadCullKey BuildKey(object zdo, int hash, PayloadCullValueKind kind)
        {
            if (!ZdoReflection.TryGetIdKey(zdo, out ZdoIdKey zdoId))
                zdoId = ZdoIdKey.FromRuntimeObject(zdo);
            return new PayloadCullKey(zdoId, hash, kind);
        }
    }

    [HarmonyPatch]
    internal static class ZDOSetVector3ReducerPatch
    {
        private static MethodBase TargetMethod()
        {
            Type zdo = ReflectionCache.ZDOType ?? AccessTools.TypeByName("ZDO");
            return AccessTools.Method(zdo, "Set", new[] { typeof(int), typeof(Vector3) });
        }

        private static bool Prefix(object __instance, int __0, Vector3 __1)
        {
            if (!EffectiveConfig.PayloadReducerEnabled) return true;
            if (__instance == null || ReflectionCache.ZDOGetVec3Method == null) return true;

            int hash = __0;
            Vector3 value = __1;
            if (ZdoKeyPolicy.ShouldNeverCull(hash)) return true;

            try
            {
                Vector3 current = (Vector3)ReflectionCache.ZDOGetVec3Method.Invoke(__instance, new object[] { hash, Vector3.zero });
                float threshold = Math.Max(0f, EffectiveConfig.PayloadVec3CullSize);
                bool smallDelta = (current - value).sqrMagnitude < threshold * threshold;
                if (!smallDelta)
                {
                    PayloadCullState.MarkAllowed(__instance, hash, PayloadCullValueKind.Vector3);
                    return true;
                }

                // Safety valve missing from plain threshold culling: even tiny changes are allowed periodically.
                if (PayloadCullState.ShouldAllowByTime(__instance, hash, PayloadCullValueKind.Vector3))
                    return true;

                return false;
            }
            catch { return true; }
        }
    }

    [HarmonyPatch]
    internal static class ZDOSetQuaternionReducerPatch
    {
        private static MethodBase TargetMethod()
        {
            Type zdo = ReflectionCache.ZDOType ?? AccessTools.TypeByName("ZDO");
            return AccessTools.Method(zdo, "Set", new[] { typeof(int), typeof(Quaternion) });
        }

        private static bool Prefix(object __instance, int __0, Quaternion __1)
        {
            if (!EffectiveConfig.PayloadReducerEnabled) return true;
            if (__instance == null || ReflectionCache.ZDOGetQuaternionMethod == null) return true;

            int hash = __0;
            Quaternion value = __1;
            if (ZdoKeyPolicy.ShouldNeverCull(hash)) return true;

            try
            {
                Quaternion current = (Quaternion)ReflectionCache.ZDOGetQuaternionMethod.Invoke(__instance, new object[] { hash, Quaternion.identity });
                float dot = Math.Abs(Quaternion.Dot(current, value));
                bool smallDelta = dot > EffectiveConfig.PayloadQuaternionDotThreshold;
                if (!smallDelta)
                {
                    PayloadCullState.MarkAllowed(__instance, hash, PayloadCullValueKind.Quaternion);
                    return true;
                }

                // Safety valve missing from plain threshold culling: even tiny changes are allowed periodically.
                if (PayloadCullState.ShouldAllowByTime(__instance, hash, PayloadCullValueKind.Quaternion))
                    return true;

                return false;
            }
            catch { return true; }
        }
    }
}
