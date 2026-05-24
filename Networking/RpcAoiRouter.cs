using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkadiNet
{
    internal enum RpcAoiKind
    {
        Visual,
        StateCritical,
        Unknown
    }

    internal static class RpcAoiRouter
    {
        private static readonly Dictionary<int, RpcAoiKind> Known = new Dictionary<int, RpcAoiKind>();
        private static int _damageTextHash;
        private static int _talkerSayHash;
        private static bool _initialized;

        internal static void Initialize()
        {
            Known.Clear();
            _damageTextHash = StableHash("DamageText");
            _talkerSayHash = StableHash("TalkerSay");
            Add(_damageTextHash, RpcAoiKind.Visual);
            Add(_talkerSayHash, RpcAoiKind.Visual);

            // State-sensitive RPCs intentionally bypass AoI unless a later version proves a narrower route is safe.
            Add("HealthChanged", RpcAoiKind.StateCritical);
            Add("WNTHealthChanged", RpcAoiKind.StateCritical);
            Add("SetTarget", RpcAoiKind.StateCritical);
            Add("TriggerOnDeath", RpcAoiKind.StateCritical);
            Add("SpawnedZone", RpcAoiKind.StateCritical);
            _initialized = true;
        }

        private static void Add(string name, RpcAoiKind kind)
        {
            Add(StableHash(name), kind);
        }

        private static void Add(int methodHash, RpcAoiKind kind)
        {
            Known[methodHash] = kind;
        }

        internal static bool TryRoute(object routedRpc, object package)
        {
            if (!EffectiveConfig.RpcAoiEnabled || !NetReflection.IsServer())
                return false;
            if (routedRpc == null || package == null || RpcReflection.RoutedRPCDataType == null)
                return false;
            if (!_initialized) Initialize();

            try
            {
                int oldPos = ZPackageTools.GetPos(package);
                ZPackageTools.SetPos(package, 0);
                object data = RpcReflection.CreateRoutedRpcData();
                bool deserialized = RpcReflection.DeserializeRoutedRpcData(data, package);
                ZPackageTools.SetPos(package, oldPos);
                if (!deserialized)
                    return false;

                int methodHash = RpcReflection.GetMethodHash(data);
                RpcAoiKind kind = Known.TryGetValue(methodHash, out RpcAoiKind knownKind) ? knownKind : RpcAoiKind.Unknown;

                if (kind == RpcAoiKind.Unknown)
                    return false;
                if (kind == RpcAoiKind.StateCritical)
                    return false;
                if (!IsKindEnabled(kind, methodHash))
                    return false;

                long targetPeer = RpcReflection.GetTargetPeerId(data);
                if (targetPeer != 0)
                    return false; // Already directed; do not second-guess.

                float radius = RadiusFor(kind);
                if (radius <= 0f)
                    return false;

                if (!TryResolveZdoOrigin(data, out Vector3 origin))
                    return false;

                var recipients = new List<long>();
                int peerCount = 0;
                foreach (object peer in ZdoReflection.EnumeratePeers(ZdoReflection.ZDOManInstance))
                {
                    if (!NetReflection.TryGetPeerUid(peer, out long uid) || uid == 0)
                        continue;

                    peerCount++;
                    Vector3 refPos = NetReflection.GetPeerRefPos(peer);
                    if ((refPos - origin).sqrMagnitude > radius * radius)
                        continue;
                    if (!FeatureNegotiation.IsRpcAoiActiveForUid(uid))
                        return false;

                    recipients.Add(uid);
                }

                if (recipients.Count <= 0)
                    return false;
                if (recipients.Count >= peerCount)
                    return false;

                int routed = 0;
                foreach (long uid in recipients)
                {
                    RpcReflection.SetTargetPeerId(data, uid);
                    if (RpcReflection.RouteRpc(routedRpc, data))
                        routed++;
                }

                if (routed <= 0)
                    return false;
                if (routed < recipients.Count && ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"RPC AoI routed partial hash={methodHash} kind={kind} recipients={routed}/{recipients.Count} radius={radius}; vanilla fallback is suppressed to avoid duplicates for already-routed visual RPCs.");

                if (ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"RPC AoI routed hash={methodHash} kind={kind} recipients={routed} radius={radius}");

                return true;
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"RPC AoI failed; vanilla route used: {ex.Message}");
                return false;
            }
        }

        private static bool IsKindEnabled(RpcAoiKind kind, int methodHash)
        {
            if (kind != RpcAoiKind.Visual) return false;
            return methodHash == _damageTextHash || methodHash == _talkerSayHash;
        }

        private static float RadiusFor(RpcAoiKind kind)
        {
            switch (kind)
            {
                case RpcAoiKind.Visual:
                default:
                    return Math.Max(1f, EffectiveConfig.RpcAoiVisualRadius);
            }
        }

        private static bool TryResolveZdoOrigin(object routedData, out Vector3 origin)
        {
            origin = Vector3.zero;
            try
            {
                object zdoId = RpcReflection.GetTargetZdo(routedData);
                if (zdoId != null && !ZdoReflection.IsIdNone(zdoId))
                {
                    object zdo = ZdoReflection.GetById(zdoId);
                    if (zdo != null)
                    {
                        origin = ZdoReflection.GetPosition(zdo, Vector3.zero);
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        // Matches the stable hash used by Valheim's StringExtensionMethods.GetStableHashCode.
        internal static int StableHash(string text)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < text.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ text[i];
                    if (i == text.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ text[i + 1];
                }

                return hash1 + hash2 * 1566083941;
            }
        }
    }

    [HarmonyPatch]
    internal static class ZRoutedRpcAoiPatch
    {
        private static MethodBase TargetMethod()
        {
            Type zrr = ReflectionCache.ZRoutedRpcType ?? AccessTools.TypeByName("ZRoutedRpc");
            Type zrpc = ReflectionCache.ZRpcType ?? AccessTools.TypeByName("ZRpc");
            Type zpkg = ReflectionCache.ZPackageType ?? AccessTools.TypeByName("ZPackage");
            return AccessTools.Method(zrr, "RPC_RoutedRPC", zrpc != null && zpkg != null ? new[] { zrpc, zpkg } : null)
                   ?? AccessTools.Method(zrr, "RPC_RoutedRPC");
        }

        private static bool Prefix(object __instance, object __0, object __1)
        {
            // Return false only when the AoI router successfully forwarded to selected peers.
            return !RpcAoiRouter.TryRoute(__instance, __1);
        }
    }
}
