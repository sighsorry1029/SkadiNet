using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SkadiNet
{
    internal static class NetReflection
    {
        private static Func<object, object> _peerRpcGetter;
        private static Func<object, object> _peerUidGetter;
        private static Func<object, Vector3> _peerRefPosGetter;
        private static Func<object, object> _rpcSocketGetter;
        private static Func<object, object> _socketSendQueueSizeGetter;

        internal static void Initialize()
        {
            _peerRpcGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.PeerRpcField);
            _peerUidGetter = ReflectionDelegateFactory.BoxedFieldGetter(ReflectionCache.PeerUidField);
            _peerRefPosGetter = ReflectionDelegateFactory.Vector3FieldGetter(ReflectionCache.PeerRefPosField);
            _rpcSocketGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZRpcGetSocketMethod);
            _socketSendQueueSizeGetter = ReflectionDelegateFactory.BoxedInstanceMethod(ReflectionCache.ZSteamSocketGetSendQueueSizeMethod);
        }

        internal static Type ZNetType => ReflectionCache.ZNetType;
        internal static Type ZNetPeerType => ReflectionCache.ZNetPeerType;
        internal static Type ZRpcType => ReflectionCache.ZRpcType;
        internal static Type ZPackageType => ReflectionCache.ZPackageType;

        internal static object ZNetInstance => ReflectionCache.ZNetInstanceField?.GetValue(null);

        internal static bool IsServer()
        {
            try
            {
                object znet = ZNetInstance;
                if (znet == null || ReflectionCache.ZNetIsServerMethod == null) return false;
                return (bool)ReflectionCache.ZNetIsServerMethod.Invoke(znet, null);
            }
            catch { return false; }
        }

        internal static bool IsDedicatedServer()
        {
            try
            {
                object znet = ZNetInstance;
                if (znet == null || ReflectionCache.ZNetIsDedicatedMethod == null) return false;
                return (bool)ReflectionCache.ZNetIsDedicatedMethod.Invoke(znet, null);
            }
            catch { return false; }
        }

        internal static bool TryGetPeerUid(object peerOrRpc, out long uid)
        {
            uid = 0;
            if (peerOrRpc == null) return false;

            try
            {
                object value = TryGet(_peerUidGetter, peerOrRpc);
                if (ReflectionCache.TryConvertToLong(value, out uid)) return true;

                FieldInfo directUid = ReflectionCache.CachedField(peerOrRpc.GetType(), "m_uid");
                value = directUid?.GetValue(peerOrRpc);
                if (ReflectionCache.TryConvertToLong(value, out uid)) return true;

                object rpc = GetPeerRpc(peerOrRpc);
                return TryGetUidFromPeerObject(rpc, out uid);
            }
            catch { return false; }
        }

        internal static bool TryGetUidFromPeerObject(object peerOrRpc, out long uid)
        {
            uid = 0;
            if (peerOrRpc == null) return false;
            try
            {
                FieldInfo field = ReflectionCache.CachedField(peerOrRpc.GetType(), "m_uid");
                object value = field?.GetValue(peerOrRpc);
                return ReflectionCache.TryConvertToLong(value, out uid);
            }
            catch { return false; }
        }

        internal static object GetPeerRpc(object peer)
        {
            if (peer == null) return null;
            try
            {
                FieldInfo directRpc = ReflectionCache.CachedField(peer.GetType(), "m_rpc");
                object rpc = directRpc?.GetValue(peer);
                if (rpc != null) return rpc;

                object peerOrRpc = TryGet(_peerRpcGetter, peer);
                if (peerOrRpc == null) return null;

                FieldInfo rpcField = ReflectionCache.CachedField(peerOrRpc.GetType(), "m_rpc");
                return rpcField?.GetValue(peerOrRpc) ?? peerOrRpc;
            }
            catch { return null; }
        }

        internal static object GetSocketFromRpc(object rpc)
        {
            if (rpc == null) return null;
            try { return TryGet(_rpcSocketGetter, rpc) ?? ReflectionCache.ZRpcGetSocketMethod?.Invoke(rpc, null); }
            catch { return null; }
        }

        internal static Vector3 GetPeerRefPos(object peer)
        {
            if (peer == null) return Vector3.zero;
            try
            {
                if (TryGetVector3(_peerRefPosGetter, peer, out Vector3 pos)) return pos;

                FieldInfo direct = ReflectionCache.CachedField(peer.GetType(), "m_refPos");
                if (direct?.GetValue(peer) is Vector3 p) return p;

                FieldInfo nestedPeer = ReflectionCache.CachedField(peer.GetType(), "m_peer");
                object nested = nestedPeer?.GetValue(peer);
                if (nested != null && !ReferenceEquals(nested, peer))
                {
                    FieldInfo nestedRef = ReflectionCache.CachedField(nested.GetType(), "m_refPos");
                    if (nestedRef?.GetValue(nested) is Vector3 np) return np;
                }
            }
            catch { }
            return Vector3.zero;
        }

        internal static int GetSendQueueSizeForPeer(object zdoPeer)
        {
            try
            {
                object rpc = GetPeerRpc(zdoPeer);
                if (rpc == null) return 0;

                object socket = GetSocketFromRpc(rpc);
                if (socket == null) return 0;

                object result = TryGet(_socketSendQueueSizeGetter, socket)
                                ?? ReflectionCache.ZSteamSocketGetSendQueueSizeMethod?.Invoke(socket, null)
                                ?? AccessTools.Method(socket.GetType(), "GetSendQueueSize")?.Invoke(socket, null);
                if (result is int i) return i;
                if (result is long l) return (int)Math.Min(int.MaxValue, l);
            }
            catch { }
            return 0;
        }

        internal static IEnumerable<object> EnumerateZNetPeers()
        {
            object znet = ZNetInstance;
            if (znet == null) yield break;

            IEnumerable peers = null;
            try { peers = ReflectionCache.ZNetGetPeersMethod?.Invoke(znet, null) as IEnumerable; } catch { }
            if (peers == null)
            {
                try { peers = ReflectionCache.ZNetPeersField?.GetValue(znet) as IEnumerable; } catch { }
            }
            if (peers == null) yield break;
            foreach (object peer in peers) yield return peer;
        }

        internal static Vector3 GetReferencePosition(Vector3 fallback)
        {
            try
            {
                object znet = ZNetInstance;
                if (znet != null && ReflectionCache.ZNetGetReferencePositionMethod != null)
                {
                    object raw = ReflectionCache.ZNetGetReferencePositionMethod.Invoke(znet, null);
                    if (raw is Vector3 p) return p;
                }
            }
            catch { }
            return fallback;
        }

        private static object TryGet(Func<object, object> getter, object instance)
        {
            try { return getter != null && instance != null ? getter(instance) : null; }
            catch { return null; }
        }

        private static bool TryGetVector3(Func<object, Vector3> getter, object instance, out Vector3 value)
        {
            value = Vector3.zero;
            try
            {
                if (getter == null || instance == null) return false;
                value = getter(instance);
                return true;
            }
            catch { return false; }
        }
    }
}
