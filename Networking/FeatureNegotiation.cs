using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    [Flags]
    internal enum PeerFeatureFlags
    {
        None = 0,
        Compression = 1,
        // Bit 2 is intentionally left unused. Older SkadiNet builds advertised ZDO delta here.
        RpcAoi = 4
    }

    internal sealed class PeerFeatureState
    {
        public object Rpc;
        public object Socket;
        public long Uid;
        public bool RegisteredRpc;
        public bool HandshakeSent;
        public bool HandshakeReceived;
        public int RemoteProtocol;
        public PeerFeatureFlags RemoteCapabilities;
        public bool CompressionActive;
        public bool RpcAoiActive;
        public int CompressionFailures;
        public double LastHandshakeTime;
    }

    internal static class FeatureNegotiation
    {
        internal const int ProtocolVersion = 2;
        internal const int FeatureMagic = 0x464E4B53; // "SKNF" little-endian-ish
        internal const string RpcName = "SkadiNet_Features";

        private static readonly object Lock = new object();
        private static readonly Dictionary<object, PeerFeatureState> ByRpc = new Dictionary<object, PeerFeatureState>();
        private static readonly Dictionary<object, PeerFeatureState> BySocket = new Dictionary<object, PeerFeatureState>();
        private static readonly Dictionary<long, PeerFeatureState> ByUid = new Dictionary<long, PeerFeatureState>();

        internal static void Initialize()
        {
            lock (Lock)
            {
                ByRpc.Clear();
                BySocket.Clear();
                ByUid.Clear();
            }
        }

        internal static PeerFeatureFlags LocalCapabilities
        {
            get
            {
                return PeerFeatureFlags.Compression | PeerFeatureFlags.RpcAoi;
            }
        }

        internal static PeerFeatureState GetOrCreateByRpc(object rpc, long uid = 0)
        {
            if (rpc == null) return null;

            lock (Lock)
            {
                if (!ByRpc.TryGetValue(rpc, out PeerFeatureState state))
                {
                    state = new PeerFeatureState { Rpc = rpc, Uid = uid };
                    ByRpc[rpc] = state;
                }

                if (uid != 0)
                {
                    state.Uid = uid;
                    ByUid[uid] = state;
                }

                object socket = NetReflection.GetSocketFromRpc(rpc);
                if (socket != null)
                {
                    state.Socket = socket;
                    BySocket[socket] = state;
                }

                return state;
            }
        }

        internal static PeerFeatureState GetBySocket(object socket)
        {
            if (socket == null) return null;
            lock (Lock)
            {
                BySocket.TryGetValue(socket, out PeerFeatureState state);
                return state;
            }
        }

        internal static PeerFeatureState GetByUid(long uid)
        {
            lock (Lock)
            {
                ByUid.TryGetValue(uid, out PeerFeatureState state);
                return state;
            }
        }

        internal static void ClearPeer(object peerOrRpc, long uid)
        {
            object rpc = NetReflection.GetPeerRpc(peerOrRpc);
            if (rpc == null && peerOrRpc != null && ReflectionCache.ZRpcType != null && ReflectionCache.ZRpcType.IsInstanceOfType(peerOrRpc))
                rpc = peerOrRpc;

            object socket = NetReflection.GetSocketFromRpc(rpc);

            lock (Lock)
            {
                PeerFeatureState state = null;
                if (uid != 0) ByUid.TryGetValue(uid, out state);
                if (state == null && rpc != null) ByRpc.TryGetValue(rpc, out state);
                if (state == null && socket != null) BySocket.TryGetValue(socket, out state);

                if (uid != 0) ByUid.Remove(uid);
                if (rpc != null) ByRpc.Remove(rpc);
                if (socket != null) BySocket.Remove(socket);

                if (state == null) return;

                if (state.Uid != 0) ByUid.Remove(state.Uid);
                if (state.Rpc != null) ByRpc.Remove(state.Rpc);
                if (state.Socket != null) BySocket.Remove(state.Socket);

                RemoveState(ByRpc, state);
                RemoveState(BySocket, state);
                RemoveState(ByUid, state);
            }
        }

        private static void RemoveState<TKey>(Dictionary<TKey, PeerFeatureState> map, PeerFeatureState state)
        {
            if (state == null || map.Count == 0) return;

            var remove = new List<TKey>();
            foreach (KeyValuePair<TKey, PeerFeatureState> pair in map)
            {
                if (ReferenceEquals(pair.Value, state))
                    remove.Add(pair.Key);
            }

            foreach (TKey key in remove)
                map.Remove(key);
        }

        internal static bool IsCompressionActiveForSocket(object socket)
        {
            if (!EffectiveConfig.CompressionEnabled) return false;
            PeerFeatureState state = GetBySocket(socket);
            if (state == null) return false;
            if (state.CompressionFailures >= EffectiveConfig.CompressionFailureLimitPerPeer) return false;
            return state.HandshakeReceived && Supports(state, PeerFeatureFlags.Compression);
        }

        internal static bool IsRpcAoiActiveForUid(long uid)
        {
            if (!EffectiveConfig.RpcAoiEnabled) return false;
            PeerFeatureState state = GetByUid(uid);
            if (state == null) return true;
            return !state.HandshakeReceived || Supports(state, PeerFeatureFlags.RpcAoi);
        }

        internal static void RecordCompressionFailure(object socket)
        {
            PeerFeatureState state = GetBySocket(socket);
            if (state == null) return;
            state.CompressionFailures++;
            state.CompressionActive = false;
        }

        internal static void OnNewConnection(object znetPeer)
        {
            if (!ModConfig.Enabled.Value) return;

            object rpc = NetReflection.GetPeerRpc(znetPeer);
            if (rpc == null) return;
            NetReflection.TryGetPeerUid(znetPeer, out long uid);

            PeerFeatureState state = GetOrCreateByRpc(rpc, uid);
            RegisterRpc(rpc, state);
            SendHello(rpc, state);
        }

        private static void RegisterRpc(object rpc, PeerFeatureState state)
        {
            if (rpc == null || state == null || state.RegisteredRpc) return;
            try
            {
                if (ReflectionCache.ZRpcRegisterGenericPackageMethod == null || ReflectionCache.ZRpcType == null || ReflectionCache.ZPackageType == null)
                    return;

                MethodInfo handler = typeof(FeatureNegotiation)
                    .GetMethod(nameof(RPC_Features_Generic), BindingFlags.Static | BindingFlags.NonPublic)
                    .MakeGenericMethod(ReflectionCache.ZRpcType, ReflectionCache.ZPackageType);
                Type delegateType = typeof(Action<,>).MakeGenericType(ReflectionCache.ZRpcType, ReflectionCache.ZPackageType);
                Delegate del = Delegate.CreateDelegate(delegateType, handler);
                ReflectionCache.ZRpcRegisterGenericPackageMethod.Invoke(rpc, new object[] { RpcName, del });
                state.RegisteredRpc = true;
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"Could not register {RpcName}: {ex.Message}");
            }
        }

        private static void SendHello(object rpc, PeerFeatureState state)
        {
            if (rpc == null || state == null || state.HandshakeSent) return;
            try
            {
                object pkg = ZPackageTools.NewPackage();
                ZPackageTools.WriteInt(pkg, FeatureMagic);
                ZPackageTools.WriteInt(pkg, ProtocolVersion);
                ZPackageTools.WriteInt(pkg, (int)LocalCapabilities);
                ZPackageTools.WriteString(pkg, Plugin.PluginVersion);

                ReflectionCache.ZRpcInvokeMethod?.Invoke(rpc, new object[] { RpcName, new object[] { pkg } });
                state.HandshakeSent = true;
                state.LastHandshakeTime = UnityEngine.Time.realtimeSinceStartupAsDouble;
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"Could not send SkadiNet feature handshake: {ex.Message}");
            }
        }

        private static void RPC_Features_Generic<TRpc, TPkg>(TRpc rpc, TPkg pkg)
        {
            RPC_Features(rpc, pkg);
        }

        private static void RPC_Features(object rpc, object pkg)
        {
            try
            {
                PeerFeatureState state = GetOrCreateByRpc(rpc);
                if (state == null || pkg == null) return;

                int oldPos = ZPackageTools.GetPos(pkg);
                ZPackageTools.SetPos(pkg, 0);
                int magic = ZPackageTools.ReadInt(pkg);
                if (magic != FeatureMagic)
                {
                    ZPackageTools.SetPos(pkg, oldPos);
                    return;
                }

                int protocol = ZPackageTools.ReadInt(pkg);
                int flagsRaw = ZPackageTools.ReadInt(pkg);
                string remoteVersion = ZPackageTools.ReadString(pkg);

                state.RemoteProtocol = protocol;
                state.RemoteCapabilities = (PeerFeatureFlags)flagsRaw;
                state.HandshakeReceived = protocol >= 1;
                RefreshActiveFlags(state);

                if (ModConfig.DebugLogging.Value)
                    Plugin.Log.LogDebug($"SkadiNet feature handshake: protocol={protocol}, capabilities={state.RemoteCapabilities}, version={remoteVersion}, compression={state.CompressionActive}, rpcAoi={state.RpcAoiActive}");

                // If the remote initiated first, answer once.
                if (!state.HandshakeSent)
                    SendHello(rpc, state);
            }
            catch (Exception ex)
            {
                if (ModConfig.DebugLogging.Value) Plugin.Log.LogWarning($"SkadiNet feature handshake receive failed: {ex.Message}");
            }
        }

        private static bool Supports(PeerFeatureState state, PeerFeatureFlags flag)
        {
            return state != null && (state.RemoteCapabilities & flag) != 0;
        }

        private static void RefreshActiveFlags(PeerFeatureState state)
        {
            if (state == null) return;
            state.CompressionActive = EffectiveConfig.CompressionEnabled && Supports(state, PeerFeatureFlags.Compression);
            state.RpcAoiActive = EffectiveConfig.RpcAoiEnabled && Supports(state, PeerFeatureFlags.RpcAoi);
        }
    }

    [HarmonyPatch]
    internal static class ZNetOnNewConnectionFeatureHandshakePatch
    {
        private static MethodBase TargetMethod()
        {
            Type znet = ReflectionCache.ZNetType ?? AccessTools.TypeByName("ZNet");
            Type peer = ReflectionCache.ZNetPeerType ?? AccessTools.TypeByName("ZNetPeer");
            return AccessTools.Method(znet, "OnNewConnection", peer != null ? new[] { peer } : null)
                   ?? AccessTools.Method(znet, "OnNewConnection");
        }

        private static void Postfix(object __0)
        {
            FeatureNegotiation.OnNewConnection(__0);
        }
    }
}
