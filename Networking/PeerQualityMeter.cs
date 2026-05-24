using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    internal sealed class PeerQualityState
    {
        public object Rpc;
        public long Uid;
        public readonly Queue<float> Samples = new Queue<float>();
        public float LastPingMs;
        public float PingEmaMs;
        public float PingMeanMs;
        public float PingStdDevMs;
        public float PingJitterMs;
        public float ConnectionQualityMs;
        public double LastUpdateTime;
        public int OwnedDynamicEstimate = 0;
        public bool HasAnySample;
    }

    internal static class PeerQualityMeter
    {
        private static readonly Dictionary<object, PeerQualityState> ByRpc = new Dictionary<object, PeerQualityState>();
        private static readonly Dictionary<long, PeerQualityState> ByUid = new Dictionary<long, PeerQualityState>();
        private static readonly Dictionary<Type, MethodInfo> SocketQualityMethods = new Dictionary<Type, MethodInfo>();
        private static readonly object Lock = new object();
        private static FieldInfo _zrpcPingField;

        internal static void Initialize()
        {
            lock (Lock)
            {
                ByRpc.Clear();
                ByUid.Clear();
                SocketQualityMethods.Clear();
            }

            if (ReflectionCache.ZRpcType != null)
                _zrpcPingField = ReflectionCache.SilentField(ReflectionCache.ZRpcType, "m_ping");
        }

        internal static PeerQualityState GetOrCreateByRpc(object rpc, long uid = 0)
        {
            if (rpc == null) return null;
            lock (Lock)
            {
                if (!ByRpc.TryGetValue(rpc, out PeerQualityState state))
                {
                    state = new PeerQualityState { Rpc = rpc, Uid = uid, PingEmaMs = 999f, ConnectionQualityMs = 999f };
                    ByRpc[rpc] = state;
                }
                else if (state.Rpc == null)
                {
                    state.Rpc = rpc;
                }

                if (uid != 0)
                {
                    state.Uid = uid;
                    ByUid[uid] = state;
                }
                return state;
            }
        }

        internal static void ClearPeer(object peerOrRpc, long uid)
        {
            object rpc = NetReflection.GetPeerRpc(peerOrRpc);
            if (rpc == null && peerOrRpc != null && ReflectionCache.ZRpcType != null && ReflectionCache.ZRpcType.IsInstanceOfType(peerOrRpc))
                rpc = peerOrRpc;

            lock (Lock)
            {
                PeerQualityState state = null;
                if (uid != 0) ByUid.TryGetValue(uid, out state);
                if (state == null && rpc != null) ByRpc.TryGetValue(rpc, out state);

                if (uid != 0) ByUid.Remove(uid);
                if (rpc != null) ByRpc.Remove(rpc);

                if (state == null) return;

                if (state.Uid != 0) ByUid.Remove(state.Uid);
                if (state.Rpc != null) ByRpc.Remove(state.Rpc);

                RemoveState(ByRpc, state);
                RemoveState(ByUid, state);
            }
        }

        private static void RemoveState<TKey>(Dictionary<TKey, PeerQualityState> map, PeerQualityState state)
        {
            if (state == null || map.Count == 0) return;

            var remove = new List<TKey>();
            foreach (KeyValuePair<TKey, PeerQualityState> pair in map)
            {
                if (ReferenceEquals(pair.Value, state))
                    remove.Add(pair.Key);
            }

            foreach (TKey key in remove)
                map.Remove(key);
        }

        internal static PeerQualityState GetByUid(long uid)
        {
            lock (Lock)
            {
                ByUid.TryGetValue(uid, out PeerQualityState state);
                return state;
            }
        }

        internal static PeerQualityState GetByPeer(object zdoPeer)
        {
            object rpc = NetReflection.GetPeerRpc(zdoPeer);
            NetReflection.TryGetPeerUid(zdoPeer, out long uid);
            return GetOrCreateByRpc(rpc, uid);
        }

        internal static PeerQualityState UpdateFromPeer(object zdoPeer)
        {
            if (zdoPeer == null) return null;
            object rpc = NetReflection.GetPeerRpc(zdoPeer);
            NetReflection.TryGetPeerUid(zdoPeer, out long uid);
            return UpdateFromRpcCore(rpc, uid);
        }

        internal static float GetQualityForUid(long uid, float fallback = 999f)
        {
            PeerQualityState state = GetByUid(uid);
            return state != null && state.HasAnySample ? state.ConnectionQualityMs : fallback;
        }

        internal static void UpdateFromRpc(object rpc)
        {
            UpdateFromRpcCore(rpc, 0);
        }

        private static PeerQualityState UpdateFromRpcCore(object rpc, long uid)
        {
            if (!EffectiveConfig.PeerQualityEnabled || rpc == null) return null;

            float pingMs = TryReadPingMs(rpc);
            if (pingMs <= 0f || float.IsNaN(pingMs) || float.IsInfinity(pingMs)) return GetOrCreateByRpc(rpc, uid);

            if (uid == 0)
                NetReflection.TryGetUidFromPeerObject(rpc, out uid);
            PeerQualityState state = GetOrCreateByRpc(rpc, uid);
            if (state == null) return null;

            double now = UnityEngine.Time.realtimeSinceStartup;
            lock (Lock)
            {
                double dt = state.LastUpdateTime > 0 ? Math.Max(0.001, now - state.LastUpdateTime) : 0.05;
                state.LastUpdateTime = now;

                float previousLast = state.LastPingMs;
                state.LastPingMs = pingMs;

                float halfLife = Math.Max(0.1f, EffectiveConfig.PeerPingEmaHalfLifeSeconds);
                double tau = halfLife / Math.Log(2.0);
                float alpha = (float)(1.0 - Math.Exp(-dt / tau));
                state.PingEmaMs = state.HasAnySample ? alpha * pingMs + (1f - alpha) * state.PingEmaMs : pingMs;
                state.HasAnySample = true;

                state.Samples.Enqueue(pingMs);
                while (state.Samples.Count > Math.Max(4, EffectiveConfig.PeerPingSampleWindow))
                    state.Samples.Dequeue();

                RecalculateWindowStats(state, previousLast);

                state.ConnectionQualityMs =
                    state.PingMeanMs * EffectiveConfig.PeerQualityMeanWeight +
                    state.PingStdDevMs * EffectiveConfig.PeerQualityStdDevWeight +
                    state.PingJitterMs * EffectiveConfig.PeerQualityJitterWeight +
                    state.PingEmaMs * EffectiveConfig.PeerQualityEmaWeight;
            }

            return state;
        }

        private static float TryReadPingMs(object rpc)
        {
            if (TryReadSocketPingMs(NetReflection.GetSocketFromRpc(rpc), out float socketPing))
                return socketPing;

            try
            {
                object raw = _zrpcPingField?.GetValue(rpc);
                if (raw is float f)
                {
                    // Valheim stores many time values in seconds. Treat small values as seconds.
                    return f < 10f ? f * 1000f : f;
                }
                if (raw is double d)
                {
                    return d < 10.0 ? (float)(d * 1000.0) : (float)d;
                }
            }
            catch { }
            return -1f;
        }

        private static bool TryReadSocketPingMs(object socket, out float pingMs)
        {
            pingMs = 0f;
            if (socket == null) return false;

            try
            {
                MethodInfo method = GetSocketQualityMethod(socket.GetType());
                if (method == null) return false;

                object[] args = { 0f, 0f, 0, 0f, 0f };
                method.Invoke(socket, args);

                float localQuality = args[0] is float lq ? lq : 0f;
                float remoteQuality = args[1] is float rq ? rq : 0f;
                int ping = args[2] is int i ? i : Convert.ToInt32(args[2]);

                if (ping > 0)
                {
                    pingMs = ping;
                    return true;
                }

                if (localQuality > 0f || remoteQuality > 0f)
                {
                    pingMs = 1f;
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static MethodInfo GetSocketQualityMethod(Type socketType)
        {
            if (socketType == null) return null;

            lock (Lock)
            {
                if (SocketQualityMethods.TryGetValue(socketType, out MethodInfo cached))
                    return cached;

                MethodInfo found = null;
                foreach (MethodInfo method in socketType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.Name != "GetConnectionQuality") continue;
                    if (method.GetParameters().Length != 5) continue;
                    found = method;
                    break;
                }

                SocketQualityMethods[socketType] = found;
                return found;
            }
        }

        private static void RecalculateWindowStats(PeerQualityState state, float previousLast)
        {
            int n = state.Samples.Count;
            if (n == 0) return;

            float sum = 0f;
            foreach (float v in state.Samples) sum += v;
            float mean = sum / n;
            float variance = 0f;
            foreach (float v in state.Samples)
            {
                float d = v - mean;
                variance += d * d;
            }
            state.PingMeanMs = mean;
            state.PingStdDevMs = (float)Math.Sqrt(variance / Math.Max(1, n));
            state.PingJitterMs = previousLast > 0 ? Math.Abs(state.LastPingMs - previousLast) : 0f;
        }
    }

    [HarmonyPatch]
    internal static class ZRpcReceivePingPatch
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(ReflectionCache.ZRpcType ?? AccessTools.TypeByName("ZRpc"), "ReceivePing");
        }

        private static void Postfix(object __instance)
        {
            PeerQualityMeter.UpdateFromRpc(__instance);
        }
    }
}
